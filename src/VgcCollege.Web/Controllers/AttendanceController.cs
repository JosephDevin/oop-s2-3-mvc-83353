using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

[Authorize]
public class AttendanceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public AttendanceController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
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
            var records = await _db.AttendanceRecords
                .Include(a => a.CourseEnrolment).ThenInclude(e => e!.Course)
                .Where(a => a.CourseEnrolment!.StudentProfileId == profile.Id)
                .OrderByDescending(a => a.SessionDate)
                .ToListAsync();
            return View(records);
        }

        IQueryable<AttendanceRecord> query = _db.AttendanceRecords
            .Include(a => a.CourseEnrolment).ThenInclude(e => e!.StudentProfile)
            .Include(a => a.CourseEnrolment).ThenInclude(e => e!.Course);

        if (User.IsInRole("Faculty"))
        {
            var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
            if (profile == null) return Forbid();
            var courseIds = await _db.FacultyCourseAssignments
                .Where(a => a.FacultyProfileId == profile.Id)
                .Select(a => a.CourseId)
                .ToListAsync();
            query = query.Where(a => courseIds.Contains(a.CourseEnrolment!.CourseId));
        }

        return View(await query.OrderByDescending(a => a.SessionDate).ToListAsync());
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Create()
    {
        await PopulateEnrolmentsAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Create(AttendanceRecord record)
    {
        if (User.IsInRole("Faculty"))
        {
            var enrolment = await _db.CourseEnrolments.FindAsync(record.CourseEnrolmentId);
            if (enrolment == null || !await IsFacultyCourseAsync(enrolment.CourseId))
            {
                return Forbid();
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateEnrolmentsAsync();
            return View(record);
        }

        _db.AttendanceRecords.Add(record);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Attendance record saved.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Edit(int id)
    {
        var record = await _db.AttendanceRecords.FindAsync(id);
        if (record == null) return NotFound();

        if (User.IsInRole("Faculty"))
        {
            var enrolment = await _db.CourseEnrolments.FindAsync(record.CourseEnrolmentId);
            if (enrolment == null || !await IsFacultyCourseAsync(enrolment.CourseId)) return Forbid();
        }

        await PopulateEnrolmentsAsync(record.CourseEnrolmentId);
        return View(record);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator,Faculty")]
    public async Task<IActionResult> Edit(int id, AttendanceRecord record)
    {
        if (id != record.Id) return BadRequest();
        if (User.IsInRole("Faculty"))
        {
            var enrolment = await _db.CourseEnrolments.FindAsync(record.CourseEnrolmentId);
            if (enrolment == null || !await IsFacultyCourseAsync(enrolment.CourseId)) return Forbid();
        }
        if (!ModelState.IsValid)
        {
            await PopulateEnrolmentsAsync(record.CourseEnrolmentId);
            return View(record);
        }
        _db.AttendanceRecords.Update(record);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Attendance updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _db.AttendanceRecords
            .Include(a => a.CourseEnrolment).ThenInclude(e => e!.StudentProfile)
            .Include(a => a.CourseEnrolment).ThenInclude(e => e!.Course)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (record == null) return NotFound();
        return View(record);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var record = await _db.AttendanceRecords.FindAsync(id);
        if (record != null)
        {
            _db.AttendanceRecords.Remove(record);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Attendance record deleted.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateEnrolmentsAsync(int? selectedId = null)
    {
        var enrolments = await _db.CourseEnrolments
            .Include(e => e.StudentProfile)
            .Include(e => e.Course)
            .ToListAsync();
        ViewBag.CourseEnrolments = new SelectList(
            enrolments.Select(e => new { e.Id, Label = $"{e.StudentProfile?.Name} - {e.Course?.Name}" }),
            "Id", "Label", selectedId);
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
