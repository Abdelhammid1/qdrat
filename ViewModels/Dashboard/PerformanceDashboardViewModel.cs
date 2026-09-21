using QdratNew.ViewModels.Students;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Dashboard
{
    public class PerformanceDashboardViewModel
    {
        public int TotalExams { get; set; }
        public int TotalBatches { get; set; }
        public int TotalStudents { get; set; }
        public int StudentsWithRemedial { get; set; }
        public double AverageScore { get; set; }

        // تحليل المحاور
        public List<SectionPerformanceItem> WeakSections { get; set; } = new();

        public List<BatchPerformanceItem> ActiveBatches { get; set; } = new();

        public int TotalExamsThisWeek { get; set; }
   
        public double SuccessRate { get; set; }
        public List<ExamSummaryItem> ExamsThisWeek { get; set; } = new();
        public StudentRankViewModel? StudentRank { get; internal set; }
    }

    public class ExamSummaryItem
    {
        public int ExamId { get; set; }
        public string BatchName { get; set; }
        public string CurriculumTitle { get; set; }
        public string ReferenceCode { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class SectionPerformanceItem
    {
        public string SectionTitle { get; set; }
        public double AverageScore { get; set; }
        public int FailedStudents { get; set; }

    }

    public class BatchPerformanceItem
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public string ExamTitle { get; set; }
        public string CurriculumTitle { get; set; }
        public int StudentsCount { get; set; }
        public int FailedCount { get; set; }
        public DateTime CreatedAt { get; set; }

        public List<BatchPerformanceItem> ActiveBatches { get; set; } = new();
        public int ExamId { get; set; }          // ✅ أضف هذا السطر
    }




}
