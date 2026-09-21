using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Promo
{
    public class PromoReportRequestViewModel
    {
        public int SessionId { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "رقم الجوال مطلوب")]
        [Phone(ErrorMessage = "رقم الجوال غير صالح")]
        public string PhoneNumber { get; set; }

        public string? WhatsAppNumber { get; set; }
    }
}
