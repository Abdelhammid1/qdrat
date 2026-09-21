using QdratNew.Entities;
using QdratNew.ViewModels.Section;

namespace QdratNew.Services.AI
{
    public interface ISectionAIAnalyzerService
    {
        Task<SectionAIDashboardViewModel> AnalyzePerformancesAsync(List<StudentPerformance> performances, List<Section> sections);
    }
}
