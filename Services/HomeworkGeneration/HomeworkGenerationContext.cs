using QdratNew.Services.Homework.Enums;

namespace QdratNew.Services.HomeworkGeneration
{
    public class HomeworkGenerationContext
    {
        // 🔹 من أرسل الواجب
        public int PartnerId { get; set; }
        public int? InstructorId { get; set; }
        public int? AdminId { get; set; }

        // 🔹 السياق الأكاديمي
        public int CourseId { get; set; }
        public int BatchId { get; set; }
        public int SubscriptionPeriodId { get; set; }

        // 🔹 سبب التوليد
        public HomeworkGenerationSource Source { get; set; }

        // 🔹 المؤشرات / الدروس التي انتهت
        public List<int> LessonIds { get; set; } = new();

        // 🔹 إعدادات التوليد
        public HomeworkGenerationOptions Options { get; set; } = new();

        // 🔹 تاريخ الإرسال
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;



        // السياق الأكاديمي
        public int SectionId { get; set; }

        // المؤشرات المختارة (فعالة فقط)
        public List<LessonGenerationInput> Lessons { get; set; } = new();

        // إعدادات عامة
        public DateTime? PublishAt { get; set; }
        public int? LectureId { get; set; }

    }
}
