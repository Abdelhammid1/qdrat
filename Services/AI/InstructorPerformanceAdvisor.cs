using QdratNew.ViewModels.Instructor;

namespace QdratNew.Services.AI
{
    public class InstructorPerformanceAdvisor
    {
        public static string GetRecommendation(InstructorPerformanceAIViewModel instructor)
        {
            if (instructor.AverageStudentScore < 60 && instructor.SuccessRate < 50)
                return "أداء المدرب منخفض، يُوصى بإشراكه في برنامج تطوير مهني وتحسين آلية شرح المناهج.";

            if (instructor.SuccessRate >= 80 && instructor.AverageStudentScore >= 75)
                return "مدرب متميز، يُوصى بمشاركته في تطوير المناهج التعليمية ونقل خبراته للمدربين الجدد.";

            if (instructor.TotalCurriculums >= 5 && instructor.AverageStudentScore >= 65)
                return "أداء مستقر، يُوصى بمراقبة استقرار النتائج وتعزيز المحتوى التعليمي.";

            return "لا توجد ملاحظات حرجة حاليًا، يُنصح بمواصلة التقييم الدوري.";
        }
    }

}
