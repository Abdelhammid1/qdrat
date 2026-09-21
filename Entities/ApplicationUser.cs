using Microsoft.AspNetCore.Identity;
using QdratNew.Enums;

namespace QdratNew.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string? FullName { get; set; }
        public string? WhatsAppNumber { get; set; }
        public string NationalID { get; set; } = string.Empty;

        public string? ProfileImagePath { get; set; }
        public GenderType Gender { get; set; }
        public DateTime? LastNameChangeDate { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? Level { get; set; }  // ✅ مستوى الطالب مثلاً (مبتدئ - متوسط - متقدم)
        public int? PartnerId { get; set; }

        public bool IsActive { get; set; } = true;

    }
}
