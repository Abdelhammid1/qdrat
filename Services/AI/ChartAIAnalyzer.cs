using Microsoft.ML;
using Microsoft.ML.Data;
using QdratNew.ViewModels.AI;

namespace QdratNew.Services.AI
{
    public class ChartAIAnalyzer
    {
        private readonly MLContext _mlContext;

        public ChartAIAnalyzer()
        {
            _mlContext = new MLContext();
        }

        public ChartAIResult Analyze(ChartAnalysisInputViewModel input)
        {
            // نموذج أولي: تحليل يدوي ذكي مؤقت حتى يُدرّب النموذج فعليًا

            var riskScore = 100 - (input.SuccessRate * 0.5f + input.SessionCompletionRatio * 0.3f);
            var level = riskScore >= 80 ? "ضعيف"
                       : riskScore >= 50 ? "متوسط"
                       : "متميز";

            var recommendations = new List<string>();

            if (riskScore > 80)
                recommendations.Add("⚠️ أداؤك العام منخفض، ننصح بمتابعة خطة علاجية مركزة خلال أسبوع.");
            if (input.SessionCompletionRatio < 0.5)
                recommendations.Add("💡 حاول حضور كل الجلسات المقررة، نسبة الحضور منخفضة.");
            if (input.AverageHomeworkTime > 600)
                recommendations.Add("⏱️ وقت حل الواجبات طويل، راجع مهاراتك في إدارة الوقت.");
            if (input.SuccessRate < 60)
                recommendations.Add("📉 نسبة الإجابات الصحيحة منخفضة، راجع أساسيات المنهج.");
            if (input.TotalExams == 0)
                recommendations.Add("📋 لم تُجري أي اختبارات، حاول تجربة اختبار محاكاة قريبًا.");

            return new ChartAIResult
            {
                RiskScore = riskScore,
                AssessedLevel = level,
                Recommendations = recommendations
            };
        }
    }
}
