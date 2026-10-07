using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>
    /// RTK-S11 (D23/D28): لقطة ثابتة لتقرير ولي الأمر (عدم اجتياز محور أو ختامي).
    /// تُنشأ تلقائيًا داخل معاملة تسليم الاختبار؛ صفحة ولي الأمر تقرأ من SnapshotJson ولا تعيد الحساب.
    /// </summary>
    public class RemedialTrackParentReport
    {
        public int Id { get; set; }
        public int EnrollmentId { get; set; }
        public RemedialTrackEnrollment? Enrollment { get; set; }
        public int? AxisProgressId { get; set; }                     // null للتقرير الختامي
        public RemedialTrackParentReportKind Kind { get; set; }
        public int StudentId { get; set; }
        public int? ParentId { get; set; }                           // لقطة وقت الإنشاء (D28)

        [Required] public string SnapshotJson { get; set; } = "{}";  // حد أقصى 64KB يُتحقق قبل الحفظ

        public RemedialTrackParentReportStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? SentAtUtc { get; set; }
        public DateTime? AcknowledgedAtUtc { get; set; }
        [MaxLength(450)] public string? SentByUserId { get; set; }   // null = تلقائي

        [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
