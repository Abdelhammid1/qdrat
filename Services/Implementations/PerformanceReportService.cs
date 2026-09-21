using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Reports;

namespace QdratNew.Services.Implementations
{
    public class PerformanceReportService : IPerformanceReportService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PerformanceReportService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<IndicatorPerformanceReportViewModel>> GetBatchPerformanceReportAsync(int batchId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧠 جلب بيانات النتائج مع الخطة العلاجية
            var query =
     from r in _context.StudentIndicatorResults.AsNoTracking()
     join s in _context.Students.AsNoTracking() on r.StudentId equals s.StudentID
     join sb in _context.StudentBatchEnrollments.AsNoTracking() on s.StudentID equals sb.StudentID
     join b in _context.Batches.AsNoTracking() on sb.BatchId equals b.Id
     join sec in _context.Sections.AsNoTracking() on r.SectionId equals sec.Id
     join rp in _context.RemedialPlans.AsNoTracking() on r.RemedialPlanId equals rp.Id into rpJoin
     from rp in rpJoin.DefaultIfEmpty()
     where sb.BatchId == batchId
     select new IndicatorPerformanceReportViewModel
     {
         StudentId = s.StudentID,
         StudentName = s.FullName,
         BatchName = b.Name,
         SectionTitle = sec.Title,
         ScorePercent = r.ScorePercent,
         IsPassed = r.IsPassed,
         FailureReason = r.FailureReason,
         HasRemedialPlan = r.RemedialPlanId != null,
         RemedialPlanTitle = rp != null ? rp.Title : null,
         RemedialStartDate = rp != null ? (DateTime?)rp.StartDate : null,
         RemedialEndDate = rp != null ? (DateTime?)rp.EndDate : null
     };


            return await query.OrderBy(x => x.StudentName).ToListAsync();
        }


        // 🔹 دالة جديدة لحساب الإحصاءات العامة للدفعة
        public async Task<(int TotalStudents, int Passed, int Failed, int WithRemedial)> GetBatchStatisticsAsync(int batchId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var results = await _context.StudentIndicatorResults
                .AsNoTracking()
                .Join(_context.StudentBatchEnrollments.AsNoTracking(),
                      r => r.StudentId,
                      sb => sb.StudentID,
                      (r, sb) => new { Result = r, StudentBatch = sb })
                .Where(x => x.StudentBatch.BatchId == batchId)
                .Select(x => new { x.Result.StudentId, x.Result.IsPassed, x.Result.RemedialPlanId })
                .ToListAsync();

            int total = results.Select(r => r.StudentId).Distinct().Count();
            int failed = results.Count(r => !r.IsPassed);
            int passed = results.Count(r => r.IsPassed);
            int withRemedial = results.Count(r => r.RemedialPlanId != null);

            return (total, passed, failed, withRemedial);
        }




    }
}
