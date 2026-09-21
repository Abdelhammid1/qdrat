using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Users
{
    public class UserAccountDropdownViewModel
    {
        // البريد الإلكتروني
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; }

        // الاسم الكامل
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; }

        // رابط لتعديل الاسم (اختياري)
        public string EditNameUrl { get; set; }

        // قائمة الأدوار التي يمتلكها المستخدم
        [Display(Name = "الأدوار")]
        public List<string> Roles { get; set; } = new();

        // الدور النشط حاليًا (لتحديد العرض والتصفح)
        public string SelectedRole { get; set; }

        // هل الدور الحالي هو طالب؟
        public bool IsStudent => SelectedRole?.ToLower() == "student";

        // قائمة الدورات المرتبطة بالمستخدم في حال كان طالبًا
        [Display(Name = "الدورات")]
        public List<UserCourseOption> Courses { get; set; } = new();

        // الدورة المختارة حاليًا (لو طالب)
        public int? SelectedCourseId { get; set; }

        // رابط تسجيل الخروج
        public string LogoutUrl { get; set; }

        public string ProfileImageUrl { get; set; }
        public string ProfileImageUploadUrl { get; set; } // رابط رفع صورة
        public string ChangeFullNameUrl { get; set; } // رابط تعديل الاسم


    }

    public class UserCourseOption
    {
        public int Id { get; set; }

        [Display(Name = "اسم الدورة")]
        public string Title { get; set; }
    }
}
