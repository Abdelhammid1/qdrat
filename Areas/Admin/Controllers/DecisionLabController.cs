using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.DecisionLab;
using QdratNew.ViewModels.Admin.DecisionLab;
using System.Security.Claims;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Owner,Developer")]
    public class DecisionLabController : Controller
    {
        private readonly IDecisionLabAnalysisService _analysisService;
        private readonly IDecisionRecommendationService _recommendationService;
        private readonly IDecisionRecommendationApprovalService _approvalService;
        private readonly IInterventionTaskService _interventionTaskService;
        private readonly ApplicationDbContext _context;

        public DecisionLabController(
            IDecisionLabAnalysisService analysisService,
            IDecisionRecommendationService recommendationService,
            IDecisionRecommendationApprovalService approvalService,
            IInterventionTaskService interventionTaskService,
            ApplicationDbContext context)
        {
            _analysisService = analysisService;
            _recommendationService = recommendationService;
            _approvalService = approvalService;
            _interventionTaskService = interventionTaskService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? batchId, int? curriculumId)
        {
            var model = await _analysisService.BuildIndexAsync(
                batchId,
                curriculumId,
                fromDate: null,
                toDate: null);

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> BatchAnalysis(int batchId, int? curriculumId)
        {
            if (batchId <= 0)
            {
                return BadRequest();
            }

            var model = await _analysisService.AnalyzeBatchAsync(
                batchId,
                curriculumId,
                fromDate: null,
                toDate: null);

            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> RecommendationPreview(
            int batchId,
            int? curriculumId,
            int? savedRecommendationId,
            string? sourceArea,
            string? sourceController,
            string? sourceAction,
            string? sourceMetric,
            string? sourcePage,
            string? returnUrl)
        {
            if (batchId <= 0)
            {
                return BadRequest();
            }

            if (string.IsNullOrWhiteSpace(returnUrl) || !IsSafeLocalUrl(returnUrl))
            {
                returnUrl = Url.Action(nameof(Index), "DecisionLab", new { area = "Admin" });
            }

            var analysis = await _analysisService.AnalyzeBatchAsync(
                batchId,
                curriculumId,
                fromDate: null,
                toDate: null);

            if (analysis == null)
            {
                return NotFound();
            }

            var recommendations = await _recommendationService.BuildRecommendationsAsync(analysis);
            var model = recommendations.FirstOrDefault();

            if (model != null)
            {
                model.SavedRecommendationId = savedRecommendationId;
                model.SourceContext = BuildSourceContext(
                    sourceArea,
                    sourceController,
                    sourceAction,
                    sourceMetric,
                    sourcePage,
                    returnUrl);
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SavedRecommendations()
        {
            var recommendations = await _context.DecisionRecommendations
                .AsNoTracking()
                .OrderByDescending(item => item.CreatedAt)
                .Take(100)
                .ToListAsync();

            return View(recommendations);
        }

        [HttpGet]
        public async Task<IActionResult> RecommendationDetails(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            var recommendation = await _context.DecisionRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (recommendation == null)
            {
                return NotFound();
            }

            var existingTaskId = await _interventionTaskService.GetTaskIdForRecommendationAsync(recommendation.Id);
            var existingTask = existingTaskId.HasValue
                ? await _interventionTaskService.GetDetailsAsync(existingTaskId.Value)
                : null;

            var model = new DecisionRecommendationDetailsPageViewModel
            {
                Recommendation = recommendation,
                ExistingInterventionTaskId = existingTaskId,
                ExistingInterventionTask = existingTask,
                CanCreateInterventionTask = recommendation.Status == "Approved" && !existingTaskId.HasValue
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveRecommendation(
            int batchId,
            int? curriculumId,
            string? recommendationCode,
            string? sourceArea,
            string? sourceController,
            string? sourceAction,
            string? sourceMetric,
            string? sourcePage,
            string? returnUrl)
        {
            if (batchId <= 0)
            {
                return BadRequest();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var savedRecommendationId = await _approvalService.SaveForReviewAsync(
                batchId,
                curriculumId,
                recommendationCode,
                userId,
                BuildSourceContext(
                    sourceArea,
                    sourceController,
                    sourceAction,
                    sourceMetric,
                    sourcePage,
                    returnUrl));

            return RedirectToAction(
                nameof(RecommendationPreview),
                new
                {
                    batchId,
                    curriculumId,
                    savedRecommendationId,
                    sourceArea,
                    sourceController,
                    sourceAction,
                    sourceMetric,
                    sourcePage,
                    returnUrl
                });
        }

        private DecisionRecommendationSourceContextViewModel BuildSourceContext(
            string? sourceArea,
            string? sourceController,
            string? sourceAction,
            string? sourceMetric,
            string? sourcePage,
            string? returnUrl)
        {
            return new DecisionRecommendationSourceContextViewModel
            {
                SourceArea = string.IsNullOrWhiteSpace(sourceArea) ? "Admin" : sourceArea.Trim(),
                SourceController = string.IsNullOrWhiteSpace(sourceController) ? "DecisionLab" : sourceController.Trim(),
                SourceAction = string.IsNullOrWhiteSpace(sourceAction) ? nameof(RecommendationPreview) : sourceAction.Trim(),
                SourceMetric = sourceMetric?.Trim() ?? string.Empty,
                SourcePage = string.IsNullOrWhiteSpace(sourcePage) ? nameof(RecommendationPreview) : sourcePage.Trim(),
                ReturnUrl = IsSafeLocalUrl(returnUrl) ? returnUrl!.Trim() : string.Empty
            };
        }

        private bool IsSafeLocalUrl(string? returnUrl)
        {
            return !string.IsNullOrWhiteSpace(returnUrl)
                && Url.IsLocalUrl(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveRecommendation(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            await _approvalService.ApproveAsync(id, userId);

            // استرجع returnUrl من RecommendationJson المحفوظ إن وُجد
            var savedRec = await _context.DecisionRecommendations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            string? returnUrl = null;
            if (!string.IsNullOrWhiteSpace(savedRec?.RecommendationJson))
            {
                try
                {
                    var details = System.Text.Json.JsonSerializer
                        .Deserialize<QdratNew.ViewModels.Admin.DecisionLab.DecisionLabRecommendationDetailsViewModel>(
                            savedRec.RecommendationJson);
                    returnUrl = details?.SourceContext?.ReturnUrl;
                }
                catch { /* ignore deserialization errors */ }
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                TempData["ApprovalSuccess"] = "تمت الموافقة على التوصية بنجاح.";
                return Redirect(returnUrl);
            }

            return RedirectToAction(
                nameof(RecommendationDetails),
                new { id });
        }

        [HttpGet]
        public async Task<IActionResult> CreateInterventionTask(int decisionRecommendationId)
        {
            if (decisionRecommendationId <= 0)
            {
                return BadRequest();
            }

            var model = await _interventionTaskService.BuildCreateModelAsync(decisionRecommendationId);
            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateInterventionTask(CreateInterventionTaskViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var rebuiltModel = await _interventionTaskService.RebuildCreateModelAsync(model);
                if (rebuiltModel == null)
                {
                    return NotFound();
                }

                return View(rebuiltModel);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var taskId = await _interventionTaskService.CreateAsync(model, userId);

            return RedirectToAction(
                nameof(InterventionTaskDetails),
                new { id = taskId });
        }

        [HttpGet]
        public async Task<IActionResult> InterventionTasks()
        {
            var model = await _interventionTaskService.GetListAsync();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> InterventionTaskDetails(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            var model = await _interventionTaskService.GetDetailsAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }
    }
}
