using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>RTK: «أمر النشر» — نشر خطة لدفعة أو طلاب محددين.</summary>
    public class RemedialTrackPublication
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public RemedialTrack? Track { get; set; }

        public int BatchId { get; set; }
        public Batch? Batch { get; set; }
        public RemedialTrackPublicationScope Scope { get; set; }
        public RemedialTrackDeliveryMode Mode { get; set; }

        public DateTime PublishAtUtc { get; set; }          // وقت ظهورها للطالب
        public RemedialTrackPublicationStatus Status { get; set; } = RemedialTrackPublicationStatus.Active;

        [MaxLength(6)] public string? AccessCode { get; set; }   // للحضوري فقط
        public int CodeVersion { get; set; } = 1;
        public DateTime? CodeGeneratedAtUtc { get; set; }

        [MaxLength(500)] public string? AdminNote { get; set; }
        public int TotalStudents { get; set; }               // عدّاد يُحسب عند النشر

        [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = string.Empty;
        [MaxLength(200)]           public string? CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        [MaxLength(300)] public string? CancelReason { get; set; }

        // RTK v2 / D26: حذف ناعم — يخفي الأمر عن الطلاب والقائمة وتبقى كل البيانات
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
        [MaxLength(450)] public string? DeletedByUserId { get; set; }
        [MaxLength(200)] public string? DeletedByName { get; set; }
        [MaxLength(300)] public string? DeleteReason { get; set; }

        [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public ICollection<RemedialTrackEnrollment> Enrollments { get; set; } = new List<RemedialTrackEnrollment>();
    }
}
