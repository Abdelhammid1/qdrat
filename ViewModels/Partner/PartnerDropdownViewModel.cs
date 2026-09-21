namespace QdratNew.ViewModels.Partner
{
    public class PartnerDropdownViewModel
    {
        public int? ActivePartnerId { get; set; }
        public string? ActivePartnerName { get; set; }

        public List<PartnerItem> Partners { get; set; } = new();

        public bool HasMultiplePartners => Partners.Count > 1;
    }

    public class PartnerItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
