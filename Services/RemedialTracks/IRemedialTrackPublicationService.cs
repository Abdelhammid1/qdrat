using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S3: النشر والاستهداف (دفعة/طلاب، أونلاين/حضوري، وقت النشر، الرقم المرجعي، الإلغاء).
    /// كل عملية تتحقق من نطاق الدفعات المسموح للمستخدم (<see cref="RemedialTrackBatchScope"/>) داخل الخدمة نفسها.
    /// </summary>
    public interface IRemedialTrackPublicationService
    {
        Task<RemedialTrackPublicationIndexVm> GetIndexAsync(int page, RemedialTrackBatchScope scope, CancellationToken ct = default);

        Task<RemedialTrackPublishFormVm> GetPublishFormAsync(int? trackId, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>null إن كانت الدفعة غير موجودة/غير مسموحة للمستخدم.</summary>
        Task<RemedialTrackBatchStudentsPageVm?> GetStudentsOfBatchAsync(int batchId, string? search, int page, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>عند النجاح: Data = <see cref="RemedialTrackPublicationCreated"/>.</summary>
        Task<RemedialTrackResult> CreateAsync(CreateRemedialTrackPublicationInput input, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <param name="includeAccessCode">يُمرَّر true فقط لمن يملك صلاحية ManageCode.</param>
        Task<RemedialTrackPublicationDetailsVm?> GetDetailsAsync(int id, bool includeAccessCode, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>عند النجاح: Data = الرقم المرجعي الجديد (string).</summary>
        Task<RemedialTrackResult> RegenerateCodeAsync(int id, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        Task<RemedialTrackResult> CancelAsync(int id, string? reason, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);
    }
}
