using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using QdratNew.Enums;

namespace QdratNew.ViewModels.Frontend.Register
{
    public class RegisterPageVM
    {
        public List<RegisterProgramCardVM> Programs { get; set; } = new();
        public RegisterLeadFormVM Form { get; set; } = new();
    }

    public class RegisterProgramCardVM
    {
        public int ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Icon { get; set; } = "fa-graduation-cap";
        public string AccentColor { get; set; } = "#1B5EAE";
        public List<RegisterCourseItemVM> Courses { get; set; } = new();
    }

    public class RegisterCourseItemVM
    {
        public int CourseId { get; set; }
        public int ProjectId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? StartDate { get; set; }
    }

    public class RegisterLeadFormVM
    {
        [Required(ErrorMessage = "من فضلك اختر صفة المُسجِّل")]
        public LeadApplicantType ApplicantType { get; set; } = LeadApplicantType.Student;

        [Required(ErrorMessage = "اسم الطالب مطلوب")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "اكتب الاسم بشكل صحيح")]
        public string StudentName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الجوال مطلوب")]
        [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم الجوال يجب أن يبدأ بـ 05 ويتكون من 10 أرقام")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(150)]
        public string? ParentName { get; set; }

        [RegularExpression(@"^05\d{8}$", ErrorMessage = "رقم جوال ولي الأمر يجب أن يبدأ بـ 05 ويتكون من 10 أرقام")]
        public string? ParentPhone { get; set; }

        public GenderType? Gender { get; set; }

        [StringLength(50)]
        public string? SchoolStage { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public List<int> SelectedCourseIds { get; set; } = new();

        // Honeypot — يجب أن يبقى فارغًا
        public string? Website { get; set; }
    }
}
