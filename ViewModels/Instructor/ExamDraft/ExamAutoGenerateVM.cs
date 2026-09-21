using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Instructor.ExamDraft
{
    public class ExamAutoGenerateVM
    {
        public int CourseId { get; set; }

        public int CurriculumId { get; set; }

        public string Title { get; set; }

        // توزيع الأسئلة لكل Lesson
        public Dictionary<int, int> LessonQuestionCounts { get; set; } = new();

        // ============================
        // 🔴 تحكم في الصعوبة
        // ============================
        public int EasyPercentage { get; set; } = 30;

        public int MediumPercentage { get; set; } = 40;

        public int HardPercentage { get; set; } = 30;
    }
}