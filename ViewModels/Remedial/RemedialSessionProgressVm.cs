using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Remedial
{
    public class RemedialSessionProgressVm
    {
        public int SessionId { get; set; }

        public string StudentName { get; set; }
        public string PlanTitle { get; set; }

        public double AverageWatchMinutes { get; set; }
        public int CompletedVideos { get; set; }

        public int TotalQuizzes { get; set; }
        public double AverageQuizScore { get; set; }

        public DateTime? CreatedAt { get; set; }
        public bool IsCompleted { get; set; }

        // 🔹 تحليل تفصيلي لكل فيديو داخل الجلسة
        public List<RemedialVideoProgressItem> VideoProgressList { get; set; } = new();
    }

    public class RemedialVideoProgressItem
    {
        public string VideoTitle { get; set; }
        public double WatchedMinutes { get; set; }
        public bool IsCompleted { get; set; }
        public double QuizScore { get; set; }
    }
}
