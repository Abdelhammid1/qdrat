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
    public class InstitutePlanFollowUpController : ParentBaseController
    {
        private readonly IInstitutePlanFollowUpService _planService;

        public InstitutePlanFollowUpController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService,
            IInstitutePlanFollowUpService planService)
            : base(contextFactory, userManager, parentAccessService)
        {
            _planService = planService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? studentId)
        {
            int sid = studentId ?? SelectedStudentId ?? 0;
            if (sid == 0) return RedirectToAction("Index", "Children");

            var userId = _userManager.GetUserId(User)!;
            var vm = await _planService.GetFollowUpAsync(userId, sid);
            return View(vm);
        }
    }
}
