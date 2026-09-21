using Microsoft.AspNetCore.Mvc;
using QdratNew.Modules.QuestionBank.Read.Contracts;
using QdratNew.Modules.QuestionBank.Read.Dtos;

namespace QdratNew.Modules.QuestionBank.Read.Api
{
    [ApiController]
    [Route("internal-api/question-bank")]
    public class QuestionBankReadController : ControllerBase
    {
        private readonly IQuestionBankReadService _service;

        public QuestionBankReadController(IQuestionBankReadService service)
        {
            _service = service;
        }

        [HttpPost("query")]
        public async Task<IActionResult> Query([FromBody] QuestionBankQueryRequestDto request)
        {
            var result = await _service.QueryAsync(request);
            return Ok(result);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> Stats(
            [FromQuery] int? curriculumId,
            [FromQuery] int? sectionId)
        {
            var result = await _service.GetStatsAsync(curriculumId, sectionId);
            return Ok(result);
        }
    }
}
