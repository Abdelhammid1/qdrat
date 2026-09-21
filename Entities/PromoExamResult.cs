using System;

namespace QdratNew.Entities
{
    public class PromoExamResult
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public PromoExamSession Session { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedQuestions { get; set; }

        public double ScorePercent => Math.Round(((double)CorrectAnswers / Math.Max(TotalQuestions, 1)) * 100, 2);
        public DateTime SubmittedAt { get; set; } = DateTime.Now;
    }
}
