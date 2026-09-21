using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Modules.QuestionBank.Lookups.Contracts;

namespace QdratNew.Modules.QuestionBank.Lookups.Api
{
    [ApiController]
    [Route("internal-api/question-bank/lookups")]
    [Authorize(Roles = "Owner,Developer")]
    public class QuestionBankLookupController : ControllerBase
    {
        private readonly IQuestionBankLookupService _service;

        public QuestionBankLookupController(IQuestionBankLookupService service)
        {
            _service = service;
        }

        [HttpGet("curriculums")]
        public async Task<IActionResult> GetCurriculums()
        {
            return Ok(await _service.GetCurriculumsAsync());
        }

        [HttpGet("sections")]
        public async Task<IActionResult> GetSections([FromQuery] int curriculumId)
        {
            return Ok(await _service.GetSectionsByCurriculumAsync(curriculumId));
        }

        [HttpGet("lessons")]
        public async Task<IActionResult> GetLessons([FromQuery] int sectionId)
        {
            return Ok(await _service.GetLessonsBySectionAsync(sectionId));
        }
    }
}
