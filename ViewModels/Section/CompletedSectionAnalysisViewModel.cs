namespace QdratNew.ViewModels.Section
{
    public class CompletedSectionAnalysisViewModel
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int CompletedLessonsCount { get; set; }
        public int ReviewQuestionsCount { get; set; }
        public bool IsHomeworkGenerated { get; set; }
        public bool IsExamGenerated { get; set; }
        public double? StudentCompletionRate { get; set; } // نسبة تقريبية
    }
}
