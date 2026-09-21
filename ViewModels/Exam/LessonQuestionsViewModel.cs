namespace QdratNew.ViewModels.Exam
{
    public class LessonQuestionsViewModel
    {
        public int AssignmentId { get; set; }
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }

        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public string AvgTimeFormatted { get; set; }

        public List<QuestionAnalyticsVm> Questions { get; set; } = new();
    }
}
