using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

public class FacultyCourseAssignment
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Faculty")]
    public int FacultyProfileId { get; set; }

    [Required]
    [Display(Name = "Course")]
    public int CourseId { get; set; }

    public FacultyProfile? FacultyProfile { get; set; }
    public Course? Course { get; set; }
}
