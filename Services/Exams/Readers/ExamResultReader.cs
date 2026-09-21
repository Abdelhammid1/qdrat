using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Services.Exams.Abstractions;
using System.Text.Json;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams.Readers
{
    public class ExamResultReader : IExamResultReader
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public ExamResultReader(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<ExamFinalResultDto?> GetResultAsync(
     int examAssignmentId,
     int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var status = await db.ExamStudentStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    (
                        s.ExamAssignmentId == examAssignmentId ||
                        s.ExamAssignmentToStudentId == examAssignmentId
                    ) &&
                    s.IsSubmitted);

            if (status == null || string.IsNullOrWhiteSpace(status.Note))
                return null;

            return JsonSerializer.Deserialize<ExamFinalResultDto>(status.Note);
        }

    }
}
