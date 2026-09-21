using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using QdratNew.ViewModels.Section;

namespace QdratNew.ViewModels
{
    public class SessionFilterViewModel
    {
        // 🔍 الفلاتر
        public string StatusFilter { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public List<SelectListItem> StatusList { get; set; } = new();

        public string CompletionFilter { get; set; } // 🆕
        public List<SelectListItem> CompletionList { get; set; } // 🆕

        // 📋 نتائج البحث
        public List<AdminSessionApprovalViewModel> Sessions { get; set; } = new List<AdminSessionApprovalViewModel>();

        // 📌 لتعبئة القائمة المنسدلة للحالات
        public List<StudySessionStatus> StatusOptions { get; set; } = Enum.GetValues(typeof(StudySessionStatus)).Cast<StudySessionStatus>().ToList();
    }
}
