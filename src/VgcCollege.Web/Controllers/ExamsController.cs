using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

[Authorize]
public class ExamsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public ExamsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);

        if (User.IsInRole("Student"))
        {
            var profile = await _db.StudentProfiles.FirstOrDefaultAsync(s => s.IdentityUserId == userId);
            if (profile == null) return Forbid();

            // Students only see released results
            var results = await _db.ExamResults
                .Include(r => r.Exam).ThenInclude(e => e!.Course)
                .Where(r => r.StudentProfileId == profile.Id && r.Exam!.ResultsReleased)
                .ToListAsync();
            return View("StudentResults", results);
        }

        IQueryable<Exam> query = _db.Exams.Include(e => e.Course).ThenInclude(c => c!.Branch);

        if (User.IsInRole("Faculty"))
        {
            var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
            if (profile == null) return Forbid();
            var courseIds = await _db.FacultyCourseAssignments
                .Where(a => a.FacultyProfileId == profile.Id)
                .Select(a => a.CourseId)
                .ToListAsync();
            query = query.Where(e => courseIds.Contains(e.CourseId));
        }

        return View(await query.ToListAsync());
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Details(int id)
    {
        var exam = await _db.Exams
            .Include(e => e.Course)
            .Include(e => e.Results).ThenInclude(r => r.StudentProfile)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (exam == null) return NotFound();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(exam.CourseId)) return Forbid();
        return View(exam);
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Create()
    {
        await PopulateCoursesAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Create(Exam exam)
    {
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(exam.CourseId)) return Forbid();
        if (!ModelState.IsValid)
        {
            await PopulateCoursesAsync();
            return View(exam);
        }
        _db.Exams.Add(exam);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Exam created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Edit(int id)
    {
        var exam = await _db.Exams.FindAsync(id);
        if (exam == null) return NotFound();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(exam.CourseId)) return Forbid();
        await PopulateCoursesAsync(exam.CourseId);
        return View(exam);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Edit(int id, Exam exam)
    {
        if (id != exam.Id) return BadRequest();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(exam.CourseId)) return Forbid();
        if (!ModelState.IsValid)
        {
            await PopulateCoursesAsync(exam.CourseId);
            return View(exam);
        }
        _db.Exams.Update(exam);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Exam updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        var exam = await _db.Exams.Include(e => e.Course).FirstOrDefaultAsync(e => e.Id == id);
        if (exam == null) return NotFound();
        return View(exam);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var exam = await _db.Exams.FindAsync(id);
        if (exam != null)
        {
            _db.Exams.Remove(exam);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Exam deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleRelease(int id)
    {
        var exam = await _db.Exams.FindAsync(id);
        if (exam == null) return NotFound();
        exam.ResultsReleased = !exam.ResultsReleased;
        await _db.SaveChangesAsync();
        TempData["Success"] = exam.ResultsReleased ? "Results released to students." : "Results hidden from students.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> AddResult(int examId)
    {
        var exam = await _db.Exams.Include(e => e.Course).FirstOrDefaultAsync(e => e.Id == examId);
        if (exam == null) return NotFound();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(exam.CourseId)) return Forbid();

        var enrolledStudents = await _db.CourseEnrolments
            .Where(e => e.CourseId == exam.CourseId)
            .Include(e => e.StudentProfile)
            .ToListAsync();

        ViewBag.Students = new SelectList(
            enrolledStudents.Select(e => new { e.StudentProfileId, Name = e.StudentProfile?.Name }),
            "StudentProfileId", "Name");
        ViewBag.Exam = exam;
        return View(new ExamResult { ExamId = examId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> AddResult(ExamResult result)
    {
        var exam = await _db.Exams.FindAsync(result.ExamId);
        if (exam == null) return NotFound();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(exam.CourseId)) return Forbid();

        if (result.Score > exam.MaxScore)
            ModelState.AddModelError("Score", $"Score cannot exceed {exam.MaxScore}.");

        if (await _db.ExamResults.AnyAsync(r => r.ExamId == result.ExamId && r.StudentProfileId == result.StudentProfileId))
            ModelState.AddModelError("", "Result already exists for this student.");

        if (!ModelState.IsValid)
        {
            var enrolledStudents = await _db.CourseEnrolments
                .Where(e => e.CourseId == exam.CourseId)
                .Include(e => e.StudentProfile)
                .ToListAsync();
            ViewBag.Students = new SelectList(
                enrolledStudents.Select(e => new { e.StudentProfileId, Name = e.StudentProfile?.Name }),
                "StudentProfileId", "Name");
            ViewBag.Exam = exam;
            return View(result);
        }

        // Calculate grade
        result.Grade = CalculateGrade(result.Score, exam.MaxScore);
        _db.ExamResults.Add(result);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Exam result recorded.";
        return RedirectToAction(nameof(Details), new { id = result.ExamId });
    }

    public static string CalculateGrade(decimal score, decimal maxScore)
    {
        var pct = (double)(score / maxScore * 100);
        return pct switch
        {
            >= 85 => "A",
            >= 70 => "B",
            >= 55 => "C",
            >= 40 => "D",
            _ => "F"
        };
    }

    private async Task PopulateCoursesAsync(int? selectedId = null)
    {
        var userId = _userManager.GetUserId(User);
        IQueryable<Course> query = _db.Courses.Include(c => c.Branch);
        if (User.IsInRole("Faculty"))
        {
            var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
            if (profile != null)
            {
                var courseIds = await _db.FacultyCourseAssignments
                    .Where(a => a.FacultyProfileId == profile.Id)
                    .Select(a => a.CourseId)
                    .ToListAsync();
                query = query.Where(c => courseIds.Contains(c.Id));
            }
        }
        ViewBag.Courses = new SelectList(await query.ToListAsync(), "Id", "Name", selectedId);
    }

    private async Task<bool> IsFacultyCourseAsync(int courseId)
    {
        var userId = _userManager.GetUserId(User);
        var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
        if (profile == null) return false;
        return await _db.FacultyCourseAssignments
            .AnyAsync(a => a.FacultyProfileId == profile.Id && a.CourseId == courseId);
    }
}
