using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Batch;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Lesson
{
    public class LessonCompletionFormViewModel
    {
        public int SelectedBatchId { get; set; }
        public int SelectedCurriculumId { get; set; }
        public int SelectedSectionId { get; set; }

        public List<SelectListItem> BatchList { get; set; } = new();
        public List<SelectListItem> CurriculumList { get; set; } = new();
        public List<SelectListItem> SectionList { get; set; } = new();
        public List<int> PreSelectedLessonIds { get; set; } = new();

        public List<LessonViewItem> AvailableLessons { get; set; } = new();
        public string CompletedLessonIds { get; set; } // سترسل كـ hidden input

        [Required(ErrorMessage = "يرجى إدخال عنوان تسجيل المؤشرات")]
        public string CompletionTitle { get; set; }

    }

}
