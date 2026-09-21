using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementDetailsVm
    {
        public int SetId { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string BatchName { get; set; } = "";
        public string LectureTitle { get; set; } = "";
        public int QuestionsPerStudent { get; set; }
        public bool IsSent { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? ScheduledSendAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int GenerationMethod { get; set; }
        public string? ProfessionalModelTitle { get; set; }

        public List<EnhancementStudentRowVm> Students { get; set; } = new List<EnhancementStudentRowVm>();
        public List<EnhancementQuestionReviewVm> ReviewQuestions { get; set; } = new List<EnhancementQuestionReviewVm>();
        public List<EnhancementIndicatorSummaryVm> IndicatorSummaries { get; set; } = new List<EnhancementIndicatorSummaryVm>();
    }

    public class EnhancementStudentRowVm
    {
        public int StudentId { get; set; }
        public string FullName { get; set; } = "";
        public string? PhoneNumber { get; set; }
        public int TotalQuestions { get; set; }
        public int AnsweredQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int AnsweredPercentage => TotalQuestions > 0 ? (int)Math.Round(AnsweredQuestions * 100.0 / TotalQuestions) : 0;
        public int ScorePercentage => TotalQuestions > 0 ? (int)Math.Round(CorrectAnswers * 100.0 / TotalQuestions) : 0;
    }

    public class EnhancementQuestionReviewVm
    {
        public Guid QuestionId { get; set; }
        public string Title { get; set; } = "";
        public string LessonTitle { get; set; } = "";
        public string SectionTitle { get; set; } = "";
        public string Difficulty { get; set; } = "";
    }

    public class EnhancementIndicatorSummaryVm
    {
        public int LessonId { get; set; }
        public string SectionTitle { get; set; } = "";
        public string LessonTitle { get; set; } = "";
        public int RequestedCount { get; set; }
        public int ActualCount { get; set; }
        /// <summary>الأسئلة المُعيَّنة فعلياً لهذا المؤشر (للعرض والتعديل)</summary>
        public List<EnhancementQuestionReviewVm> Questions { get; set; } = new();
    }
}
