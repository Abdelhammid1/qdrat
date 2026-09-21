using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Public
{
    public class ContactFormViewModel
    {
        [Required(ErrorMessage = "الاسم مطلوب")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "الاسم يجب أن يكون بين 3 و 100 حرف.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        public string Email { get; set; }

        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "الرسالة مطلوبة")]
        [StringLength(1000, ErrorMessage = "الرسالة طويلة جدًا.")]
        public string Message { get; set; }

        public string? IPAddress { get; set; }
    }
}
