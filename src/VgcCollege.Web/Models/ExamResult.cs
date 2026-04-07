using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

public class ExamResult
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Exam")]
    public int ExamId { get; set; }

    [Required]
    [Display(Name = "Student")]
    public int StudentProfileId { get; set; }

    [Required]
    [Range(0, 1000)]
    public decimal Score { get; set; }

    [StringLength(2)]
    public string? Grade { get; set; }

    public Exam? Exam { get; set; }
    public StudentProfile? StudentProfile { get; set; }
}
