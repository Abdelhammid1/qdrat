using QdratNew.Enums;

namespace QdratNew.Services.PartnerDispatch
{
    // APD-S1.2: مصدر الإرسال الذي تُحسب عليه الأهلية وتوافق الدفعات.
    // ProfessionalModelId: الخدمة تقرأ منهج النموذج من القاعدة ولا تعتمد على CurriculumId القادم من الطلب.
    // CurriculumId: منهج المصدر (مؤشر الأداء). CourseId: كورس المصدر (معمل القياس).
    public sealed record DispatchSource(PartnerDispatchKind Kind, int? ProfessionalModelId, int? CurriculumId, int? CourseId)
    {
        public bool IsFromProfessionalModel =>
            Kind is PartnerDispatchKind.Homework or PartnerDispatchKind.Exam or PartnerDispatchKind.Placement;
    }

    public sealed class DispatchBatchVm
    {
        public int BatchId { get; init; }
        public string Name { get; init; } = "";
        public string BranchName { get; init; } = "";
        public int ActiveStudents { get; init; }
    }

    public sealed class PartnerEligibilityVm
    {
        public int PartnerId { get; init; }
        public string Name { get; init; } = "";

        public bool Eligible { get; init; }

        // «لا عقد نشط» / «العقد لا يشمل …» / «النموذج غير متاح لهذا الشريك»
        public string? BlockReason { get; init; }

        // false عند عدم وجود عقد نشط: لا يُتجاوز أبدًا
        public bool Overridable { get; init; }

        public List<DispatchBatchVm> Batches { get; init; } = new();
    }

    public sealed class AudienceSelection
    {
        public DispatchAudience Audience { get; set; } = DispatchAudience.Platform;
        public List<int> PlatformBatchIds { get; set; } = new();

        // PartnerId → BatchIds. شريك بقائمة فارغة يُعامل كغير مختار.
        public Dictionary<int, List<int>> PartnerBatchIds { get; set; } = new();

        public bool OverrideContract { get; set; }
        public string? OverrideReason { get; set; }
    }

    // ما يُرسل لشريك واحد بعد التحقق
    public sealed class PartnerDispatchTarget
    {
        public int PartnerId { get; init; }
        public string PartnerName { get; init; } = "";
        public List<int> BatchIds { get; init; } = new();

        // true فقط لشريك غير مؤهل أُرسل له بالتجاوز
        public bool ContractOverridden { get; init; }
        public string? OverrideReason { get; init; }
    }

    public sealed class AudienceValidation
    {
        public DispatchAudience Audience { get; init; }
        public List<string> Errors { get; } = new();
        public bool IsValid => Errors.Count == 0;

        public List<int> PlatformBatchIds { get; init; } = new();
        public List<PartnerDispatchTarget> Partners { get; init; } = new();
    }

    public static class DispatchRules
    {
        public const int OverrideReasonMinLength = 10;
        public const int OverrideReasonMaxLength = 500;

        public const string NoActiveContract = "لا عقد نشط";
        public const string ModelNotAvailable = "النموذج غير متاح لهذا الشريك";

        public static string FeatureMissing(PartnerDispatchKind kind) => kind switch
        {
            PartnerDispatchKind.Homework => "العقد لا يشمل الواجبات",
            PartnerDispatchKind.Exam => "العقد لا يشمل الاختبارات",
            PartnerDispatchKind.Placement => "العقد لا يشمل تحديد المستوى",
            PartnerDispatchKind.PerformanceIndicator => "العقد لا يشمل مؤشر الأداء",
            PartnerDispatchKind.MinistrySim => "العقد لا يشمل معمل القياس",
            _ => "العقد لا يشمل هذا النوع"
        };
    }
}
