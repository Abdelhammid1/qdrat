using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Parents.Controllers.Base;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Parents.Controllers
{
    public class ChildrenController : ParentBaseController
    {
        public ChildrenController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IParentAccessService parentAccessService)
            : base(contextFactory, userManager, parentAccessService)
        { }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            using var db = _contextFactory.CreateDbContext();

            var children = await db.Students
                .AsNoTracking()
                .Where(s => s.ParentId == ParentId)
                .Select(s => new ParentChildCardViewModel
                {
                    StudentId = s.StudentID,
                    StudentName = s.FullName,
                    Level = s.Level,
                    School = s.School,
                    OverallStatus = "مستقر",
                    StatusColor = "success",
                    CommitmentPercent = 0
                })
                .ToListAsync();

            return View(children);
        }
    }
}
