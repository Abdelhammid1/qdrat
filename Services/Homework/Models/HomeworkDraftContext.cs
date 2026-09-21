using System;
using System.Collections.Generic;

namespace QdratNew.Services.Homework.Models
{
    /// <summary>
    /// يمثل السياق الكامل لتوليد واجب دراسي
    /// محايد تمامًا عن الـ Area (Admin / Partner / Instructor)
    /// </summary>
    public class HomeworkDraftContext
    {
        // ===============================
        // 🧩 السياق التعاقدي
        // ===============================
        public int PartnerId { get; set; }
        public int SubscriptionPeriodId { get; set; }

        // ===============================
        // 🎓 السياق الأكاديمي
        // ===============================
        public int CourseId { get; set; }
        public int BatchId { get; set; }

        // ===============================
        // 🧠 نمط التوليد
        // ===============================
        /// <summary>
        /// Lesson / Section / Day / Manual
        /// </summary>
        public HomeworkGenerationMode GenerationMode { get; set; }

        /// <summary>
        /// مؤشرات محددة (في حال التوليد من Lessons)
        /// </summary>
        public List<int> LessonIds { get; set; } = new();

        /// <summary>
        /// محور كامل (في حال التوليد من Section)
        /// </summary>
        public int? SectionId { get; set; }

        // ===============================
        // 🔢 إعدادات التوليد
        // ===============================
        public int QuestionsPerLesson { get; set; }

        /// <summary>
        /// هل يسمح بمراجعة الأسئلة قبل الإرسال
        /// </summary>
        public bool AllowQuestionReview { get; set; }

        // ===============================
        // 🏦 مصدر الأسئلة
        // ===============================
        public bool UsePlatformQuestionBank { get; set; }
        public bool UsePrivateQuestionBank { get; set; }

        // ===============================
        // 🧑‍💼 الجهة المولّدة
        // ===============================
        /// <summary>
        /// Admin / Partner / Instructor
        /// </summary>
        public string GeneratedBy { get; set; } = string.Empty;

        public int GeneratedByUserId { get; set; }
        public object GenerationSource { get; internal set; }
    }
}
