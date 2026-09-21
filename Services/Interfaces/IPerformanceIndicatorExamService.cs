using QdratNew.Entities;

namespace QdratNew.Services.Interfaces
{
    public interface IPerformanceIndicatorExamService
    {
        /// <summary>
        /// توليد اختبار مؤشر أداء جديد لمنهج محدد داخل دفعة معينة.
        /// </summary>
        Task GenerateExamAsync(int examId, int curriculumId, int totalQuestions, bool manualSelection, int? professionalModelId);

        /// <summary>
        /// جلب كل اختبارات المؤشر الخاصة بالدفعة.
        /// </summary>
        Task<List<PerformanceIndicatorExam>> GetExamsByBatchAsync(int batchId);

        /// <summary>
        /// جلب اختبار محدد بالتفاصيل (يشمل المحاور).
        /// </summary>
        Task<PerformanceIndicatorExam?> GetExamWithSectionsAsync(int examId);

        /// <summary>
        /// حذف اختبار مؤشر الأداء بأمان دون كسر العلاقات.
        /// </summary>
        Task<bool> DeleteExamAsync(int examId);

        /// <summary>
        /// تحليل نتيجة طالب وتسجيلها في StudentIndicatorResults.
        /// </summary>
        Task RecordStudentResultAsync(int studentId, int examId, int sectionId, double scorePercent, double avgTime, bool rushed, bool unfocused);

    
    
    }
}
