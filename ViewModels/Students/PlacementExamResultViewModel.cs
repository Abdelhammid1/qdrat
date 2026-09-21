namespace QdratNew.ViewModels.Students
{
    public class PlacementExamResultViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int ScorePercentage { get; set; }
        public List<PlacementExamQuestionVm> Questions { get; set; }
    }

    public class PlacementExamQuestionVm
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public bool IsQuantitative { get; set; }
    }

    public class PlacementExamReportViewModel
    {
        public int ExamId { get; set; }
        public string ExamTitle { get; set; }
        public List<SectionScoreVm> SectionScores { get; set; }
        public double OverallScore { get; set; }
    }

    public class SectionScoreVm
    {
        public string SectionName { get; set; }
        public double Accuracy { get; set; }
    }

}
