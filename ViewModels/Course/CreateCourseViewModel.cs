using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Course
{
    public class CreateCourseViewModel
    {
        public required string Name { get; set; }

        public int? ProjectId { get; set; } // 🟰 المشروع المرتبط

        public List<SelectListItem> Projects { get; set; } = new(); // 🟰 لعرض المشاريع DropDown

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public bool IsActive { get; set; }
    }
}
