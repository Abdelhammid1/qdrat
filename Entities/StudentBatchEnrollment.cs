using QdratNew.Entities;
using System.ComponentModel.DataAnnotations.Schema;

public class StudentBatchEnrollment
{
    public int Id { get; set; }

    public int StudentID { get; set; }
    public int BatchId { get; set; }

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Active";

    [ForeignKey(nameof(StudentID))]
    public Student Student { get; set; }

    [ForeignKey(nameof(BatchId))]
    public Batch Batch { get; set; }

}
