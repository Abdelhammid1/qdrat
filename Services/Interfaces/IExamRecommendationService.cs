using QdratNew.Enums;
using QdratNew.ViewModels.Reports;

namespace QdratNew.Services.Interfaces
{
    public interface IExamRecommendationService
    {
        // 🔹 النسخة القديمة (مازالت مستخدمة في اختبارات تحديد المستوى)
        ExamRecommendationVm GetRecommendation(double overallPercent, int solveMinutes, double totalMinutes);

        // 🔹 النسخة الجديدة (تدعم تمرير نوع الاختبار لتخصيص التوصيات)
        ExamRecommendationVm GetRecommendation(double overallPercent, int solveMinutes, double totalMinutes, ExamType? examType);
    }
}
