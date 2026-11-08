using QdratNew.Enums;

namespace QdratNew.ViewModels.OnlineLecture
{
    // OLC-S6 — صفحة مشاهدة محاضرة (عقد C.5: «مشاهدة»). تُبنى فقط لمحاضرة يحق للطالب فتحها (R2 + R15).
    // المصدر (Embed) يُملأ على الخادم وفق C.7-P6: المحتوى الكامل المفتوح، وفي قائمة المؤشرات: المكتمل (للمراجعة P11)
    // والعنصر المفتوح الحالي والتالي مباشرة فقط. القيم كلها من الخادم — لا شيء يُقبل من الطلب.

    public class StudentLectureWatchVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public LectureContentMode Mode { get; set; }

        public string LectureTitle { get; set; } = string.Empty;
        public DateTime LectureDate { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;

        /// <summary>P11: المحاضرة مكتملة ⇒ مشاهدة حرة بلا نبضات ولا احتساب.</summary>
        public bool IsReview { get; set; }
        public bool IsCompleted { get; set; }
        public int ProgressPercent { get; set; }

        /// <summary>P1: من سجل الطالب المسجّل دخوله (Student.FullName / Student.NationalID).</summary>
        public StudentLectureWatermarkVm Watermark { get; set; } = new();

        /// <summary>FullVideo فقط: مصدر الفيديو الكامل. null في IndicatorVideos.</summary>
        public StudentLectureEmbedVm? Embed { get; set; }
        public int? DurationSeconds { get; set; }
        /// <summary>R9: ceil(المدة × 80%).</summary>
        public int RequiredSeconds { get; set; }
        /// <summary>أبعد موضع وثّقه الخادم (MaxPositionSeconds) — نقطة الاستئناف وحدّ التنقل (P4).</summary>
        public int ResumeAtSeconds { get; set; }

        /// <summary>FullVideo: الفصول. IndicatorVideos: قائمة التشغيل المرتبة.</summary>
        public List<StudentLectureWatchItemVm> Items { get; set; } = new();

        /// <summary>المحاضرة التالية في نفس المنهج (R13) إن وُجدت.</summary>
        public int? NextLectureContentId { get; set; }
        public string? NextLectureTitle { get; set; }
        public bool NextLectureUnlocked { get; set; }

        public int CompletedItems => Items.Count(i => i.IsCompleted);
    }

    public class StudentLectureWatermarkVm
    {
        public string Name { get; set; } = string.Empty;
        public string NationalId { get; set; } = string.Empty;
    }

    public class StudentLectureEmbedVm
    {
        public LectureVideoProvider Provider { get; set; }
        public string ExternalId { get; set; } = string.Empty;
        /// <summary>رابط التضمين يبنيه الخادم من المعرّف (R8) — لا يُستخدم الرابط الخام أبدًا.</summary>
        public string EmbedUrl { get; set; } = string.Empty;
    }

    public class StudentLectureWatchItemVm
    {
        public int Id { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int SortOrder { get; set; }

        /// <summary>FullVideo: بداية/نهاية الفصل.</summary>
        public int? StartSeconds { get; set; }
        public int? EndSeconds { get; set; }

        /// <summary>IndicatorVideos: المصدر (P6 — null للعنصر المقفل غير التالي مباشرة). FullVideo: null دائمًا.</summary>
        public StudentLectureEmbedVm? Embed { get; set; }
        public int? DurationSeconds { get; set; }
        public int RequiredSeconds { get; set; }
        public int ResumeAtSeconds { get; set; }

        public bool IsCompleted { get; set; }
        /// <summary>R14 (IndicatorVideos): كل ما قبله مكتمل. FullVideo: بداية الفصل داخل ما شوهد (P4).</summary>
        public bool IsUnlocked { get; set; }
        /// <summary>R9: FullVideo — MaxPositionSeconds ≥ EndSeconds − 3 (من الخادم).</summary>
        public bool ReachedEnd { get; set; }

        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? LessonTitle : Title;
    }
}
