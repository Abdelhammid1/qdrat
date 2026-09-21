using System;

namespace QdratNew.ViewModels.Remedial
{
    public class RemedialSessionSummaryVm
    {
        public int Id { get; set; }
        public string StudentName { get; set; }
        public string PlanTitle { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool IsConfirmed { get; set; }
        public bool IsCompleted { get; set; }

        public double WatchedMinutes { get; set; }
        public double AverageScore { get; set; }
        public int StudentId { get; set; }
        public int? SectionId { get; set; } // يمكن أن تكون Null لو لم تكن الجلسة مرتبطة بمحور محدد
        public int RemedialPlanId { get; set; }

        // 🟢 حالة ذكية للجلسة
        public string Status =>
            IsCompleted ? "منتهية" :
            IsConfirmed ? "قيد التنفيذ" : "في انتظار البدء";
    }
}
