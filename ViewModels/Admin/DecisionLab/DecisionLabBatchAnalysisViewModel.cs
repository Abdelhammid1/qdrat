namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class DecisionLabBatchAnalysisViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;

        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;

        public int? CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = string.Empty;

        public int ActiveStudentsCount { get; set; }
        public int CompletedLessonsCount { get; set; }
        public int TotalQuestionAttempts { get; set; }

        public int SentHomeworkSetsCount { get; set; }
        public double HomeworkSubmissionPercent { get; set; }
        public double AverageHomeworkScore { get; set; }

        public int SentExamsCount { get; set; }
        public double ExamParticipationPercent { get; set; }
        public double AverageExamScore { get; set; }

        public double AttendancePercent { get; set; }
        public int WeakLessonsCount { get; set; }
        public int HighRiskQuestionsCount { get; set; }

        public double RiskScore { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
        public string DecisionMessage { get; set; } = string.Empty;
        public string DecisionSummary { get; set; } = string.Empty;

        public DecisionRiskBreakdownViewModel RiskBreakdown { get; set; } = new();
        public List<DecisionLabWeakLessonViewModel> WeakLessons { get; set; } = new();
        public List<DecisionLabHighRiskQuestionViewModel> HighRiskQuestions { get; set; } = new();
        public List<DecisionLabRecommendationPreviewViewModel> Recommendations { get; set; } = new();
    }
}
