using System;
using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    /// <summary>
    /// OLC-S1 (العقد C.2 — جدول LectureContentItems): مؤشر داخل محتوى المحاضرة —
    /// فصل بتوقيت داخل الفيديو الكامل (FullVideo) أو فيديو مستقل (IndicatorVideos).
    /// </summary>
    public class LectureContentItem
    {
        public int Id { get; set; }

        public int LectureContentId { get; set; }
        public LectureContent LectureContent { get; set; } = null!;

        /// <summary>المؤشر.</summary>
        public int LessonId { get; set; }
        public Lesson Lesson { get; set; } = null!;

        public int SortOrder { get; set; }
        /// <summary>عنوان بديل للعرض (وإلا عنوان المؤشر).</summary>
        [MaxLength(200)] public string? Title { get; set; }

        /// <summary>FullVideo: بداية/نهاية الفصل بالثواني.</summary>
        public int? StartSeconds { get; set; }
        public int? EndSeconds { get; set; }

        public LectureContentSourceKind SourceVideoKind { get; set; } = LectureContentSourceKind.Manual;
        /// <summary>بلا FK عمدًا (العقد C.2): المصدر قد يكون جدولًا قديمًا للقراءة فقط في المشروع الجديد.</summary>
        public int? SourceVideoId { get; set; }

        // لقطة (Snapshot — R6) من المصدر وقت الإضافة
        public LectureVideoProvider? VideoProvider { get; set; }
        [MaxLength(500)] public string? VideoUrl { get; set; }
        [MaxLength(50)] public string? VideoExternalId { get; set; }
        public int? DurationSeconds { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        [MaxLength(450)] public string? CreatedBy { get; set; }
        [MaxLength(450)] public string? UpdatedBy { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
