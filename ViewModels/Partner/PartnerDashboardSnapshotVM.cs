using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Partner
{
    public class PartnerDashboardSnapshotVM
    {
        // =====================
        // 🧱 KPIs
        // =====================
        public int TotalStudents { get; set; }
        public int ActiveBatches { get; set; }

        public int TotalSentExams { get; set; }
        public int TotalSolvedExams { get; set; }
        public int TotalUnsolvedExams { get; set; }

        public double AverageScorePercent { get; set; }

        // =====================
        // 📊 Charts (مرحلة لاحقة)
        // =====================
        public List<BatchPerformanceItem> BatchPerformances { get; set; } = new();

        // =====================
        // ⏱ Meta
        // =====================
        public DateTime GeneratedAt { get; set; }
    }

    public class BatchPerformanceItem
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public double AverageScore { get; set; }
        public int StudentsCount { get; set; }
    }
}
