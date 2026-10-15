using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    /// <summary>
    /// RTK: سجل تعديلات الخطة العلاجية — كل تعديل ناجح يقوم به أي أدمن/موظف (مع بروفايل الصلاحيات) يُسجَّل على الخطة نفسها.
    /// للقراءة فقط: لا يُعدَّل ولا يُحذف من الواجهة.
    /// </summary>
    public class RemedialTrackAuditLog
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public RemedialTrack? Track { get; set; }

        [Required, MaxLength(60)] public string ActionType { get; set; } = string.Empty;
        [Required, MaxLength(600)] public string Description { get; set; } = string.Empty;

        [Required, MaxLength(450)] public string ActorUserId { get; set; } = string.Empty;
        [MaxLength(200)] public string? ActorName { get; set; }
        [MaxLength(200)] public string? ActorProfileName { get; set; }   // بروفايل الصلاحيات وقت التعديل (إن وُجد)
        [MaxLength(100)] public string? ActorRoles { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
