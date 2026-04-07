using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class FacultyController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public FacultyController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var faculty = await _db.FacultyProfiles
            .Include(f => f.CourseAssignments).ThenInclude(a => a.Course)
            .ToListAsync();
        return View(faculty);
    }

    public async Task<IActionResult> Details(int id)
    {
        var profile = await _db.FacultyProfiles
            .Include(f => f.CourseAssignments).ThenInclude(a => a.Course).ThenInclude(c => c!.Branch)
            .FirstOrDefaultAsync(f => f.Id == id);
        if (profile == null) return NotFound();
        return View(profile);
    }

    public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FacultyProfile profile)
    {
        if (!ModelState.IsValid) return View(profile);
        _db.FacultyProfiles.Add(profile);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Faculty profile created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var profile = await _db.FacultyProfiles.FindAsync(id);
        if (profile == null) return NotFound();
        return View(profile);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, FacultyProfile profile)
    {
        if (id != profile.Id) return BadRequest();
        if (!ModelState.IsValid) return View(profile);
        _db.FacultyProfiles.Update(profile);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Faculty profile updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var profile = await _db.FacultyProfiles.FindAsync(id);
        if (profile == null) return NotFound();
        return View(profile);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var profile = await _db.FacultyProfiles.FindAsync(id);
        if (profile != null)
        {
            _db.FacultyProfiles.Remove(profile);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Faculty profile deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> AssignCourse(int id)
    {
        var profile = await _db.FacultyProfiles.FindAsync(id);
        if (profile == null) return NotFound();
        var assignedCourseIds = await _db.FacultyCourseAssignments
            .Where(a => a.FacultyProfileId == id)
            .Select(a => a.CourseId)
            .ToListAsync();
        ViewBag.Courses = new SelectList(
            await _db.Courses.Include(c => c.Branch)
                .Where(c => !assignedCourseIds.Contains(c.Id))
                .ToListAsync(),
            "Id", "Name");
        ViewBag.FacultyProfile = profile;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignCourse(int id, int courseId)
    {
        if (await _db.FacultyCourseAssignments.AnyAsync(a => a.FacultyProfileId == id && a.CourseId == courseId))
        {
            TempData["Error"] = "Already assigned.";
            return RedirectToAction(nameof(Details), new { id });
        }
        _db.FacultyCourseAssignments.Add(new FacultyCourseAssignment { FacultyProfileId = id, CourseId = courseId });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Course assigned to faculty.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCourseAssignment(int assignmentId, int facultyId)
    {
        var assignment = await _db.FacultyCourseAssignments.FindAsync(assignmentId);
        if (assignment != null)
        {
            _db.FacultyCourseAssignments.Remove(assignment);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Assignment removed.";
        }
        return RedirectToAction(nameof(Details), new { id = facultyId });
    }
}
