using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Attendance
{
    public class AttendanceBatchArchiveAccessVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public List<AttendanceArchiveUserAccessItem> Users { get; set; } = new();
    }

    public class AttendanceArchiveUserAccessItem
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool IsAllowed { get; set; }
    }

    public class AttendanceBatchArchiveHistoryVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public List<AttendanceArchiveHistoryItem> Items { get; set; } = new();
    }

    public class AttendanceArchiveHistoryItem
    {
        public DateTime Timestamp { get; set; }
        public string AdminName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
