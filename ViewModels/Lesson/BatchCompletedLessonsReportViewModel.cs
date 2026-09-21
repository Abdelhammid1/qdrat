namespace QdratNew.ViewModels.Lesson
{
    public class BatchCompletedLessonsReportViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string CompletionTitle { get; set; }
        public string AddedBy { get; set; }
        public string AddedByName { get; set; }
        public int CompletedLessonCount { get; set; }
        public DateTime CompletionDate { get; set; }
        public int? HomeworkSetId { get; set; } // ✅ نضيف هذا

    }
}
