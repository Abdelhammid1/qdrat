using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkArchiveAccessViewModel
    {
        public int HomeworkSetId { get; set; }
        public string HomeworkTitle { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public List<HomeworkArchiveUserAccessItem> Users { get; set; } = new();
    }

    public class HomeworkArchiveUserAccessItem
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool IsAllowed { get; set; }
    }

    public class HomeworkBatchArchiveAccessViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public List<HomeworkArchiveUserAccessItem> Users { get; set; } = new();
    }

    public class HomeworkArchiveHistoryViewModel
    {
        public int HomeworkSetId { get; set; }
        public string HomeworkTitle { get; set; } = string.Empty;
        public string BatchName { get; set; } = string.Empty;
        public List<HomeworkArchiveHistoryItem> Items { get; set; } = new();
    }

    public class HomeworkArchiveHistoryItem
    {
        public DateTime Timestamp { get; set; }
        public string AdminName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
