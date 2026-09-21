using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace QdratNew.ViewModels.Section
{
    public class SectionsAIReportViewModel
    {
        // 📌 قائمة المحاور مع التحليل الذكي لكل محور
        public List<SectionAIAnalysisViewModel> SectionsStats { get; set; }

        // 📊 إحصائيات عامة
        public int TotalQuestions { get; set; }
        public double SuccessRate { get; set; } // نسبة النجاح العامة
        public double FailureRate { get; set; } // نسبة الرسوب العامة
        public int HardQuestionsCount { get; set; }

        // 🔽 فلاتر
        public int? SelectedBatchId { get; set; }
        public List<SelectListItem> BatchList { get; set; } = new();
    }

    public class SectionAIStatItem
    {
        public string SectionTitle { get; set; } = string.Empty;
        public int TotalQuestions { get; set; }

        public double SuccessRate { get; set; }
        public double FailureRate { get; set; }

        // تحليل الذكاء الاصطناعي (مثال: "أغلب الأسئلة صعبة", "المحور متوازن", ...)
        public string DifficultyAnalysis { get; set; } = "غير محدد";
    }
}
