using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

[Authorize]
public class StudentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public StudentsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);

        if (User.IsInRole("Administrator"))
        {
            var students = await _db.StudentProfiles
                .Include(s => s.Enrolments)
                .ToListAsync();
            return View(students);
        }
        else if (User.IsInRole("Faculty"))
        {
            var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
            if (profile == null) return Forbid();
            var courseIds = await _db.FacultyCourseAssignments
                .Where(a => a.FacultyProfileId == profile.Id)
                .Select(a => a.CourseId)
                .ToListAsync();
            var studentIds = await _db.CourseEnrolments
                .Where(e => courseIds.Contains(e.CourseId))
                .Select(e => e.StudentProfileId)
                .Distinct()
                .ToListAsync();
            var students = await _db.StudentProfiles
                .Where(s => studentIds.Contains(s.Id))
                .Include(s => s.Enrolments)
                .ToListAsync();
            return View(students);
        }
        else if (User.IsInRole("Student"))
        {
            return RedirectToAction(nameof(MyProfile));
        }

        return Forbid();
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User);

        if (User.IsInRole("Student"))
        {
            var myProfile = await _db.StudentProfiles.FirstOrDefaultAsync(s => s.IdentityUserId == userId);
            if (myProfile == null || myProfile.Id != id) return Forbid();
        }
        else if (User.IsInRole("Faculty"))
        {
            if (!await IsFacultyStudentAsync(id)) return Forbid();
        }

        var student = await _db.StudentProfiles
            .Include(s => s.Enrolments).ThenInclude(e => e.Course).ThenInclude(c => c!.Branch)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (student == null) return NotFound();
        return View(student);
    }

    [Authorize(Roles = "Student")]
    public async Task<IActionResult> MyProfile()
    {
        var userId = _userManager.GetUserId(User);
        var profile = await _db.StudentProfiles
            .Include(s => s.Enrolments).ThenInclude(e => e.Course).ThenInclude(c => c!.Branch)
            .FirstOrDefaultAsync(s => s.IdentityUserId == userId);
        if (profile == null) return NotFound();
        return View(profile);
    }

    [Authorize(Roles = "Administrator")]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create(StudentProfile student)
    {
        if (!ModelState.IsValid) return View(student);
        if (await _db.StudentProfiles.AnyAsync(s => s.StudentNumber == student.StudentNumber))
        {
            ModelState.AddModelError("StudentNumber", "Student number already exists.");
            return View(student);
        }
        _db.StudentProfiles.Add(student);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Student profile created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id)
    {
        var student = await _db.StudentProfiles.FindAsync(id);
        if (student == null) return NotFound();
        return View(student);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id, StudentProfile student)
    {
        if (id != student.Id) return BadRequest();
        if (!ModelState.IsValid) return View(student);
        _db.StudentProfiles.Update(student);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Student profile updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        var student = await _db.StudentProfiles.FindAsync(id);
        if (student == null) return NotFound();
        return View(student);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var student = await _db.StudentProfiles.FindAsync(id);
        if (student != null)
        {
            _db.StudentProfiles.Remove(student);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Student profile deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsFacultyStudentAsync(int studentId)
    {
        var userId = _userManager.GetUserId(User);
        var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
        if (profile == null) return false;
        var courseIds = await _db.FacultyCourseAssignments
            .Where(a => a.FacultyProfileId == profile.Id)
            .Select(a => a.CourseId)
            .ToListAsync();
        return await _db.CourseEnrolments
            .AnyAsync(e => courseIds.Contains(e.CourseId) && e.StudentProfileId == studentId);
    }
}
