using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Partner.HomeworkDraft
{
    public class SendHomeworkDraftVM
    {
        public int DraftId { get; set; }

        public List<int> BatchIds { get; set; } = new();

        public int? LectureId { get; set; }

        public DateTime? StartAt { get; set; }
        public DateTime EndAt { get; set; }

        public List<SelectListItem> Batches { get; set; } = new();
        public List<SelectListItem> Lectures { get; set; } = new();

        // lecture.Date per lecture ID — used by JS to auto-fill StartAt
        public Dictionary<int, string> LectureDates { get; set; } = new();

        public string? Title { get; internal set; }
        public List<SelectListItem>? Courses { get; internal set; }
    }

}
