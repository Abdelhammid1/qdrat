using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Section
{
    public class BatchSectionsAnalysisViewModel
    {
        public int BatchId { get; set; }
        public string BatchName { get; set; }
        public List<SelectListItem> AllBatches { get; set; }
        public List<CompletedSectionAnalysisViewModel> Sections { get; set; } = new();
    }
}
