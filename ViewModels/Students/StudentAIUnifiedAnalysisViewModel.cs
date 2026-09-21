using System;
using System.Collections.Generic;
using QdratNew.Services.AI;

namespace QdratNew.ViewModels.Students
{
    public class StudentAIUnifiedAnalysisViewModel
    {
        public DateTime AnalyzedAt { get; set; }

        // من StudentAIAnalyzer أو ChartAnalyzer
        public string AssessedLevel { get; set; }
        public float RiskScore { get; set; }
        public List<string> Recommendations { get; set; } = new();

        // من StudentPerformanceReport
        public double SuccessRate { get; set; }
        public double AverageScore { get; set; }
        public List<string> WeakTopics { get; set; } = new();
        public List<SectionPerformanceSummary> SectionSummaries { get; set; } = new();

        // من StudentActivityAIAnalyzer
        public List<string> ActivityInsights { get; set; } = new();

        // من StudentAIAnalysisService (ML.NET)
        public string PredictedPerformanceComment { get; set; }


        public float AverageBatchSuccessRate { get; set; }
        public float AverageBatchRiskScore { get; set; }
        public float AverageBatchHomeworkTime { get; set; }
        public float TargetHomeworkTime { get; set; } = 600f; // 10 دقائق

        public float AverageHomeworkTime { get; set; }

        public double AttendanceRate { get; set; } // نسبة الحضور المئوية
        public int TotalLectures { get; set; }     // عدد المحاضرات
        public int AttendedLectures { get; set; }  // عدد المحاضرات التي حضرها الطالب


        public string? LastLectureTitle { get; set; }
        public DateTime? LastLectureDate { get; set; }






    }
}
