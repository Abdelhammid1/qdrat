using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Route("Students/[controller]/[action]")]
    public class RemedialPlansController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RemedialPlansController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetCurrent()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (student == null) return NotFound("الطالب غير موجود");

            var plan = await _context.RemedialPlans
        .FirstOrDefaultAsync(p => p.StudentID == student.StudentID && !(p.IsCompleted ?? false));

            if (plan == null)
                return Content("<div class='alert alert-warning'>لا توجد خطة علاجية حالية</div>", "text/html");

            var viewModel = new RemedialPlanMiniViewModel
            {
                Title = plan.Title,
                PerformanceLevel = plan.PerformanceLevel,
                StartDate = plan.StartDate ?? DateTime.MinValue,
                EndDate = plan.EndDate ?? DateTime.MinValue,
                Recommendations = plan.Recommendations,
                CompletionPercentage = plan.CompletionPercentage
            };

            return PartialView("_RemedialPlanPartial", viewModel);
        }
    }

}
