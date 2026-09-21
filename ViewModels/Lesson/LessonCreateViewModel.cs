using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Lesson
{
    public class LessonCreateViewModel
    {
        public int SelectedSectionId { get; set; }
        public int SelectedUnitId { get; set; }
        [ValidateNever]
        public List<SelectListItem> Sections { get; set; }
        [ValidateNever]
        public List<SelectListItem> Units { get; set; }

        public string Title { get; set; }
        public string Content { get; set; }
        public bool IsActive { get; set; } = true; // ✅ الحقل الجديد لتفعيل / تعطيل الدرس





    }
}
