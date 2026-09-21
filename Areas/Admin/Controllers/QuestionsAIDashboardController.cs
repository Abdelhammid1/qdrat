using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Services.AI;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class QuestionsAIDashboardController : Controller
    {
        private readonly AIQuestionAnalyzer _analyzer;

        public QuestionsAIDashboardController(AIQuestionAnalyzer analyzer)
        {
            _analyzer = analyzer;
        }

        public async Task<IActionResult> Index()
        {
            var data = await _analyzer.AnalyzeAsync();
            return View(data); // ✅ تمرير ViewModel للصفحة
        }


        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            try
            {
                var data = await _analyzer.AnalyzeAsync();
                return Json(data);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message, stack = ex.StackTrace });
            }
        }

    }
}
