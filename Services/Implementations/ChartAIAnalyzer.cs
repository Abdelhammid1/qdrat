using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Students;

public class ChartAIAnalyzer : IChartAIAnalyzer
{
    public StudentAIAnalysisResult Analyze(QdratNew.ViewModels.AI.ChartAnalysisInputViewModel input)
    {
        return new StudentAIAnalysisResult
        {
            AssessedLevel = "متوسط",
            RiskScore = 2,
            Recommendations = new List<string>
            {
                "نوصي بمراجعة الدروس المتأخرة.",
                "الاستمرار في الجلسات العلاجية."
            }
        };
    }
}
