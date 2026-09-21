using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Instructor
{
    public class InstructorViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string WhatsAppNumber { get; set; }


        [Required(ErrorMessage = "يجب اختيار الجنس.")]
        public GenderType? Gender { get; set; }
        [ValidateNever]
        public List<SelectListItem> Genders { get; set; }

        public string Specialization { get; set; }
        public bool IsActive { get; set; }
        public string? UserId { get; set; }
        public List<SelectListItem>? Users { get; set; } // لعرض الـ Dropdown

        [Required(ErrorMessage = "رقم الهوية مطلوب")]
        public string? NationalID { get; set; }

        [ValidateNever]
        public List<CurriculumLinkVm> LinkedCurriculums { get; set; } = new();

        [ValidateNever]
        public List<BatchLinkVm> LinkedBatches { get; set; } = new();
    }

    public class CurriculumLinkVm
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; } = string.Empty;
    }

    public class BatchLinkVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
    }
}
