using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S6: تقارير الطالب وولي الأمر والدفعة + لوحة متابعة أمر النشر + فتح المحور التالي (إدارة).
    /// كل عملية إدارية تتحقق من نطاق الدفعات المسموح للمستخدم داخل الخدمة نفسها (<see cref="RemedialTrackBatchScope"/>).
    /// </summary>
    public interface IRemedialTrackReportService
    {
        /// <summary>RTK-S6.1: تقرير الطالب النهائي (مالك فقط). null = غير موجود لهذا الطالب/غير منشور بعد/ملغى.</summary>
        Task<RemedialTrackEnrollmentReportVm?> GetStudentReportAsync(int studentId, int enrollmentId, CancellationToken ct = default);

        /// <summary>RTK-S6.2: لوحة المتابعة (KPIs + رسوم + جدول مرقّم + قائمة من لم يجتز). 5 استعلامات بلا حلقات.</summary>
        Task<RemedialTrackDashboardVm?> GetDashboardAsync(int publicationId, RemedialTrackDashboardFilter filter, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>
        /// RTK-S6.3: فتح المحور التالي لمحور حُجب. السبب إلزامي 10–300 حرف. يعمل فقط على FailedBlocked،
        /// ويفرض نطاق الدفعات وانتماء المحور للتسجيل. بعد النجاح: إشعار للطالب + سجل نشاط أدمن.
        /// </summary>
        Task<RemedialTrackResult> UnlockNextAxisAsync(int enrollmentId, int axisProgressId, string? reason, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>RTK-S6.4: تقرير ولي الأمر (أدمن فقط) — يشمل ملاحظة الأدمن. null = غير موجود/خارج نطاق الدفعات.</summary>
        Task<RemedialTrackEnrollmentReportVm?> GetParentReportAsync(int enrollmentId, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>RTK-S6.4: حفظ ملاحظة الأدمن (≤ 2000 حرف) + حدث ReportNoteSaved.</summary>
        Task<RemedialTrackResult> SaveReportNoteAsync(int enrollmentId, string? note, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>RTK-S6.4: تقرير الدفعة (جدول ملخص لكل طلاب أمر النشر). null = غير موجود/خارج النطاق.</summary>
        Task<RemedialTrackBatchReportVm?> GetBatchReportAsync(int publicationId, RemedialTrackBatchScope scope, CancellationToken ct = default);
    }
}
