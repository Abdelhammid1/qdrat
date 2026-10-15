using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;

namespace QdratNew.Areas.Admin.Controllers
{
    /// <summary>
    /// الخطة العلاجية العاجلة — بناء الخطة (RTK-S2). المنطق كله في IRemedialTrackBuilderService؛ الكنترولر رفيع.
    /// طلبات Json (fetch) تعيد دائمًا { success, message, data, warnings }.
    /// </summary>
    [Area("Admin")]
    public class RemedialTracksController : Controller
    {
        private readonly IRemedialTrackBuilderService _builder;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthorizationService _authorization;
        private readonly IRemedialTrackAuditService _audit;

        public RemedialTracksController(
            IRemedialTrackBuilderService builder,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorization,
            IRemedialTrackAuditService audit)
        {
            _builder = builder;
            _userManager = userManager;
            _authorization = authorization;
            _audit = audit;
        }

        // ---------------- القائمة ----------------

        [HttpGet]
        [AdminPermission("RemedialTracks", "Read")]
        public async Task<IActionResult> Index(string? search, int? curriculumId, int? status, int page = 1, CancellationToken ct = default)
        {
            var filter = new RemedialTrackIndexFilter
            {
                Search = search,
                CurriculumId = curriculumId,
                Status = status.HasValue && Enum.IsDefined(typeof(RemedialTrackStatus), status.Value)
                    ? (RemedialTrackStatus)status.Value
                    : null,
                Page = page
            };

            var vm = await _builder.GetIndexAsync(filter, ct);
            vm.CanCreate = await CanAsync(AdminPermissionPolicies.RemedialTracks_Create);
            return View(vm);
        }

        // ---------------- الإنشاء ----------------

