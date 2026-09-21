namespace QdratNew.ViewModels.Homework
{
    public class CompletedSectionSummaryViewModel
    {
        public int? SectionId { get; set; }
        public string SectionTitle { get; set; }
        public int CompletedLessonsCount { get; set; }
        public string? LastLectureTitle { get; set; }
        public DateTime? LastLectureDate { get; set; }
    }
}
