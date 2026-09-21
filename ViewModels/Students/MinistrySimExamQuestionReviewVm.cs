using System;
using System.Collections.Generic;
using QdratNew.Enums;
using QdratNew.ViewModels.Homework;

namespace QdratNew.ViewModels.Students
{
    // Sprint 13 (MSE-G / G6): شاشة مراجعة أسئلة/إجابات مرحلة واحدة من اختبار معمل القياس بعد انتهاء الاختبار —
    // مستنسخة من نمط HomeworkReviewViewModel (راجع Areas/Students/Views/StudentHomeworkResults/Review.cshtml).
    public class MinistrySimExamQuestionReviewVm
    {
        public int MinistrySimExamId { get; set; }
        public int AttemptId { get; set; }
        public int StageNumber { get; set; }

        // Sprint 14 (MSE-H / H1): يُملأ فقط عند استدعاء الخدمة من سياق Admin (Drill-down)
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string StageAxisSummary { get; set; }
        public bool TimeExpired { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedAnswers { get; set; }
        public bool IsRTL { get; set; } = true;
        public List<MinistrySimExamQuestionReviewItem> Questions { get; set; } = new();
    }

    public class MinistrySimExamQuestionReviewItem
    {
        public Guid QuestionId { get; set; }
        public int GlobalOrder { get; set; }
        public bool IsQuant { get; set; }
        public string QuestionText { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsQuantitative { get; set; }
        public bool? IsCorrect { get; set; } // null = متخطى (لم يُجب)
        public string? StudentAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? Explanation { get; set; }
        public string? VideoUrl { get; set; }
        public string? VerbalPassageTitle { get; set; }
        public string? VerbalPassageContent { get; set; }
        public string? VerbalPassageMediaUrl { get; set; }
        public PassageType? VerbalPassageType { get; set; }
        public string? ComparisonValue1 { get; set; }
        public string? ComparisonValue2 { get; set; }
        public QuestionDisplayType DisplayType { get; set; }
        public bool IsRTL { get; set; } = true;
        public List<HomeworkOptionReviewItem> Options { get; set; } = new();
    }
}