        [HttpGet]
        [AdminPermission("RemedialTracks", "Create")]
        public async Task<IActionResult> Create(CancellationToken ct)
            => View(new RemedialTrackCreateVm { Curricula = await _builder.GetCurriculaAsync(ct) });

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Create")]
        public async Task<IActionResult> Create([Bind(Prefix = "Input")] CreateRemedialTrackInput input, CancellationToken ct)
        {
            var actor = await ActorAsync();
            if (actor is null)
                return Forbid();

            if (ModelState.IsValid)
            {
                var result = await _builder.CreateAsync(input, actor, ct);
                if (result.Success && result.Data is int id)
                {
                    await _audit.LogAsync(id, "RemedialTrackCreated", "إنشاء الخطة", User, actor.Name, ct);
                    TempData["RtkSuccess"] = result.Message;
                    return RedirectToAction(nameof(Builder), new { id });
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }

            return View(new RemedialTrackCreateVm
            {
                Input = input,
                Curricula = await _builder.GetCurriculaAsync(ct)
            });
        }

        // ---------------- صفحة البناء ----------------

        [HttpGet]
        [AdminPermission("RemedialTracks", "Read")]
        public async Task<IActionResult> Builder(int id, CancellationToken ct)
        {
            var vm = await _builder.GetBuilderAsync(id, ct);
            if (vm is null)
                return NotFound();

            vm.CanEdit = await CanAsync(AdminPermissionPolicies.RemedialTracks_Edit);
            vm.CanCreate = await CanAsync(AdminPermissionPolicies.RemedialTracks_Create);
            vm.CanArchive = await CanAsync(AdminPermissionPolicies.RemedialTracks_Archive);
            vm.CanPublish = await CanAsync(AdminPermissionPolicies.RemedialTrackPublications_Publish);
            vm.CanViewAudit = await CanAsync(AdminPermissionPolicies.RemedialTracks_ViewAudit);
            if (vm.CanViewAudit)
                vm.AuditEntries = await _audit.GetAsync(id, 100, ct);
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> EditHeader([FromForm] EditRemedialTrackHeaderInput input, CancellationToken ct)
            => ModelState.IsValid
                ? await AuditedAsync(input.Id, "RemedialTrackHeaderEdited", "تعديل بيانات الخطة", () => _builder.EditHeaderAsync(input, ct), ct)
                : ValidationFail();

        // ---------------- المحاور ----------------

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> AddAxis([FromForm] AddRemedialAxisInput input, CancellationToken ct)
            => ModelState.IsValid
                ? await AuditedAsync(input.TrackId, "RemedialTrackAxisAdded", "إضافة محور", () => _builder.AddAxisAsync(input, ct), ct)
                : ValidationFail();

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> MoveAxis([FromForm] int axisId, [FromForm] int dir, CancellationToken ct)
            => await AuditedAsync(await _audit.TrackIdOfAxisAsync(axisId, ct), "RemedialTrackAxisMoved", "تغيير ترتيب محور (#" + axisId + ")", () => _builder.MoveAxisAsync(axisId, dir, ct), ct);

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> RemoveAxis([FromForm] int axisId, CancellationToken ct)
            => await AuditedAsync(await _audit.TrackIdOfAxisAsync(axisId, ct), "RemedialTrackAxisRemoved", "حذف محور (#" + axisId + ")", () => _builder.RemoveAxisAsync(axisId, ct), ct);

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> SaveAxisExams([FromForm] SaveRemedialAxisExamsInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationFail();

            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            return await AuditedAsync(await _audit.TrackIdOfAxisAsync(input.AxisId, ct), "RemedialTrackAxisExamsSaved", "تعديل اختبارات محور (#" + input.AxisId + ")", () => _builder.SaveAxisExamsAsync(input, actor, ct), ct);
        }

        // ---------------- الفيديوهات ----------------

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> AddVideo([FromForm] AddRemedialVideoInput input, CancellationToken ct)
            => ModelState.IsValid
                ? await AuditedAsync(await _audit.TrackIdOfAxisAsync(input.AxisId, ct), "RemedialTrackVideoAdded", "إضافة فيديو لمحور (#" + input.AxisId + ")", () => _builder.AddVideoAsync(input, ct), ct)
                : ValidationFail();

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> EditVideo([FromForm] EditRemedialVideoInput input, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationFail();

            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            return await AuditedAsync(await _audit.TrackIdOfVideoAsync(input.VideoId, ct), "RemedialTrackVideoEditedByAdmin", "تعديل فيديو (#" + input.VideoId + ")", () => _builder.EditVideoAsync(input, actor, ct), ct);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> MoveVideo([FromForm] int videoId, [FromForm] int dir, CancellationToken ct)
            => await AuditedAsync(await _audit.TrackIdOfVideoAsync(videoId, ct), "RemedialTrackVideoMoved", "تغيير ترتيب فيديو (#" + videoId + ")", () => _builder.MoveVideoAsync(videoId, dir, ct), ct);

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> RemoveVideo([FromForm] int videoId, CancellationToken ct)
            => await AuditedAsync(await _audit.TrackIdOfVideoAsync(videoId, ct), "RemedialTrackVideoRemoved", "حذف فيديو (#" + videoId + ")", () => _builder.RemoveVideoAsync(videoId, ct), ct);

        // ---------------- التحقق / الأرشفة / النسخ ----------------

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Edit")]
        public async Task<IActionResult> MarkReady([FromForm] int id, CancellationToken ct)
            => await AuditedAsync(id, "RemedialTrackMarkedReady", "اعتماد الخطة (جاهزة للنشر)", () => _builder.MarkReadyAsync(id, ct), ct);

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Archive")]
        public async Task<IActionResult> Archive([FromForm] int id, CancellationToken ct)
            => await AuditedAsync(id, "RemedialTrackArchived", "أرشفة الخطة", () => _builder.ArchiveAsync(id, ct), ct);

        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("RemedialTracks", "Create")]
        public async Task<IActionResult> Duplicate([FromForm] int id, CancellationToken ct)
        {
            var actor = await ActorAsync();
            if (actor is null)
                return Json(new { success = false, message = "🚫 تعذّر تحديد المستخدم الحالي." });

            var result = await _builder.DuplicateAsync(id, actor, ct);
            if (result.Success && result.Data is int copyId)
            {
                await _audit.LogAsync(copyId, "RemedialTrackCreated", "إنشاء الخطة كنسخة من الخطة #" + id, User, actor.Name, ct);
                await _audit.LogAsync(id, "RemedialTrackDuplicated", "نسخ الخطة إلى الخطة #" + copyId, User, actor.Name, ct);
            }
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                data = result.Success && result.Data is int newId
                    ? new { id = newId, url = Url.Action(nameof(Builder), "RemedialTracks", new { area = "Admin", id = newId }) }
                    : null,
                warnings = result.Warnings
            });
        }

        // ---------------- مساعدات ----------------

        // يُسجَّل التعديل على الخطة بعد نجاحه فقط (فشل التسجيل لا يُبطل التعديل)
        private async Task<IActionResult> AuditedAsync(int? trackId, string action, string label, Func<Task<RemedialTrackResult>> run, CancellationToken ct)
        {
            var r = await run();
            if (r.Success && trackId is > 0)
                await _audit.LogAsync(trackId.Value, action, string.IsNullOrWhiteSpace(r.Message) ? label : label + " — " + r.Message, User, (await ActorAsync())?.Name, ct);
            return JsonResult(r);
        }

        private IActionResult JsonResult(RemedialTrackResult r)
            => Json(new { success = r.Success, message = r.Message, data = r.Data, warnings = r.Warnings });

        private IActionResult ValidationFail()
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .Take(3)
                .ToList();

            var message = errors.Count == 0 ? "⚠️ البيانات غير مكتملة." : "⚠️ " + string.Join(" • ", errors);
            return Json(new { success = false, message, data = (object?)null, warnings = (object?)null });
        }

        private async Task<bool> CanAsync(string policy)
            => (await _authorization.AuthorizeAsync(User, policy)).Succeeded;

        private async Task<RemedialTrackActor?> ActorAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            return user is null
                ? null
                : new RemedialTrackActor(user.Id, user.FullName ?? user.UserName ?? "SYSTEM");
        }
    }
}
