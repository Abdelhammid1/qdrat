using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Curriculum
{
    public class CurriculumAIDashboardViewModel
    {
        public List<CurriculumPerformanceViewModel> CurriculumStats { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<SelectListItem> BatchList { get; set; }
        public int? SelectedBatchId { get; set; }
        public int TotalCurriculumsInSystem { get; set; }
        public int TotalCurriculumsWithPerformance { get; set; }





    }
}
