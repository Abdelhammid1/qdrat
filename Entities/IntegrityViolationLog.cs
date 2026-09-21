using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    /// <summary>
    /// يسجّل كل محاولة رصدها النظام لاستخدام ترجمة المتصفح (أو أي أداة تحايل مماثلة لاحقًا)
    /// أثناء حل اختبار/واجب/مهارة تعزيزية/تحديد مستوى/مؤشر أداء/معمل قياس.
    /// لا يحمل Foreign Key حقيقي على AttemptEntityId لأن معناه متغيّر حسب AttemptType —
    /// نفس نمط SuspiciousActivity.ExamContextId الموجود مسبقًا في المشروع (ADR-QG-1).
    /// </summary>
    public class IntegrityViolationLog
    {
        public int Id { get; set; }

        public int StudentId { get; set; }

        public IntegrityAttemptType AttemptType { get; set; }

        /// <summary>معرّف الكيان حسب النوع: HomeworkSetId / ExamAssignmentId / EnhancementSkillSetId / اختبار تحديد المستوى Id / PerformanceIndicatorExam Id / MinistrySimExam Id</summary>
        public int AttemptEntityId { get; set; }

        /// <summary>"BrowserTranslate" حاليًا — يسمح بإضافة أنواع كشف أخرى لاحقًا بدون Migration جديدة</summary>
        [MaxLength(50)]
        public string ViolationType { get; set; } = "BrowserTranslate";

        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        [MaxLength(300)]
        public string? PageUrl { get; set; }

        /// <summary>false = المحاولة موقوفة فعليًا وتحتاج إعادة فتح من الأدمن</summary>
        public bool IsResolved { get; set; } = false;

        public DateTime? ResolvedAt { get; set; }

        /// <summary>ApplicationUser.Id (string) لمستخدم الأدمن الذي أعاد فتح المحاولة</summary>
        [MaxLength(450)]
        public string? ResolvedByAdminId { get; set; }

        [MaxLength(300)]
        public string? ResolutionNote { get; set; }

        /// <summary>true = هذا الصف أُعيد فتحه ذاتيًا من الطالب (مسموح مرة واحدة فقط لكل محاولة، وليس من الأدمن)</summary>
        public bool SelfResolved { get; set; } = false;
    }
}
