namespace QdratNew.ViewModels.Students
{
    public class StudentResultsViewModel
    {
        public List<StudentHomeworkResultEntry> HomeworkResults { get; set; } = new();
        public List<StudentExamResultEntry> ExamResults { get; set; } = new();
    }

    public class StudentHomeworkResultEntry
    {
        public int HomeworkSetId { get; set; }


        
        public string HomeworkTitle { get; set; } = "";
        public string LessonTitle { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int Score { get; set; }

        // ✅ تم تعديل الخاصية هنا لتعرض المحور
        public string SectionTitle { get; set; } = string.Empty;

        public DateTime? SubmittedAt { get; set; }

    }

    public class StudentExamResultEntry
    {
        public string ExamTitle { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int Score { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int ExamAssignmentId { get; set; }

        public string SectionTitle { get; set; } = "—"; // ✅ مضافة الآن




    }

}
