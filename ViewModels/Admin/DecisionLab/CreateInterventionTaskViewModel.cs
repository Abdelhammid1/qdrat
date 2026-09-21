using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Admin.DecisionLab
{
    public class CreateInterventionTaskViewModel
    {
        [Required]
        public int DecisionRecommendationId { get; set; }

        [Required]
        public int BatchId { get; set; }

        public int? CourseId { get; set; }
        public int? CurriculumId { get; set; }
        public int? InstructorId { get; set; }
        public int? LectureId { get; set; }
        public int? RecommendedNextLectureId { get; set; }
        public DateTime? DueDate { get; set; }

        public string BatchName { get; set; } = string.Empty;
        public string CourseName { get; set; } = string.Empty;
        public string CurriculumTitle { get; set; } = string.Empty;
        public string RecommendationType { get; set; } = string.Empty;
        public string RecommendationSummary { get; set; } = string.Empty;
        public string RecommendationReason { get; set; } = string.Empty;
        public string RecommendationStatus { get; set; } = string.Empty;

        [Required(ErrorMessage = "عنوان المهمة مطلوب")]
        [StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "نوع المهمة مطلوب")]
        public string TaskType { get; set; } = "InstructorReExplanation";

        [Required(ErrorMessage = "طريقة التنفيذ مطلوبة")]
        public string DeliveryMode { get; set; } = "InLecture";

        [Required(ErrorMessage = "الجهة المستهدفة مطلوبة")]
        public string TargetType { get; set; } = "WholeBatch";

        [Required(ErrorMessage = "الأولوية مطلوبة")]
        public string Priority { get; set; } = "Normal";

        [Required(ErrorMessage = "تعليمات المالك مطلوبة")]
        [StringLength(4000)]
        public string OwnerInstructions { get; set; } = string.Empty;

        public string? TargetLessonsJson { get; set; }
        public string? TargetQuestionsJson { get; set; }
        public string? BeforeSnapshotJson { get; set; }

        public IReadOnlyList<SelectListItem> TaskTypeOptions { get; set; } = new List<SelectListItem>();
        public IReadOnlyList<SelectListItem> DeliveryModeOptions { get; set; } = new List<SelectListItem>();
        public IReadOnlyList<SelectListItem> TargetTypeOptions { get; set; } = new List<SelectListItem>();
        public IReadOnlyList<SelectListItem> PriorityOptions { get; set; } = new List<SelectListItem>();
        public IReadOnlyList<SelectListItem> InstructorOptions { get; set; } = new List<SelectListItem>();
        public IReadOnlyList<SelectListItem> LectureOptions { get; set; } = new List<SelectListItem>();
        public IReadOnlyList<InterventionTaskLectureMapItemViewModel> LectureMap { get; set; } =
            new List<InterventionTaskLectureMapItemViewModel>();
    }
}
