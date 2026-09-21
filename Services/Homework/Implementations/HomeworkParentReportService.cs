using QdratNew.Enums;
using QdratNew.ViewModels.Homework;

namespace QdratNew.Services.Homework.Implementations
{
    public class HomeworkParentReportService 
    {
        public ParentReportResult BuildParentReport(
            double score,
            int totalQuestions,
            int correct,
            int wrong,
            int skipped,
            double avgTime,
            StudentBehaviorLevel behaviorLevel,
            List<double> previousScores)
        {
            var result = new ParentReportResult();

            // ===============================
            // 📊 المستوى
            // ===============================
            if (score >= 85)
                result.Level = "ممتاز";
            else if (score >= 70)
                result.Level = "جيد جدًا";
            else if (score >= 50)
                result.Level = "متوسط";
            else
                result.Level = "ضعيف";

            // ===============================
            // 🧠 السلوك
            // ===============================
            result.BehaviorText = behaviorLevel switch
            {
                StudentBehaviorLevel.غش_محتمل => "⚠️ الطالب حل بسرعة غير طبيعية ويحتاج متابعة دقيقة",
                StudentBehaviorLevel.مريب => "🔎 الطالب يظهر تسرع في الحل ويحتاج تحسين التركيز",
                _ => "✅ الطالب يتعامل بشكل طبيعي مع الواجب"
            };

            // ===============================
            // 📉 الالتزام
            // ===============================
            double answeredRatio = totalQuestions > 0
                ? ((correct + wrong) * 100.0 / totalQuestions)
                : 0;

            if (answeredRatio < 60)
                result.Commitment = "❌ لا يلتزم بحل جميع الأسئلة";
            else if (answeredRatio < 85)
                result.Commitment = "⚠️ التزام متوسط";
            else
                result.Commitment = "✅ ملتزم بحل الواجب";

            // ===============================
            // 📈 التقدم
            // ===============================
            if (previousScores != null && previousScores.Count >= 2)
            {
                var last = previousScores.Last();
                var prev = previousScores.Skip(previousScores.Count - 2).First();

                if (last > prev)
                    result.Progress = "📈 تحسن في المستوى";
                else if (last < prev)
                    result.Progress = "📉 تراجع في المستوى";
                else
                    result.Progress = "➖ مستوى ثابت";
            }

            // ===============================
            // 🏠 توصيات منزلية
            // ===============================
            var tips = new List<string>();

            if (skipped > 0)
                tips.Add("مراجعة الأسئلة التي لم يتم حلها");

            if (avgTime < 5)
                tips.Add("يجب التمهل أثناء الحل وعدم التسرع");

            if (score < 60)
                tips.Add("إعادة مذاكرة الدرس قبل حل الواجب");

            result.HomeRecommendations = tips;

            return result;
        }
    }
}
