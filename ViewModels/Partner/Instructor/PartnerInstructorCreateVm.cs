using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner.Instructor
{
    public class PartnerInstructorCreateVm
    {
        [Required]
        public string FullName { get; set; }

        [Required]
        public string NationalID { get; set; }

        [Required, EmailAddress]
        public string Email { get; set; }

        public string? PhoneNumber { get; set; }

        [Required]
        public GenderType Gender { get; set; }

        public string? Specialization { get; set; }

        [Required]
        public string Password { get; set; }



        [Required]
        [Display(Name = "الحد الأقصى للمدربين")]
        public int MaxInstructors { get; set; }


    }

}
