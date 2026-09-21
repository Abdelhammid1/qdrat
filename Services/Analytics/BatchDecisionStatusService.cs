using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Admin.Analytics;

namespace QdratNew.Services.Analytics
{
    public class BatchDecisionStatusService : IBatchDecisionStatusService
    {
        private readonly ApplicationDbContext _context;

        public BatchDecisionStatusService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<int, BatchDecisionStatusVM>> GetStatusForBatchesAsync(
            IEnumerable<int> batchIds)
        {
            var ids = batchIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, BatchDecisionStatusVM>();

            // Query 1: all recommendations for these batches (latest first)
            var recommendations = await _context.DecisionRecommendations
                .AsNoTracking()
                .Where(r => r.BatchId.HasValue && ids.Contains(r.BatchId.Value))
                .Select(r => new { r.Id, r.BatchId, r.Status, r.CreatedAt })
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var recommendationIds = recommendations.Select(r => r.Id).ToList();

            // Query 2: active intervention tasks linked to those recommendations
            var activeTasks = await _context.InterventionTasks
                .AsNoTracking()
                .Where(t => t.DecisionRecommendationId.HasValue
                         && recommendationIds.Contains(t.DecisionRecommendationId.Value)
                         && t.Status != "Completed")
                .Select(t => new { t.BatchId, t.DecisionRecommendationId })
                .ToListAsync();

            var result = new Dictionary<int, BatchDecisionStatusVM>(ids.Count);

            foreach (var id in ids)
            {
                var batchRecs = recommendations.Where(r => r.BatchId == id).ToList();
                var latest   = batchRecs.FirstOrDefault();

                result[id] = new BatchDecisionStatusVM
                {
                    BatchId                   = id,
                    HasPendingRecommendation  = batchRecs.Any(r => r.Status is "Pending" or "PendingReview"),
                    HasApprovedRecommendation = batchRecs.Any(r => r.Status == "Approved"),
                    HasActiveInterventionTask = activeTasks.Any(t => t.BatchId == id),
                    LatestRecommendationId    = latest?.Id,
                    LatestDecisionAt          = latest?.CreatedAt
                };
            }

            return result;
        }
    }
}
