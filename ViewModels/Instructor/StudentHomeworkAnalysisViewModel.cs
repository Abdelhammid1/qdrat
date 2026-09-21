namespace QdratNew.ViewModels.Instructor
{
    public class StudentHomeworkAnalysisViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        public int TotalHomeworks { get; set; }
        public int CorrectAnswers { get; set; }
        public int IncorrectAnswers { get; set; }
        public double CompletionRate { get; set; }
        public double AccuracyRate { get; set; }
        public List<WeakLessonViewModel> WeakLessons { get; set; } = new();
        public List<HomeworkSummaryViewModel> LatestHomeworks { get; set; } = new();
        public string AIRecommendation { get; set; } // ناتج من ML.NET
        public string BatchName { get; set; }
        public double AverageScore { get; set; }

    }
    public class WeakLessonViewModel
    {
        public string LessonTitle { get; set; }
        public string SectionTitle { get; set; }
        public int ErrorCount { get; set; }

    }
}
