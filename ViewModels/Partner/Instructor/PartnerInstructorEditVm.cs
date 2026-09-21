using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner.Instructor
{
    public class PartnerInstructorEditVm
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        public string? PhoneNumber { get; set; }

        public string? Specialization { get; set; }

        [Required]
        public GenderType Gender { get; set; }

        public bool IsActive { get; set; }


        [Display(Name = "الحد الأقصى للمدربين")]
        public int? MaxInstructors { get; set; }
    }

}
