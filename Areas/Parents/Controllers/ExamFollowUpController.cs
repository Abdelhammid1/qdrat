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
    public class ExamFollowUpController : ParentBaseController
    {
        private readonly IParentSafeInsightService _insightService;

        public ExamFollowUpController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService,
            IParentSafeInsightService insightService)
            : base(contextFactory, userManager, parentAccessService)
        {
            _insightService = insightService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? studentId)
        {
            int sid = studentId ?? SelectedStudentId ?? 0;
            if (sid == 0) return RedirectToAction("Index", "Children");

            var vm = await _insightService.GetExamFollowUpAsync(sid);
            return View(vm);
        }
    }
}
