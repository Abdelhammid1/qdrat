using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    public class ExamArchiveBatchAccessVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public List<ExamArchiveUserAccessItem> Users { get; set; } = new();
    }

    public class ExamArchiveUserAccessItem
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool IsAllowed { get; set; }
    }
}
