using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentRankingService
    {
        /// <summary>
        /// 🔹 يقوم بتسجيل ترتيب الطالب داخل الدفعة الحالية في جدول StudentRankHistories
        /// </summary>
        Task RecordStudentRankAsync(int studentId, int batchId);

        /// <summary>
        /// 🔹 يجلب آخر ترتيب مسجل للطالب بناءً على آخر تحديث في StudentRankHistories
        /// </summary>
        Task<StudentRankVm?> GetCurrentRankAsync(int studentId);
    }
}
