using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    // APD-S1: سجل قراءة فقط لما أرسله الأدمن لشريك (صف لكل شريك ودفعة).
    // يُكتب داخل Transaction الشريك نفسها، ولا يحمل أي بيانات طلاب.
    public class PartnerDispatchLog
    {
        public long Id { get; set; }

        // كل الشركاء في ضغطة إرسال واحدة يشتركون في نفس المعرّف
        public Guid DispatchGroupId { get; set; }

        public PartnerDispatchKind Kind { get; set; }

        [Required, MaxLength(300)]
        public string Title { get; set; } = "";

        public int? SourceProfessionalModelId { get; set; }
        public int? SourceMinistrySimExamId { get; set; }

        // اختبار مؤشر الأداء الأصل الذي نُسخ منه
        public int? SourcePerformanceIndicatorExamId { get; set; }

        public int PartnerId { get; set; }
        public Partner? Partner { get; set; }

        // بلا مفتاح أجنبي عمدًا: السجل يبقى كما هو مهما تغيّرت الدفعة
        public int BatchId { get; set; }

        // HomeworkSetId / ExamId / AssignmentId / PerformanceIndicatorExamId حسب Kind
        public int TargetEntityId { get; set; }

        public int StudentsCount { get; set; }

        public bool ContractOverridden { get; set; }

        [MaxLength(500)]
        public string? OverrideReason { get; set; }

        [Required, MaxLength(450)]
        public string SentByUserId { get; set; } = "";

        public DateTime SentAtUtc { get; set; }
    }
}
