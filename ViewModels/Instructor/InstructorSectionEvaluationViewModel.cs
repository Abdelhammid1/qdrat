using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.ViewModels.Section;

namespace QdratNew.ViewModels.Instructor
{
    public class InstructorSectionEvaluationViewModel
    {
        // معلومات عامة
        public int InstructorId { get; set; }
        public string InstructorName { get; set; }

        // فلترة وتحكم
        public int? SelectedBatchId { get; set; }
        public List<SelectListItem> BatchList { get; set; }

        public int? SelectedCurriculumId { get; set; }
        public List<SelectListItem> CurriculumList { get; set; }

        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // الأداء على مستوى المحاور التعليمية
        public List<SectionEvaluationAIViewModel> SectionsEvaluation { get; set; }

        // مؤشرات الذكاء الاصطناعي
        public double AIOverallScore { get; set; }  // التقييم العام AI
        public string AIEvaluationComment { get; set; }  // تعليق الذكاء الاصطناعي

        // مقاييس تحليل الأداء
        public int TotalSectionsCovered { get; set; }
        public double AverageStudentScore { get; set; }
        public double SuccessRate { get; set; }
        public double EngagementRate { get; set; } // من تفاعل الطلاب
        public double AttendanceRate { get; set; } // من الجلسات الحضورية

        // توصيات
        public List<string> AIRecommendations { get; set; }

        // عدد الطلاب الكلي الذين درّسهم المدرب (مفيد للتحليل)
        public int TotalStudentsTaught { get; set; }

        // عدد الجلسات التي قام بتدريسها
        public int TotalSessionsHeld { get; set; }

        // عدد الطلاب الذين أظهروا تحسنًا ملحوظًا (منخفض → مرتفع)
        public int StudentsImprovedCount { get; set; }

        // مؤشر AI للتوصية بـ "استمرار / دعم تدريبي / مراجعة"
        public string AIDecision { get; set; }  // "استمر"، "يحتاج دعم"، "راجع الأداء"



     


    }


}
