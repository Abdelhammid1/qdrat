using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Entities;

namespace QdratNew.ViewModels.Students
{
    public class StudentFilterViewModel
    {
        // الفلاتر
        public string SelectedBranchId { get; set; }
        public string SelectedBatchId { get; set; }
        public string Gender { get; set; }
        public string Level { get; set; }
        public string EnrollmentStatus { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // القوائم المنسدلة
        public List<SelectListItem> Branches { get; set; } = new();
        public List<SelectListItem> Batches { get; set; } = new();
        public List<SelectListItem> Genders { get; set; } = new()
    {
        new SelectListItem { Value = "", Text = "الكل" },
        new SelectListItem { Value = "ذكر", Text = "ذكر" },
        new SelectListItem { Value = "أنثى", Text = "أنثى" }
    };
        public List<SelectListItem> EnrollmentStatuses { get; set; } = new()
    {
        new SelectListItem { Value = "", Text = "الكل" },
        new SelectListItem { Value = "منتظم", Text = "منتظم" },
        new SelectListItem { Value = "موقوف", Text = "موقوف" },
        new SelectListItem { Value = "منسحب", Text = "منسحب" }
    };

        // النتائج
        public List<StudentViewModel> Students { get; set; } = new();
    }

}
