using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Students
{
    public class StudentProfileEditVm
    {
        public string FullName { get; set; } = string.Empty;
        public string? ProfileImagePath { get; set; }
        public DateTime? LastNameChangeDate { get; set; }

        public bool CanChangeName =>
            !LastNameChangeDate.HasValue || LastNameChangeDate.Value.AddDays(7) <= DateTime.Now;

        public DateTime? NextNameChangeAllowedAt =>
            LastNameChangeDate.HasValue ? LastNameChangeDate.Value.AddDays(7) : null;
    }

    public class UpdateDisplayNameRequest
    {
        [Required(ErrorMessage = "الاسم مطلوب")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "الاسم يجب أن يكون بين 2 و 100 حرف")]
        public string FullName { get; set; } = string.Empty;
    }

    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "كلمة المرور الحالية مطلوبة")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور الجديدة مطلوبة")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون 6 أحرف على الأقل")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "تأكيد كلمة المرور مطلوب")]
        [Compare(nameof(NewPassword), ErrorMessage = "كلمتا المرور غير متطابقتين")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
