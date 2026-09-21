using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Attendance
{
    public class MarkAttendanceFilterViewModel
    {
        public int? SelectedInstructorId { get; set; }
        public int? SelectedBatchId { get; set; }
        public DateTime? SelectedDate { get; set; }

        public List<SelectListItem> Instructors { get; set; }
        public List<SelectListItem> Batches { get; set; }

        public List<MarkAttendanceLectureRow> Lectures { get; set; }
    }

    public class MarkAttendanceLectureRow
    {
        public int LectureId { get; set; }
        public string Title { get; set; }
        public string SectionTitle { get; set; }
        public string CourseName { get; set; }
        public string InstructorName { get; set; }
        public DateTime Date { get; set; }
        public bool IsToday { get; set; }
    }

}
