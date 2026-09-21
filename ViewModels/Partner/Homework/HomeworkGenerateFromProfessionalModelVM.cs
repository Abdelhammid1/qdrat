using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.Partner.Homework
{
    public class HomeworkGenerateFromProfessionalModelVM
    {
        [Required]
        public int ProfessionalModelId { get; set; }

        [Required]
        public int CourseId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public List<SelectListItem> Courses { get; set; } = new();

    }
}
