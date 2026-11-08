using QdratNew.Enums;

namespace QdratNew.ViewModels.OnlineLecture
{
    // OLC-S5 — قسم «المحتوى التعليمي» للطالب (عقد C.5: قائمة القسم). لا يحتوي أي رابط أو معرّف فيديو — المصدر
    // يُرسل فقط من صفحة المشاهدة (OLC-S6) وللعنصر المفتوح فقط (C.7-P6). حارس اختبار يفحص ذلك بالـ Reflection.

    public class StudentOnlineLearningIndexVm
    {
        public string CourseTitle { get; set; } = string.Empty;
        public string BatchTitle { get; set; } = string.Empty;

        /// <summary>R3: للطالب دفعة أونلاين متاحة في الكورس النشط.</summary>
        public bool IsEligible { get; set; }

        /// <summary>الكورس النشط ليس أونلاين لكن للطالب دفعة أونلاين في كورس آخر — يُعرض تلميح «بدّل الدورة».</summary>
        public bool HasOtherEligibleCourse { get; set; }

        /// <summary>تعذّر التحميل (حالة الخطأ).</summary>
        public bool LoadFailed { get; set; }

        public List<StudentOnlineCurriculumVm> Curricula { get; set; } = new();

        public int TotalLectures => Curricula.Sum(c => c.TotalCount);
        public int CompletedLectures => Curricula.Sum(c => c.CompletedCount);
        public int ProgressPercent => TotalLectures == 0 ? 0 : (int)Math.Floor(CompletedLectures * 100.0 / TotalLectures);
    }

    public class StudentOnlineCurriculumVm
    {
        public int CurriculumId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int CompletedCount { get; set; }
        public int TotalCount { get; set; }
        public List<StudentOnlineLectureVm> Lectures { get; set; } = new();
    }

    public class StudentOnlineLectureVm
    {
        public int LectureContentId { get; set; }
        public string LectureTitle { get; set; } = string.Empty;
        public DateTime LectureDate { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public LectureContentMode Mode { get; set; }
        public string Title { get; set; } = string.Empty;
        public int ItemsCount { get; set; }
        public int ProgressPercent { get; set; }
        public bool IsCompleted { get; set; }

        /// <summary>R13/R15 من الخادم: مقفلة وغير مكتملة ⇒ بلا رابط.</summary>
        public bool IsLocked { get; set; }

        /// <summary>أول محاضرة مفتوحة غير مكتملة في المنهج — تُبرز «تابع من هنا».</summary>
        public bool IsNext { get; set; }
    }
}
