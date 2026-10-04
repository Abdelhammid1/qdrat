using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>RTK: تقدّم (طالب × محور) — حالة المحور وفق آلة الحالة.</summary>
    public class RemedialTrackAxisProgress
    {
        public int Id { get; set; }
        public int EnrollmentId { get; set; }
        public RemedialTrackEnrollment? Enrollment { get; set; }
        public int AxisId { get; set; }
        public RemedialTrackAxis? Axis { get; set; }
        public int Order { get; set; }                       // نسخة من Axis.Order

        public RemedialTrackAxisStatus Status { get; set; } = RemedialTrackAxisStatus.Locked;
        public int Round { get; set; } = 1;                  // 1 أو 2

        public double? Exam101Percent { get; set; }
        public double? Exam102Percent { get; set; }

        public DateTime? OpenedAtUtc { get; set; }
        public DateTime? PassedAtUtc { get; set; }
        public DateTime? FailedAtUtc { get; set; }           // لحظة الرسب في 102

        [MaxLength(450)] public string? AdminOpenedByUserId { get; set; }
        [MaxLength(200)] public string? AdminOpenedByName { get; set; }
        [MaxLength(300)] public string? AdminOpenReason { get; set; }
        public DateTime? AdminOpenedAtUtc { get; set; }

        [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public ICollection<RemedialTrackVideoProgress> VideoProgresses { get; set; } = new List<RemedialTrackVideoProgress>();
        public ICollection<RemedialTrackExamAttempt> Attempts { get; set; } = new List<RemedialTrackExamAttempt>();
    }
}
