using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>
    /// OLC-S1 (العقد C.2 — جدول LectureContents): محتوى أونلاين واحد لكل محاضرة من محاضرات دفعة أونلاين.
    /// بلا Base class عمدًا — أعمدة التدقيق صريحة لتطابق AuditableEntity في المشروع الجديد حرفيًا.
    /// </summary>
    public class LectureContent
    {
        public int Id { get; set; }

        public int LectureId { get; set; }
        public Lecture Lecture { get; set; } = null!;

        public LectureContentMode ContentMode { get; set; }
        public LectureContentStatus Status { get; set; } = LectureContentStatus.Draft;

        [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
        [MaxLength(1000)] public string? Description { get; set; }

        /// <summary>للوضع FullVideo فقط.</summary>
        public LectureVideoProvider? VideoProvider { get; set; }
        [MaxLength(500)] public string? VideoUrl { get; set; }
        /// <summary>يُستخرج على الخادم من الرابط (R8).</summary>
        [MaxLength(50)] public string? VideoExternalId { get; set; }
        /// <summary>يُدخله الأدمن (يتقدّم على مدة المتصفح).</summary>
        public int? DurationSeconds { get; set; }
        [MaxLength(500)] public string? ThumbnailUrl { get; set; }

        public DateTime? PublishedAt { get; set; }
        [MaxLength(450)] public string? PublishedByUserId { get; set; }

        // أعمدة التدقيق — نفس أسماء AuditableEntity في المشروع الجديد
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [MaxLength(450)] public string? CreatedBy { get; set; }
        [MaxLength(450)] public string? UpdatedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }

        public ICollection<LectureContentItem> Items { get; set; } = new List<LectureContentItem>();
    }
}
