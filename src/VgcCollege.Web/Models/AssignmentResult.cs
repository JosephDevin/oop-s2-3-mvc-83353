using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

public class AssignmentResult
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Assignment")]
    public int AssignmentId { get; set; }

    [Required]
    [Display(Name = "Student")]
    public int StudentProfileId { get; set; }

    [Required]
    [Range(0, 1000)]
    public decimal Score { get; set; }

    [StringLength(1000)]
    public string? Feedback { get; set; }

    public Assignment? Assignment { get; set; }
    public StudentProfile? StudentProfile { get; set; }
}
