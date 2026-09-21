namespace QdratNew.ViewModels.Reports
{
    public class ExamSectionSummaryVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int TotalQuestions { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Skipped { get; set; }
    }
}
