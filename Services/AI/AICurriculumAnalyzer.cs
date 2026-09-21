using System.Collections.Generic;
using System.Linq;
using QdratNew.Entities;
using QdratNew.Entities;

namespace QdratNew.Services.AI
{
    public class AICurriculumAnalyzer
    {
        // ✅ تأكد أن هذه الدالة تستقبل `Curriculum` و `List<StudentPerformance>`
        public static CurriculumAnalysisReport AnalyzeCurriculum(Curriculum curriculum, List<StudentPerformance> studentPerformances)
        {
            var report = new CurriculumAnalysisReport
            {
                CurriculumId = curriculum.Id,
                CurriculumTitle = curriculum.Title,
                TotalStudents = studentPerformances.Count
            };

            if (studentPerformances.Any())
            {
                report.AverageScore = studentPerformances.Average(sp => sp.Score);
                report.DifficultyLevel = report.AverageScore < 60 ? "صعب" : (report.AverageScore > 85 ? "سهل" : "متوسط");
            }
            else
            {
                report.AverageScore = 0;
                report.DifficultyLevel = "غير متاح";
            }

            return report;
        }
    }

    // ✅ تعريف `CurriculumAnalysisReport`
    public class CurriculumAnalysisReport
    {
        public int CurriculumId { get; set; }
        public string CurriculumTitle { get; set; }
        public int TotalStudents { get; set; }
        public double AverageScore { get; set; }
        public string DifficultyLevel { get; set; }
    }
}
