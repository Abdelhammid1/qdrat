using Microsoft.AspNetCore.Mvc.Rendering;

namespace QdratNew.ViewModels.Section
{
    public class SectionAIDashboardViewModel
    {
        public int TotalSections { get; set; }


        // ✅ لإظهار الفلاتر في الصفحة
        public List<SelectListItem> BatchList { get; set; } = new();
        public List<SelectListItem> CurriculumList { get; set; } = new();

        // ✅ لتحديد ما تم اختياره
        public int? SelectedBatchId { get; set; }
        public int? SelectedCurriculumId { get; set; }

        // تحليل لكل محور تعليمي
        public List<SectionAnalysisData> SectionAnalytics { get; set; } = new List<SectionAnalysisData>();

        // بيانات الرسم البياني - مستوى الصعوبة
        public List<PieChartData> DifficultyDistribution { get; set; } = new List<PieChartData>();

        // بيانات الرسم البياني - نسب النجاح حسب المحور
        public List<BarChartData> SuccessRatesPerSection { get; set; } = new List<BarChartData>();
        public int TotalSectionsWithPerformance { get; set; } // المحاور التي لديها أداء فعلي
        public int TotalSectionsInDatabase { get; set; }      // جميع المحاور في النظام


     
        public double CoverageRate
        {
            get
            {
                if (TotalSectionsInDatabase == 0) return 0;
                return Math.Round((double)TotalSectionsWithPerformance * 100 / TotalSectionsInDatabase, 1);
            }
        }




    }

    public class SectionAnalysisData
    {
        public int SectionId { get; set; }
        public string SectionTitle { get; set; }
        public string CurriculumTitle { get; set; }

        public int TotalQuestions { get; set; }
        public int TotalAttempts { get; set; }

        public double SuccessRate { get; set; }   // كنسبة مئوية
        public double FailureRate { get; set; }   // كنسبة مئوية

        public string DifficultyLevel { get; set; } // "سهل" | "متوسط" | "صعب"
    }

    public class PieChartData
    {
        public string Label { get; set; } // مستوى الصعوبة
        public int Count { get; set; }    // عدد المحاور من هذا المستوى
    }

    public class BarChartData
    {
        public string SectionTitle { get; set; }
        public double SuccessRate { get; set; }
    }
}
