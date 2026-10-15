using System.Security.Claims;

namespace QdratNew.Services.RemedialTracks
{
    public sealed class RemedialTrackAuditEntryVm
    {
        public DateTime CreatedAtUtc { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ActorName { get; set; } = string.Empty;
        public string? ActorProfileName { get; set; }
        public string? ActorRoles { get; set; }
    }

    /// <summary>RTK: تسجيل وقراءة سجل تعديلات الخطة. فشل التسجيل لا يُبطل التعديل نفسه.</summary>
    public interface IRemedialTrackAuditService
    {
        Task LogAsync(int trackId, string actionType, string description, ClaimsPrincipal user, string? actorName, CancellationToken ct = default);

        // تحديد الخطة من معرّف فرعي — تُستدعى قبل التعديل لأن الحذف يُضيّع الصلة
        Task<int?> TrackIdOfAxisAsync(int axisId, CancellationToken ct = default);
        Task<int?> TrackIdOfVideoAsync(int videoId, CancellationToken ct = default);
        Task<int?> TrackIdOfPublicationAsync(int publicationId, CancellationToken ct = default);
        Task<int?> TrackIdOfAddendumAsync(int addendumId, CancellationToken ct = default);
        Task<int?> TrackIdOfEnrollmentAsync(int enrollmentId, CancellationToken ct = default);

        Task<List<RemedialTrackAuditEntryVm>> GetAsync(int trackId, int take = 100, CancellationToken ct = default);
    }
}
