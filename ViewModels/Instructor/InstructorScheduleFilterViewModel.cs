using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Instructor
{
    public class InstructorScheduleFilterViewModel
    {
        public int? SelectedInstructorId { get; set; }
        public DayOfWeek? SelectedDayOfWeek { get; set; }

        public List<SelectListItem> Instructors { get; set; } = new();
        public List<SelectListItem> ArabicDays { get; set; } = new();

        public List<InstructorWorkScheduleViewModel> Schedules { get; set; } = new();

        
    }

}
