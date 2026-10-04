using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>RTK: الطالب داخل أمر نشر.</summary>
    public class RemedialTrackEnrollment
    {
        public int Id { get; set; }
        public int PublicationId { get; set; }
        public RemedialTrackPublication? Publication { get; set; }
        public int TrackId { get; set; }                     // مكرَّر للفلترة بلا Join
        public int StudentId { get; set; }
        public Student? Student { get; set; }

        public RemedialTrackEnrollmentStatus Status { get; set; } = RemedialTrackEnrollmentStatus.NotStarted;
        public int? CurrentAxisId { get; set; }              // المحور المفتوح حاليًا

        public int? VerifiedCodeVersion { get; set; }        // D6/D7
        public int FailedCodeAttempts { get; set; }          // D8
        public DateTime? CodeLockedUntilUtc { get; set; }

        [MaxLength(2000)] public string? AdminReportNote { get; set; }   // ملاحظة تقرير ولي الأمر

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public ICollection<RemedialTrackAxisProgress> AxisProgresses { get; set; } = new List<RemedialTrackAxisProgress>();
        public ICollection<RemedialTrackEvent> Events { get; set; } = new List<RemedialTrackEvent>();
    }
}
