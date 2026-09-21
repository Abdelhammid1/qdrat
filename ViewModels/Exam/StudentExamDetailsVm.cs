namespace QdratNew.ViewModels.Exam
{
    public class StudentExamDetailsVm
    {
        public int StudentId { get; set; }     // ✅ أضف هذا السطر
        public int ExamId { get; set; }

        public string StudentName { get; set; }
        public string ExamTitle { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public List<StudentExamAnswerVm> Answers { get; set; } = new();
    }

    public class StudentExamAnswerVm
    {
        public string QuestionTitle { get; set; }
        public string SelectedAnswer { get; set; }
        public string CorrectAnswer { get; set; }
        public bool IsCorrect { get; set; }
    }
}
