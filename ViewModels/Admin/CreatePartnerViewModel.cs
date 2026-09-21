using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner
{
    public class CreatePartnerViewModel
    {
        [Required]
        public string Name { get; set; }

        [Required]
        public string Code { get; set; }

        public DateTime PartnershipStart { get; set; }
        public DateTime PartnershipEnd { get; set; }

        // ⬅️ الملف
        public IFormFile? LogoFile { get; set; }
    }
}
