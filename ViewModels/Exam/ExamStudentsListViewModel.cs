namespace QdratNew.ViewModels.Exam
{
    public class ExamStudentsListViewModel
    {
        public int AssignmentId { get; set; }
        public string ExamTitle { get; set; }
        public string BatchName { get; set; }
        public List<ExamStudentViewModel> Students { get; set; } = new();


    
        public int ExamDuration { get; set; }

    }

    public class ExamStudentViewModel
    {
        public int StudentId { get; set; }
        public string FullName { get; set; }
        public string Status { get; set; }
        public double? Score { get; set; }

       
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedQuestions { get; set; }
        public DateTime? ExamDate { get; set; }
        public bool IsPresent { get; set; }
        public object AnsweredQuestions { get; internal set; }
        public double TimeSpentMinutes { get; internal set; }
    }
}
