using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Services.QuestionReviewTasks;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Areas.Admin.Controllers
{
    /// <summary>
    /// مهام مراجعة أسئلة البنك — جانب الأدمن (QRT-S2: الإنشاء والمدربون المؤهلون).
    /// المنطق كله في IQuestionReviewTaskService؛ الكنترولر رفيع.
    /// </summary>
    [Area("Admin")]
    public class QuestionReviewTasksController : Controller
    {
        private readonly IQuestionReviewTaskService _service;
        private readonly UserManager<ApplicationUser> _userManager;

        public QuestionReviewTasksController(
            IQuestionReviewTaskService service,
            UserManager<ApplicationUser> userManager)
        {
            _service = service;
            _userManager = userManager;
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Create")]
        public async Task<IActionResult> Create([FromForm] CreateQuestionReviewTaskInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = BuildValidationMessage() });

            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var result = await _service.CreateTaskAsync(input, actor, ct);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        // يعيد المدربين المؤهلين لمناهج الأسئلة المحددة (أو لأول N حسب الفلتر)
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Create")]
        public async Task<IActionResult> EligibleInstructors([FromForm] EligibleInstructorsInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = BuildValidationMessage() });

            var result = await _service.GetEligibleInstructorsAsync(input, ct);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        private string BuildValidationMessage()
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .Take(3)
                .ToList();

            return errors.Count == 0
                ? "⚠️ بيانات المهمة غير مكتملة."
                : "⚠️ " + string.Join(" • ", errors);
        }

        private async Task<ReviewActor?> ActorAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
                return null;

            var role = User.IsInRole("Developer") ? UserRoleType.Developer :
                       User.IsInRole("SuperAdmin") ? UserRoleType.SuperAdmin :
                       User.IsInRole("Owner") ? UserRoleType.Owner :
                       User.IsInRole("Admin") ? UserRoleType.Admin :
                       UserRoleType.Unknown;

            return new ReviewActor(user.Id, user.FullName ?? user.UserName ?? "SYSTEM", role);
        }
    }
}
