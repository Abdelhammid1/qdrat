using QdratNew.ViewModels.Students;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentExamStatisticsService
    {
        /// <summary>
        /// 🔹 إحصاءات جميع الاختبارات العامة للطالب (Dashboard)
        /// </summary>
        Task<ExamStatisticsViewModel> GetExamStatisticsAsync(int studentId);

        /// <summary>
        /// 🔹 حساب نتيجة اختبار واحد (دفعة – فردي – مؤشر أداء – تحديد مستوى)
        /// </summary>
        Task<ExamResultSummaryVm> GetExamResultAsync(int examIdOrAssignmentId, int studentId);
    }
}
