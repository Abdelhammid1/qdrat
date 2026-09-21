using QdratNew.Entities;
using QdratNew.ViewModels.Students;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QdratNew.Services.AI
{
    public static class AIStudentRemedialPlanGenerator
    {
        public static List<RemedialRecommendation> Generate(List<StudentPerformance> performances)
        {
            var recommendations = new List<RemedialRecommendation>();

            if (performances == null || !performances.Any())
            {
                recommendations.Add(new RemedialRecommendation
                {
                    SectionTitle = "لا توجد بيانات أداء كافية.",
                    RecommendationText = "الرجاء إضافة اختبارات أو تقييمات للطالب أولاً.",
                    SuggestedDays = 0 // ⏳ لتفادي خطأ في العرض لاحقًا
                });

                return recommendations;
            }


            // 🔍 تحليل المحاور ذات الأداء الضعيف
            var weakSections = performances
                .Where(p => p.Score < 60 && p.Section != null)
                .GroupBy(p => p.Section.Title)
                .Select(g => new
                {
                    SectionTitle = g.Key,
                    AverageScore = g.Average(p => p.Score),
                    Attempts = g.Count()
                })
                .OrderBy(s => s.AverageScore)
                .ToList();

            foreach (var section in weakSections)
            {
                recommendations.Add(new RemedialRecommendation
                {
                    SectionTitle = section.SectionTitle,
                    RecommendationText = $"الطالب يحتاج لمراجعة هذا المحور حيث أن متوسط الدرجة هو {Math.Round(section.AverageScore, 1)} بعد {section.Attempts} محاولة. ننصح بمراجعة الشرح أو التدرب على تمارين إضافية."
                });
            }

            if (!recommendations.Any())
            {
                recommendations.Add(new RemedialRecommendation
                {
                    SectionTitle = "ممتاز!",
                    RecommendationText = "الطالب ليس لديه محاور ضعيفة واضحة حالياً."
                });
            }

            return recommendations;
        }
    }

    public class RemedialRecommendation
    {
        public int SectionId { get; set; }                    // ✅ معرف المحور

        public string SectionTitle { get; set; }
        public string RecommendationText { get; set; }
        // ✅ أضف هذا السطر الجديد:
        public int SuggestedDays { get; set; }
    }
}
