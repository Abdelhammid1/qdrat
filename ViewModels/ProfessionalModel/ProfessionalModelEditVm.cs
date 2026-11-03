using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.ProfessionalModel
{
    public class ProfessionalModelEditVm : IPartnerAudienceFields
    {
        [Required(ErrorMessage = "كود النموذج مطلوب")]
        public string Title { get; set; }

        [Required(ErrorMessage = "الوصف مطلوب")]
        public string Description { get; set; }

        public int? CurriculumId { get; set; }

        [Required(ErrorMessage = "نوع النموذج مطلوب")]
        public ProfessionalModelType ModelType { get; set; } = ProfessionalModelType.Exam;

        public List<SelectListItem> Curriculums { get; set; } = new();

        // PPM-S1: الإتاحة للشركاء. تُتجاهل من الـ POST لمن لا يملك ProfessionalModels.Visibility
        public bool IsForPartners { get; set; }
        public PartnerAudience PartnerAudience { get; set; } = PartnerAudience.All;
        public List<int> SelectedPartnerIds { get; set; } = new();

        [ValidateNever, BindNever]
        public List<PartnerOptionVm> PartnerOptions { get; set; } = new();

        [ValidateNever, BindNever]
        public bool CanManageVisibility { get; set; }
    }
}
