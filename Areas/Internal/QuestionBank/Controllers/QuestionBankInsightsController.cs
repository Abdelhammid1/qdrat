using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Modules.QuestionBank.Insights.Services;

namespace QdratNew.Areas.Internal.QuestionBank.Controllers
{
    [Area("Internal")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,DataEntry")]
    [Route("internal/question-bank/insights")]
    public class QuestionBankInsightsController : Controller
    {
        private readonly QuestionBankInsightsService _service;

        public QuestionBankInsightsController(QuestionBankInsightsService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get(
            int? curriculumId,
            int? sectionId,
            int? lessonId)
        {
            var result = await _service.GetInsightsAsync(
                curriculumId,
                sectionId,
                lessonId);

            return Json(result);
        }
    }
}
