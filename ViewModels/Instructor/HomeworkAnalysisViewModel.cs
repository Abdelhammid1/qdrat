namespace QdratNew.ViewModels.Instructor
{
    public class HomeworkAnalysisViewModel
    {
        public int HomeworkId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }
        public DateTime? LectureDate { get; set; }
        public string SectionTitle { get; set; }
        public float? Score { get; set; }
        public bool IsCompleted { get; set; }

        // إحصائيات عامة
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int IncorrectAnswers { get; set; }
        public double AccuracyPercentage => TotalQuestions > 0 ? (CorrectAnswers / (double)TotalQuestions) * 100 : 0;

        // تحليل الأسئلة الخاطئة
        public List<WrongAnswerViewModel> WrongAnswers { get; set; }

        // توصيات الذكاء الاصطناعي
        public string AIRecommendation { get; set; }

        // واجبات أخرى
        public List<StudentHomeworkSummaryViewModel> StudentHomeworks { get; set; }
    }

    public class WrongAnswerViewModel
    {
        public string QuestionText { get; set; }
        public string StudentAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public string IndicatorTitle { get; set; }
    }

    public class StudentHomeworkSummaryViewModel
    {
        public int HomeworkId { get; set; }
        public DateTime? LectureDate { get; set; }
        public float? Score { get; set; }
        public bool IsCompleted { get; set; }
    }
}
