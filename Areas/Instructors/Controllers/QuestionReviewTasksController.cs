using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.Services.QuestionReviewTasks;
using QdratNew.ViewModels.Question;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Areas.Instructors.Controllers
{
    /// <summary>
    /// مهام مراجعة الأسئلة — جانب المدرب (QRT-S3): مهامي، صفحة المراجعة، اعتماد، إرجاع للإدارة.
    /// المدرب يُحدَّد دائمًا من الجلسة في الخادم (D6) ولا يُقبل من العميل.
    /// </summary>
    public class QuestionReviewTasksController : BaseInstructorController
    {
        private const string NoInstructorMessage = "🚫 حسابك غير مرتبط بسجل مدرب.";

        private readonly IQuestionReviewTaskService _service;
        private readonly IQuestionReviewTaskQueryService _query;

        public QuestionReviewTasksController(
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService,
            IQuestionReviewTaskService service,
            IQuestionReviewTaskQueryService query)
            : base(userManager, scopeService)
        {
            _service = service;
            _query = query;
        }

        // ─── مهامي ───────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Index(QuestionReviewTaskStatus? status, CancellationToken ct)
        {
            var instructorId = await RequireInstructorAsync();

            // الأدمن/المالك بدون سجل مدرب لا يراجع من هنا: حالة فارغة بدل خطأ
            var vm = instructorId == 0
                ? new InstructorTasksIndexVm { StatusFilter = status }
                : await _query.GetInstructorTasksAsync(instructorId, status, ct);

            return View(vm);
        }

        // ─── صفحة المراجعة ───────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Review(int id, CancellationToken ct)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return NotFound();

            var vm = await _query.GetInstructorTaskReviewAsync(instructorId, id, ct);
            return vm is null ? NotFound() : View(vm);
        }

        // DataTables خادمي (≤100 صف/صفحة)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> LoadItemsData(CancellationToken ct)
        {
            var instructorId = await RequireInstructorAsync();
            var draw = Request.Form["draw"].ToString();
            if (instructorId == 0 || !int.TryParse(Request.Form["taskId"], out var taskId))
                return Json(EmptyTable(draw));

            var start = int.TryParse(Request.Form["start"], out var st) ? st : 0;
            var length = int.TryParse(Request.Form["length"], out var ln) ? ln : 25;
            int? statusFilter = int.TryParse(Request.Form["statusFilter"], out var sf) ? sf : null;
            string? search = Request.Form["searchTitle"];

            var page = await _query.GetItemsPageAsync(instructorId, taskId, start, length, statusFilter, search, ct);
            if (page is null)
                return Json(EmptyTable(draw));

            return Json(new
            {
                draw,
                recordsTotal = page.Total,
                recordsFiltered = page.Filtered,
                data = page.Rows,
                summary = page.Progress,
                taskStatus = (int)page.TaskStatus
            });
        }

        // ─── معاينة سؤال ضمن مهمة المدرب ─────────────────────────
        [HttpGet]
        public async Task<IActionResult> Preview(long itemId, CancellationToken ct)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return NotFound();

            var question = await _query.GetPreviewQuestionAsync(instructorId, itemId, ct);
            if (question is null) return NotFound();

            var model = question.ToDisplayModel();
            model.VerbalPassageContent = question.VerbalPassage?.Content;
            model.VerbalPassageMediaUrl = question.VerbalPassage?.MediaUrl;
            model.VerbalPassageType = question.VerbalPassage?.Type;
            model.VerbalPassageDuration = question.VerbalPassage?.DurationSeconds;
            model.IsRTL = question.Curriculum?.IsRTL ?? true;

            model.Options ??= new List<QuestionOptionDisplayViewModel>();
            while (model.Options.Count < 4)
                model.Options.Add(new QuestionOptionDisplayViewModel { Text = string.Empty, ImageUrl = string.Empty });

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", model);
        }

        // ─── اعتماد (فردي أو جماعي) ──────────────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int taskId, [FromForm] List<long> itemIds, CancellationToken ct)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Json(new { success = false, message = NoInstructorMessage });

            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var result = await _service.ApproveItemsAsync(instructorId, taskId, itemIds ?? new List<long>(), actor, ct);
            _query.InvalidatePendingCount(actor.UserId);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        // ─── إرجاع للإدارة بملاحظة إلزامية ───────────────────────
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(long itemId, [FromForm] string note, CancellationToken ct)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Json(new { success = false, message = NoInstructorMessage });

            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var result = await _service.ReturnItemAsync(instructorId, itemId, note, actor, ct);
            _query.InvalidatePendingCount(actor.UserId);
            return Json(new { success = result.Success, message = result.Message, data = result.Data });
        }

        private static object EmptyTable(string draw) => new
        {
            draw,
            recordsTotal = 0,
            recordsFiltered = 0,
            data = Array.Empty<object>()
        };

        private async Task<ReviewActor?> ActorAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return null;

            var role = User.IsInRole("Developer") ? UserRoleType.Developer :
                       User.IsInRole("SuperAdmin") ? UserRoleType.SuperAdmin :
                       User.IsInRole("Owner") ? UserRoleType.Owner :
                       UserRoleType.Instructor;

            return new ReviewActor(user.Id, user.FullName ?? user.UserName ?? "SYSTEM", role);
        }
    }
}
