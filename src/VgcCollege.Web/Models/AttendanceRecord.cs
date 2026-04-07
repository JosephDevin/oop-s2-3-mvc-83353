using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

public class AttendanceRecord
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Enrolment")]
    public int CourseEnrolmentId { get; set; }

    [Required]
    [Display(Name = "Session Date")]
    [DataType(DataType.Date)]
    public DateTime SessionDate { get; set; }

    [Required]
    [Range(1, 52)]
    [Display(Name = "Week Number")]
    public int WeekNumber { get; set; }

    public bool Present { get; set; }

    public CourseEnrolment? CourseEnrolment { get; set; }
}
