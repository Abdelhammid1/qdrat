using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Reports;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations.Remedial
{
    public class RemedialReportService : IRemedialReportService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public RemedialReportService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<StudentRemedialReportVm> GenerateStudentReportAsync(int studentId, int sessionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var totalVideos = await _context.RemedialSessionLogs
                .Where(r => r.SessionId == sessionId && r.StudentId == studentId)
                .Select(r => r.VideoId)
                .Distinct()
                .CountAsync();

            var totalWatched = await _context.RemedialSessionLogs
                .Where(r => r.SessionId == sessionId && r.StudentId == studentId && r.IsCompleted)
                .CountAsync();

            var quizzes = await _context.StudentRemedialQuizResults
                .Where(r => r.StudentId == studentId)
                .ToListAsync();

            double avgScore = quizzes.Any() ? quizzes.Average(q => q.Score) : 0;

            return new StudentRemedialReportVm
            {
                StudentId = studentId,
                SessionId = sessionId,
                VideosWatched = totalWatched,
                TotalVideos = totalVideos,
                AverageQuizScore = avgScore,
                CompletionPercent = totalVideos == 0 ? 0 : (totalWatched * 100 / totalVideos)
            };
        }
    }
}
