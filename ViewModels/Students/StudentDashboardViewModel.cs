using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class StudentDashboardViewModel
    {

        public string FullName { get; set; }
        public string Level { get; set; }
        public DateTime LastLogin { get; set; }

        // الإحصائيات العامة
        public double AverageScore { get; set; }
        public int TotalHomeworks { get; set; }
        public int TotalExams { get; set; }

        // 📊 أفضل نتيجة اختبار
        public string BestExamTitle { get; set; }
        public int? BestExamScore { get; set; }
        public DateTime? BestExamDate { get; set; }

        // 📉 أقل نتيجة اختبار
        public string WorstExamTitle { get; set; }
        public int? WorstExamScore { get; set; }
        public DateTime? WorstExamDate { get; set; }
        public int ExamSuccessRate { get; set; }
        public int HomeworkSuccessRate { get; set; }

        // 📘 نسبة إتمام الواجبات
        public int HomeworkCompletionPercentage { get; set; }

        // 📅 الواجب القادم
        public string NextHomeworkTitle { get; set; }
        public DateTime? NextHomeworkDeadline { get; set; }

        // 🏆 معدل النجاح العام
        public int OverallSuccessRate { get; set; }

        // ⚠️ المحاور الضعيفة
        public int WeakSectionsCount { get; set; }
        public string FirstWeakSectionTitle { get; set; }

        // الأداء العام
        public double AverageHomeworkScore { get; set; }
        public double AverageExamScore { get; set; }

    
        // آخر اختبار
        public string? LastExamTitle { get; set; }
        public DateTime? LastExamDate { get; set; }
        public int? LastExamScore { get; set; }

        // تقدم الأداء الزمني
        public List<ExamPerformanceEntry> ExamProgress { get; set; } = new();
        public List<HomeworkPerformanceEntry> HomeworkProgress { get; set; } = new();
        public List<ComparisonPerformanceEntry> ComparisonPerformance { get; set; } = new();

        // تحليل الضعف
        public List<WeakSectionEntry> WeakSections { get; set; } = new();

        // أحدث المهام
        public List<RecentTaskViewModel> RecentHomeworks { get; set; } = new();
        public List<RecentTaskViewModel> RecentExams { get; set; } = new();
        public List<RecentExamResult> RecentResults { get; set; } = new();

        // الرسائل والتحفيز
        public string SmartStatusMessage { get; set; }
        public List<string> WeeklyObjectives { get; set; } = new();
        public List<string> Achievements { get; set; } = new();
        public List<string> Notifications { get; set; } = new();

        // مقترحات ذكية
        public List<SuggestedLessonViewModel> SuggestedLessons { get; set; } = new();
  
        // الحضور
        public AttendanceChartData AttendanceChart { get; set; } = new();


       

        public string LastExamType { get; set; } // جديد لمعرفة نوع الاختبار (محور/منهج كامل)

        public string LastHomeworkTitle { get; set; }
        public DateTime? LastHomeworkDate { get; set; }

  

        // 📊 تفاصيل الاختبارات
        public int CompletedExamsCount { get; set; }
        public int PendingExamsCount { get; set; }

        // 📘 تفاصيل الواجبات
        public int CompletedHomeworksCount { get; set; }
        public int PendingHomeworksCount { get; set; }

        public List<HomeworkComparisonEntry> HomeworkComparisons { get; set; }
        public List<ExamComparisonEntry> ExamComparisons { get; set; }



        public int TotalAssigned { get; set; }
        public int Completed { get; set; }
        public int Pending { get; set; }
        public int Late { get; set; }

        // للرادار شارت
        public List<string> SectionLabels { get; set; } = new();
        public List<double> StudentScores { get; set; } = new();
        public List<double> BatchAverages { get; set; } = new();


        public int? BestExamAssignmentId { get; set; }
        public int? WorstExamAssignmentId { get; set; }

        public int ActiveHomeworksCount { get; set; }
        public int UpcomingHomeworksCount { get; set; }
        public int OverdueHomeworksCount { get; set; }


    }

    public class ExamPerformanceEntry
    {
        public string ExamTitle { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswersCount { get; set; }

    }

    public class HomeworkPerformanceEntry
    {
        public string LessonTitle { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswersCount { get; set; }

    }


    public class WeakSectionEntry
    {
        public string SectionTitle { get; set; }
        public int AverageScore { get; set; }
    }

    public class RecentExamResult
    {
        public string ExamTitle { get; set; }
        public DateTime Date { get; set; }
        public int Score { get; set; }
        public int? ExamAssignmentId { get; set; }
    }

    public class RecentTaskViewModel
    {
        public string Title { get; set; }
        public string SectionName { get; set; }
        public double Score { get; set; }
        public DateTime Date { get; set; }
        public string DetailsUrl { get; set; }
    }

    public class AttendanceChartData
    {
        public int Present { get; set; }
        public int Absent { get; set; }
    
    }

}
