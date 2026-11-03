using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.HomeworkDraft.Interfaces;
using QdratNew.Services.ProfessionalModels;
using QdratNew.ViewModels.Partner.Homework;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [QdratNew.Attributes.PartnerFeature(PartnerFeatureType.Homework)] // PPL-S1.4
    public class HomeworkGenerationController : PartnerBaseController
    {
        private readonly IHomeworkDraftService _draftService;
        private readonly IProfessionalModelAvailabilityService _availabilityService;

        public HomeworkGenerationController(
            IHomeworkDraftService draftService,
            IProfessionalModelAvailabilityService availabilityService,
            IPartnerSubscriptionService subscriptionService,
            ApplicationDbContext context)
            : base(subscriptionService, context)
        {
            _draftService = draftService;
            _availabilityService = availabilityService;
        }

        // =========================
        // 🎓 Generate From Professional Model
        // =========================
        [HttpGet]
        public async Task<IActionResult> FromProfessionalModel(int modelId)
        {
            // PPM-S1: نفس قاعدة الإتاحة، لا يكفي أن يكون النموذج غير مؤرشف
            var modelIsAvailable = await _availabilityService
                .IsAvailableForPartnerAsync(modelId, ActivePartnerId, ProfessionalModelType.Homework);

            if (!modelIsAvailable)
            {
                TempData["Error"] = "النموذج الاحترافي غير متاح أو مؤرشف.";
                return RedirectToAction("Index", "ProfessionalModels");
            }

            var model = new HomeworkGenerateFromProfessionalModelVM
            {
                ProfessionalModelId = modelId,
                Courses = AllowedCourses
                    .Select(c => new SelectListItem
                    {
                        Value = c.CourseId.ToString(),
                        Text = c.CourseName
                    })
                    .ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FromProfessionalModel(
            HomeworkGenerateFromProfessionalModelVM model)
        {
            var modelIsAvailable = await _availabilityService
                .IsAvailableForPartnerAsync(model.ProfessionalModelId, ActivePartnerId, ProfessionalModelType.Homework);

            if (!modelIsAvailable)
            {
                TempData["Error"] = "النموذج الاحترافي غير متاح أو مؤرشف.";
                return RedirectToAction("Index", "ProfessionalModels");
            }

            if (!ModelState.IsValid)
                return View(model);

            var draftId = await _draftService.GenerateDraftFromProfessionalModel(
                ActivePartnerId,
                ActiveSubscriptionPeriodId,
                model);

            return RedirectToAction(
                "Preview",
                "HomeworkDrafts",
                new { id = draftId });
        }

        // =========================
        // ⚙️ Auto Generate
        // =========================
        [HttpGet]
        public IActionResult AutoGenerate(int courseId)
        {
            return View(new HomeworkAutoGenerateVM
            {
                CourseId = courseId
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoGenerate(HomeworkAutoGenerateVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var draftId = await _draftService.GenerateAutoDraft(
                ActivePartnerId,
                ActiveSubscriptionPeriodId,
                model);

            return RedirectToAction(
                "Preview",
                "HomeworkDrafts",
                new { id = draftId });
        }
    }

}
