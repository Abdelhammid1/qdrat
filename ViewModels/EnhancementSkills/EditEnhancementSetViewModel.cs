using System.ComponentModel.DataAnnotations;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class EditEnhancementSetViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "العنوان مطلوب")]
        [StringLength(200, ErrorMessage = "العنوان لا يجب أن يتجاوز 200 حرف")]
        public string Title { get; set; } = "";

        public string? Description { get; set; }

        [Range(1, 100, ErrorMessage = "عدد الأسئلة يجب أن يكون بين 1 و 100")]
        public int QuestionsPerStudent { get; set; } = 5;

        public DateTime? EndAt { get; set; }
        public DateTime? ScheduledSendAt { get; set; }

        // للعرض فقط
        public string BatchName { get; set; } = "";
        public string LectureTitle { get; set; } = "";
        public bool IsSent { get; set; }
    }
}
