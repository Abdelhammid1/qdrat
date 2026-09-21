namespace QdratNew.ViewModels.Students
{
    public class StudentCompletionDetailViewModel
    {
        public string StudentName { get; set; }
        public int QuestionCount { get; set; }
        public int AnsweredCount { get; set; }
        public int CorrectCount { get; set; }
        public double Percentage { get; set; }
        public string Status { get; set; }
        public int StudentId { get; set; } // ✅ أضف هذا السطر
      
    }
}
