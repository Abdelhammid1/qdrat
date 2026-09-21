using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Entities;
using QdratNew.Services.Exams.Models;
using QdratNew.Services.Instructors.Exams.Interfaces;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Areas.Instructors.Controllers.Exam
{
    [Area("Instructors")]
    public class InstructorExamGenerateController : BaseInstructorController
    {
        private readonly IInstructorExamGenerationService _generationService;

        public InstructorExamGenerateController(
            IInstructorExamGenerationService generationService,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _generationService = generationService;
        }

        [HttpGet]
        public async Task<IActionResult> Generate()
        {
            await RequireInstructorAsync();
            return View(new GenerateExamRequestVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Preview(GenerateExamRequestVM request)
        {
            await RequireInstructorAsync();

            if (!ModelState.IsValid)
                return View("Generate", request);

            GeneratedExamResult result;

            try
            {
                result = _generationService.GeneratePreview(request, CurrentInstructorId);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View("Generate", request);
            }

            TempData["GeneratedExam"] =
                System.Text.Json.JsonSerializer.Serialize(result);

            return View("PreviewGenerated", result);
        }
    }
}