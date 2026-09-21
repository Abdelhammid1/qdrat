using System.ComponentModel.DataAnnotations;

namespace QdratNew.Enums
{
    public enum UserRoleType
    {
        [Display(Name = "مطور النظام")]
        Developer = 1,

        [Display(Name = "شريك تجاري")]
        Partner = 2,

        [Display(Name = "مدير النظام")]
        Admin = 3,

        [Display(Name = "موظف")]
        Employee = 4,

        [Display(Name = "مدير عام")]
        SuperAdmin = 5,

        [Display(Name = "مدرب")]
        Instructor = 6,

        [Display(Name = "مدخل بيانات")]
        DataEntry = 7,

        [Display(Name = "طالب")]
        Student = 8,

        [Display(Name = "مالك النظام")]
        Owner = 9,

        [Display(Name = "غير معروف")]
        
        Unknown = 10
    }
}
