namespace QdratNew.ViewModels.Instructor.Exam
{
    public class SectionGroupVM
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }

        public List<ExamDraftQuestionVM> Questions { get; set; }
    }
}
