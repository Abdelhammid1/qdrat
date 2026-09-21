using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.PartnerSubscriptionPeriods
{
    public class PartnerSubscriptionPeriodCreateViewModel
    {
        // 🔹 معرف العقد (الوحيد)
        [Required]
        public int PartnerSubscriptionId { get; set; }

        // 🔹 للعرض فقط
        public string PartnerName { get; set; }

        // 🔹 تواريخ الفترة
        [Required(ErrorMessage = "تاريخ البداية مطلوب")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        // 🔹 حد الطلاب في هذه الفترة
        [Range(1, int.MaxValue, ErrorMessage = "العدد يجب أن يكون أكبر من صفر")]
        public int? MaxStudents { get; set; }
    }
}
