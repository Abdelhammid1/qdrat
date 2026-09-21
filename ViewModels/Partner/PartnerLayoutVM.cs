namespace QdratNew.ViewModels.Partner
{
    public class PartnerLayoutVM
    {
        public int PartnerId { get; set; }
        public string PartnerName { get; set; }

        public List<PartnerSchoolItemVM> Schools { get; set; } = new();
        public int? ActiveSchoolId { get; set; }
    }

    public class PartnerSchoolItemVM
    {
        public int SchoolId { get; set; }
        public string SchoolName { get; set; }
    }

}
