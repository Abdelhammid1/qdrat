using System;
using System.Collections.Generic;
using System.Linq;

namespace QdratNew.ViewModels.Reports
{
    public class PlacementBatchReportViewModel
    {
        public string BatchName { get; set; }
        public string CourseTitle { get; set; }
        public int TotalStudents { get; set; }
        public int CompletedCount { get; set; }
        public double AverageScore { get; set; }
        public double AverageTimeMinutes { get; set; }
        public double EngagementIndex { get; set; }
        public List<CurriculumPerformanceVm> CurriculumReports { get; set; } = new();
        public List<StudentPerformanceVm> StudentReports { get; set; } = new();


        // 🧭 تفاصيل الاختبار
        public string ExamTitle { get; set; }
        public DateTime? ExamDate { get; set; }

        public double BatchAverage { get; set; }   // ✅ لدعم الكود القديم


        public double HighestScore { get; set; }
        public double LowestScore { get; set; }

       
        // 🔹 رسوم بيانية كمية ولفظية (في حال تم تحليلها)
        public List<string> QuantLabels { get; set; } = new();
        public List<double> QuantScores { get; set; } = new();
        public List<string> VerbalLabels { get; set; } = new();
        public List<double> VerbalScores { get; set; } = new();

        // 🔹 توصيات للإدارة
        public string RecommendationText { get; set; }
        public string AdminDecisionTip { get; set; }


        public List<StudentPerformanceVm> StudentResults { get; set; } = new(); // ✅ دعم للكود القديم أيضًا



    }

    public class CurriculumPerformanceVm
    {
        public string CurriculumTitle { get; set; }
        public List<SectionPerformanceDetailedVm> Sections { get; set; } = new();
     
        public double AverageScore { get; set; }
        public double AverageTime { get; set; }
        public double EngagementIndex { get; set; }
        public string StartingLevel { get; set; }

    
    }

    public class SectionPerformanceDetailedVm
    {
        public string SectionTitle { get; set; }
        public List<LessonPerformancesVm> Lessons { get; set; } = new();
        public int Total { get; internal set; }
        public int Correct { get; internal set; }
        public int Wrong { get; internal set; }
        public int Skipped { get; internal set; }
    }
}
