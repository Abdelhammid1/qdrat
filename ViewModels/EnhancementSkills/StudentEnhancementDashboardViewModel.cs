using QdratNew.ViewModels.Curriculum;
using QdratNew.ViewModels.Students;

namespace QdratNew.ViewModels.EnhancementSkills
{
    public class StudentEnhancementDashboardViewModel
    {
        public int TotalAssigned { get; set; }   // إجمالي المهارات التعزيزية المرسلة
        public int Completed { get; set; }       // عدد الجلسات التي حلها الطالب
        public int Pending { get; set; }         // المطلوب حلها
        public int Late { get; set; }            // الجلسات المتأخرة

        // 📊 رسومات بيانية
        public List<string> SectionLabels { get; set; } = new();
        public List<double> StudentScores { get; set; } = new();
        public List<double> BatchAverages { get; set; } = new();

        public List<CurriculumViewModel> Curriculums { get; set; } = new();

        // 📘 تطور الأداء
        public List<EnhancementPerformanceEntry> EnhancementProgress { get; set; } = new();

        // 📘 مقارنة نتائج الطالب مع باقي الدفعة
        public List<EnhancementComparisonEntry> EnhancementComparisons { get; set; } = new();

        // 📊 تحليل الأداء
        public PerformanceAnalysisResult PerformanceAnalysis { get; set; }
        public List<WeaknessArea> WeaknessAreas { get; set; } = new();
        public List<string> Recommendations { get; set; } = new();



    }

    public class EnhancementPerformanceEntry
    {
        public string Title { get; set; }
        public double Score { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswersCount { get; set; }
    }

    public class EnhancementComparisonEntry
    {
        public string Title { get; set; }
        public double StudentScore { get; set; }
        public double BatchAverageScore { get; set; }
    }

}
