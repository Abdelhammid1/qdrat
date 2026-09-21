using QdratNew.Entities;
using QdratNew.ViewModels.Branches;

namespace QdratNew.Services.AI
{
    public static class BranchAIAnalyzer
    {
        public static BranchAIReport Analyze(Branch branch)
        {
            var avg = branch.AveragePerformance;
            var studentCount = branch.Students?.Count ?? 0;
            var courseCount = branch.Courses?.Count ?? 0;
            var projectCount = branch.Projects?.Count ?? 0;

            var comment = avg switch
            {
                >= 85 => "أداء ممتاز. يعكس جودة التعليم والتفاعل.",
                >= 70 => "أداء جيد مع فرص للتحسين.",
                >= 50 => "أداء متوسط. يوصى بتكثيف الدعم.",
                < 50 => "أداء ضعيف. بحاجة لتحسين شامل.",
                _ => "لا توجد بيانات كافية للتقييم."
            };

            var recommendation = (avg, studentCount) switch
            {
                ( < 50, > 20) => "ينصح بإعادة هيكلة الفرع وتكثيف التدريب.",
                ( < 50, <= 20) => "فكر في دمج الفرع مع آخر قريب.",
                ( >= 85, _) => "يمكن استخدام هذا الفرع كنموذج تدريبي للفروع الأخرى.",
                (_, > 100) => "عدد الطلاب مرتفع. تأكد من توافر الكوادر.",
                _ => "استمر في المتابعة والتقييم المستمر."
            };

            return new BranchAIReport
            {
                BranchName = branch.Name,
                TotalStudents = studentCount,
                TotalCourses = courseCount,
                TotalProjects = projectCount,
                AveragePerformance = avg,
                AiComment = comment,
                Recommendation = recommendation
            };
        }
    }
}
