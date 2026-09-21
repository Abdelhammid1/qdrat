using QdratNew.ViewModels.Exam;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentExamStatusService
    {
        // =====================================================
        // 🔴 LEGACY – لا يُستخدم في الداشبورد الجديد
        // (موجود فقط لتفادي كسر أي كود قديم)
        // =====================================================

        Task<ExamStatusSummaryVm> GetStatusSummaryAsync(int studentId);

        Task<List<ExamListVm>> GetAllExamsAsync(int studentId);

        Task<List<ExamListVm>> GetRequiredExamsAsync(int studentId);

        Task<List<ExamListVm>> GetSolvedExamsAsync(int studentId);

        Task<List<ExamListVm>> GetLateExamsAsync(int studentId);

        Task<LateExamsPageVm> GetLateExamsPageAsync(int studentId);

        // =====================================================
        // 🟢 NEW – المعتمد في البرودكشن (Course + Batch Scoped)
        // =====================================================

        /// <summary>
        /// Dashboard summary (Total / Solved / Required / Late)
        /// scoped by active course & batch
        /// </summary>
        Task<ExamStatusSummaryVm> GetStatusSummaryAsync(
            int studentId,
            int activeCourseId,
            int activeBatchId
        );

        /// <summary>
        /// All exams cards for active course & batch
        /// </summary>
        Task<List<StudentExamCardViewModel>> GetAllExamsAsync(
            int studentId,
            int activeCourseId,
            int activeBatchId
        );

        /// <summary>
        /// Required (not submitted) exams
        /// </summary>
        Task<List<StudentExamCardViewModel>> GetRequiredExamsAsync(
            int studentId,
            int activeCourseId,
            int activeBatchId
        );

        /// <summary>
        /// Solved exams
        /// </summary>
        Task<List<StudentExamCardViewModel>> GetSolvedExamsAsync(
            int studentId,
            int activeCourseId,
            int activeBatchId
        );

        /// <summary>
        /// Late exams (not submitted & expired)
        /// </summary>
        Task<List<StudentExamCardViewModel>> GetLateExamsPageAsync(
            int studentId,
            int activeCourseId,
            int activeBatchId
        );
    }
}
