using System.Collections.Generic;
using System;

namespace QdratNew.ViewModels.Parents
{
    public class ParentHomeworkFollowUpViewModel
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public int TotalAssigned { get; set; }
        public int Completed { get; set; }
        public int Late { get; set; }
        public int Pending { get; set; }
        public int CommitmentPercent => TotalAssigned > 0
            ? (int)Math.Round(Completed * 100.0 / TotalAssigned)
            : 0;
        public string CommitmentLabel { get; set; } = "جيد";
        public string CommitmentColor { get; set; } = "success";
        public string SafeRecommendation { get; set; } = string.Empty;
        public List<HomeworkItemSummary> RecentHomeworks { get; set; } = new();
    }

    public class HomeworkItemSummary
    {
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "secondary";
        public string? DueDate { get; set; }
    }
}
