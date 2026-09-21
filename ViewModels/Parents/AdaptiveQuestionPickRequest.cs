using System.Collections.Generic;

namespace QdratNew.ViewModels.Parents
{
    public class AdaptiveQuestionPickRequest
    {
        public int StudentId { get; set; }
        public int? CurriculumId { get; set; }
        public int? SectionId { get; set; }
        public int QuestionCount { get; set; } = 10;
        public string PracticeMode { get; set; } = "QuickPractice";
        public string StudentLevel { get; set; } = "Average";
        public List<int> WeakSectionIds { get; set; } = new();
    }

    public class StudentWeaknessAnalysisResult
    {
        public int StudentId { get; set; }
        public string StudentLevel { get; set; } = "Average";
        public double OverallScore { get; set; }
        public string SafeWeaknessSummary { get; set; } = string.Empty;
        public bool IsImproving { get; set; }
        public List<WeakSectionSummary> WeakSections { get; set; } = new();
    }

    public class WeakSectionSummary
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public double Score { get; set; }
        public string SafeLabel { get; set; } = string.Empty;
    }
}
