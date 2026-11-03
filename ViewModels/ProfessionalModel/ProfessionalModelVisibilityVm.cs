using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;

namespace QdratNew.ViewModels.ProfessionalModel
{
    public class ProfessionalModelVisibilityVm : IPartnerAudienceFields
    {
        public int ProfessionalModelId { get; set; }

        [Display(Name = "الشركاء")]
        public List<int> SelectedPartnerIds { get; set; } = new();

        [Display(Name = "فترات الاشتراك (المدارس)")]
        public List<int> SelectedSubscriptionPeriodIds { get; set; } = new();

        // Dropdown data
        public List<SelectListItem> SubscriptionPeriods { get; set; } = new();

        // PPM-S1: الإتاحة للشركاء. تُتجاهل من الـ POST لمن لا يملك ProfessionalModels.Visibility
        public bool IsForPartners { get; set; }
        public PartnerAudience PartnerAudience { get; set; } = PartnerAudience.All;

        [ValidateNever, BindNever]
        public List<PartnerOptionVm> PartnerOptions { get; set; } = new();

        [ValidateNever, BindNever]
        public bool CanManageVisibility { get; set; }
    }
}
