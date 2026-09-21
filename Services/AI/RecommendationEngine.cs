using System;
using System.Collections.Generic;
using System.Linq;
using QdratNew.Entities;
using QdratNew.Entities;

namespace QdratNew.Services.AI
{
    public class RecommendationEngine
    {
        public static List<CurriculumRecommendation> GetRecommendedCurriculums(int studentId, List<StudentPerformance> performances, List<Curriculum> curriculums)
        {
            var studentPerformance = performances.Where(sp => sp.StudentID == studentId).ToList();
            if (!studentPerformance.Any())
                return new List<CurriculumRecommendation>(); // إذا لم يكن هناك بيانات للطالب

            double averageScore = studentPerformance.Average(sp => sp.Score);
            var recommendedCurriculums = new List<CurriculumRecommendation>();

            foreach (var curriculum in curriculums)
            {
                double curriculumDifficulty = curriculum.Sections.Count * 10; // مثال لتقييم صعوبة المنهج بناءً على عدد المحاور
                string recommendationLevel = GetRecommendationLevel(averageScore, curriculumDifficulty);

                recommendedCurriculums.Add(new CurriculumRecommendation
                {
                    CurriculumId = curriculum.Id,
                    CurriculumTitle = curriculum.Title,
                    RecommendedLevel = recommendationLevel
                });
            }

            return recommendedCurriculums;
        }

        private static string GetRecommendationLevel(double studentScore, double curriculumDifficulty)
        {
            if (studentScore < 50 && curriculumDifficulty > 70)
                return "❌ صعب جدًا، غير موصى به";
            if (studentScore >= 50 && studentScore < 75 && curriculumDifficulty > 50)
                return "⚠️ متوسط الصعوبة، يتطلب مجهود";
            if (studentScore >= 75 || curriculumDifficulty < 50)
                return "✅ مناسب جدًا، موصى به";

            return "🔍 يتطلب تحليل إضافي";
        }
    }

    public class CurriculumRecommendation
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; }
        public string RecommendedLevel { get; set; }
    }
}
