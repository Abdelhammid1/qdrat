namespace QdratNew.ViewModels.Students
{
    public class StudentAIProgressRecommendationViewModel
    {
        public string StudentName { get; set; } = "";
        public string Recommendation { get; set; } = "";
        public float Score { get; set; }
        public float ProgressPercentage { get; set; }
        public int CompletedLessons { get; set; }
        public int CompletedExercises { get; set; }
        public string DifficultyLevel { get; set; } = "";
    }
}
