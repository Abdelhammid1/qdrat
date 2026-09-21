using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels
{
    public class CorporateRegistrationViewModel
    {
        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "الرقم الوظيفي مطلوب")]
        public string EmployeeNumber { get; set; }

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "بريد إلكتروني غير صالح")]
        public string Email { get; set; }

        [Required(ErrorMessage = "رقم الهوية مطلوب")]
        public string NationalID { get; set; }

        [Required(ErrorMessage = "رقم الجوال مطلوب")]
        public string PhoneNumber { get; set; }

        public string Residence { get; set; }

        [Required(ErrorMessage = "اسم الشركة الراعية مطلوب")]
        public string CompanyName { get; set; }
    }

}
