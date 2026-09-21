using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels
{
  
        public class ResetPasswordByAdminViewModel
        {
            public string UserId { get; set; }

            [Display(Name = "البريد الإلكتروني")]
            public string Email { get; set; }

            [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
            [DataType(DataType.Password)]
            [Display(Name = "كلمة المرور الجديدة")]
            public string NewPassword { get; set; }
        }


}
