using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.ProfessionalModel
{
    public class ProfessionalModelVisibilityVm
    {
        public int ProfessionalModelId { get; set; }

        [Display(Name = "الشركاء")]
        public List<int> SelectedPartnerIds { get; set; } = new();

        [Display(Name = "فترات الاشتراك (المدارس)")]
        public List<int> SelectedSubscriptionPeriodIds { get; set; } = new();

        // Dropdown data
        public List<SelectListItem> Partners { get; set; } = new();
        public List<SelectListItem> SubscriptionPeriods { get; set; } = new();

        [Display(Name = "نموذج عام")]
        public bool IsGlobal { get; set; }
    }
}
