using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Entities;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Parent
{
    public class ParentFormViewModel
    {
        public int ParentID { get; set; }

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [StringLength(100, ErrorMessage = "الاسم لا يتجاوز 100 حرف")]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "الرقم القومي مطلوب")]
        [StringLength(14, MinimumLength = 10, ErrorMessage = "الرقم القومي بين 10 و 14 رقم")]
        [Display(Name = "الرقم القومي")]
        public string NationalID { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "صيغة البريد غير صحيحة")]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "صيغة رقم الهاتف غير صحيحة")]
        [Display(Name = "رقم الهاتف")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Phone(ErrorMessage = "صيغة رقم الواتساب غير صحيحة")]
        [Display(Name = "رقم الواتساب")]
        public string? WhatsAppNumber { get; set; }

        [Required(ErrorMessage = "صلة القرابة مطلوبة")]
        [Display(Name = "صلة القرابة بالطالب")]
        public ParentRelation RelationToStudent { get; set; }

        [Display(Name = "ربط بحساب مستخدم موجود")]
        public string? UserId { get; set; }

        // IDs of students to link to this parent
        [Display(Name = "الطلاب المرتبطون")]
        public List<int> SelectedStudentIds { get; set; } = new();

        // Dropdowns populated in controller
        public List<SelectListItem> AvailableUsers { get; set; } = new();
        public List<StudentSelectItem> AvailableStudents { get; set; } = new();
    }

    public class StudentSelectItem
    {
        public int StudentID { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string NationalID { get; set; } = string.Empty;
        public string? Level { get; set; }
        public string? School { get; set; }
        public bool IsSelected { get; set; }
    }
}
