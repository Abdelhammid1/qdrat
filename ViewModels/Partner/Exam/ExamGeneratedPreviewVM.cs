using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner.Exam
{
    public class ExamGeneratedPreviewVM
    {
        // معلومات العرض
        public string ExamTitle { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public int CurriculumId { get; set; }

        // الأسئلة
        public List<ExamPreviewQuestionVM> Questions { get; set; } = new();

        public int TotalQuestions => Questions.Count;

        // رسائل الخطأ (للعرض في صفحة الريفيو)
        public string? ErrorMessage { get; set; }
        public int ExamId { get; internal set; }
        public object Title { get; internal set; }
        public object DraftId { get; internal set; }
        public int LessonId { get; set; } // ✅ أضف هذا

    }

    public class ExamPreviewQuestionVM
    {
        public Guid QuestionId { get; set; }
        public string? Title { get; set; }

        public string? SectionTitle { get; set; }
        public string? LessonTitle { get; set; }
        public int LessonId { get; set; } // ✅ أضف هذا

        public string Difficulty { get; set; } = string.Empty;
    }
}
