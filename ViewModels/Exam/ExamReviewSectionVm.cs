namespace QdratNew.ViewModels.Exam
{
    public class ExamReviewSectionVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; } = "";

        public int Total { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Skipped { get; set; }
        public int TotalQuestions { get; internal set; }
        public int CorrectCount { get; internal set; }
        public int WrongCount { get; internal set; }
        public int SkippedCount { get; internal set; }
    }
}
