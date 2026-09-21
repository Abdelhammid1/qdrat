using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Section;

namespace QdratNew.ViewModels.Instructor
{
    public class InstructorsAIDashboardViewModel
    {
        // 📊 الإحصائيات العامة
        public int TotalInstructors { get; set; }
        public int ActiveInstructors { get; set; }
        public int InstructorsWithCurriculums { get; set; }

        // 🔍 تحليل أداء المدربين
        public List<InstructorPerformanceAIViewModel> InstructorsStats { get; set; } = new();

        // 🎓 فلاتر المناهج والزمن
        public List<SelectListItem> CurriculumList { get; set; } = new();
        public int? SelectedCurriculumId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // ✅ دعم الفلترة حسب الدفعة
        public List<SelectListItem> BatchList { get; set; } = new();
        public int? SelectedBatchId { get; set; }


        public List<InstructorPerformanceChartViewModel> InstructorPerformanceChartData { get; set; } = new();



        public List<InstructorSectionPerformanceViewModel> InstructorSectionStats { get; set; } = new();
        public int? SelectedBatchIdForSections { get; set; }
        public List<SelectListItem> BatchListForSections { get; set; }
        public List<SectionPerformanceAIViewModel> SectionPerformances { get; set; } = new List<SectionPerformanceAIViewModel>();

    }


}
