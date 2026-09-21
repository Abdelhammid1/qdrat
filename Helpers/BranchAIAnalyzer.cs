using QdratNew.Entities;
using QdratNew.ViewModels.Branches;

namespace QdratNew.Helpers
{
    public static class BranchAIAnalyzer
    {
        public static BranchAIAnalysisResult Analyze(Branch branch)
        {
            var result = new BranchAIAnalysisResult();

            var allPerformances = branch.Students?
                .SelectMany(s => s.StudentPerformances)
                .Where(p => p != null)
                .ToList() ?? new List<StudentPerformance>();

            double avgScore = allPerformances.Any() ? allPerformances.Average(p => p.Score) : 0;
            double avgEngagement = allPerformances.Any() ? allPerformances.Average(p => p.EngagementRate) : 0;
            double avgAttendance = allPerformances.Any() ? allPerformances.Average(p => p.AttendanceCount) : 0;

            result.AveragePerformance = avgScore;
            result.AverageEngagement = avgEngagement;
            result.AverageAttendance = avgAttendance;

            var messages = new List<string>();

            // 🎯 الأداء العام
            if (avgScore < 50)
            {
                result.AiComment = "⚠️ الأداء العام منخفض جدًا في هذا الفرع.";
                messages.Add("ينبغي التدخل المباشر لتحسين جودة التدريس.");
            }
            else if (avgScore < 70)
            {
                result.AiComment = "📉 الأداء متوسط، وهناك مجال كبير للتحسين.";
                messages.Add("يمكن تحسين المحتوى وتكثيف المراجعات.");
            }
            else
            {
                result.AiComment = "✅ الأداء العام جيد ومستقر.";
                messages.Add("استمر في السياسات الحالية مع تقييم شهري.");
            }

            // 🎯 التفاعل
            if (avgEngagement < 40)
                messages.Add("نسبة التفاعل منخفضة، حاول زيادة الأنشطة الصفية.");
            else if (avgEngagement > 80)
                messages.Add("💡 التفاعل مميز جدًا، حافظ عليه.");

            // 🎯 الحضور
            if (avgAttendance < 50)
                messages.Add("معدل الحضور ضعيف، اقترح نظام تحفيزي للحضور.");

            // 🎯 شهور متتالية سيئة
            var trend = allPerformances
                .GroupBy(p => p.ExamDate.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .Select(g => new { Month = g.Key, Avg = g.Average(x => x.Score) })
                .ToList();

            if (trend.Count >= 3 && trend.TakeLast(3).All(p => p.Avg < 50))
                messages.Add("⏳ الأداء منخفض آخر 3 أشهر، يستدعي إعادة تقييم المنهج.");

            result.Recommendation = string.Join(" ", messages);
            return result;
        }
        public static string GenerateMlBasedInsight(Branch branch)
        {
            // تجميع بيانات الأداء مرة واحدة لتقليل التكرار
            var allPerformances = branch.Students
                .Where(s => s.StudentPerformances != null)
                .SelectMany(s => s.StudentPerformances)
                .ToList();

            var input = new BranchPerformanceInput
            {
                TotalStudents = (float)(branch.Students?.Count ?? 0),
                TotalCourses = (float)(branch.Courses?.Count ?? 0),
                TotalProjects = (float)(branch.Projects?.Count ?? 0),
                AverageEngagement = allPerformances.Any()
                    ? (float)allPerformances.Average(p => p.EngagementRate)
                    : 0f,
                AverageAttendance = allPerformances.Any()
                    ? (float)allPerformances.Average(p => p.AttendanceCount)
                    : 0f
            };

            float predicted = BranchPerformanceTrainer.Predict(input);

            if (predicted > 85)
                return "🧠 الأداء المتوقع عالي جداً، يمكن التوسع في الفرع.";

            if (predicted > 65)
                return "📊 الأداء مستقر ولكن يستحق المتابعة.";

            return "⚠️ الأداء منخفض ويتطلب تدخل تدريبي.";
        }



    }

    public class BranchAIAnalysisResult
    {
        public string AiComment { get; set; } = "لم يتم التحليل بعد";
        public string Recommendation { get; set; } = "لا توجد توصيات حاليًا";
        public double AveragePerformance { get; set; }
        public double AverageEngagement { get; set; }
        public double AverageAttendance { get; set; }
    }
}
