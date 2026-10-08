using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>
    /// RTK-S13: «ملحق المحور» — فيديو إضافي (+ اختبار قصير اختياري) يُلزَم به طلاب أمر نشر بعد النشر.
    /// كيان مستقل: لا يغيّر AxisProgress ولا حالة التسجيل ولا يمنع المحور التالي (D29).
    /// </summary>
    public class RemedialTrackAddendum
    {
        public int Id { get; set; }
        public int PublicationId { get; set; }
        public RemedialTrackPublication? Publication { get; set; }
        public int AxisId { get; set; }                       // المحور المعني (للعرض والتقارير فقط)
        public RemedialTrackAxis? Axis { get; set; }

        [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
        [Required, MaxLength(500)] public string Url { get; set; } = string.Empty;   // https فقط عبر RemedialTrackVideoUrlParser
        public RemedialTrackVideoProvider Provider { get; set; }
        [MaxLength(50)] public string? ExternalId { get; set; }
        public int? DurationSeconds { get; set; }

        public int? ExamModelId { get; set; }                 // اختياري (D30): ProfessionalModel واحد
        public ProfessionalModel? ExamModel { get; set; }
        public int? ExamDurationMinutes { get; set; }

        [Required, MaxLength(300)] public string Reason { get; set; } = string.Empty;   // سبب الإضافة (5–300)
        public bool IsActive { get; set; } = true;

        [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = string.Empty;
        [MaxLength(200)] public string? CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public ICollection<RemedialTrackAddendumProgress> Progresses { get; set; } = new List<RemedialTrackAddendumProgress>();
    }

    /// <summary>RTK-S13: تقدّم (طالب × ملحق). المشاهدة بزمن الخادم بنفس قواعد D4.</summary>
    public class RemedialTrackAddendumProgress
    {
        public int Id { get; set; }
        public int AddendumId { get; set; }
        public RemedialTrackAddendum? Addendum { get; set; }
        public int EnrollmentId { get; set; }
        public RemedialTrackEnrollment? Enrollment { get; set; }

        public double WatchedSeconds { get; set; }            // بزمن الخادم
        public int? DurationSeconds { get; set; }
        public bool EndedSeen { get; set; }
        public bool VideoCompleted { get; set; }
        public DateTime? LastPingAtUtc { get; set; }
        [MaxLength(10)] public string? LastPingState { get; set; }
        public DateTime? VideoCompletedAtUtc { get; set; }

        public bool ExamPassed { get; set; }
        public double? BestScorePercent { get; set; }
        public int AttemptsCount { get; set; }                // المحاولات المُسلَّمة
        public DateTime? CompletedAtUtc { get; set; }          // اكتمل الملحق كله (فيديو + اختبار إن وُجد)

        [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
