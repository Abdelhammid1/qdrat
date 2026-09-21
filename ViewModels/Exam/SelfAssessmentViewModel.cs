namespace QdratNew.ViewModels.Exam
{
    public class SelfAssessmentViewModel
    {
        public List<SelectableSection> CompletedSections { get; set; }
        public int SelectedCount { get; set; }
        public int MaxQuestions { get; set; }
    }
}
