using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S12.1 (D23/D28): طابور تقارير أولياء الأمور للأدمن + زر التحكم (إرسال/إعادة/إيقاف) + مفتاح الإرسال التلقائي لأمر النشر.
    /// كل إجراء يفرض نطاق الدفعات (<see cref="RemedialTrackBatchScope"/>) داخل الخدمة ويكتب سجل نشاط بوسم <c>[RTK pub:id]</c>.
    /// </summary>
    public interface IRemedialTrackParentReportAdminService
    {
        Task<RemedialTrackParentReportQueueVm> GetQueueAsync(
            RemedialTrackParentReportQueueFilter filter, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>
        /// إرسال تقرير غير مُرسل (Pending/Suppressed، أو NoParent بعد ربط الطالب بولي أمر). يعيد بناء اللقطة إن كانت فارغة.
        /// </summary>
        Task<RemedialTrackParentReportActionResult> SendAsync(
            int reportId, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>إعادة إشعار ولي الأمر بتقرير مُرسل. لا تكرار خلال دقيقة من آخر إرسال (حماية النقر المزدوج).</summary>
        Task<RemedialTrackParentReportActionResult> ResendAsync(
            int reportId, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>إيقاف تقرير (Pending/Sent): يختفي عن ولي الأمر. Idempotent.</summary>
        Task<RemedialTrackParentReportActionResult> SuppressAsync(
            int reportId, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>تشغيل/إيقاف الإرسال التلقائي لأمر نشر (يؤثر على التقارير القادمة فقط). Idempotent.</summary>
        Task<RemedialTrackParentReportActionResult> ToggleAutoSendAsync(
            int publicationId, bool enabled, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);
    }
}
