using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>RTK: فيديو داخل محور في الخطة.</summary>
    public class RemedialTrackVideo
    {
        public int Id { get; set; }
        public int AxisId { get; set; }
        public RemedialTrackAxis? Axis { get; set; }
        public int Order { get; set; }
        [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
        [Required, MaxLength(500)] public string Url { get; set; } = string.Empty;   // https فقط
        public RemedialTrackVideoProvider Provider { get; set; }
        [MaxLength(50)] public string? ExternalId { get; set; }                       // يُستخرج من الرابط عند الحفظ
        public int? DurationSeconds { get; set; }                                     // إلزامي لـ Other (D5)
        public bool IsActive { get; set; } = true;
    }
}
