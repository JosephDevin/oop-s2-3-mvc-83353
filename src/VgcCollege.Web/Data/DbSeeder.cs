using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await db.Database.MigrateAsync();

        // Roles
        string[] roles = { "Administrator", "Faculty", "Student" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // Admin user
        var admin = await EnsureUser(userManager, "admin@vgc.ie", "Admin@123", "Administrator");

        // Faculty users
        var fac1 = await EnsureUser(userManager, "faculty1@vgc.ie", "Faculty@123", "Faculty");
        var fac2 = await EnsureUser(userManager, "faculty2@vgc.ie", "Faculty@123", "Faculty");

        // Student users
        var stu1 = await EnsureUser(userManager, "student1@vgc.ie", "Student@123", "Student");
        var stu2 = await EnsureUser(userManager, "student2@vgc.ie", "Student@123", "Student");
        var stu3 = await EnsureUser(userManager, "student3@vgc.ie", "Student@123", "Student");

        // Branches
        if (!await db.Branches.AnyAsync())
        {
            var branches = new List<Branch>
            {
                new Branch { Name = "Dublin City", Address = "123 O'Connell Street, Dublin 1" },
                new Branch { Name = "Cork Campus", Address = "45 Patrick Street, Cork City" },
                new Branch { Name = "Galway Campus", Address = "78 Shop Street, Galway City" }
            };
            db.Branches.AddRange(branches);
            await db.SaveChangesAsync();
        }

        var dublinBranch = await db.Branches.FirstAsync(b => b.Name == "Dublin City");
        var corkBranch = await db.Branches.FirstAsync(b => b.Name == "Cork Campus");
        var galwayBranch = await db.Branches.FirstAsync(b => b.Name == "Galway Campus");

        // Courses
        if (!await db.Courses.AnyAsync())
        {
            var courses = new List<Course>
            {
                new Course { Name = "BSc Computer Science", BranchId = dublinBranch.Id, StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 6, 30) },
                new Course { Name = "BA Business Management", BranchId = corkBranch.Id, StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 6, 30) },
                new Course { Name = "MSc Data Analytics", BranchId = galwayBranch.Id, StartDate = new DateTime(2025, 10, 1), EndDate = new DateTime(2026, 9, 30) },
                new Course { Name = "Diploma in Web Development", BranchId = dublinBranch.Id, StartDate = new DateTime(2026, 1, 15), EndDate = new DateTime(2026, 12, 15) }
            };
            db.Courses.AddRange(courses);
            await db.SaveChangesAsync();
        }

        // Faculty profiles
        if (!await db.FacultyProfiles.AnyAsync())
        {
            db.FacultyProfiles.AddRange(
                new FacultyProfile { IdentityUserId = fac1.Id, Name = "Dr. Mary O'Brien", Email = "faculty1@vgc.ie", Phone = "+353-1-2345678" },
                new FacultyProfile { IdentityUserId = fac2.Id, Name = "Prof. Seán Murphy", Email = "faculty2@vgc.ie", Phone = "+353-21-9876543" }
            );
            await db.SaveChangesAsync();
        }

        var fp1 = await db.FacultyProfiles.FirstAsync(f => f.IdentityUserId == fac1.Id);
        var fp2 = await db.FacultyProfiles.FirstAsync(f => f.IdentityUserId == fac2.Id);
        var allCourses = await db.Courses.ToListAsync();
        var csBsc = allCourses.First(c => c.Name == "BSc Computer Science");
        var baBus = allCourses.First(c => c.Name == "BA Business Management");
        var mscData = allCourses.First(c => c.Name == "MSc Data Analytics");
        var dipWeb = allCourses.First(c => c.Name == "Diploma in Web Development");

        // Faculty course assignments
        if (!await db.FacultyCourseAssignments.AnyAsync())
        {
            db.FacultyCourseAssignments.AddRange(
                new FacultyCourseAssignment { FacultyProfileId = fp1.Id, CourseId = csBsc.Id },
                new FacultyCourseAssignment { FacultyProfileId = fp1.Id, CourseId = dipWeb.Id },
                new FacultyCourseAssignment { FacultyProfileId = fp2.Id, CourseId = baBus.Id },
                new FacultyCourseAssignment { FacultyProfileId = fp2.Id, CourseId = mscData.Id }
            );
            await db.SaveChangesAsync();
        }

        // Student profiles
        if (!await db.StudentProfiles.AnyAsync())
        {
            db.StudentProfiles.AddRange(
                new StudentProfile { IdentityUserId = stu1.Id, StudentNumber = "VGC2025001", Name = "Aoife Kelly", Email = "student1@vgc.ie", Phone = "+353-87-1234567", Address = "10 Grafton St, Dublin 2", DateOfBirth = new DateTime(2002, 3, 15) },
                new StudentProfile { IdentityUserId = stu2.Id, StudentNumber = "VGC2025002", Name = "Ciarán Walsh", Email = "student2@vgc.ie", Phone = "+353-85-9876543", Address = "22 South Mall, Cork", DateOfBirth = new DateTime(2001, 7, 22) },
                new StudentProfile { IdentityUserId = stu3.Id, StudentNumber = "VGC2025003", Name = "Siobhán Brennan", Email = "student3@vgc.ie", Phone = "+353-86-5551234", Address = "5 Eyre Square, Galway", DateOfBirth = new DateTime(2003, 11, 8) }
            );
            await db.SaveChangesAsync();
        }

        var sp1 = await db.StudentProfiles.FirstAsync(s => s.IdentityUserId == stu1.Id);
        var sp2 = await db.StudentProfiles.FirstAsync(s => s.IdentityUserId == stu2.Id);
        var sp3 = await db.StudentProfiles.FirstAsync(s => s.IdentityUserId == stu3.Id);

        // Enrolments
        if (!await db.CourseEnrolments.AnyAsync())
        {
            db.CourseEnrolments.AddRange(
                new CourseEnrolment { StudentProfileId = sp1.Id, CourseId = csBsc.Id, EnrolDate = new DateTime(2025, 8, 20), Status = EnrolmentStatus.Active },
                new CourseEnrolment { StudentProfileId = sp1.Id, CourseId = dipWeb.Id, EnrolDate = new DateTime(2026, 1, 10), Status = EnrolmentStatus.Active },
                new CourseEnrolment { StudentProfileId = sp2.Id, CourseId = baBus.Id, EnrolDate = new DateTime(2025, 8, 25), Status = EnrolmentStatus.Active },
                new CourseEnrolment { StudentProfileId = sp2.Id, CourseId = mscData.Id, EnrolDate = new DateTime(2025, 9, 15), Status = EnrolmentStatus.Withdrawn },
                new CourseEnrolment { StudentProfileId = sp3.Id, CourseId = mscData.Id, EnrolDate = new DateTime(2025, 9, 20), Status = EnrolmentStatus.Active },
                new CourseEnrolment { StudentProfileId = sp3.Id, CourseId = baBus.Id, EnrolDate = new DateTime(2025, 8, 28), Status = EnrolmentStatus.Completed }
            );
            await db.SaveChangesAsync();
        }

        var enrolments = await db.CourseEnrolments.ToListAsync();
        var e1 = enrolments.First(e => e.StudentProfileId == sp1.Id && e.CourseId == csBsc.Id);
        var e3 = enrolments.First(e => e.StudentProfileId == sp2.Id && e.CourseId == baBus.Id);
        var e5 = enrolments.First(e => e.StudentProfileId == sp3.Id && e.CourseId == mscData.Id);

        // Attendance records
        if (!await db.AttendanceRecords.AnyAsync())
        {
            db.AttendanceRecords.AddRange(
                new AttendanceRecord { CourseEnrolmentId = e1.Id, SessionDate = new DateTime(2025, 9, 8), WeekNumber = 1, Present = true },
                new AttendanceRecord { CourseEnrolmentId = e1.Id, SessionDate = new DateTime(2025, 9, 15), WeekNumber = 2, Present = true },
                new AttendanceRecord { CourseEnrolmentId = e1.Id, SessionDate = new DateTime(2025, 9, 22), WeekNumber = 3, Present = false },
                new AttendanceRecord { CourseEnrolmentId = e3.Id, SessionDate = new DateTime(2025, 9, 8), WeekNumber = 1, Present = true },
                new AttendanceRecord { CourseEnrolmentId = e3.Id, SessionDate = new DateTime(2025, 9, 15), WeekNumber = 2, Present = false },
                new AttendanceRecord { CourseEnrolmentId = e5.Id, SessionDate = new DateTime(2025, 10, 6), WeekNumber = 1, Present = true },
                new AttendanceRecord { CourseEnrolmentId = e5.Id, SessionDate = new DateTime(2025, 10, 13), WeekNumber = 2, Present = true }
            );
            await db.SaveChangesAsync();
        }

        // Assignments
        if (!await db.Assignments.AnyAsync())
        {
            db.Assignments.AddRange(
                new Assignment { CourseId = csBsc.Id, Title = "Programming Fundamentals CA1", MaxScore = 100, DueDate = new DateTime(2025, 11, 1) },
                new Assignment { CourseId = csBsc.Id, Title = "Data Structures Project", MaxScore = 100, DueDate = new DateTime(2026, 2, 15) },
                new Assignment { CourseId = baBus.Id, Title = "Marketing Strategy Report", MaxScore = 100, DueDate = new DateTime(2025, 11, 30) },
                new Assignment { CourseId = mscData.Id, Title = "Machine Learning Lab Report", MaxScore = 50, DueDate = new DateTime(2025, 12, 15) }
            );
            await db.SaveChangesAsync();
        }

        var assignments = await db.Assignments.ToListAsync();
        var asgn1 = assignments.First(a => a.CourseId == csBsc.Id && a.Title.Contains("CA1"));
        var asgn3 = assignments.First(a => a.CourseId == baBus.Id);
        var asgn4 = assignments.First(a => a.CourseId == mscData.Id);

        // Assignment results
        if (!await db.AssignmentResults.AnyAsync())
        {
            db.AssignmentResults.AddRange(
                new AssignmentResult { AssignmentId = asgn1.Id, StudentProfileId = sp1.Id, Score = 78, Feedback = "Good understanding of loops and functions." },
                new AssignmentResult { AssignmentId = asgn3.Id, StudentProfileId = sp2.Id, Score = 85, Feedback = "Excellent analysis of the market." },
                new AssignmentResult { AssignmentId = asgn4.Id, StudentProfileId = sp3.Id, Score = 42, Feedback = "Strong practical work." }
            );
            await db.SaveChangesAsync();
        }

        // Exams
        if (!await db.Exams.AnyAsync())
        {
            db.Exams.AddRange(
                new Exam { CourseId = csBsc.Id, Title = "Semester 1 Exam", ExamDate = new DateTime(2026, 1, 20), MaxScore = 100, ResultsReleased = true },
                new Exam { CourseId = csBsc.Id, Title = "Semester 2 Exam", ExamDate = new DateTime(2026, 6, 10), MaxScore = 100, ResultsReleased = false },
                new Exam { CourseId = baBus.Id, Title = "Business Law Exam", ExamDate = new DateTime(2026, 1, 22), MaxScore = 100, ResultsReleased = true },
                new Exam { CourseId = mscData.Id, Title = "Statistics Exam", ExamDate = new DateTime(2026, 1, 25), MaxScore = 100, ResultsReleased = false }
            );
            await db.SaveChangesAsync();
        }

        var exams = await db.Exams.ToListAsync();
        var exam1 = exams.First(e => e.CourseId == csBsc.Id && e.ResultsReleased);
        var exam3 = exams.First(e => e.CourseId == baBus.Id);
        var exam4 = exams.First(e => e.CourseId == mscData.Id);

        // Exam results
        if (!await db.ExamResults.AnyAsync())
        {
            db.ExamResults.AddRange(
                new ExamResult { ExamId = exam1.Id, StudentProfileId = sp1.Id, Score = 72, Grade = "B" },
                new ExamResult { ExamId = exam3.Id, StudentProfileId = sp2.Id, Score = 88, Grade = "A" },
                new ExamResult { ExamId = exam4.Id, StudentProfileId = sp3.Id, Score = 65, Grade = "C" }
            );
            await db.SaveChangesAsync();
        }
    }

    private static async Task<IdentityUser> EnsureUser(UserManager<IdentityUser> userManager, string email, string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new Exception($"Failed to create user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
        return user;
    }
}
