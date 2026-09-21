namespace QdratNew.ViewModels.Students
{
    public class AssignedExamViewModel
    {
        public int ExamAssignmentId { get; set; }
        public string Title { get; set; }
        public string SectionTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public DateTime AssignedAt { get; set; }
        public bool IsSolved { get; set; }
    }
}
