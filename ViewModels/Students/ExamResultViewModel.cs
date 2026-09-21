using QdratNew.ViewModels.Reports;

namespace QdratNew.ViewModels.Students
{
    public class ExamResultViewModel
    {
        public string ExamTitle { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int Rank { get; set; }
        public int TotalStudents { get; set; }
        public int SuccessRate { get; set; }
        // ✅ تم تعديل الخاصية هنا لتعرض المحور
        public string SectionTitle { get; set; } = string.Empty;
        public double ScorePercentage { get; set; }
        public double Percent { get; set; }
        public int ExamAssignmentId { get; set; }
        public int SkippedQuestions { get; set; }

        public List<AnsweredQuestionViewModel> AnsweredQuestions { get; set; } = new();
        public List<QuestionReviewEntry> Questions { get; set; } = new(); // ✅ أضف هذا السطر
        public int TimeSpentMinutes { get; set; }
        public string TimeSpentFormatted { get; set; }


        public DateTime LastUpdated { get; set; }
 

        public string EncouragementMessage { get; set; } = "";
        public string MoodIcon { get; set; } = "";

        public List<WrongQuestionVm> WrongQuestions { get; set; } = new();
        public double AverageTimePerQuestion { get; internal set; }
        public bool IsIndividual { get; internal set; }
        public bool IsRTL { get; internal set; }
    }


    public class QuestionReviewEntry
    {
        public string QuestionTitle { get; set; }
        public string CorrectAnswer { get; set; }
        public string StudentAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsQuantitative { get; set; }
        // 🟦 جديد:
        public string? SectionTitle { get; set; }
        public string? LessonTitle { get; set; }
    }
}
