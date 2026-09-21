namespace QdratNew.ViewModels.Students
{
    public class ExamResultSummaryVm
    {
        public int TotalQuestions { get; set; }
        public int AnsweredQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }

        // 🆕 عدد إجابات "لا أعرف الإجابة" (مُتضمَّنة أصلًا داخل WrongAnswers)
        public int DontKnowAnswers { get; set; }

        public int SkippedQuestions { get; set; }
        public double ScorePercentage { get; set; }
        public double SolveMinutes { get; internal set; }
        public double AverageTimePerQuestion { get; internal set; }
    }
}
