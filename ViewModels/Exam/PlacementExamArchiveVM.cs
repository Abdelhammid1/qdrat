using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Exam
{
    // ViewModel لصفحة Index بعد إضافة الأرشفة
    public class PlacementExamsIndexVM
    {
        public List<PlacementBatchExamVm> ActiveBatches { get; set; } = new();
        public int ArchivedBatchCount { get; set; }
        public List<SelectListItem> AllBatchesForArchive { get; set; } = new();
    }

    // بطاقة دفعة مؤرشفة في صفحة الأرشيف
    public class PlacementExamArchivedBatchVm
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public int ExamsCount { get; set; }
        public DateTime? LastExamDate { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string ArchivedByUserName { get; set; } = string.Empty;
    }

    // ViewModel لصفحة الأرشيف الرئيسية
    public class PlacementExamArchivedIndexVM
    {
        public int TotalArchivedBatches { get; set; }
        public int TotalExams { get; set; }
        public List<PlacementExamArchivedBatchVm> BatchCards { get; set; } = new();
    }

    // ViewModel لإدارة صلاحيات الأرشيف (Owner/Developer فقط)
    public class PlacementExamBatchArchiveAccessVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public List<PlacementExamArchiveUserAccessItem> Users { get; set; } = new();
    }

    public class PlacementExamArchiveUserAccessItem
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Roles { get; set; } = string.Empty;
        public bool IsAllowed { get; set; }
    }

    // ViewModel لسجل تاريخ الأرشفة
    public class PlacementExamBatchArchiveHistoryVM
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = string.Empty;
        public List<PlacementExamArchiveHistoryItem> Items { get; set; } = new();
    }

    public class PlacementExamArchiveHistoryItem
    {
        public DateTime Timestamp { get; set; }
        public string AdminName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
