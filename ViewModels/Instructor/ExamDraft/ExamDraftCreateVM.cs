using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Instructor.Exam;

namespace QdratNew.ViewModels.Instructor.ExamDraft
{
    public class ExamDraftCreateVM
    {
        public string Title { get; set; }

        public int CurriculumId { get; set; }
        public List<SelectListItem> Batches { get; set; } = new();
        public List<SelectListItem> Courses { get; set; } = new();

        public List<Guid> SelectedQuestionIds { get; set; } = new();
        public List<SelectItemVM> Curriculums { get; internal set; }
    }
}