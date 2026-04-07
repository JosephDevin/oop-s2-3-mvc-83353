using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

public enum EnrolmentStatus
{
    Active,
    Withdrawn,
    Completed
}

public class CourseEnrolment
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Student")]
    public int StudentProfileId { get; set; }

    [Required]
    [Display(Name = "Course")]
    public int CourseId { get; set; }

    [Required]
    [Display(Name = "Enrol Date")]
    [DataType(DataType.Date)]
    public DateTime EnrolDate { get; set; }

    [Required]
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Active;

    public StudentProfile? StudentProfile { get; set; }
    public Course? Course { get; set; }
    public ICollection<AttendanceRecord> AttendanceRecords { get; set; } = new List<AttendanceRecord>();
}
