using QdratNew.Entities;

public class RemedialPlanInteraction
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string ActionTaken { get; set; } = string.Empty;
    public string ActionType { get; set; } = "General"; // ✅ أضف قيمة افتراضية لتفادي الخطأ
    public DateTime CreatedAt { get; set; } = DateTime.Now; // لتسجيل وقت التعليق

    public DateTime InteractionTime { get; set; } = DateTime.Now;
    public string? Comment { get; set; }
    public int? RemedialPlanId { get; set; }

    public Student Student { get; set; }
}
