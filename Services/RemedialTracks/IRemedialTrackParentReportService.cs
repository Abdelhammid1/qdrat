using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S11.3 (D28): قراءة تقارير ولي الأمر وإقراره بالاطلاع. كل دالة تأخذ معرّف ولي الأمر من الهوية في الخادم
    /// (لا يُقبل من العميل)، وغير المملوك/غير المُرسل/المحذوف يُعامَل كغير موجود (404).
    /// </summary>
    public interface IRemedialTrackParentReportService
    {
        Task<RemedialTrackParentReportListVm> GetListAsync(int parentId, int page, CancellationToken ct = default);

        Task<RemedialTrackParentReportDetailsVm?> GetDetailsAsync(int parentId, int reportId, CancellationToken ct = default);

        /// <summary>إقرار بالاطلاع مرة واحدة (Idempotent). Found=false ← غير موجود/غير مملوك.</summary>
        Task<RemedialTrackParentAckResult> AcknowledgeAsync(
            int parentId, int reportId, string? userId, string? userName, CancellationToken ct = default);
    }
}
