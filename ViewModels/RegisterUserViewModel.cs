using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels
{

    public class RegisterUserViewModel
    {
        public string FullName { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string NationalID { get; set; }
        public string Password { get; set; }

        [Required]
        public string Role { get; set; }

        // خصائص للطالب فقط
        public string? Gender { get; set; }
        public int? Age { get; set; }
        public string? School { get; set; }
        public string? Level { get; set; }
        public int? BranchId { get; set; }
        public int? BatchId { get; set; }
        // 🔹 القوائم المنسدلة:
        public List<SelectListItem> Branches { get; set; } = new();
        public List<SelectListItem> Batches { get; set; } = new();

        // خصائص لولي الأمر فقط
        [Phone]
        public string? PhoneNumber { get; set; }

        [Phone]
        public string? WhatsAppNumber { get; set; }

        public string? RelationToStudent { get; set; }

        // خصائص مشتركة (مثل UserId أو تاريخ الإضافة يمكن تعيينها بالسيرفر)

        public string? UserId { get; set; }

    }


}
