namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamSectionSelectionVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; }
        public int QuestionCount { get; set; } // عدد الأسئلة المطلوب من هذا المحور
        public bool IsSelected { get; set; }
    }
}
