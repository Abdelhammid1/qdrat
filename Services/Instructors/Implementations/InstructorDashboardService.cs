using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Interfaces;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Instructor;

namespace QdratNew.Services.Instructors.Implementations
{
    public class InstructorDashboardService : IInstructorDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IInstructorAccessService _access;

        public InstructorDashboardService(
            ApplicationDbContext context,
            IInstructorAccessService access)
        {
            _context = context;
            _access = access;
        }

        public async Task<InstructorDashboardViewModel> BuildAsync(int instructorId)
        {
            var userId = await _context.Instructors
                .Where(x => x.Id == instructorId && !x.IsDeleted)
                .Select(x => x.UserId)
                .FirstOrDefaultAsync();

            if (userId == null)
                return new InstructorDashboardViewModel();

            var batchIds = await _access.GetAccessibleBatchIdsAsync(userId);

            if (!batchIds.Any())
                return new InstructorDashboardViewModel();

            // ==========================================
            // 🟢 الدفعات
            // ==========================================

            var allBatches = await _context.Batches
                .AsNoTracking()
                .Where(b => b.IsActive && !b.IsDeleted)
                .Select(b => new { b.Id, b.Name })
                .ToListAsync();

            var batches = allBatches
                .Where(b => batchIds.Contains(b.Id))
                .ToList();

            // ==========================================
            // 🟢 الطلاب
            // ==========================================

            var allEnrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(sbe => sbe.Status == "Active")
                .Select(sbe => new { sbe.StudentID, sbe.BatchId })
                .ToListAsync();

            var students = allEnrollments
                .Where(sbe => batchIds.Contains(sbe.BatchId))
                .Select(sbe => sbe.StudentID)
                .Distinct()
                .ToList();

            // ==========================================
            // 🟢 المحاضرات
            // ==========================================

            var allLectures = await _context.Lecture
                .AsNoTracking()
                .Select(l => new { l.Id, l.BatchId })
                .ToListAsync();

            var totalLectures = allLectures
                .Where(l => batchIds.Contains(l.BatchId))
                .Count();

            // ==========================================
            // 🟢 الحضور
            // ==========================================

            var allAttendance = await _context.AttendanceRecords
                .AsNoTracking()
                .Select(ar => new { ar.Id, ar.StudentId })
                .ToListAsync();

            var attendanceCount = allAttendance
                .Where(ar => students.Contains(ar.StudentId))
                .Count();

            double avgAttendance =
                (totalLectures > 0 && students.Count > 0)
                    ? attendanceCount / (double)(students.Count * totalLectures) * 100
                    : 0;

            // ==========================================
            // 🟢 الواجبات
            // ==========================================

            var allHomeworks = await _context.Homeworks
                .AsNoTracking()
                .Select(h => new
                {
                    h.StudentId,
                    h.Score,
                    h.IsCompleted
                })
                .ToListAsync();

            var homeworkData = allHomeworks
                .Where(h => students.Contains(h.StudentId))
                .ToList();

            double homeworkCompletionRate =
                students.Count > 0
                    ? homeworkData
                        .Where(x => x.IsCompleted)
                        .Select(x => x.StudentId)
                        .Distinct()
                        .Count()
                        / (double)students.Count * 100
                    : 0;

            double avgHomeworkScore =
                homeworkData
                    .Where(x => x.Score.HasValue)
                    .Select(x => x.Score.Value)
                    .DefaultIfEmpty(0)
                    .Average();

            // ==========================================
            // 🟢 اختبارات الأداء
            // ==========================================

            var allPerformances = await _context.StudentPerformances
                .AsNoTracking()
                .Select(sp => new { sp.StudentID, sp.Score })
                .ToListAsync();

            var testScores = allPerformances
                .Where(sp => students.Contains(sp.StudentID))
                .Select(sp => sp.Score)
                .ToList();

            double avgTestScore =
                testScores.Any() ? testScores.Average() : 0;

            // ==========================================
            // 🟢 الطلاب المعرضون للخطر
            // ==========================================

            var allStudents = await _context.Students
                .AsNoTracking()
                .Select(s => new { s.StudentID, s.FullName })
                .ToListAsync();

            var atRiskStudents = (
                from sp in allPerformances
                join s in allStudents on sp.StudentID equals s.StudentID
                join sbe in allEnrollments on sp.StudentID equals sbe.StudentID
                join b in allBatches on sbe.BatchId equals b.Id
                where batchIds.Contains(sbe.BatchId)
                      && sp.Score < 50
                select new AtRiskStudentViewModel
                {
                    StudentId = s.StudentID,
                    StudentName = s.FullName,
                    Score = sp.Score,
                    BatchName = b.Name
                }
            ).ToList();

            // ==========================================
            // 🟢 ViewModel
            // ==========================================

            return new InstructorDashboardViewModel
            {
                TotalBatches = batches.Count,
                TotalStudents = students.Count,
                TotalHomeworks = homeworkData.Count,
                AverageExamScore = Math.Round(avgTestScore, 2),
                HomeworkCompletionRate = Math.Round(homeworkCompletionRate, 2),
                LessonCompletionRate = Math.Round(avgAttendance, 2),
                AtRiskStudents = atRiskStudents,
                BatchInsights = new List<BatchAnalysisViewModel>()
            };
        }
    }
}