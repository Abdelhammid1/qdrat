using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class AdminActivityLogger : IAdminActivityLogger
    {
        private readonly ApplicationDbContext _context;

        public AdminActivityLogger(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string actionType, string description, string? adminId, string? adminName, int? homeworkSetId = null, int? studentId = null, int? batchId = null)
        {
            if (string.IsNullOrWhiteSpace(actionType) || string.IsNullOrWhiteSpace(description)) return;

            var log = new AdminActivityLog
            {
                AdminId = adminId ?? "Unknown",
                AdminName = adminName ?? "غير معروف",
                ActionType = actionType,
                Description = description,
                HomeworkSetId = homeworkSetId,
                StudentId = studentId,
                BatchId = batchId,
                Timestamp = DateTime.Now
            };

            _context.AdminActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task LogExamQuestionsChangeAsync(
            string actionType,
            string reason,
            string? adminId,
            string? adminName,
            int? examAssignmentId,
            int? examAssignmentToStudentId,
            int? studentId,
            int questionsBeforeCount,
            int questionsAfterCount,
            int affectedAttemptsCount)
        {
            if (string.IsNullOrWhiteSpace(actionType)) return;

            var impact = new
            {
                StudentId = studentId,
                QuestionsBeforeCount = questionsBeforeCount,
                QuestionsAfterCount = questionsAfterCount,
                AffectedAttemptsCount = affectedAttemptsCount
            };

            var description =
                $"تم تغيير أسئلة الاختبار من {questionsBeforeCount} إلى {questionsAfterCount} سؤال. " +
                $"{affectedAttemptsCount} محاولة إجابة سابقة للطالب/ة تم حذفها/تصفيرها نتيجة هذا التعديل.";

            var log = new AdminActivityLog
            {
                AdminId = adminId ?? "Unknown",
                AdminName = adminName ?? "غير معروف",
                ActionType = actionType,
                Description = description,
                StudentId = studentId,
                ExamAssignmentId = examAssignmentId,
                ExamAssignmentToStudentId = examAssignmentToStudentId,
                Reason = reason,
                ImpactJson = JsonSerializer.Serialize(impact),
                Timestamp = DateTime.Now
            };

            _context.AdminActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
