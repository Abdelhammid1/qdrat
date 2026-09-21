using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Modules.QuestionBank.Read.Contracts;
using QdratNew.Modules.QuestionBank.Read.Dtos;
using System.Diagnostics;

namespace QdratNew.Modules.QuestionBank.Read.Api
{
    [ApiController]
    [Route("internal-api/question-bank/diagnostics")]
    [Authorize(Roles = "Owner,Developer")]
    public class QuestionBankDiagnosticsController : ControllerBase
    {
        private readonly IQuestionBankReadService _service;

        public QuestionBankDiagnosticsController(IQuestionBankReadService service)
        {
            _service = service;
        }

        /// <summary>
        /// Diagnostic query for performance & data distribution
        /// </summary>
        /// Example:
        /// /internal-api/question-bank/diagnostics?curriculumId=15&sectionId=45
        [HttpGet]
        public async Task<IActionResult> Diagnose(
            [FromQuery] int? curriculumId,
            [FromQuery] int? sectionId)
        {
            var stopwatch = Stopwatch.StartNew();

            // 🔹 نطلب صفحة واحدة فقط (اختبار أداء)
            var queryResult = await _service.QueryAsync(new QuestionBankQueryRequestDto
            {
                CurriculumId = curriculumId,
                SectionId = sectionId,
                Page = 1,
                PageSize = 50,
                IncludeIncomplete = true,
                IncludeWithoutAnswer = true
            });

            // 🔹 إحصائيات كاملة
            var stats = await _service.GetStatsAsync(curriculumId, sectionId);

            stopwatch.Stop();

            var result = new QuestionBankDiagnosticsDto
            {
                TotalQuestions = stats.TotalQuestions,

                IncompleteCount = stats.IncompleteCount,
                MissingAnswerCount = stats.MissingAnswerCount,
                ReadyForReviewCount = stats.ReadyForReviewCount,
                ApprovedCount = stats.ApprovedCount,

                QueryExecutionMilliseconds = stopwatch.Elapsed.TotalMilliseconds,

                PageSize = 50,
                ReturnedItems = queryResult.Items.Count,

                GeneratedAt = DateTime.UtcNow
            };

            return Ok(result);
        }
    }
}
