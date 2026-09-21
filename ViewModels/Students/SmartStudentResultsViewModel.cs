namespace QdratNew.ViewModels.Students
{
    public class SmartStudentResultsViewModel
    {
        // 📊 البيانات الأساسية
        public string StudentName { get; set; } = "";
        public double HomeworkAverage { get; set; }
        public double ExamAverage { get; set; }
        public List<ResultEntryViewModel> HomeworkResults { get; set; } = new();
        public List<ResultEntryViewModel> ExamResults { get; set; } = new();

        // 🧠 الذكاء الاصطناعي
        public double PredictedNextExamScore { get; set; }
        public List<string> Recommendations { get; set; } = new();

        // 🎖️ التحفيز
        public string Badge { get; set; } = "";
    }

    public class ResultEntryViewModel
    {
        public string Title { get; set; } = "";
        public string Section { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public double Score { get; set; }
        public DateTime SubmittedAt { get; set; }
    }
}
