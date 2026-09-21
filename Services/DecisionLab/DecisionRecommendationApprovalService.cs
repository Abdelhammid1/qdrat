using System.Text.Encodings.Web;
using System.Text.Json;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Admin.DecisionLab;

namespace QdratNew.Services.DecisionLab
{
    public class DecisionRecommendationApprovalService : IDecisionRecommendationApprovalService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = null,
            WriteIndented = true
        };

        private readonly ApplicationDbContext _context;
        private readonly IDecisionLabAnalysisService _analysisService;
        private readonly IDecisionRecommendationService _recommendationService;

        public DecisionRecommendationApprovalService(
            ApplicationDbContext context,
            IDecisionLabAnalysisService analysisService,
            IDecisionRecommendationService recommendationService)
        {
            _context = context;
            _analysisService = analysisService;
            _recommendationService = recommendationService;
        }

        public async Task<int> SaveForReviewAsync(
            int batchId,
            int? curriculumId,
            string? recommendationCode,
            string requestedByUserId,
            DecisionRecommendationSourceContextViewModel? sourceContext = null)
        {
            if (batchId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(batchId));
            }

            if (string.IsNullOrWhiteSpace(requestedByUserId))
            {
                throw new ArgumentException("Requested user id is required.", nameof(requestedByUserId));
            }

            var analysis = await _analysisService.AnalyzeBatchAsync(
                batchId,
                curriculumId,
                fromDate: null,
                toDate: null);

            if (analysis == null)
            {
                throw new InvalidOperationException("DecisionLab analysis was not found for the selected batch.");
            }

            var recommendations = await _recommendationService.BuildRecommendationsAsync(analysis);
            var preview = recommendations.FirstOrDefault(item =>
                string.IsNullOrWhiteSpace(recommendationCode)
                || item.Code == recommendationCode);

            if (preview == null)
            {
                throw new InvalidOperationException("DecisionLab recommendation was not found for the selected batch.");
            }

            var details = await _recommendationService.BuildRecommendationDetailsAsync(
                analysis,
                preview.Code);

            details.SourceContext = NormalizeSourceContext(sourceContext);

            var entity = new DecisionRecommendation
            {
                SourceType = "DecisionLab",
                EngineType = "RuleBased",
                RecommendationType = preview.InterventionType,
                BatchId = batchId,
                CurriculumId = curriculumId,
                InputSnapshotJson = JsonSerializer.Serialize(analysis, JsonOptions),
                RecommendationJson = JsonSerializer.Serialize(details, JsonOptions),
                Summary = preview.Title,
                Reason = preview.Reason,
                Status = "PendingReview",
                RequestedByUserId = requestedByUserId,
                CreatedAt = DateTime.Now
            };

            _context.DecisionRecommendations.Add(entity);
            await _context.SaveChangesAsync();

            return entity.Id;
        }

        private static DecisionRecommendationSourceContextViewModel NormalizeSourceContext(
            DecisionRecommendationSourceContextViewModel? sourceContext)
        {
            var context = sourceContext ?? new DecisionRecommendationSourceContextViewModel();

            context.SourceArea = Clean(context.SourceArea, "Admin");
            context.SourceController = Clean(context.SourceController, "DecisionLab");
            context.SourceAction = Clean(context.SourceAction, "RecommendationPreview");
            context.SourceMetric = Clean(context.SourceMetric, string.Empty);
            context.SourcePage = Clean(context.SourcePage, context.SourceAction);
            context.ReturnUrl = Clean(context.ReturnUrl, string.Empty);
            context.CapturedAt = DateTime.Now;

            return context;
        }

        private static string Clean(string? value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim();
        }

        public async Task ApproveAsync(int id, string approvedByUserId)
        {
            if (id <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(approvedByUserId))
            {
                throw new ArgumentException("Approved user id is required.", nameof(approvedByUserId));
            }

            var recommendation = await _context.DecisionRecommendations.FindAsync(id);
            if (recommendation == null)
            {
                throw new InvalidOperationException("DecisionLab recommendation was not found.");
            }

            if (recommendation.Status != "PendingReview")
            {
                throw new InvalidOperationException("Only pending DecisionLab recommendations can be approved.");
            }

            recommendation.Status = "Approved";
            recommendation.ApprovedByUserId = approvedByUserId;
            recommendation.ApprovedAt = DateTime.Now;

            await _context.SaveChangesAsync();
        }
    }
}
