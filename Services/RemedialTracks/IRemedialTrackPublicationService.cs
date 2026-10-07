using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S3: النشر والاستهداف (دفعة/طلاب، أونلاين/حضوري، وقت النشر، الرقم المرجعي، الإلغاء).
    /// كل عملية تتحقق من نطاق الدفعات المسموح للمستخدم (<see cref="RemedialTrackBatchScope"/>) داخل الخدمة نفسها.
    /// </summary>
    public interface IRemedialTrackPublicationService
    {
        /// <param name="deleted">RTK v2/D26: false = الأوامر النشطة/الملغاة، true = تبويب «المحذوفة».</param>
        Task<RemedialTrackPublicationIndexVm> GetIndexAsync(int page, RemedialTrackBatchScope scope, CancellationToken ct = default, bool deleted = false);

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

        /// <summary>RTK v2/D26: حذف ناعم (يخفي الأمر عن الطلاب والقائمة؛ لا يمس التسجيلات ولا التقدّم). السبب 5–300 حرف.</summary>
        Task<RemedialTrackResult> DeleteAsync(int id, string? reason, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>RTK v2/D26: استرجاع أمر محذوف ناعمًا. قد يُولَّد رقم مرجعي جديد عند تعارضه مع أمر نشط.</summary>
        Task<RemedialTrackResult> RestoreAsync(int id, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);

        /// <summary>
        /// RTK v2/D24: تشغيل/إيقاف وضع مراجعة الفيديوهات (للقراءة فقط) لفرد أو محدّدين أو كل تسجيلات الأمر.
        /// أي معرّف لا ينتمي للأمر (أو تسجيل ملغى) يُرفض الطلب كله. حد أقصى 500 معرّف، والانتهاء في المستقبل بحد 90 يومًا.
        /// عند النجاح: Data = عدد التسجيلات المحدَّثة (int).
        /// </summary>
        Task<RemedialTrackResult> SetVideoReviewAsync(int publicationId, RemedialTrackReviewInput input, RemedialTrackActor actor, RemedialTrackBatchScope scope, CancellationToken ct = default);
    }
}
