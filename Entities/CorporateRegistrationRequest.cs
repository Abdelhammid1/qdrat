using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Entities
{
    public class CorporateRegistrationRequest
    {
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string FullName { get; set; }

        [Required, MaxLength(50)]
        public string EmployeeNumber { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        [Required, MaxLength(20)]
        public string NationalID { get; set; }

        [Required, MaxLength(20)]
        public string PhoneNumber { get; set; }

        [MaxLength(150)]
        public string Residence { get; set; }

        [Required, MaxLength(150)]
        public string CompanyName { get; set; } // الشركة الراعية

        public DateTime SubmittedAt { get; set; } = DateTime.Now;


    

        // 🔹 ملاحظات من الأدمن (سبب التراجع أو تفاصيل الاتصال)
        public string? AdminNotes { get; set; }

        // 🔹 وقت آخر تحديث للحالة
        public DateTime? LastUpdated { get; set; }

        // 🔹 اسم الأدمن الذي تعامل مع الطلب
        public string? HandledBy { get; set; }

        public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

    }

}
