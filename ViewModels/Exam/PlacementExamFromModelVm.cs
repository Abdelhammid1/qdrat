using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Exam
{
    public class PlacementExamFromModelVm
    {
        public int SelectedModelId { get; set; }
        public int SelectedBatchId { get; set; }
        public int SelectedCourseId { get; set; }

        public int DurationMinutes { get; set; } = 60;
        public DateTime StartAt { get; set; } = DateTime.Now;
        public DateTime EndAt { get; set; } = DateTime.Now.AddHours(2);

        public List<SelectListItem> AvailableModels { get; set; } = new();
        public List<SelectListItem> AvailableBatches { get; set; } = new();
        public List<SelectListItem> AvailableCourses { get; set; } = new();
    }
}
