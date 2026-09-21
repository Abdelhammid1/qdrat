using QdratNew.ViewModels.AI;
using QdratNew.ViewModels.Students;

public interface IChartAIAnalyzer
{
    StudentAIAnalysisResult Analyze(QdratNew.ViewModels.AI.ChartAnalysisInputViewModel input);
}
