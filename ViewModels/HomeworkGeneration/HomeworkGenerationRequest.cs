using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.HomeworkGeneration
{
    public class HomeworkGenerationRequest
    {
        // السياق الأكاديمي
        public int CourseId { get; set; }
        public int SectionId { get; set; }   // المحور
        public int BatchId { get; set; }

        // المؤشرات (Lessons) التي تم تدريسها
        public List<int> LessonIds { get; set; } = new();

        // عدد الأسئلة الافتراضي لكل مؤشر
        public int DefaultQuestionsPerLesson { get; set; }

        // التوقيت
        public DateTime HomeworkDate { get; set; }
        public TimeSpan? AvailableFromTime { get; set; }

        // الجهة المنشئة (Partner / Admin / Instructor)
        public string GeneratedBy { get; set; } = string.Empty;
    }
}
