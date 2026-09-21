using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Section
{
    public class SectionPerformancePerInstructorViewModel
    {
        public int InstructorId { get; set; }  // <-- تأكد من وجود هذا

        public string InstructorName { get; set; }
        public List<SectionPerformanceAIViewModel> Sections { get; set; }
        public List<SelectListItem> BatchList { get; set; }
        public int? SelectedBatchId { get; set; }
    }
}
