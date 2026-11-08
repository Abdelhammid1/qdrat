namespace QdratNew.ViewModels.Lecture
{
    public class LectureListViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public DateTime Date { get; set; }
        public TimeSpan? ScheduledTime { get; set; }
        public int? DurationMinutes { get; set; }
        public TimeSpan? ScheduledEndTime { get; set; }
        public string InstructorName { get; set; } = "غير محدد";
        public bool IsManualInstructorAssignment { get; set; }
        public string SectionTitle { get; set; } = "غير محدد";
        public string CourseName { get; set; } = "غير محدد";
        public string Location { get; set; }
        public string BatchName { get; set; } = "غير محدد";

        /// <summary>OLC-S2.3: حالة المحتوى أونلاين (null = لا يوجد) — تُعرض لدفعات الأونلاين فقط.</summary>
        public QdratNew.Enums.LectureContentStatus? OnlineContentStatus { get; set; }
    }

}
