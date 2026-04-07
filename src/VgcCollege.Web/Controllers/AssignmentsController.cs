using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

[Authorize]
public class AssignmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public AssignmentsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
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
            var results = await _db.AssignmentResults
                .Include(r => r.Assignment).ThenInclude(a => a!.Course)
                .Where(r => r.StudentProfileId == profile.Id)
                .ToListAsync();
            return View("StudentResults", results);
        }

        IQueryable<Assignment> query = _db.Assignments.Include(a => a.Course).ThenInclude(c => c!.Branch);

        if (User.IsInRole("Faculty"))
        {
            var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
            if (profile == null) return Forbid();
            var courseIds = await _db.FacultyCourseAssignments
                .Where(a => a.FacultyProfileId == profile.Id)
                .Select(a => a.CourseId)
                .ToListAsync();
            query = query.Where(a => courseIds.Contains(a.CourseId));
        }

        return View(await query.ToListAsync());
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Details(int id)
    {
        var assignment = await _db.Assignments
            .Include(a => a.Course)
            .Include(a => a.Results).ThenInclude(r => r.StudentProfile)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (assignment == null) return NotFound();

        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(assignment.CourseId))
            return Forbid();

        return View(assignment);
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Create()
    {
        await PopulateCoursesAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Create(Assignment assignment)
    {
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(assignment.CourseId))
            return Forbid();

        if (!ModelState.IsValid)
        {
            await PopulateCoursesAsync();
            return View(assignment);
        }
        _db.Assignments.Add(assignment);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Assignment created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Edit(int id)
    {
        var assignment = await _db.Assignments.FindAsync(id);
        if (assignment == null) return NotFound();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(assignment.CourseId)) return Forbid();
        await PopulateCoursesAsync(assignment.CourseId);
        return View(assignment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Edit(int id, Assignment assignment)
    {
        if (id != assignment.Id) return BadRequest();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(assignment.CourseId)) return Forbid();
        if (!ModelState.IsValid)
        {
            await PopulateCoursesAsync(assignment.CourseId);
            return View(assignment);
        }
        _db.Assignments.Update(assignment);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Assignment updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Delete(int id)
    {
        var assignment = await _db.Assignments.Include(a => a.Course).FirstOrDefaultAsync(a => a.Id == id);
        if (assignment == null) return NotFound();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(assignment.CourseId)) return Forbid();
        return View(assignment);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var assignment = await _db.Assignments.FindAsync(id);
        if (assignment != null)
        {
            _db.Assignments.Remove(assignment);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Assignment deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> AddResult(int assignmentId)
    {
        var assignment = await _db.Assignments.Include(a => a.Course).FirstOrDefaultAsync(a => a.Id == assignmentId);
        if (assignment == null) return NotFound();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(assignment.CourseId)) return Forbid();

        var enrolledStudents = await _db.CourseEnrolments
            .Where(e => e.CourseId == assignment.CourseId)
            .Include(e => e.StudentProfile)
            .ToListAsync();

        ViewBag.Students = new SelectList(
            enrolledStudents.Select(e => new { e.StudentProfileId, Name = e.StudentProfile?.Name }),
            "StudentProfileId", "Name");
        ViewBag.Assignment = assignment;
        return View(new AssignmentResult { AssignmentId = assignmentId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> AddResult(AssignmentResult result)
    {
        var assignment = await _db.Assignments.FindAsync(result.AssignmentId);
        if (assignment == null) return NotFound();
        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(assignment.CourseId)) return Forbid();

        if (result.Score > assignment.MaxScore)
        {
            ModelState.AddModelError("Score", $"Score cannot exceed maximum score of {assignment.MaxScore}.");
        }

        if (await _db.AssignmentResults.AnyAsync(r => r.AssignmentId == result.AssignmentId && r.StudentProfileId == result.StudentProfileId))
        {
            ModelState.AddModelError("", "Result already exists for this student.");
        }

        if (!ModelState.IsValid)
        {
            var enrolledStudents = await _db.CourseEnrolments
                .Where(e => e.CourseId == assignment.CourseId)
                .Include(e => e.StudentProfile)
                .ToListAsync();
            ViewBag.Students = new SelectList(
                enrolledStudents.Select(e => new { e.StudentProfileId, Name = e.StudentProfile?.Name }),
                "StudentProfileId", "Name");
            ViewBag.Assignment = assignment;
            return View(result);
        }

        _db.AssignmentResults.Add(result);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Result recorded.";
        return RedirectToAction(nameof(Details), new { id = result.AssignmentId });
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
