using QdratNew.Entities;
using QdratNew.Entities;
using QdratNew.Entities;
using QdratNew.ViewModels.Section; // ✅ هذا هو النوع الذي عندك حالياً

namespace QdratNew.Services.AI
{
    public interface ISectionAIService
    {
        Task<SectionAIDashboardViewModel> AnalyzePerformancesAsync(List<StudentPerformance> performances, List<Section> sections);
    }
}
