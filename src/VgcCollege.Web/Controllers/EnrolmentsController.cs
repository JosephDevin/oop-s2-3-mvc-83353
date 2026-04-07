using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

[Authorize(Roles = "Administrator,Faculty")]
public class EnrolmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public EnrolmentsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        IQueryable<CourseEnrolment> query = _db.CourseEnrolments
            .Include(e => e.StudentProfile)
            .Include(e => e.Course).ThenInclude(c => c!.Branch);

        if (User.IsInRole("Faculty"))
        {
            var userId = _userManager.GetUserId(User);
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

    public async Task<IActionResult> Details(int id)
    {
        var enrolment = await _db.CourseEnrolments
            .Include(e => e.StudentProfile)
            .Include(e => e.Course).ThenInclude(c => c!.Branch)
            .Include(e => e.AttendanceRecords)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (enrolment == null) return NotFound();

        if (User.IsInRole("Faculty") && !await IsFacultyCourseAsync(enrolment.CourseId))
            return Forbid();

        return View(enrolment);
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Students = new SelectList(await _db.StudentProfiles.ToListAsync(), "Id", "Name");
        ViewBag.Courses = new SelectList(await _db.Courses.Include(c => c.Branch).ToListAsync(), "Id", "Name");
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Create(CourseEnrolment enrolment)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Students = new SelectList(await _db.StudentProfiles.ToListAsync(), "Id", "Name");
            ViewBag.Courses = new SelectList(await _db.Courses.Include(c => c.Branch).ToListAsync(), "Id", "Name");
            return View(enrolment);
        }

        if (await _db.CourseEnrolments.AnyAsync(e => e.StudentProfileId == enrolment.StudentProfileId && e.CourseId == enrolment.CourseId))
        {
            ModelState.AddModelError("", "Student is already enrolled in this course.");
            ViewBag.Students = new SelectList(await _db.StudentProfiles.ToListAsync(), "Id", "Name");
            ViewBag.Courses = new SelectList(await _db.Courses.Include(c => c.Branch).ToListAsync(), "Id", "Name");
            return View(enrolment);
        }

        enrolment.Status = EnrolmentStatus.Active;
        _db.CourseEnrolments.Add(enrolment);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Student enrolled successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id)
    {
        var enrolment = await _db.CourseEnrolments.FindAsync(id);
        if (enrolment == null) return NotFound();
        ViewBag.Students = new SelectList(await _db.StudentProfiles.ToListAsync(), "Id", "Name", enrolment.StudentProfileId);
        ViewBag.Courses = new SelectList(await _db.Courses.Include(c => c.Branch).ToListAsync(), "Id", "Name", enrolment.CourseId);
        return View(enrolment);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Edit(int id, CourseEnrolment enrolment)
    {
        if (id != enrolment.Id) return BadRequest();
        if (!ModelState.IsValid)
        {
            ViewBag.Students = new SelectList(await _db.StudentProfiles.ToListAsync(), "Id", "Name");
            ViewBag.Courses = new SelectList(await _db.Courses.Include(c => c.Branch).ToListAsync(), "Id", "Name");
            return View(enrolment);
        }
        _db.CourseEnrolments.Update(enrolment);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Enrolment updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        var enrolment = await _db.CourseEnrolments
            .Include(e => e.StudentProfile)
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (enrolment == null) return NotFound();
        return View(enrolment);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var enrolment = await _db.CourseEnrolments.FindAsync(id);
        if (enrolment != null)
        {
            _db.CourseEnrolments.Remove(enrolment);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Enrolment removed.";
        }
        return RedirectToAction(nameof(Index));
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
