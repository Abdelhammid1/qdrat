using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class PerformanceExamReviewViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public bool IsOnline { get; set; }
        public string ReferenceCode { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public string LessonTitle { get; set; }  // ✅ المؤشر (الدرس)

        public List<SectionReviewItem> Sections { get; set; } = new List<SectionReviewItem>();
    }

    public class SectionReviewItem
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public List<QuestionReviewItem> Questions { get; set; } = new List<QuestionReviewItem>();
    }

    public class QuestionReviewItem
    {
        public Guid QuestionId { get; set; }
        public int Order { get; set; }
        public string Title { get; set; }
        public string LessonTitle { get; set; }
        public string QuestionType { get; set; }
        public double DifficultyLevel { get; set; }
        public string CorrectAnswer { get; set; }


        public string QuestionText { get; set; }
        public string StudentAnswer { get; set; }
        public string SourceType { get; set; } // واجب / اختبار
        public string SourceTitle { get; set; }


 

    }
}
