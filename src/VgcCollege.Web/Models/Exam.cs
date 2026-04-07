using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

public class Exam
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Course")]
    public int CourseId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Exam Date")]
    [DataType(DataType.Date)]
    public DateTime ExamDate { get; set; }

    [Required]
    [Range(1, 1000)]
    [Display(Name = "Max Score")]
    public decimal MaxScore { get; set; }

    [Display(Name = "Results Released")]
    public bool ResultsReleased { get; set; } = false;

    public Course? Course { get; set; }
    public ICollection<ExamResult> Results { get; set; } = new List<ExamResult>();
}
