using System.ComponentModel.DataAnnotations;

namespace VgcCollege.Web.Models;

public class Assignment
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Course")]
    public int CourseId { get; set; }

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Range(1, 1000)]
    [Display(Name = "Max Score")]
    public decimal MaxScore { get; set; }

    [Required]
    [Display(Name = "Due Date")]
    [DataType(DataType.Date)]
    public DateTime DueDate { get; set; }

    public Course? Course { get; set; }
    public ICollection<AssignmentResult> Results { get; set; } = new List<AssignmentResult>();
}
