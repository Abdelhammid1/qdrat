namespace QdratNew.ViewModels.Batch
{
    public class BatchesMissingLessonsTodayViewModel
    {
        public string BatchName { get; set; }
        public DateTime LectureDate { get; set; }
        public bool HasCompletedLessons { get; set; }
    }
}
