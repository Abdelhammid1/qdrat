using QdratNew.ViewModels.Exam;
using System;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Students
{
    public class PlacementExamDashboardViewModel
    {
        public string StudentName { get; set; } = "";
        public string CurriculumTitle { get; set; } = "";
        public int TotalPlacementExams { get; set; }

        public double InitialScore { get; set; }
        public double LatestScore { get; set; }
        public double Improvement { get; set; }
        
        // ✅ خاصية محسوبة

        // ✅ خط الزمن لتطور الدرجات
        public List<PlacementExamProgressEntry> ProgressTimeline { get; set; } = new();

        // ✅ نقاط القوة
        public List<string> Strengths { get; set; } = new();

        // ✅ نقاط الضعف
        public List<WeaknessAreaVm> Weaknesses { get; set; } = new();

        // ✅ توصية
        public string Recommendation { get; set; } = "";

        // ✅ بيانات آخر اختبار تحديد مستوى
        public List<PlacementExamVm> PlacementExams { get; set; } = new();

    }

    // 🔹 خط الزمن
    public class PlacementExamProgressEntry
    {
        public DateTime ExamDate { get; set; }
        public double Score { get; set; }
    }

    // 🔹 بيانات الاختبار نفسه
    public class PlacementExamVm
    {
        public int ExamAssignmentId { get; set; }   // رقم التكليف (لو موجود)
        public int ExamId { get; set; }             // رقم الامتحان نفسه
        public string Title { get; set; }
        public DateTime AssignedAt { get; set; }
        public bool IsSubmitted { get; set; }
        public bool HasAnyAttempt { get;  set; }

        public bool IsInProgress { get; set; }
        public double Score { get; set; }
    }
}
