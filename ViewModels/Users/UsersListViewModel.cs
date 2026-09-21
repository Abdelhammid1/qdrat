using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Users
{
    public class UsersListViewModel
    {
        public string Title { get; set; } = "قائمة المستخدمين";
        public string? FilterDescription { get; set; }
        public List<UserRowDto> Users { get; set; } = new List<UserRowDto>();

        // 🧮 الإحصائيات
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int StudentsCount { get; set; }

        // 📦 الدفعات المؤرشفة
        public List<ActiveBatchFilterDto> ActiveBatches { get; set; } = new();
        public List<ArchivedBatchSummaryDto> ArchivedBatches { get; set; } = new();
        public int ArchivedStudentsCount { get; set; }
    }

    public class ActiveBatchFilterDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class ArchivedBatchSummaryDto
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; } = "";
        public string? CourseName { get; set; }
        public string? BranchName { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public int StudentsCount { get; set; }
    }
}
