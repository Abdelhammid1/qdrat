using QdratNew.ViewModels.Branches;

namespace QdratNew.AI.Recommendations
{
    // Namespace: QdratNew.AI.Recommendations
    public class BranchSmartAnalyzer
    {
        public static List<string> Generate(BranchAIAnalysisViewModel branch)
        {
            var list = new List<string>();

            if (branch.TotalStudents > 100 && branch.AveragePerformance < 0.6)
                list.Add("يوجد عدد طلاب مرتفع لكن الأداء ضعيف. يُنصح بإعادة هيكلة المناهج أو توزيع الدورات.");

            if (branch.TotalCourses > 15 && branch.TotalStudents < 20)
                list.Add("عدد الدورات غير متناسب مع عدد الطلاب. يُفضل تقليص المحتوى مؤقتًا.");

            if (branch.IsPartner && branch.AveragePerformance < 0.5)
                list.Add("الفرع الشريك يعاني من ضعف الأداء. يُفضل مراجعة الشراكة ومدى جدواها.");

            if (branch.MonthlyPerformanceTrend.Count >= 3 &&
                branch.MonthlyPerformanceTrend.All(p => p.AverageScore < 0.4))
                list.Add("الأداء في الثلاث أشهر الأخيرة ضعيف جدًا. يُنصح بتقييم عاجل للكوادر.");

            return list;
        }
    }

}
