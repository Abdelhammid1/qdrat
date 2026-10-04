using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Services.RemedialTracks
{
    public sealed record RemedialTrackActor(string UserId, string Name);

    /// <summary>نتيجة موحّدة لعمليات بناء الخطة. Data: معرّف/قائمة أسباب حسب العملية.</summary>
    public sealed record RemedialTrackResult(
        bool Success,
        string Message,
        object? Data = null,
        IReadOnlyList<string>? Warnings = null)
    {
        public static RemedialTrackResult Ok(string message, object? data = null, IReadOnlyList<string>? warnings = null)
            => new(true, message, data, warnings);

        public static RemedialTrackResult Fail(string message, object? data = null)
            => new(false, message, data);
    }

    /// <summary>RTK-S2: بناء الخطة (محاور، فيديوهات، نموذجا 101/102، التحقق).</summary>
    public interface IRemedialTrackBuilderService
    {
        Task<RemedialTrackIndexVm> GetIndexAsync(RemedialTrackIndexFilter filter, CancellationToken ct = default);
        Task<List<RemedialTrackSelectOption>> GetCurriculaAsync(CancellationToken ct = default);
        Task<RemedialTrackBuilderVm?> GetBuilderAsync(int trackId, CancellationToken ct = default);

        Task<RemedialTrackResult> CreateAsync(CreateRemedialTrackInput input, RemedialTrackActor actor, CancellationToken ct = default);
        Task<RemedialTrackResult> EditHeaderAsync(EditRemedialTrackHeaderInput input, CancellationToken ct = default);

        Task<RemedialTrackResult> AddAxisAsync(AddRemedialAxisInput input, CancellationToken ct = default);
        /// <param name="direction">-1 للأعلى، +1 للأسفل.</param>
        Task<RemedialTrackResult> MoveAxisAsync(int axisId, int direction, CancellationToken ct = default);
        Task<RemedialTrackResult> RemoveAxisAsync(int axisId, CancellationToken ct = default);
        Task<RemedialTrackResult> SaveAxisExamsAsync(SaveRemedialAxisExamsInput input, CancellationToken ct = default);

        Task<RemedialTrackResult> AddVideoAsync(AddRemedialVideoInput input, CancellationToken ct = default);
        Task<RemedialTrackResult> EditVideoAsync(EditRemedialVideoInput input, CancellationToken ct = default);
        Task<RemedialTrackResult> MoveVideoAsync(int videoId, int direction, CancellationToken ct = default);
        Task<RemedialTrackResult> RemoveVideoAsync(int videoId, CancellationToken ct = default);

        /// <summary>عند الفشل: Data = قائمة أسباب (IReadOnlyList&lt;string&gt;).</summary>
        Task<RemedialTrackResult> MarkReadyAsync(int trackId, CancellationToken ct = default);
        Task<RemedialTrackResult> ArchiveAsync(int trackId, CancellationToken ct = default);
        /// <summary>عند النجاح: Data = معرّف النسخة الجديدة.</summary>
        Task<RemedialTrackResult> DuplicateAsync(int trackId, RemedialTrackActor actor, CancellationToken ct = default);
    }
}
