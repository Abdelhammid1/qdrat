using QdratNew.ViewModels.Students;
using QdratNew.ViewModels.AI;

namespace QdratNew.ML
{
    public class ChartAIAnalyzer
    {
        public StudentAIAnalysisResult Analyze(ChartAnalysisInputViewModel input)
        {
            var result = new StudentAIAnalysisResult();

            if (input.SuccessRate >= 85 && input.SessionCompletionRatio >= 0.8f)
            {
                result.AssessedLevel = "ممتاز";
                result.RiskScore = 1;
                result.Recommendations = new List<string>
                {
                    "استمر على نفس الأداء المميز.",
                    "حاول التطوع لمساعدة زملائك في الدراسة.",
                    "قد تكون جاهزًا لتحديات أكبر أو مستويات أعلى."
                };
            }
            else if (input.SuccessRate >= 60 && input.SessionCompletionRatio >= 0.5f)
            {
                result.AssessedLevel = "متوسط";
                result.RiskScore = 2;
                result.Recommendations = new List<string>
                {
                    "احرص على الانتظام في الجلسات العلاجية.",
                    "تابع مراجعة الدروس المقترحة من النظام.",
                    "اطلب المساعدة عند الشعور بالصعوبة."
                };
            }
            else
            {
                result.AssessedLevel = "ضعيف";
                result.RiskScore = 3;
                result.Recommendations = new List<string>
                {
                    "تواصل مع المدرّب لمراجعة النقاط الضعيفة.",
                    "خصص وقتًا أكثر لحل الواجبات.",
                    "اطلب خطة علاجية مخصصة لتحسين الأداء."
                };
            }

            return result;
        }
    }
}
