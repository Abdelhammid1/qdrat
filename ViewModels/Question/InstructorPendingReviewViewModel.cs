using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Question
{
    public class InstructorPendingReviewViewModel
    {
        public int? CurriculumId { get; set; }
        public List<SelectListItem> Curriculums { get; set; } = new();
    }
}
