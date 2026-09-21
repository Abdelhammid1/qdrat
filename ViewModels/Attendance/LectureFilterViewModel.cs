using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Attendance
{
    public class LectureFilterViewModel
    {
        public int? SelectedBatchId { get; set; }
        public int? SelectedInstructorId { get; set; }
        public DateTime? SelectedDate { get; set; }

        public List<SelectListItem> Instructors { get; set; } = new();
        public List<SelectListItem> Batches { get; set; } = new();

        public List<LectureFilterItemViewModel> Lectures { get; set; } = new();
    }
}
