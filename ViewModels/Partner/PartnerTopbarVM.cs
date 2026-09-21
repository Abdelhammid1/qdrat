using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner
{
    public class PartnerTopbarVM
    {
        // المستخدم
        public string FullName { get; set; }
        public string ProfileImageUrl { get; set; }

        // الدور
        public string ActiveRole { get; set; }
        public List<string> AvailableRoles { get; set; } = new();

        // الشريك
        public int ActivePartnerId { get; set; }
        public string ActivePartnerName { get; set; }
        public string ActivePartnerLogo { get; set; }
        public List<PartnerItemVM> Partners { get; set; } = new();

        // المدرسة
        public int? ActiveSchoolId { get; set; }
        public List<SchoolItemVM> Schools { get; set; } = new();
    }

    public class PartnerItemVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? LogoPath { get; internal set; }
    }

    public class SchoolItemVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
}
