namespace QdratNew.ViewModels.Exam
{
    public class StudentExamFullDataVm
    {
        public int TotalExams { get; set; }
        public int CompletedExams { get; set; }
        public int PendingExams { get; set; }
        public int LateExams { get; set; }
        public double AverageScore { get; set; }

        public List<ExamListVm> Exams { get; set; } = new();
    }
}
