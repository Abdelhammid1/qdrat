using QdratNew.Entities;
using System.ComponentModel.DataAnnotations.Schema;

public class StudentCourse
{
    public int Id { get; set; }

    public int StudentID { get; set; }
    [ForeignKey(nameof(StudentID))]
    public Student Student { get; set; }

    public int CourseId { get; set; }
    [ForeignKey(nameof(CourseId))]
    [InverseProperty(nameof(Course.StudentCourses))]
    public Course Course { get; set; }

    public DateTime DateEnrolled { get; set; } = DateTime.UtcNow;
    public bool IsCompleted { get; set; } = false;
    public float? Grade { get; set; }
}
