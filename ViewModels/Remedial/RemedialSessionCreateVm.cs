using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Remedial
{
    public class RemedialSessionCreateVm
    {
        [Required(ErrorMessage = "الطالب مطلوب")]
        [Display(Name = "الطالب")]
        public int StudentId { get; set; }

        [Required(ErrorMessage = "الخطة العلاجية مطلوبة")]
        [Display(Name = "الخطة العلاجية")]
        public int RemedialPlanId { get; set; }

        [Display(Name = "تاريخ ووقت الجلسة")]
        public DateTime ScheduledAt { get; set; } = DateTime.Now.AddDays(1);

        // قوائم العرض
        public List<SelectListItem> Students { get; set; } = new();
        public List<SelectListItem> Plans { get; set; } = new();

        public List<string> WeakSections { get; set; } = new();

    }
}
