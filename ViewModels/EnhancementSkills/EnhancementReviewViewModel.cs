namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EnhancementReviewViewModel
    {
        public int SetId { get; set; }
        public string Title { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int TimeSpentMinutes { get; set; }
        public bool IsRTL { get; set; } = true;
        public bool IsQuantitative { get; set; }
        public List<EnhancementReviewQuestionVm> Questions { get; set; } = new();
    }
}
