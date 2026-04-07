using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public HomeController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);

        if (User.IsInRole("Administrator"))
        {
            ViewBag.BranchCount = await _db.Branches.CountAsync();
            ViewBag.CourseCount = await _db.Courses.CountAsync();
            ViewBag.StudentCount = await _db.StudentProfiles.CountAsync();
            ViewBag.FacultyCount = await _db.FacultyProfiles.CountAsync();
            ViewBag.EnrolmentCount = await _db.CourseEnrolments.CountAsync(e => e.Status == EnrolmentStatus.Active);
            return View("AdminDashboard");
        }
        else if (User.IsInRole("Faculty"))
        {
            var profile = await _db.FacultyProfiles.FirstOrDefaultAsync(f => f.IdentityUserId == userId);
            if (profile == null) return View("NeedProfile");
            var assignedCourseIds = await _db.FacultyCourseAssignments
                .Where(a => a.FacultyProfileId == profile.Id)
                .Select(a => a.CourseId)
                .ToListAsync();
            ViewBag.CourseCount = assignedCourseIds.Count;
            ViewBag.StudentCount = await _db.CourseEnrolments
                .Where(e => assignedCourseIds.Contains(e.CourseId) && e.Status == EnrolmentStatus.Active)
                .Select(e => e.StudentProfileId)
                .Distinct()
                .CountAsync();
            ViewBag.FacultyName = profile.Name;
            return View("FacultyDashboard");
        }
        else if (User.IsInRole("Student"))
        {
            var profile = await _db.StudentProfiles.FirstOrDefaultAsync(s => s.IdentityUserId == userId);
            if (profile == null) return View("NeedProfile");
            ViewBag.StudentName = profile.Name;
            ViewBag.StudentNumber = profile.StudentNumber;
            ViewBag.ActiveEnrolments = await _db.CourseEnrolments
                .Where(e => e.StudentProfileId == profile.Id && e.Status == EnrolmentStatus.Active)
                .CountAsync();
            return View("StudentDashboard");
        }

        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    [AllowAnonymous]
    [Route("/Home/NotFound")]
    public IActionResult NotFoundPage() => View("NotFound");

    [AllowAnonymous]
    [Route("/Home/ServerError")]
    public IActionResult ServerErrorPage() => View("ServerError");
}
