using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class SuccessPartner
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } // اسم الشركة أو الجهة

        [MaxLength(500)]
        public string? Url { get; set; } // رابط الشريك (اختياري)

        [MaxLength(300)]
        public string? LogoPath { get; set; } // مسار اللوجو

        public bool IsActive { get; set; } = true; // حالة التفعيل

        public int DisplayOrder { get; set; } = 0; // لترتيب العرض في الصفحة

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
