namespace QdratNew.ViewModels.Exam
{
    public class ExamModeSelectionViewModel
    {
        public int BatchId { get; set; }
        public int SectionId { get; set; }
        public int CurriculumId { get; set; }
        public int LessonId { get; set; }

        public string BatchName { get; set; }
        public string SectionTitle { get; set; }

        public bool IsOnline { get; set; }
        public DateTime? ScheduledDate { get; set; }



    }

}
