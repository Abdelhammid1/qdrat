using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.HomeworkDraft.Interfaces;
using QdratNew.ViewModels.Partner.Homework;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public class HomeworkGenerationController : PartnerBaseController
    {
        private readonly IHomeworkDraftService _draftService;

        public HomeworkGenerationController(
            IHomeworkDraftService draftService,
            IPartnerSubscriptionService subscriptionService,
            ApplicationDbContext context)
            : base(subscriptionService, context)
        {
            _draftService = draftService;
        }

        // =========================
        // 🎓 Generate From Professional Model
        // =========================
        [HttpGet]
        public IActionResult FromProfessionalModel(int modelId)
        {
            var modelIsAvailable = _context.ProfessionalModels
                .Any(m => m.Id == modelId && !m.IsArchived && m.ModelType == ProfessionalModelType.Homework);

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
        public IActionResult FromProfessionalModel(
            HomeworkGenerateFromProfessionalModelVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var modelIsAvailable = _context.ProfessionalModels
                .Any(m => m.Id == model.ProfessionalModelId && !m.IsArchived && m.ModelType == ProfessionalModelType.Homework);

            if (!modelIsAvailable)
            {
                TempData["Error"] = "النموذج الاحترافي غير متاح أو مؤرشف.";
                return RedirectToAction("Index", "ProfessionalModels");
            }

            var draftId = _draftService.GenerateDraftFromProfessionalModel(
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
        public IActionResult AutoGenerate(HomeworkAutoGenerateVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var draftId = _draftService.GenerateAutoDraft(
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
