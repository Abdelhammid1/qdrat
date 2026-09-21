using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class IndicatorInputItem
    {
        public int LessonId { get; set; }
        public int QuestionCount { get; set; }
    }

    public class CreateEnhancementSetViewModel
    {
        public string Title { get; set; } = "مهارات تعزيزية";
        public string? Description { get; set; }
        public int BatchId { get; set; }
        public int? LectureId { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime? ScheduledSendAt { get; set; }

        // 1 = محاور/مؤشرات، 2 = نموذج احترافي
        public int GenerationMethod { get; set; } = 1;

        // الطريقة 1
        [ValidateNever]
        public List<IndicatorInputItem> SelectedIndicators { get; set; } = new List<IndicatorInputItem>();

        // الطريقة 2
        public int? ProfessionalModelId { get; set; }
        public int QuestionsPerStudent { get; set; } = 10;

        [ValidateNever]
        public List<SelectListItem> Batches { get; set; } = new List<SelectListItem>();

        [ValidateNever]
        public List<SelectListItem> Lectures { get; set; } = new List<SelectListItem>();

        [ValidateNever]
        public List<SelectListItem> ProfessionalModels { get; set; } = new List<SelectListItem>();
    }
}
