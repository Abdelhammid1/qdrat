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
    public class DashboardController : ParentBaseController
    {
        private readonly IParentDashboardService _dashboardService;

        public DashboardController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService,
            IParentDashboardService dashboardService)
            : base(contextFactory, userManager, parentAccessService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? studentId)
        {
            var userId = _userManager.GetUserId(User)!;
            var vm = await _dashboardService.GetDashboardAsync(userId, studentId ?? SelectedStudentId);
            return View(vm);
        }

        [HttpPost]
        public IActionResult SwitchStudent(int studentId, string returnUrl = "/Parents/Dashboard")
        {
            HttpContext.Session.SetString("ParentSelectedStudent", studentId.ToString());
            return Redirect(returnUrl + "?studentId=" + studentId);
        }
    }
}
