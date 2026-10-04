using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>
    /// RTK: الخطة العلاجية العاجلة (قالب قابل للنشر). مستقلة تمامًا عن كيانات Remedial* القديمة (D1).
    /// </summary>
    public class RemedialTrack
    {
        public int Id { get; set; }
        [Required, MaxLength(20)]  public string Code { get; set; } = string.Empty;          // RTK-2026-0001
        [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
        [MaxLength(1000)]          public string? Description { get; set; }

        public int CurriculumId { get; set; }
        public Curriculum? Curriculum { get; set; }

        public int PassPercent { get; set; } = 60;        // 1..100
        public int MinWatchPercent { get; set; } = 90;    // 50..100

        public RemedialTrackStatus Status { get; set; } = RemedialTrackStatus.Draft;
        public bool IsStructureLocked { get; set; }       // D12: يصبح true عند أول نشر

        [Required, MaxLength(450)] public string CreatedByUserId { get; set; } = string.Empty;
        [MaxLength(200)]           public string? CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }

        [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public ICollection<RemedialTrackAxis> Axes { get; set; } = new List<RemedialTrackAxis>();
    }
}
