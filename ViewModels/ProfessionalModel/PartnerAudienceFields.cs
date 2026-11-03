using QdratNew.Enums;

namespace QdratNew.ViewModels.ProfessionalModel
{
    // PPM-S1: حقول قسم «الإتاحة للشركاء» المشتركة بين Create وEdit وVisibility
    public interface IPartnerAudienceFields
    {
        bool IsForPartners { get; set; }
        PartnerAudience PartnerAudience { get; set; }
        List<int> SelectedPartnerIds { get; set; }
        List<PartnerOptionVm> PartnerOptions { get; set; }
        bool CanManageVisibility { get; set; }
    }

    public sealed class PartnerOptionVm
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
        public string Code { get; init; } = "";
        public bool IsActive { get; init; }
    }
}
