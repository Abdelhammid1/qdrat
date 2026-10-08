using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.ViewModels.RemedialTracks
{
    // ============ مدخلات (Form) — لا Entities ولا RowVersion ولا Status من العميل (منع Overposting) ============

    public sealed class CreateRemedialTrackPublicationInput
    {
        [Range(1, int.MaxValue, ErrorMessage = "اختر الخطة")]
        public int TrackId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "اختر الدفعة")]
        public int BatchId { get; set; }

        public RemedialTrackPublicationScope Scope { get; set; } = RemedialTrackPublicationScope.WholeBatch;

        public RemedialTrackDeliveryMode Mode { get; set; } = RemedialTrackDeliveryMode.Online;

        /// <summary>وقت النشر بتوقيت الأدمن المحلي (يُحوَّل إلى UTC في الخدمة). فارغ = الآن.</summary>
        public DateTime? PublishAtLocal { get; set; }

        [StringLength(500, ErrorMessage = "الملاحظة أطول من 500 حرف")]
        public string? AdminNote { get; set; }

        /// <summary>RTK-S11 (D23): إرسال تقارير أولياء الأمور تلقائيًا عند عدم الاجتياز/الختام (الافتراضي مفعّل).</summary>
        public bool AutoSendParentReports { get; set; } = true;

        /// <summary>تُستخدم فقط مع Scope = SelectedStudents.</summary>
        public List<int>? StudentIds { get; set; }
    }

    // ============ نطاق الدفعات المسموحة للمستخدم ============

    /// <summary>
    /// نطاق الدفعات المسموحة للمستخدم الحالي. PermittedBatchIds = null تعني وصولًا كاملًا
    /// (المالك/المبرمج أو صلاحية Batches كاملة)، وإلا فالدفعات المذكورة فقط.
    /// </summary>
    public sealed record RemedialTrackBatchScope(IReadOnlySet<int>? PermittedBatchIds)
    {
        public static RemedialTrackBatchScope Unrestricted { get; } = new((IReadOnlySet<int>?)null);

        public bool Allows(int batchId) => PermittedBatchIds is null || PermittedBatchIds.Contains(batchId);
    }

    /// <summary>RTK-S10.2: مدخل وضع مراجعة الفيديوهات. EnrollmentIds فارغ + ApplyToAll = كل تسجيلات الأمر.</summary>
    public sealed record RemedialTrackReviewInput(
        IReadOnlyList<int>? EnrollmentIds,
        bool ApplyToAll,
        bool Enabled,
        DateTime? UntilUtc);

    /// <summary>RTK-S10.3: نموذج POST من واجهة الأدمن (لا Overposting: الأمر والمعرّفات والنية فقط). الانتهاء بتوقيت السعودية.</summary>
    public sealed class SetRemedialTrackVideoReviewInput
    {
        public int PublicationId { get; set; }
        public List<int>? EnrollmentIds { get; set; }
        public bool ApplyToAll { get; set; }
        public bool Enabled { get; set; }
        public DateTime? UntilLocal { get; set; }
    }

    /// <summary>بيانات نتيجة نجاح النشر (تُرجَع في RemedialTrackResult.Data).</summary>
    public sealed record RemedialTrackPublicationCreated(int PublicationId, int Enrolled, int Skipped);

    // ============ شاشة النشر ============

    public sealed class RemedialTrackPublishFormVm
    {
        public CreateRemedialTrackPublicationInput Input { get; set; } = new();
        public List<RemedialTrackSelectOption> Tracks { get; set; } = new();
        public List<RemedialTrackSelectOption> Batches { get; set; } = new();
        public const int MaxStudentsPerPublication = 1000;
    }

    public sealed class RemedialTrackBatchStudentVm
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public sealed class RemedialTrackBatchStudentsPageVm
    {
        public const int PageSize = 50;

        public List<RemedialTrackBatchStudentVm> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int Total { get; set; }
        public int TotalPages => Total <= 0 ? 1 : (int)Math.Ceiling(Total / (double)PageSize);
    }

    // ============ قائمة أوامر النشر ============

    public sealed class RemedialTrackPublicationListItemVm
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public string TrackCode { get; set; } = string.Empty;
        public string TrackTitle { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public RemedialTrackDeliveryMode Mode { get; set; }
        public RemedialTrackPublicationScope Scope { get; set; }
        public DateTime PublishAtUtc { get; set; }
        public int TotalStudents { get; set; }
        public RemedialTrackPublicationStatus Status { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        // RTK v2 / D26: بيانات الحذف الناعم (تظهر في تبويب «المحذوفة»)
        public DateTime? DeletedAtUtc { get; set; }
        public string? DeletedByName { get; set; }
        public string? DeleteReason { get; set; }
    }

    public sealed class RemedialTrackPublicationIndexVm
    {
        public const int PageSize = 20;

        public List<RemedialTrackPublicationListItemVm> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public int Total { get; set; }
        public int TotalPages => Total <= 0 ? 1 : (int)Math.Ceiling(Total / (double)PageSize);

        /// <summary>RTK v2 / D26: true = تبويب «المحذوفة».</summary>
        public bool ShowingDeleted { get; set; }

        // صلاحيات العرض (تُحدَّد في الكنترولر)
        public bool CanPublish { get; set; }
        public bool CanDelete { get; set; }
        public bool CanRestore { get; set; }
    }

    // ============ التفاصيل ============

    public sealed class RemedialTrackPublicationStudentVm
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public RemedialTrackEnrollmentStatus Status { get; set; }
    }

    public sealed class RemedialTrackPublicationStatusCountVm
    {
        public RemedialTrackEnrollmentStatus Status { get; set; }
        public int Count { get; set; }
    }

    /// <summary>RTK-S9.3: سطر في بطاقة «آخر التعديلات» (من سجل نشاط الأدمن).</summary>
    public sealed class RemedialTrackRecentChangeVm
    {
        public string ActionType { get; set; } = string.Empty;
        public string ActionLabel { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    public sealed class RemedialTrackPublicationDetailsVm
    {
        public const int MaxStudentsListed = 200;
        public const int MaxRecentChanges = 20;

        /// <summary>RTK-S9.3: آخر ≤ 20 إجراءً على أمر النشر أو خطته (إنشاء/إلغاء/حذف/استرجاع/تعديل فيديو/تعديل نماذج/فتح محور).</summary>
        public List<RemedialTrackRecentChangeVm> RecentChanges { get; set; } = new();

        /// <summary>RTK-S13: لوحة الملاحق (تُملأ في الكنترولر).</summary>
        public RemedialTrackAddendaPanelVm Addenda { get; set; } = new();

        public int Id { get; set; }
        public int TrackId { get; set; }
        public string TrackCode { get; set; } = string.Empty;
        public string TrackTitle { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public RemedialTrackPublicationScope Scope { get; set; }
        public RemedialTrackDeliveryMode Mode { get; set; }
        public RemedialTrackPublicationStatus Status { get; set; }
        public DateTime PublishAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string? CreatedByName { get; set; }
        public string? AdminNote { get; set; }
        public bool AutoSendParentReports { get; set; }
        public int TotalStudents { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public string? CancelReason { get; set; }

        // RTK v2 / D26
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
        public string? DeletedByName { get; set; }
        public string? DeleteReason { get; set; }

        /// <summary>الرقم المرجعي: يُملأ فقط إن كان المستخدم يملك ManageCode وكان الأمر حضوريًا.</summary>
        public string? AccessCode { get; set; }
        public int CodeVersion { get; set; }
        public DateTime? CodeGeneratedAtUtc { get; set; }

        public List<RemedialTrackPublicationStatusCountVm> StatusCounts { get; set; } = new();
        public List<RemedialTrackPublicationStudentVm> Students { get; set; } = new();
        public bool StudentsTruncated { get; set; }

        /// <summary>RTK-S6.2: لوحة المتابعة (KPIs + رسوم + جدول مرقّم). تُملأ في الكنترولر من IRemedialTrackReportService.</summary>
        public RemedialTrackDashboardVm? Dashboard { get; set; }

        // صلاحيات العرض (تُحدَّد في الكنترولر)
        public bool CanManageCode { get; set; }
        public bool CanCancel { get; set; }
        public bool CanDelete { get; set; }
        public bool CanRestore { get; set; }

        public bool IsActive => Status == RemedialTrackPublicationStatus.Active && !IsDeleted;
        public bool IsInPerson => Mode == RemedialTrackDeliveryMode.InPerson;
    }
}
