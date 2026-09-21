using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner
{
    public class EditPartnerViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الشريك مطلوب")]
        public string Name { get; set; }

        [Required(ErrorMessage = "كود الشريك مطلوب")]
        public string Code { get; set; }

        public DateTime PartnershipStart { get; set; }
        public DateTime PartnershipEnd { get; set; }

        // اللوجو الحالي
        public string? CurrentLogoPath { get; set; }

        // لوجو جديد (اختياري)
        public IFormFile? LogoFile { get; set; }
    }
}
