// ===========================
// ✅ Namespace: ViewModels/Students/Dashboard/StudentMainDashboardVm.cs
// ===========================
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class StudentMainDashboardVm
    {
        // 🔹 الكروت الأساسية (اختبارات)
        public int TotalExams { get; set; }
        public int CompletedExams { get; set; }
        public int PendingExams { get; set; }
        public int OverdueExams { get; set; }
        public int ExpiredExams { get; set; }
        public int ExpiringSoonExams { get; set; }
        public double BestExamScore { get; set; }
        public double SuccessRateExams { get; set; }

        // 🔹 كروت الواجبات
        public int TotalHomeworks { get; set; }
        public int CompletedHomeworks { get; set; }
        public int PendingHomeworks { get; set; }
        public int OverdueHomeworks { get; set; }
        public int ExpiredHomeworks { get; set; }
        public double BestHomeworkScore { get; set; }
        public double SuccessRateHomeworks { get; set; }

        // 🔹 الرسوم البيانية
        public List<StudentHomeworkChartVm> HomeworkProgress { get; set; } = new();
        public List<StudentExamChartVm> ExamProgress { get; set; } = new();

        // 🔹 المقارنات مع الدفعة
        public List<StudentBatchComparisonVm> HomeworkComparison { get; set; } = new();
        public List<StudentBatchComparisonVm> ExamComparison { get; set; } = new();

        // 🔹 الكروت الذكية
        public string WeakestSectionTitle { get; set; } = "لا يوجد بيانات";
        public double WeakestSectionScore { get; set; }
        public double OverallProgress { get; set; }

        // 🔹 ترتيب الطالب
        public StudentRankViewModel StudentRank { get; set; }
        public double ExamCompletionRate { get; internal set; }
        public double HomeworkCompletionRate { get; internal set; }
        public int LateHomeworks { get; internal set; }
        public int LateExams { get; internal set; }
    }

    public class StudentHomeworkChartVm
    {
        public DateTime Date { get; set; }
        public double Score { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Skipped { get; set; }
        public string Title { get; set; }
    }

    public class StudentExamChartVm
    {
        public DateTime Date { get; set; }
        public double Score { get; set; }
        public string Title { get; set; }
        public int Correct { get; set; }
        public int Wrong { get; set; }
        public int Skipped { get; set; }
        public int TotalQuestions => Correct + Wrong + Skipped;
    }

    public class StudentBatchComparisonVm
    {
        public string Title { get; set; }
        public double StudentScore { get; set; }
        public double BatchAverage { get; set; }
        public DateTime Date { get; set; }
        public double Score { get; set; }
    }
}
