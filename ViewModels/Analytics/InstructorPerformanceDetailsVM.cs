namespace QdratNew.ViewModels.Admin.Analytics
{
    public class InstructorPerformanceDetailsVM
    {
        public int InstructorId { get; set; }
        public string InstructorName { get; set; } = "";

        public double OverallScore { get; set; }
        public string ScientificRating { get; set; } = "";
        public string RatingDescription { get; set; } = "";
        public string RiskLevel { get; set; } = "";
        public string DecisionRecommendation { get; set; } = "";

        public int TotalBatches { get; set; }
        public int TotalWeakLessons { get; set; }
        public int TotalHighRiskQuestions { get; set; }

        public List<InstructorBatchPerformanceVM> Batches { get; set; } = new();
    }

    public class InstructorBatchPerformanceVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string CourseName { get; set; } = "";

        public int StudentsCount { get; set; }
        public double AvgScore { get; set; }
        public double WeaknessPercentage { get; set; }

        public List<InstructorWeakLessonVM> WeakLessons { get; set; } = new();
    }

    public class InstructorWeakLessonVM
    {
        public int LessonId { get; set; }
        public string LessonName { get; set; } = "";
        public int AffectedStudents { get; set; }
        public double WeaknessPercentage { get; set; }

        public List<InstructorHighRiskQuestionVM> HighRiskQuestions { get; set; } = new();
    }

    public class InstructorHighRiskQuestionVM
    {
        public Guid QuestionId { get; set; }
        public string ReferenceNumber { get; set; } = "";
        public string QuestionTitle { get; set; } = "";
        public int AttemptsCount { get; set; }
        public int WrongCount { get; set; }
        public double ErrorPercentage { get; set; }
    }
}