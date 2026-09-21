namespace QdratNew.ViewModels.Students
{
    public class TrainingResultViewModel
    {
        public int SectionId { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double ScorePercentage { get; set; }
        public bool IsQuantitative { get; set; }
        public List<TrainingWrongQuestionVm> WrongQuestions { get; set; } = new();
    }

    public class TrainingWrongQuestionVm
    {
        public string QuestionTitle { get; set; }
        public string StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
    }

}
