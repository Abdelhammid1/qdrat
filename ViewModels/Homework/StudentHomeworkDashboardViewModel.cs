using QdratNew.ViewModels.Students;

namespace QdratNew.ViewModels.Homework
{
    public class StudentHomeworkDashboardViewModel
    {
        public int TotalAssigned { get; set; }   // كل الواجبات
        public int Completed { get; set; }       // المحلولة
        public int Pending { get; set; }         // المطلوب حلها
        public int Late { get; set; }            // المتأخرة



        // 📌 بيانات الـ Radar Chart
        public List<string> SectionLabels { get; set; } = new();
        public List<double> StudentScores { get; set; } = new();
        public List<double> BatchAverages { get; set; } = new();

        // كروت الواجبات
  

        // قائمة المناهج المرتبطة بدورة الدفعة
        public List<CurriculumViewModel> Curriculums { get; set; } = new();



        // ✅ الإضافات الجديدة للكارت
        public int Remaining => TotalAssigned - Completed;

        public List<string> HomeworkTitles { get; set; } = new();
        public List<double> StudentHomeworkScores { get; set; } = new();
        public List<double> BatchHomeworkAverages { get; set; } = new();

 

        public List<HomeworkPerformanceEntry> HomeworkProgress { get; set; } = new();
        public List<HomeworkComparisonEntry> HomeworkComparisons { get; set; } = new();



        // 🔹 نسبة إتمام الواجبات (Donut chart)
        public int HomeworkCompletionPercentage { get; set; }

        // 🔹 مقارنة الطالب بدفعته (Bar chart)

        // ✅ ✨ إضافات الذكاء الاصطناعي (تحليلات وتوصيات)
        public PerformanceAnalysisResult PerformanceAnalysis { get; set; }   // التحليل العام
        public List<string> Recommendations { get; set; } = new();           // توصيات نصية
        public List<WeaknessArea> WeaknessAreas { get; set; } = new();       // أضعف المحاور

        public HomeworkAnalyticsVm? LastHomeworkAnalysis { get; set; }

        public HomeworkRecommendationVm? Recommendation { get; set; }

    }



    public class CurriculumViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
    }

  

}
