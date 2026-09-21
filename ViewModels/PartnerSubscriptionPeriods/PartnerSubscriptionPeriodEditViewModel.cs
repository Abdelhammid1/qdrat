using System;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.PartnerSubscriptionPeriods
{
    public class PartnerSubscriptionPeriodEditViewModel
    {
        [Required]
        public int Id { get; set; }

        [Required]
        public int SubscriptionId { get; set; }

        public string PartnerName { get; set; }

        [Required(ErrorMessage = "تاريخ البداية مطلوب")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "تاريخ النهاية مطلوب")]
        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }

        public int? MaxStudents { get; set; }
        public int PartnerSubscriptionId { get; internal set; }
        public bool IsActive { get; internal set; }
    }
}
