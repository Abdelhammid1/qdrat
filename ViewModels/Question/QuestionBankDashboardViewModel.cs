namespace QdratNew.ViewModels.Question
{
    using Microsoft.AspNetCore.Mvc.Rendering;

    public class QuestionBankDashboardViewModel
    {
        public int TotalQuestions { get; set; }
        public int TotalLessons { get; set; }
        public int TotalSections { get; set; }
        public int SimilarQuestionsCount { get; set; }
        public int SimilarQuestionsSampleSize { get; set; }
        public decimal AverageQuestionsPerLesson { get; set; }
        public List<QuestionBankStatusCard> StatusCards { get; set; } = new();
        public List<QuestionBankChartItem> DifficultyStats { get; set; } = new();
        public List<QuestionBankChartItem> CurriculumCoverageStats { get; set; } = new();
        public List<QuestionBankCurriculumStats> QuestionBankStatsByCurriculum { get; set; } = new();
    }

    public class QuestionBankStatusCard
    {
        public string Title { get; set; } = string.Empty;
        public int Count { get; set; }
        public string Icon { get; set; } = "fa-chart-simple";
        public string Color { get; set; } = "#0d6efd";
        public string Url { get; set; } = "#";
        public string Description { get; set; } = string.Empty;
    }

    public class QuestionBankChartItem
    {
        public string Label { get; set; } = string.Empty;
        public int Value { get; set; }
        public string Url { get; set; } = "#";
    }

    public class QuestionBankCurriculumStats
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = string.Empty;
        public int QuestionsCount { get; set; }
        public int ApprovedCount { get; set; }
        public int PendingReviewCount { get; set; }
        public int MissingAnswersCount { get; set; }
        public int RejectedCount { get; set; }
        public int LessonsCount { get; set; }
        public List<QuestionBankSectionStats> Sections { get; set; } = new();
    }

    public class QuestionBankSectionStats
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public int LessonsCount { get; set; }
        public int QuestionsCount { get; set; }
        public int ApprovedCount { get; set; }
        public int PendingReviewCount { get; set; }
        public int MissingAnswersCount { get; set; }
        public int RejectedCount { get; set; }
        public int EasyCount { get; set; }
        public int MediumCount { get; set; }
        public int HardCount { get; set; }
        public int VeryHardCount { get; set; }
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = string.Empty;
    }

    public class SimilarQuestionGroupsViewModel
    {
        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int? LessonId { get; set; }
        public string? SearchTitle { get; set; }
        public int ScannedQuestionsCount { get; set; }
        public int GroupsCount { get; set; }
        public int SimilarPairsCount { get; set; }
        public double Threshold { get; set; }
        public int? ScanLimit { get; set; }
        public bool IsLimitedBySample { get; set; }
        public bool HasRun { get; set; }
        public string ScopeDescription { get; set; } = "أحدث الأسئلة في البنك";
        public DateTime GeneratedAt { get; set; }
        public List<SelectListItem> Curriculums { get; set; } = new();
        public List<SelectListItem> Sections { get; set; } = new();
        public List<SelectListItem> Lessons { get; set; } = new();
        public List<SimilarQuestionGroupViewModel> Groups { get; set; } = new();
    }

    public class SimilarQuestionGroupViewModel
    {
        public int GroupNumber { get; set; }
        public int QuestionsCount { get; set; }
        public int PairsCount { get; set; }
        public double AverageSimilarity { get; set; }
        public double MaxSimilarity { get; set; }
        public string CurriculumTitle { get; set; } = "—";
        public string SectionTitle { get; set; } = "—";
        public string LessonTitle { get; set; } = "—";
        public List<SimilarQuestionItemViewModel> Questions { get; set; } = new();
    }

    public class SimilarQuestionItemViewModel
    {
        public Guid Id { get; set; }
        public string ReferenceNumber { get; set; } = "—";
        public string Title { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = "—";
        public string SectionTitle { get; set; } = "—";
        public string LessonTitle { get; set; } = "—";
        public DateTime CreatedAt { get; set; }
        public double SimilarityToRepresentative { get; set; }
        public string MatchType { get; set; } = "—";
        public bool IsRepresentative { get; set; }
    }
}
