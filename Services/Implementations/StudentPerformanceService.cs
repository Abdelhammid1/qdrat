using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.AI;

namespace QdratNew.Services.Implementations
{
    public class StudentPerformanceService : IStudentPerformanceService
    {
        private readonly ApplicationDbContext _context;

        public StudentPerformanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ChartAnalysisInputViewModel> GetChartAnalysisDataAsync(int studentId)
        {
            var assignments = await _context.Homeworks
                .Where(h => h.StudentId == studentId && h.Status == HomeworkStatus.Submitted)
                .ToListAsync();

            var successRate = assignments.Count == 0
                ? 0
                : (float)assignments.Count(h => h.IsCorrect == true) / assignments.Count * 100;
            var avgTime = assignments.Count == 0 ? 0 : assignments.Average(h => h.TimeSpentSeconds ?? 0);
            var totalExams = await _context.ExamStudentStatuses.CountAsync(e => e.StudentId == studentId);
            var sessionRatio = 0.7f; // ❗ نسبة الجلسات المكتملة (اختياري - يمكن تطويره لاحقًا)

            return new ChartAnalysisInputViewModel
            {
                SuccessRate = (float)successRate,
                AverageHomeworkTime = (float)avgTime,
                TotalAssignments = assignments.Count,
                TotalExams = (float)totalExams,
                SessionCompletionRatio = (float)sessionRatio,
                RiskScore = 0
            };

        }
    }
}
