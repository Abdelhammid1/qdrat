using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Parents.Controllers.Base;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Parents.Interfaces;
using System.Threading.Tasks;

namespace QdratNew.Areas.Parents.Controllers
{
    public class ReportsController : ParentBaseController
    {
        private readonly IParentSafeInsightService _insightService;

        public ReportsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService,
            IParentSafeInsightService insightService)
            : base(contextFactory, userManager, parentAccessService)
        {
            _insightService = insightService;
        }

        [HttpGet]
        public async Task<IActionResult> Weekly(int? studentId)
        {
            int sid = studentId ?? SelectedStudentId ?? 0;
            if (sid == 0) return RedirectToAction("Index", "Children");

            var vm = await _insightService.GetWeeklyReportAsync(sid);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Monthly(int? studentId)
        {
            int sid = studentId ?? SelectedStudentId ?? 0;
            if (sid == 0) return RedirectToAction("Index", "Children");

            var vm = await _insightService.GetMonthlyReportAsync(sid);
            return View(vm);
        }
    }
}
