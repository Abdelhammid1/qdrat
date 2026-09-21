using QdratNew.Entities;
using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Users
{
    public class EditUserViewModel
    {
        public string Id { get; set; }

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; }

        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [Display(Name = "اسم المستخدم")]
        public string UserName { get; set; }

        [StringLength(14, MinimumLength = 10, ErrorMessage = "الرقم القومي يجب أن يكون بين 10 إلى 14 رقم")]
        [Display(Name = "الرقم القومي")]
        public string NationalID { get; set; }

        [Phone]
        [Display(Name = "رقم الجوال")]
        public string PhoneNumber { get; set; }

        [Phone]
        [Display(Name = "رقم الواتساب")]
        public string WhatsAppNumber { get; set; }

        [Display(Name = "الجنس")]
        public GenderType Gender { get; set; }

        [Display(Name = "نشط؟")]
        public bool IsActive { get; set; }

        // 🖼️ تحميل صورة الملف الشخصي
        [Display(Name = "الصورة الحالية")]
        public string? ExistingImagePath { get; set; }

        // =========================
        // 🔶 توسعة الإدارة: تفعيل كطالب + بيانات أكاديمية
        // =========================

        [Display(Name = "تفعيل هذا المستخدم كطالب")]
        public bool IsStudent { get; set; }

        [Display(Name = "الفرع")]
        public int? BranchId { get; set; }

        [Display(Name = "ولي الأمر (اختياري)")]
        public int? ParentId { get; set; }

        [Display(Name = "الدفعات المرتبطة")]
        public int[] SelectedBatchIds { get; set; } = System.Array.Empty<int>();

        // قوائم الاختيار (للعرض في الـ View)
        [Display(Name = "الفروع")]
        public IEnumerable<SelectListItem> Branches { get; set; } = new List<SelectListItem>();

        [Display(Name = "الدفعات")]
        public IEnumerable<SelectListItem> Batches { get; set; } = new List<SelectListItem>();

        [Display(Name = "أولياء الأمور")]
        public IEnumerable<SelectListItem> Parents { get; set; } = new List<SelectListItem>();




        // ... باقي الحقول

        // 🔶 أكاديمي:
        [Range(3, 100, ErrorMessage = "العمر يجب أن يكون بين 3 و 100")]
        [Display(Name = "العمر")]
        public int? Age { get; set; } // Nullable في الـVM لسهولة التحقق

        [Display(Name = "المدرسة")]
        public string? School { get; set; }

        [Display(Name = "المستوى")]
        public string? Level { get; set; }





    }
}
