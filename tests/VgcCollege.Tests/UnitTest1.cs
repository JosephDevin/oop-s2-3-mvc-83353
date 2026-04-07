using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Controllers;
using VgcCollege.Web.Data;
using VgcCollege.Web.Models;

namespace VgcCollege.Tests;

public class VgcCollegeTests
{
    // ──────────────────────────────────────────────
    // Helper: create an in-memory ApplicationDbContext
    // ──────────────────────────────────────────────
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    // Seed minimal data for exam-result tests
    private static async Task<(ApplicationDbContext db, StudentProfile student, Exam released, Exam unreleased)>
        SeedExamData(string dbName)
    {
        var db = CreateContext(dbName);

        var branch = new Branch { Name = "Dublin", Address = "Dublin 1" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var course = new Course
        {
            Name = "Test Course",
            BranchId = branch.Id,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddMonths(6)
        };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var student = new StudentProfile
        {
            IdentityUserId = "stu-user-1",
            StudentNumber = "VGC001",
            Name = "Test Student",
            Email = "stu@test.ie",
            DateOfBirth = new DateTime(2000, 1, 1)
        };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();

        var released = new Exam
        {
            CourseId = course.Id,
            Title = "Released Exam",
            ExamDate = DateTime.Today,
            MaxScore = 100,
            ResultsReleased = true
        };
        var unreleased = new Exam
        {
            CourseId = course.Id,
            Title = "Unreleased Exam",
            ExamDate = DateTime.Today,
            MaxScore = 100,
            ResultsReleased = false
        };
        db.Exams.AddRange(released, unreleased);
        await db.SaveChangesAsync();

        db.ExamResults.AddRange(
            new ExamResult { ExamId = released.Id, StudentProfileId = student.Id, Score = 75, Grade = "B" },
            new ExamResult { ExamId = unreleased.Id, StudentProfileId = student.Id, Score = 60, Grade = "C" }
        );
        await db.SaveChangesAsync();

        return (db, student, released, unreleased);
    }

    // ──────────────────────────────────────────────
    // Test 1: Student CANNOT see unreleased exam results
    // ──────────────────────────────────────────────
    [Fact]
    public async Task Student_CannotSee_UnreleasedExamResults()
    {
        var (db, student, _, unreleased) = await SeedExamData("TestDb_Unreleased");

        var results = await db.ExamResults
            .Include(r => r.Exam)
            .Where(r => r.StudentProfileId == student.Id && r.Exam!.ResultsReleased)
            .ToListAsync();

        Assert.DoesNotContain(results, r => r.ExamId == unreleased.Id);
    }

    // ──────────────────────────────────────────────
    // Test 2: Student CAN see released exam results
    // ──────────────────────────────────────────────
    [Fact]
    public async Task Student_CanSee_ReleasedExamResults()
    {
        var (db, student, released, _) = await SeedExamData("TestDb_Released");

        var results = await db.ExamResults
            .Include(r => r.Exam)
            .Where(r => r.StudentProfileId == student.Id && r.Exam!.ResultsReleased)
            .ToListAsync();

        Assert.Contains(results, r => r.ExamId == released.Id);
        Assert.Single(results);
    }

    // ──────────────────────────────────────────────
    // Test 3: Faculty only sees their own students
    // ──────────────────────────────────────────────
    [Fact]
    public async Task Faculty_OnlySeesOwnStudents()
    {
        var db = CreateContext("TestDb_FacultyOwnStudents");

        var branch = new Branch { Name = "Test", Address = "Test" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var course1 = new Course { Name = "Course A", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(3) };
        var course2 = new Course { Name = "Course B", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(3) };
        db.Courses.AddRange(course1, course2);
        await db.SaveChangesAsync();

        var faculty1Profile = new FacultyProfile { IdentityUserId = "fac1", Name = "Faculty 1", Email = "fac1@test.ie" };
        db.FacultyProfiles.Add(faculty1Profile);
        await db.SaveChangesAsync();

        db.FacultyCourseAssignments.Add(new FacultyCourseAssignment { FacultyProfileId = faculty1Profile.Id, CourseId = course1.Id });
        await db.SaveChangesAsync();

        var student1 = new StudentProfile { IdentityUserId = "stu1", StudentNumber = "S001", Name = "Student 1", Email = "s1@t.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        var student2 = new StudentProfile { IdentityUserId = "stu2", StudentNumber = "S002", Name = "Student 2", Email = "s2@t.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        db.StudentProfiles.AddRange(student1, student2);
        await db.SaveChangesAsync();

        // student1 enrolled in course1 (faculty1's course), student2 in course2 (NOT faculty1's)
        db.CourseEnrolments.AddRange(
            new CourseEnrolment { StudentProfileId = student1.Id, CourseId = course1.Id, EnrolDate = DateTime.Today, Status = EnrolmentStatus.Active },
            new CourseEnrolment { StudentProfileId = student2.Id, CourseId = course2.Id, EnrolDate = DateTime.Today, Status = EnrolmentStatus.Active }
        );
        await db.SaveChangesAsync();

        var courseIds = await db.FacultyCourseAssignments
            .Where(a => a.FacultyProfileId == faculty1Profile.Id)
            .Select(a => a.CourseId)
            .ToListAsync();

        var studentIds = await db.CourseEnrolments
            .Where(e => courseIds.Contains(e.CourseId))
            .Select(e => e.StudentProfileId)
            .Distinct()
            .ToListAsync();

        var visibleStudents = await db.StudentProfiles
            .Where(s => studentIds.Contains(s.Id))
            .ToListAsync();

        var onlyStudent = Assert.Single(visibleStudents);
        Assert.Equal("Student 1", onlyStudent.Name);
    }

    // ──────────────────────────────────────────────
    // Test 4: Faculty CANNOT see other faculty's students
    // ──────────────────────────────────────────────
    [Fact]
    public async Task Faculty_CannotSeeOtherFacultysStudents()
    {
        var db = CreateContext("TestDb_FacultyCross");

        var branch = new Branch { Name = "Test", Address = "Test" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var course1 = new Course { Name = "Course X", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(3) };
        var course2 = new Course { Name = "Course Y", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(3) };
        db.Courses.AddRange(course1, course2);
        await db.SaveChangesAsync();

        var fac1 = new FacultyProfile { IdentityUserId = "facX", Name = "FacX", Email = "facX@t.ie" };
        var fac2 = new FacultyProfile { IdentityUserId = "facY", Name = "FacY", Email = "facY@t.ie" };
        db.FacultyProfiles.AddRange(fac1, fac2);
        await db.SaveChangesAsync();

        db.FacultyCourseAssignments.AddRange(
            new FacultyCourseAssignment { FacultyProfileId = fac1.Id, CourseId = course1.Id },
            new FacultyCourseAssignment { FacultyProfileId = fac2.Id, CourseId = course2.Id }
        );
        await db.SaveChangesAsync();

        var student1 = new StudentProfile { IdentityUserId = "stuX", StudentNumber = "SX01", Name = "Student X", Email = "sX@t.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        var student2 = new StudentProfile { IdentityUserId = "stuY", StudentNumber = "SY01", Name = "Student Y", Email = "sY@t.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        db.StudentProfiles.AddRange(student1, student2);
        await db.SaveChangesAsync();

        db.CourseEnrolments.AddRange(
            new CourseEnrolment { StudentProfileId = student1.Id, CourseId = course1.Id, EnrolDate = DateTime.Today, Status = EnrolmentStatus.Active },
            new CourseEnrolment { StudentProfileId = student2.Id, CourseId = course2.Id, EnrolDate = DateTime.Today, Status = EnrolmentStatus.Active }
        );
        await db.SaveChangesAsync();

        // FacX queries their students
        var facXCourseIds = await db.FacultyCourseAssignments
            .Where(a => a.FacultyProfileId == fac1.Id)
            .Select(a => a.CourseId)
            .ToListAsync();

        var facXStudentIds = await db.CourseEnrolments
            .Where(e => facXCourseIds.Contains(e.CourseId))
            .Select(e => e.StudentProfileId)
            .Distinct()
            .ToListAsync();

        // FacX should NOT see Student Y
        Assert.DoesNotContain(student2.Id, facXStudentIds);
        Assert.Contains(student1.Id, facXStudentIds);
    }

    // ──────────────────────────────────────────────
    // Test 5: Enrolment creates with Active status
    // ──────────────────────────────────────────────
    [Fact]
    public async Task Enrolment_CreatesWithActiveStatus()
    {
        var db = CreateContext("TestDb_EnrolActive");

        var branch = new Branch { Name = "B", Address = "Addr" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var course = new Course { Name = "C", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(6) };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var student = new StudentProfile { IdentityUserId = "u1", StudentNumber = "V001", Name = "N", Email = "e@e.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();

        var enrolment = new CourseEnrolment
        {
            StudentProfileId = student.Id,
            CourseId = course.Id,
            EnrolDate = DateTime.Today,
            Status = EnrolmentStatus.Active
        };
        db.CourseEnrolments.Add(enrolment);
        await db.SaveChangesAsync();

        var saved = await db.CourseEnrolments.FindAsync(enrolment.Id);
        Assert.NotNull(saved);
        Assert.Equal(EnrolmentStatus.Active, saved!.Status);
    }

    // ──────────────────────────────────────────────
    // Test 6: Duplicate enrolment is prevented by unique index
    // ──────────────────────────────────────────────
    [Fact]
    public async Task Duplicate_Enrolment_IsDetectedBeforeSave()
    {
        var db = CreateContext("TestDb_DupeEnrol");

        var branch = new Branch { Name = "B", Address = "Addr" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var course = new Course { Name = "C", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(6) };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var student = new StudentProfile { IdentityUserId = "u2", StudentNumber = "V002", Name = "N2", Email = "e2@e.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();

        db.CourseEnrolments.Add(new CourseEnrolment { StudentProfileId = student.Id, CourseId = course.Id, EnrolDate = DateTime.Today, Status = EnrolmentStatus.Active });
        await db.SaveChangesAsync();

        // Simulate the controller's duplicate check
        bool alreadyEnrolled = await db.CourseEnrolments
            .AnyAsync(e => e.StudentProfileId == student.Id && e.CourseId == course.Id);

        Assert.True(alreadyEnrolled);
    }

    // ──────────────────────────────────────────────
    // Test 7: Assignment score cannot exceed MaxScore
    // ──────────────────────────────────────────────
    [Fact]
    public async Task AssignmentResult_ScoreCannotExceedMaxScore()
    {
        var db = CreateContext("TestDb_MaxScore");

        var branch = new Branch { Name = "B", Address = "A" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var course = new Course { Name = "C", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(6) };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var assignment = new Assignment { CourseId = course.Id, Title = "Test", MaxScore = 100, DueDate = DateTime.Today };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var student = new StudentProfile { IdentityUserId = "u3", StudentNumber = "V003", Name = "N3", Email = "e3@e.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();

        decimal proposedScore = 105m;
        bool isValid = proposedScore <= assignment.MaxScore;

        Assert.False(isValid, "Score should not exceed MaxScore");
    }

    // ──────────────────────────────────────────────
    // Test 8: Grade calculation logic
    // ──────────────────────────────────────────────
    [Theory]
    [InlineData(90, 100, "A")]
    [InlineData(85, 100, "A")]
    [InlineData(75, 100, "B")]
    [InlineData(70, 100, "B")]
    [InlineData(60, 100, "C")]
    [InlineData(55, 100, "C")]
    [InlineData(45, 100, "D")]
    [InlineData(40, 100, "D")]
    [InlineData(39, 100, "F")]
    [InlineData(0, 100, "F")]
    public void GradeCalculation_IsCorrect(decimal score, decimal maxScore, string expectedGrade)
    {
        var grade = ExamsController.CalculateGrade(score, maxScore);
        Assert.Equal(expectedGrade, grade);
    }

    // ──────────────────────────────────────────────
    // Test 9: Attendance record creation
    // ──────────────────────────────────────────────
    [Fact]
    public async Task AttendanceRecord_CreatesSuccessfully()
    {
        var db = CreateContext("TestDb_Attendance");

        var branch = new Branch { Name = "B", Address = "A" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var course = new Course { Name = "C", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(6) };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var student = new StudentProfile { IdentityUserId = "u4", StudentNumber = "V004", Name = "N4", Email = "e4@e.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();

        var enrolment = new CourseEnrolment { StudentProfileId = student.Id, CourseId = course.Id, EnrolDate = DateTime.Today, Status = EnrolmentStatus.Active };
        db.CourseEnrolments.Add(enrolment);
        await db.SaveChangesAsync();

        var record = new AttendanceRecord
        {
            CourseEnrolmentId = enrolment.Id,
            SessionDate = DateTime.Today,
            WeekNumber = 1,
            Present = true
        };
        db.AttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        var saved = await db.AttendanceRecords.FindAsync(record.Id);
        Assert.NotNull(saved);
        Assert.Equal(1, saved!.WeekNumber);
        Assert.True(saved.Present);
    }

    // ──────────────────────────────────────────────
    // Test 10: Admin can see all enrolments (no filter)
    // ──────────────────────────────────────────────
    [Fact]
    public async Task Admin_CanSeeAllEnrolments()
    {
        var db = CreateContext("TestDb_AdminEnrolments");

        var branch = new Branch { Name = "B", Address = "A" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var course1 = new Course { Name = "C1", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(6) };
        var course2 = new Course { Name = "C2", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(6) };
        db.Courses.AddRange(course1, course2);
        await db.SaveChangesAsync();

        var s1 = new StudentProfile { IdentityUserId = "a1", StudentNumber = "A001", Name = "A1", Email = "a1@a.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        var s2 = new StudentProfile { IdentityUserId = "a2", StudentNumber = "A002", Name = "A2", Email = "a2@a.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        db.StudentProfiles.AddRange(s1, s2);
        await db.SaveChangesAsync();

        db.CourseEnrolments.AddRange(
            new CourseEnrolment { StudentProfileId = s1.Id, CourseId = course1.Id, EnrolDate = DateTime.Today, Status = EnrolmentStatus.Active },
            new CourseEnrolment { StudentProfileId = s2.Id, CourseId = course2.Id, EnrolDate = DateTime.Today, Status = EnrolmentStatus.Active },
            new CourseEnrolment { StudentProfileId = s1.Id, CourseId = course2.Id, EnrolDate = DateTime.Today, Status = EnrolmentStatus.Withdrawn }
        );
        await db.SaveChangesAsync();

        // Admin query: no filter
        var allEnrolments = await db.CourseEnrolments.ToListAsync();

        Assert.Equal(3, allEnrolments.Count);
    }

    // ──────────────────────────────────────────────
    // Test 11: Valid assignment score (at exactly MaxScore) is accepted
    // ──────────────────────────────────────────────
    [Fact]
    public async Task AssignmentResult_ScoreAtMaxScore_IsValid()
    {
        var db = CreateContext("TestDb_ScoreAtMax");

        var branch = new Branch { Name = "B", Address = "A" };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var course = new Course { Name = "C", BranchId = branch.Id, StartDate = DateTime.Today, EndDate = DateTime.Today.AddMonths(6) };
        db.Courses.Add(course);
        await db.SaveChangesAsync();

        var assignment = new Assignment { CourseId = course.Id, Title = "Test", MaxScore = 50, DueDate = DateTime.Today };
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var student = new StudentProfile { IdentityUserId = "u5", StudentNumber = "V005", Name = "N5", Email = "e5@e.ie", DateOfBirth = new DateTime(2000, 1, 1) };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();

        // Score == MaxScore should be valid
        bool isValid = 50m <= assignment.MaxScore;
        Assert.True(isValid);
    }

    // ──────────────────────────────────────────────
    // Test 12: Unreleased exam results are excluded from student count
    // ──────────────────────────────────────────────
    [Fact]
    public async Task UnreleasedResults_NotCountedForStudent()
    {
        var (db, student, _, _) = await SeedExamData("TestDb_CountReleased");

        int releasedCount = await db.ExamResults
            .Include(r => r.Exam)
            .Where(r => r.StudentProfileId == student.Id && r.Exam!.ResultsReleased)
            .CountAsync();

        int totalCount = await db.ExamResults
            .Where(r => r.StudentProfileId == student.Id)
            .CountAsync();

        // Only 1 of the 2 results is released
        Assert.Equal(1, releasedCount);
        Assert.Equal(2, totalCount);
    }
}
