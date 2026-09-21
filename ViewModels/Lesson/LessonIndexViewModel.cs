using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Lesson
{

    public class LessonIndexViewModel
    {
        // ✅ قائمة الدروس المعروضة بعد التصفية
        public List<LessonViewModel> Lessons { get; set; } = new();

        // ✅ فلاتر البحث
        public int? SelectedCurriculumId { get; set; }
        public int? SelectedSectionId { get; set; }
        public int? SelectedUnitId { get; set; }

        // ✅ القوائم المنسدلة للفلترة
        public List<SelectListItem> CurriculumList { get; set; } = new();
        public List<SelectListItem> SectionList { get; set; } = new();
        public List<SelectListItem> UnitList { get; set; } = new();
    }
}
