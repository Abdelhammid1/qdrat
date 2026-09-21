namespace QdratNew.ViewModels.Exam
{
    public class ExamAssignmentListViewModel
    {
        public int Id { get; set; }
        public string ExamTitle { get; set; }
        public string BatchName { get; set; }
        public string CurriculumTitle { get; set; }
        public string SectionTitle { get; set; }
        public string LessonTitle { get; set; }
        public int TotalQuestions { get; set; }
        public int DurationMinutes { get; set; }
        public DateTime AssignedAt { get; set; }
        public bool IsSentToStudents { get; set; }
    }
}
