using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

[Authorize(Roles = "Administrator,Faculty")]
public class CoursesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public CoursesController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        IQueryable<Course> query = _db.Courses.Include(c => c.Branch);

        if (User.IsInRole("Faculty"))
        {
            var userId = _userManager.GetUserId(User);
            var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
            if (profile == null) return Forbid();
            var courseIds = await _db.FacultyCourseAssignments
                .Where(a => a.FacultyProfileId == profile.Id)
                .Select(a => a.CourseId)
                .ToListAsync();
            query = query.Where(c => courseIds.Contains(c.Id));
        }

        return View(await query.ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var course = await _db.Courses
            .Include(c => c.Branch)
            .Include(c => c.Enrolments).ThenInclude(e => e.StudentProfile)
            .Include(c => c.FacultyAssignments).ThenInclude(a => a.FacultyProfile)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (course == null) return NotFound();

        if (User.IsInRole("Faculty") && !await IsFacultyAssignedAsync(id))
            return Forbid();

        return View(course);
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create(Course course)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name");
            return View(course);
        }
        _db.Courses.Add(course);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Course created.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course == null) return NotFound();
        ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name", course.BranchId);
        return View(course);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id, Course course)
    {
        if (id != course.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            ViewBag.Branches = new SelectList(await _db.Branches.ToListAsync(), "Id", "Name", course.BranchId);
            return View(course);
        }
        _db.Courses.Update(course);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Course updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await _db.Courses.Include(c => c.Branch).FirstOrDefaultAsync(c => c.Id == id);
        if (course == null) return NotFound();
        return View(course);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var course = await _db.Courses.FindAsync(id);
        if (course != null)
        {
            _db.Courses.Remove(course);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Course deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsFacultyAssignedAsync(int courseId)
    {
        var userId = _userManager.GetUserId(User);
        var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
        if (profile == null) return false;
        return await _db.FacultyCourseAssignments
            .AnyAsync(a => a.FacultyProfileId == profile.Id && a.CourseId == courseId);
    }
}
