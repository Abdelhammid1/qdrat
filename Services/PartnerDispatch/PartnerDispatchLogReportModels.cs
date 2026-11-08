using QdratNew.Enums;

namespace QdratNew.Services.PartnerDispatch
{
    // APD-S6.1: فلاتر سجل الإرسال. التواريخ بتوقيت السعودية (تاريخ فقط، شامل للطرفين) وتُحوَّل إلى UTC داخل الخدمة.
    public sealed class PartnerDispatchLogFilter
    {
        public PartnerDispatchKind? Kind { get; set; }
        public int? PartnerId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string? SentByUserId { get; set; }
        public bool? Overridden { get; set; }
        public int Page { get; set; } = 1;

        public bool HasAny =>
            Kind.HasValue || PartnerId.HasValue || FromDate.HasValue || ToDate.HasValue
            || !string.IsNullOrWhiteSpace(SentByUserId) || Overridden.HasValue;
    }

    // حالة سطر تحديد المستوى الذي أُنشئ مسودة لدى المدرسة (StudentsCount = 0 وقت الإنشاء)
    public enum PartnerDispatchDraftState
    {
        None = 0,
        DraftPending = 1,      // ما زالت مسودة لدى المدرسة
        DraftSentByPartner = 2 // أرسلتها المدرسة لاحقًا من منطقتها (عدد الطلاب لا يُسجَّل هنا)
    }

    // سطر لكل مدرسة ودفعة، بلا أي بيانات طلاب
    public sealed class PartnerDispatchLogLine
    {
        public long Id { get; init; }
        public Guid DispatchGroupId { get; init; }
        public PartnerDispatchKind Kind { get; init; }
        public string Title { get; init; } = "";
        public int PartnerId { get; init; }
        public string PartnerName { get; init; } = "";
        public int BatchId { get; init; }
        public string? BatchName { get; init; }
        public string? BranchName { get; init; }
        public int TargetEntityId { get; init; }
        public int StudentsCount { get; init; }
        public bool ContractOverridden { get; init; }
        public string? OverrideReason { get; init; }
        public string SentByUserId { get; init; } = "";
        public string SentByName { get; init; } = "";
        public DateTime SentAtUtc { get; init; }
        public PartnerDispatchDraftState DraftState { get; init; }
    }

    // ضغطة إرسال واحدة (DispatchGroupId) وما تحتها من أسطر مطابقة للفلتر
    public sealed class PartnerDispatchLogGroup
    {
        public Guid DispatchGroupId { get; init; }
        public PartnerDispatchKind Kind { get; init; }
        public string Title { get; init; } = "";
        public string SentByName { get; init; } = "";
        public DateTime SentAtUtc { get; init; }
        public int PartnersCount { get; init; }
        public int BatchesCount { get; init; }
        public int StudentsCount { get; init; }
        public bool AnyOverridden { get; init; }
        public bool HasDraft { get; init; }
        public IReadOnlyList<PartnerDispatchLogLine> Lines { get; init; } = Array.Empty<PartnerDispatchLogLine>();
    }

    public sealed class PartnerDispatchLogPage
    {
        public IReadOnlyList<PartnerDispatchLogGroup> Groups { get; init; } = Array.Empty<PartnerDispatchLogGroup>();
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalGroups { get; init; }
        public int TotalLines { get; init; }
        public int TotalStudents { get; init; }
        public int OverriddenLines { get; init; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalGroups / (double)PageSize);
    }

    public sealed record PartnerDispatchLogOption(string Value, string Text);

    public sealed class PartnerDispatchLogFilterOptions
    {
        public IReadOnlyList<PartnerDispatchLogOption> Partners { get; init; } = Array.Empty<PartnerDispatchLogOption>();
        public IReadOnlyList<PartnerDispatchLogOption> Senders { get; init; } = Array.Empty<PartnerDispatchLogOption>();
    }

    public sealed record PartnerDispatchLogExport(byte[] Content, string FileName, bool Truncated);

    public static class PartnerDispatchKindLabels
    {
        public static string Of(PartnerDispatchKind kind) => kind switch
        {
            PartnerDispatchKind.Homework => "واجب من نموذج",
            PartnerDispatchKind.Exam => "اختبار من نموذج",
            PartnerDispatchKind.Placement => "تحديد المستوى",
            PartnerDispatchKind.PerformanceIndicator => "مؤشر الأداء",
            PartnerDispatchKind.MinistrySim => "معمل القياس",
            _ => kind.ToString()
        };

        public static string DraftStateOf(PartnerDispatchDraftState state) => state switch
        {
            PartnerDispatchDraftState.DraftPending => "مسودة لدى المدرسة",
            PartnerDispatchDraftState.DraftSentByPartner => "مسودة أرسلتها المدرسة",
            _ => ""
        };
    }
}
