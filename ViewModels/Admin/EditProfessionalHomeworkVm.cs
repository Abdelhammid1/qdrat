using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Admin
{
    public class EditProfessionalHomeworkVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string? Description { get; set; }
        public DateTime? DueDate { get; set; }

        public int DurationMinutes { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        // فلتر المنهج + النموذج
        public int? CurriculumId { get; set; }
        public int? ProfessionalModelId { get; set; }

        [ValidateNever]
        public List<SelectListItem> Curriculums { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> ProfessionalModels { get; set; } = new();
    }

    public class EditProfessionalExamVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public int TotalQuestions { get; set; }
        public int DurationMinutes { get; set; }

        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        public bool IsOnline { get; set; }
        public bool IsInLab { get; set; }
        public string? ReferenceCode { get; set; }
        public bool RandomizeQuestions { get; set; } = true;

        // فلتر المنهج + النموذج
        public int? CurriculumId { get; set; }
        public int? ProfessionalModelId { get; set; }

        [ValidateNever]
        public List<SelectListItem> Curriculums { get; set; } = new();

        [ValidateNever]
        public List<SelectListItem> ProfessionalModels { get; set; } = new();
    }
}
