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
    /// مهام مراجعة أسئلة البنك — جانب الأدمن (QRT-S2: الإنشاء والمدربون المؤهلون، QRT-S5: المتابعة).
    /// المنطق كله في الخدمات؛ الكنترولر رفيع.
    /// </summary>
    [Area("Admin")]
    public class QuestionReviewTasksController : Controller
    {
        private readonly IQuestionReviewTaskService _service;
        private readonly IQuestionReviewTaskAdminQueryService _adminQuery;
        private readonly UserManager<ApplicationUser> _userManager;

        public QuestionReviewTasksController(
            IQuestionReviewTaskService service,
            IQuestionReviewTaskAdminQueryService adminQuery,
            UserManager<ApplicationUser> userManager)
        {
            _service = service;
            _adminQuery = adminQuery;
            _userManager = userManager;
        }

        // ---------------- QRT-S5: متابعة الأدمن ----------------

        [HttpGet]
        [AdminPermission("QuestionReviewTasks", "Read")]
        public async Task<IActionResult> Index(CancellationToken ct)
            => View(await _adminQuery.GetIndexAsync(ct));

        // DataTables خادمي (≤100 صف/صفحة) — الأرقام من العدادات المخزّنة
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Read")]
        public async Task<IActionResult> LoadTasksData(CancellationToken ct)
        {
            var draw = Request.Form["draw"].ToString();
            var start = int.TryParse(Request.Form["start"], out var st) ? st : 0;
            var length = int.TryParse(Request.Form["length"], out var ln) ? ln : 25;
            var orderColumn = int.TryParse(Request.Form["order[0][column]"], out var oc) ? oc : -1;
            var orderDesc = Request.Form["order[0][dir]"] != "asc";

            var filter = new AdminTasksFilter
            {
                Status = ParseEnum<QuestionReviewTaskStatus>(Request.Form["status"]),
                Priority = ParseEnum<QuestionReviewTaskPriority>(Request.Form["priority"]),
                InstructorId = int.TryParse(Request.Form["instructorId"], out var ins) && ins > 0 ? ins : null,
                CurriculumId = int.TryParse(Request.Form["curriculumId"], out var cur) && cur > 0 ? cur : null,
                OverdueOnly = Request.Form["overdueOnly"] == "true",
                Search = Request.Form["searchTitle"]
            };

            var page = await _adminQuery.GetTasksPageAsync(filter, start, length, orderColumn, orderDesc, ct);
            return Json(new
            {
                draw,
                recordsTotal = page.Total,
                recordsFiltered = page.Filtered,
                data = page.Rows
            });
        }

        [HttpGet]
        [AdminPermission("QuestionReviewTasks", "Read")]
        public async Task<IActionResult> Details(int id, CancellationToken ct)
        {
            var model = await _adminQuery.GetDetailsAsync(id, ct);
            if (model is null)
                return NotFound();

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Read")]
        public async Task<IActionResult> LoadItemsData(CancellationToken ct)
        {
            var draw = Request.Form["draw"].ToString();
            if (!int.TryParse(Request.Form["taskId"], out var taskId))
                return Json(new { draw, recordsTotal = 0, recordsFiltered = 0, data = Array.Empty<object>() });

            var start = int.TryParse(Request.Form["start"], out var st) ? st : 0;
            var length = int.TryParse(Request.Form["length"], out var ln) ? ln : 25;
            int? statusFilter = int.TryParse(Request.Form["statusFilter"], out var sf) ? sf : null;
            string? search = Request.Form["searchTitle"];

            var page = await _adminQuery.GetItemsPageAsync(taskId, start, length, statusFilter, search, ct);
            if (page is null)
                return Json(new { draw, recordsTotal = 0, recordsFiltered = 0, data = Array.Empty<object>() });

            return Json(new
            {
                draw,
                recordsTotal = page.Total,
                recordsFiltered = page.Filtered,
                data = page.Rows
            });
        }

        // ---------------- QRT-S6: إدارة دورة الحياة (كلها Manage + POST + Antiforgery) ----------------

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Manage")]
        public Task<IActionResult> ResolveReturned([FromForm] ResolveReturnedInput input, CancellationToken ct)
            => RunManageAsync(input, (actor) => _service.ResolveReturnedAsync(
                input.ItemId, (ReturnResolution)input.Resolution, input.Note, actor, ct));

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Manage")]
        public Task<IActionResult> RemoveItems([FromForm] RemoveReviewItemsInput input, CancellationToken ct)
            => RunManageAsync(input, (actor) => _service.RemoveItemsAsync(input.TaskId, input.ItemIds, actor, ct));

        // المدربون المؤهلون لاستلام المتبقي (قراءة فقط لكنها جزء من تدفق إعادة الإسناد)
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Manage")]
        public async Task<IActionResult> ReassignCandidates([FromForm] int taskId, CancellationToken ct)
        {
            if (taskId <= 0)
                return Json(new { success = false, message = "⚠️ مهمة غير صالحة." });

            var result = await _service.GetReassignCandidatesAsync(taskId, ct);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Manage")]
        public Task<IActionResult> Reassign([FromForm] ReassignReviewTaskInput input, CancellationToken ct)
            => RunManageAsync(input, (actor) => _service.ReassignRemainingAsync(input.TaskId, input.NewInstructorId, actor, ct));

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Manage")]
        public Task<IActionResult> Cancel([FromForm] CancelReviewTaskInput input, CancellationToken ct)
            => RunManageAsync(input, (actor) => _service.CancelTaskAsync(input.TaskId, input.Reason, actor, ct));

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Manage")]
        public async Task<IActionResult> Close([FromForm] int taskId, CancellationToken ct)
        {
            if (taskId <= 0)
                return Json(new { success = false, message = "⚠️ مهمة غير صالحة." });

            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var result = await _service.CloseTaskAsync(taskId, actor, ct);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("QuestionReviewTasks", "Manage")]
        public Task<IActionResult> ExtendDue([FromForm] ExtendReviewTaskDueInput input, CancellationToken ct)
            => RunManageAsync(input, (actor) => _service.ExtendDueAsync(
                input.TaskId, input.DueAtLocal.HasValue ? QuestionReviewTaskMetrics.LocalToUtc(input.DueAtLocal.Value) : null, actor, ct));

        // نمط واحد لكل إجراءات الإدارة: ModelState ← المستخدم الحالي ← الخدمة ← JSON بشكل ثابت
        private async Task<IActionResult> RunManageAsync<TInput>(TInput? input, Func<ReviewActor, Task<OperationResult>> operation)
            where TInput : class
        {
            if (input is null || !ModelState.IsValid)
                return Json(new { success = false, message = BuildValidationMessage() });

            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var result = await operation(actor);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        private static TEnum? ParseEnum<TEnum>(string? raw) where TEnum : struct, Enum
            => int.TryParse(raw, out var n) && Enum.IsDefined(typeof(TEnum), n) ? (TEnum)(object)n : null;

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
