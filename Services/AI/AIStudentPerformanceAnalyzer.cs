using QdratNew.Entities;
using QdratNew.ViewModels.Students;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QdratNew.Services.AI
{
    public static class AIStudentPerformanceAnalyzer
    {
        public static StudentPerformanceReport Analyze(List<StudentPerformance> performances)
        {
            if (performances == null || performances.Count == 0)
            {
                return new StudentPerformanceReport
                {
                    Message = "لا توجد بيانات كافية لتحليل الأداء."
                };
            }

            var avgScore = performances.Average(p => p.Score);

            var weakTopics = performances
                .Where(p => p.Score < 60 && p.Section != null)
                .Select(p => p.Section.Title)
                .Distinct()
                .ToList();

            // ✅ أولًا: تحليل المحاور التعليمية
            var sectionGroups = performances
                .Where(p => p.Section != null)
                .GroupBy(p => p.Section.Title)
                .Select(g => new SectionPerformanceSummary
                {
                    SectionTitle = g.Key,
                    AverageScore = g.Average(p => p.Score),
                    SuccessRate = g.Count(p => p.Score >= 60) * 100.0 / g.Count(),
                    DifficultyLevel = g.Average(p => p.Score) < 50 ? "صعب" :
                                      g.Average(p => p.Score) < 70 ? "متوسط" : "سهل"
                })
                .ToList();

            // ✅ ثم نستخدمه هنا
            var report = new StudentPerformanceReport
            {
                AverageScore = avgScore,
                SuccessRate = performances.Count(p => p.Score >= 60) * 100.0 / performances.Count(),
                WeakTopics = weakTopics,
                Recommendations = new List<string>
        {
            GenerateRecommendation(avgScore, weakTopics)
        },
                SectionSummaries = sectionGroups
            };

            return report;
        }


        private static string GenerateRecommendation(double avgScore, List<string> weakTopics)
        {
            if (avgScore >= 85)
                return "أداء ممتاز! استمر بنفس الجهد.";

            if (avgScore >= 70)
                return "أداء جيد جدًا، ولكن هناك مجال لتحسين بعض المهارات.";

            if (avgScore >= 50)
                return $"الأداء مقبول. ننصح بالتركيز على المواضيع الضعيفة: {string.Join(", ", weakTopics)}";

            return $"الأداء ضعيف. يفضل البدء بخطة علاجية تشمل: {string.Join(", ", weakTopics)}";
        }
    }

    public class StudentPerformanceReport
    {
        public double AverageScore { get; set; }
        public double SuccessRate { get; set; }
        public List<string> WeakTopics { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public List<SectionPerformanceSummary> SectionSummaries { get; set; } = new List<SectionPerformanceSummary>();
        public string Message { get; set; } = "";
    }


}
