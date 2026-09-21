using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Remedial
{
    public class RemedialPlanCreateViewModel
    {
        [Required]
        public int StudentId { get; set; }
        public string StudentName { get; set; }

        [Required]
        [Display(Name = "عنوان الخطة")]
        public string Title { get; set; }

        [Display(Name = "الوصف")]
        public string Description { get; set; }

        [Display(Name = "تاريخ البداية")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "تاريخ النهاية")]
        public DateTime EndDate { get; set; } = DateTime.Today.AddDays(14);

        [Display(Name = "المستوى الحالي")]
        public string PerformanceLevel { get; set; }

        // ✅ اختيار المؤشرات (المحاور الضعيفة)
        public List<int> SelectedSectionIds { get; set; }
        public List<SelectListItem> Sections { get; set; }

        // ✅ روابط الفيديوهات لكل مؤشر
        public Dictionary<int, string> VideoUrls { get; set; }

        // ✅ أسئلة علاجية (اختيار من بنك الأسئلة)
        public List<int> SelectedQuestionIds { get; set; }
        public List<SelectListItem> Questions { get; set; }
    }
}
