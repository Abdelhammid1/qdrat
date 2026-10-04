using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>RTK: سجل أحداث الطالب (مصدر الخط الزمني في التقارير).</summary>
    public class RemedialTrackEvent
    {
        public int Id { get; set; }
        public int EnrollmentId { get; set; }
        public RemedialTrackEnrollment? Enrollment { get; set; }
        public int? AxisId { get; set; }
        public RemedialTrackEventType Type { get; set; }
        [Required, MaxLength(500)] public string Message { get; set; } = string.Empty;
        [MaxLength(450)] public string? ActorUserId { get; set; }
        [MaxLength(200)] public string? ActorName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
