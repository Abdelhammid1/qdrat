using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels
{
    public class ParentViewModel
    {
        public int ParentID { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد غير صحيحة")]
        public string Email { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "صيغة رقم الهاتف غير صحيحة")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "العلاقة بالطالب مطلوبة")]
        [Display(Name = "العلاقة بالطالب")]
        public string RelationToStudent { get; set; }

        [Required(ErrorMessage = "الرقم القومي مطلوب")]
        [StringLength(14, MinimumLength = 10)]
        public string NationalID { get; set; }

        // يتم تعيينه تلقائيًا في السيرفر
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        // حساب المستخدم المرتبط
        public string? UserId { get; set; }

        public List<SelectListItem> Users { get; set; } = new();
    }
}
