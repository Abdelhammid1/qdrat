using System.Threading.Tasks;
using QdratNew.ViewModels.Analytics;

namespace QdratNew.Services.Interfaces
{
    public interface IPerformanceInsightService
    {
        // 🔥 التحليل الشامل
        Task<PerformanceInsightResult> AnalyzeStudentPerformanceAsync(int studentId, int examId);

        // 🎯 شرائح النتيجة العامة
        string GetScoreBandDescription(double score);

        // 🧠 السرعة × الدقة
        string GetSpeedAccuracyFeedback(double speedPercent, double scorePercent);

        // 🧭 التوجيه الخاص بالمحور داخل اختبار مؤشر الأداء
        string GetSectionGuidance(double score);

        // 💬 الرسالة التحفيزية
        string GetMotivationalMessage(double score);
    }
}
