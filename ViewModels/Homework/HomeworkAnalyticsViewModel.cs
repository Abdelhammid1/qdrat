using QdratNew.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QdratNew.ViewModels.Homework
{
    public class HomeworkAnalyticsViewModel
    {

        public int HomeworkSetId { get; set; }
        public int StudentId { get; set; }
        public string StudentName { get; set; }
        public string BatchName { get; set; }

        // ===============================
        // 📊 الإحصائيات
        // ===============================
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public int WrongAnswers { get; set; }
        public int SkippedAnswers { get; set; }

        public double ScorePercentage { get; set; }
        public double TimeSpentMinutes { get; set; }

        // ===============================
        // 🧠 السلوك
        // ===============================
        public double AvgTimePerQuestion { get; set; }
        public StudentBehaviorLevel BehaviorLevel { get; set; }

        // ===============================
        // 📈 التشارت
        // ===============================
        public List<SectionPerformanceVm> SectionsPerformance { get; set; } = new();

        // ===============================
        // 👨‍👩‍👦 تقرير ولي الأمر
        // ===============================
        public ParentReportResult ParentReport { get; set; }




      
        // =========================
        // الأسئلة
        // =========================
        public List<HomeworkQuestionAnalyticsVm> Questions { get; set; }
            = new List<HomeworkQuestionAnalyticsVm>();

        // =========================
        // إحصائيات مطلوبة للـ View
        // =========================


        public int CorrectCount =>
            Questions?.Count(q => q.IsCorrect) ?? 0;

        public int WrongCount =>
            Questions?.Count(q => !q.IsCorrect && !string.IsNullOrEmpty(q.StudentAnswer)) ?? 0;

        public int SkippedCount =>
            Questions?.Count(q => string.IsNullOrEmpty(q.StudentAnswer)) ?? 0;

        // =========================
        // الأداء حسب الدروس والمحاور
        // (مطلوبة للعرض – حتى لو كانت فارغة)
        // =========================
        public List<LessonPerformanceVm> LessonsPerformance { get; set; }
            = new List<LessonPerformanceVm>();

        public List<HomeworkSectionIndicatorCardVm> SectionIndicatorCards { get; set; }
            = new List<HomeworkSectionIndicatorCardVm>();
   

        // =========================
        // أفضل محور (للعرض)
        // =========================
        public string BestSection { get; set; }

        // =========================
        // التوصية
        // =========================
        public HomeworkRecommendationVm Recommendation { get; set; }
        public List<HomeworkProgressPoint> ProgressTimeline { get; set; } = new();


        public string BehaviorAnalysisText { get; set; }
        public List<string> SectionLabels { get; set; } = new();
        public List<double> SectionScores { get; set; } = new();
        public List<int> ProgressScores { get; set; } = new();
    }

    public class HomeworkProgressPoint
    {
        public string HomeworkTitle { get; set; }
        public double Score { get; set; }
    }

    public class HomeworkSectionIndicatorCardVm
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }
        public double Accuracy { get; set; }
        public List<HomeworkIndicatorCardVm> Indicators { get; set; } = new();
    }

    public class HomeworkIndicatorCardVm
    {
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }
        public double Accuracy { get; set; }
        public double AvgTimeSeconds { get; set; }
        public double HardnessIndex { get; set; }
    }
}
