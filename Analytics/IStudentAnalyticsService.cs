using QdratNew.ViewModels.Students;
using System.Threading.Tasks;

namespace QdratNew.Analytics
{
    public interface IStudentAnalyticsService
    {
        // 🟢 تسجيل ضعف الطالب في محور أو مؤشر
        Task RecordWeaknessAsync(int studentId, Guid questionId, bool isCorrect);

        // 🟢 تحديث نسبة التقدم في المنهج
        Task UpdateProgressAsync(int studentId, int curriculumId, double progress);

        // 🟢 تسجيل نتيجة تدريب علاجي
        Task RecordRemedialTrainingAsync(int studentId, int sectionId, int totalQuestions, int correctAnswers);

        // 🟢 تسجيل ترتيب الطالب داخل الدفعة
        Task RecordRankAsync(int studentId, int batchId);

        // 🟣 تحليل تعلم الطالب داخل منهج معين
        Task<StudentLearningAnalyticsDto> AnalyzeStudentAsync(int studentId, int curriculumId);

        // 🟣 تحليل الأداء العام للطالب (القوة والضعف)
        Task<PerformanceAnalysisResult> AnalyzePerformanceAsync(int studentId);
        Task UpdateProgressAsync(int studentId, int curriculumId);

        Task<int> GetCurriculumIdByHomeworkAsync(int homeworkSetId);
        Task UpdateRankHistoryAsync(int studentId, int batchId);
        Task RecordAttendanceAsync(int studentId, int lectureId, bool isPresent);

    }
}
