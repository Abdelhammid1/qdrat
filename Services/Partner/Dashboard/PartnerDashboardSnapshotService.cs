using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Partner.Dashboard;
using QdratNew.ViewModels.Partner;
using System;
using System.Linq;

namespace QdratNew.Services.Partner.Dashboard
{
    public class PartnerDashboardSnapshotService : IPartnerDashboardSnapshotService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public PartnerDashboardSnapshotService(
            ApplicationDbContext context,
            IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public PartnerDashboardSnapshotVM GetSnapshot(int partnerId)
        {
            var cacheKey = $"PARTNER_DASHBOARD_SNAPSHOT_{partnerId}";

            if (_cache.TryGetValue(cacheKey, out PartnerDashboardSnapshotVM vm))
            {
                return vm;
            }

            // =====================================================
            // 🧱 KPIs
            // =====================================================

            var activeBatches = _context.Batches
                .Where(b => b.Branch.PartnerId == partnerId);

            var activeBatchesCount = activeBatches.Count();

            var totalStudents = (
                from sbe in _context.StudentBatchEnrollments
                join b in _context.Batches on sbe.BatchId equals b.Id
                where b.Branch.PartnerId == partnerId
                select sbe.StudentID
            ).Distinct().Count();

            var totalSentExams = (
                from ea in _context.ExamAssignmentsToBatches
                join b in _context.Batches on ea.BatchId equals b.Id
                where ea.IsSentToStudents
                      && b.Branch.PartnerId == partnerId
                select ea.Id
            ).Count();

            var totalSolvedExams = (
                from es in _context.ExamStudentStatuses
                join ea in _context.ExamAssignmentsToBatches
                    on es.ExamAssignmentId equals ea.Id
                join b in _context.Batches on ea.BatchId equals b.Id
                where b.Branch.PartnerId == partnerId
                      && (es.Status == ExamStatus.Completed || es.IsSubmitted)
                select es.Id
            ).Count();

            var totalUnsolvedExams = (
                from es in _context.ExamStudentStatuses
                join ea in _context.ExamAssignmentsToBatches
                    on es.ExamAssignmentId equals ea.Id
                join b in _context.Batches on ea.BatchId equals b.Id
                where b.Branch.PartnerId == partnerId
                      && es.Status == ExamStatus.Pending
                select es.Id
            ).Count();

            // =====================================================
            // 📈 Average Score (EF-safe)
            // =====================================================

            var scores = (
                from es in _context.ExamStudentStatuses
                join ea in _context.ExamAssignmentsToBatches
                    on es.ExamAssignmentId equals ea.Id
                join b in _context.Batches on ea.BatchId equals b.Id
                where b.Branch.PartnerId == partnerId
                      && es.Score.HasValue
                select es.Score.Value
            ).ToList();

            var avgScore = scores.Any()
                ? Math.Round(scores.Average(), 1)
                : 0;

            vm = new PartnerDashboardSnapshotVM
            {
                TotalStudents = totalStudents,
                ActiveBatches = activeBatchesCount,

                TotalSentExams = totalSentExams,
                TotalSolvedExams = totalSolvedExams,
                TotalUnsolvedExams = totalUnsolvedExams,

                AverageScorePercent = avgScore,
                GeneratedAt = DateTime.UtcNow
            };

            _cache.Set(cacheKey, vm, TimeSpan.FromMinutes(5));

            return vm;
        }

        public void Invalidate(int partnerId)
        {
            var cacheKey = $"PARTNER_DASHBOARD_SNAPSHOT_{partnerId}";
            _cache.Remove(cacheKey);
        }
    }
}
