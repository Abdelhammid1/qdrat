namespace QdratNew.ViewModels.Promo
{
    public class PromoExamResultViewModel
    {
        public int SessionId { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedQuestions { get; set; }
        public double ScorePercent { get; set; }
    }
}
