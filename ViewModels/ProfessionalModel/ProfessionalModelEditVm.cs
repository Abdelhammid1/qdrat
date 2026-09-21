using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Enums;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.ProfessionalModel
{
    public class ProfessionalModelEditVm
    {
        [Required(ErrorMessage = "كود النموذج مطلوب")]
        public string Title { get; set; }

        [Required(ErrorMessage = "الوصف مطلوب")]
        public string Description { get; set; }

        public int? CurriculumId { get; set; }

        [Required(ErrorMessage = "نوع النموذج مطلوب")]
        public ProfessionalModelType ModelType { get; set; } = ProfessionalModelType.Exam;

        public List<SelectListItem> Curriculums { get; set; } = new();
    }
}
