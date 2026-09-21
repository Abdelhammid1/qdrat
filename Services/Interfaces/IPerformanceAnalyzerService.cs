using QdratNew.Entities;

namespace QdratNew.Services.Interfaces
{
    public interface IPerformanceAnalyzerService
    {
        /// <summary>
        /// تحليل نتائج اختبار مؤشر الأداء وتوليد خطة علاجية آلية للطالب إذا رسب.
        /// </summary>
        Task AnalyzeExamResultsAsync(int performanceIndicatorExamId);

        /// <summary>
        /// تحليل نتيجة طالب محدد فقط (استخدام مخصص).
        /// </summary>
        Task AnalyzeStudentResultAsync(int studentId, int examId);

        /// <summary>
        /// تحليل مستوى الطالب وسبب الضعف بناءً على محاولاته السابقة وبيانات الأداء.
        /// </summary>
        string DetectFailureReason(double scorePercent, double avgTimePerQuestion, double sectionDifficulty, double avgHomeworkScore, double avgExamScore);



        Task AnalyzeExamResultsAsync(int performanceIndicatorExamId, int? batchId = null, string? gender = null);


    }
}
