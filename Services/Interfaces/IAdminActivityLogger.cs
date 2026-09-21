// Services/Interfaces/IAdminActivityLogger.cs
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IAdminActivityLogger
    {
        Task LogAsync(
            string actionType,
            string description,
            string? adminId,
            string? adminName,
            int? homeworkSetId = null,
            int? studentId = null,
            int? batchId = null
        );

        // Sprint 3 (EC2) — تسجيل تعديل أسئلة اختبار: من/ليه/الأثر (يوسّع نفس اللوجر، لا نظام موازٍ)
        Task LogExamQuestionsChangeAsync(
            string actionType,
            string reason,
            string? adminId,
            string? adminName,
            int? examAssignmentId,
            int? examAssignmentToStudentId,
            int? studentId,
            int questionsBeforeCount,
            int questionsAfterCount,
            int affectedAttemptsCount
        );
    }
}

