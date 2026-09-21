using QdratNew.Enums;
using QdratNew.ViewModels.Homework;

namespace QdratNew.Services.Homework.Interfaces
{
    public interface IHomeworkAnalyticsService
    {
        Task BuildAndStoreAsync(int studentId, int homeworkSetId);

        // ✅ إضافة الميثود الجديدة
        Task<List<HomeworkProgressPoint>> GetStudentProgressAsync(int studentId);

        ParentReportResult BuildParentReport(
    double score,
    int correct,
    int wrong,
    int skipped,
    double avgTime,
    StudentBehaviorLevel behavior,
    string progress
);
        // ✅ أضف هذا
        (StudentBehaviorLevel level, string text) AnalyzeBehavior(
            double avgTime,
            int correct,
            int total);


    }
}