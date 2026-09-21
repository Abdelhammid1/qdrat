namespace QdratNew.ViewModels.Homework
{
    public class HomeworkResultViewModel
    {
        public int HomeworkSetId { get; set; }
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public int TotalQuestions { get; set; }
        public int TotalStudents { get; set; }
        public int CorrectAnswers { get; set; }
        public int Rank { get; set; }
        public double ScorePercentage { get; set; }
        public DateTime SubmittedAt { get; set; }
        public bool IsQuantitative { get; set; }
     
        public DateTime LastUpdated { get; set; }
        public bool IsRTL { get; set; }
        public List<HomeworkResultItemViewModel> Questions { get; set; } = new();
        public object WrongAnswers { get; internal set; }
        public double TimeSpentMinutes { get; internal set; }
    }


    public class HomeworkQuestionResultItem
    {
        public string Title { get; set; }
        public string? SelectedAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? Explanation { get; set; }
        public string? VideoUrl { get; set; }

        public Guid QuestionId { get; set; }
        public string QuestionTitle { get; set; }
        public string? StudentAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public double? TimeTakenSeconds { get; set; }
        public bool IsRTL { get; internal set; }
    }




    public class HomeworkResultItemViewModel
    {
        public string QuestionTitle { get; set; }
        public string StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool? IsCorrect { get; set; }
        public Guid QuestionId { get; internal set; }
    }
}
