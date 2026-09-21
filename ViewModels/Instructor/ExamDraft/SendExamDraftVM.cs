using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Instructor.Exam;

namespace QdratNew.ViewModels.Instructor.ExamDraft
{
    public class SendExamDraftVM
    {
        public int DraftId { get; set; }

        public List<int> BatchIds { get; set; } = new();

        public List<SelectListItem> Batches { get; set; } = new();

        public DateTime? StartAt { get; set; }

        public DateTime? EndAt { get; set; }
        public List<SelectItemVM> Students { get; internal set; }
        public List<int>? StudentIds { get; internal set; }
        public string ExamTitle { get; internal set; }
        public int DurationMinutes { get; internal set; }
        public string ExamMode { get; internal set; }
    }
}