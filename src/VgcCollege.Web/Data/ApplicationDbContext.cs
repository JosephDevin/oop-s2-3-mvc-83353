using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using VgcCollege.Web.Models;

namespace VgcCollege.Web.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<FacultyProfile> FacultyProfiles => Set<FacultyProfile>();
    public DbSet<FacultyCourseAssignment> FacultyCourseAssignments => Set<FacultyCourseAssignment>();
    public DbSet<CourseEnrolment> CourseEnrolments => Set<CourseEnrolment>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentResult> AssignmentResults => Set<AssignmentResult>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<FacultyCourseAssignment>()
            .HasIndex(x => new { x.FacultyProfileId, x.CourseId })
            .IsUnique();

        builder.Entity<CourseEnrolment>()
            .HasIndex(x => new { x.StudentProfileId, x.CourseId })
            .IsUnique();

        builder.Entity<AssignmentResult>()
            .HasIndex(x => new { x.AssignmentId, x.StudentProfileId })
            .IsUnique();

        builder.Entity<ExamResult>()
            .HasIndex(x => new { x.ExamId, x.StudentProfileId })
            .IsUnique();

        builder.Entity<AssignmentResult>()
            .Property(x => x.Score)
            .HasColumnType("decimal(8,2)");

        builder.Entity<Assignment>()
            .Property(x => x.MaxScore)
            .HasColumnType("decimal(8,2)");

        builder.Entity<ExamResult>()
            .Property(x => x.Score)
            .HasColumnType("decimal(8,2)");

        builder.Entity<Exam>()
            .Property(x => x.MaxScore)
            .HasColumnType("decimal(8,2)");
    }
}
