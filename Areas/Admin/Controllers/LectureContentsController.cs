using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.OnlineLecture;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Areas.Admin.Controllers
{
    /// <summary>
    /// OLC-S2.3 — صفحة «المحتوى التعليمي أونلاين» لمحاضرة واحدة. المنطق كله في <see cref="ILectureContentAdminService"/>.
    /// OLC-S4.2 — كل Action يمس محاضرة دفعة شريك يمر على <c>PartnerGuardAsync</c> (R18)، و«الدفعات الأونلاين» بجمهور المنصة/الشركاء.
    /// الشاشة تعمل للأدمن حتى والمفتاح OnlineLectureContent.Enabled مغلق (للتجهيز قبل الإطلاق) — المفتاح يحكم ظهورها للطلاب فقط.
    /// </summary>
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class LectureContentsController : Controller
    {
        private readonly ILectureContentAdminService _service;
        private readonly IOnlineLectureFeatureService _feature;
        private readonly IAuthorizationService _authorization;

        public LectureContentsController(
            ILectureContentAdminService service,
            IOnlineLectureFeatureService feature,
            IAuthorizationService authorization)
        {
            _service = service;
            _feature = feature;
            _authorization = authorization;
        }

        // GET /Admin/LectureContents/Edit?lectureId=5
        [HttpGet]
        [AdminPermission("LectureContents", "Read")]
        public async Task<IActionResult> Edit(int lectureId, CancellationToken ct)
        {
            if (await PartnerGuardAsync(lectureId, ct) is { } denied)
                return denied;

            var vm = await BuildAsync(lectureId, ct);
            return vm is null ? NotFound() : View(vm);
        }

        // POST: إنشاء المسودة عند أول فتح (بعد تأكيد الأدمن) — R1
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("LectureContents", "Create")]
        public async Task<IActionResult> Create(int lectureId, [Bind(Prefix = "CreateInput")] LectureContentCreateVm input, CancellationToken ct)
        {
            if (await PartnerGuardAsync(lectureId, ct) is { } denied)
                return denied;

            if (ModelState.IsValid)
            {
                var result = await _service.CreateAsync(lectureId, input, CurrentUserId(), ct);
                if (result.NotFound)
                    return NotFound();
                if (result.Success)
                {
                    TempData["OlcSuccess"] = result.Message;
                    return RedirectToAction(nameof(Edit), new { lectureId });
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }

            var vm = await BuildAsync(lectureId, ct);
            if (vm is null)
                return NotFound();

            vm.CreateInput = input;
            return View(nameof(Edit), vm);
        }

        // POST: البيانات الأساسية + الوضع + الفيديو الكامل — R8
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("LectureContents", "Edit")]
        public async Task<IActionResult> Save(int id, [Bind(Prefix = "Input")] LectureContentUpdateVm input, CancellationToken ct)
        {
            var lectureId = await _service.GetLectureIdOfContentAsync(id, ct);
            if (lectureId is null)
                return NotFound();
            if (await PartnerGuardAsync(lectureId.Value, ct) is { } denied)
                return denied;

            if (ModelState.IsValid)
            {
                var result = await _service.UpdateAsync(id, input, CurrentUserId(), ct);
                if (result.NotFound)
                    return NotFound();
                if (result.Success)
                {
                    TempData["OlcSuccess"] = result.Message;
                    return RedirectToAction(nameof(Edit), new { lectureId = lectureId.Value });
                }

                ModelState.AddModelError(string.Empty, result.Message);
            }

            var vm = await BuildAsync(lectureId.Value, ct);
            if (vm is null)
                return NotFound();

            vm.Input = input;
            return View(nameof(Edit), vm);
        }

        // ═══════════════════════════ OLC-S3.1 — الفصول (FullVideo) ═══════════════════════════

        // POST: يستبدل الفصول بالكامل — R4 + R7
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("LectureContents", "Edit")]
        public async Task<IActionResult> SaveChapters(int id, [Bind(Prefix = "Chapters")] List<LectureContentChapterInputVm>? chapters, CancellationToken ct)
        {
            var lectureId = await _service.GetLectureIdOfContentAsync(id, ct);
            if (lectureId is null)
                return NotFound();
            if (await PartnerGuardAsync(lectureId.Value, ct) is { } denied)
                return denied;

            chapters ??= new List<LectureContentChapterInputVm>();
            if (ModelState.IsValid)
            {
                var result = await _service.SetChaptersAsync(id, chapters, CurrentUserId(), ct);
                if (result.NotFound)
                    return NotFound();
                if (result.Success)
                    return BackToContent(lectureId.Value, result.Message);

                ModelState.AddModelError(string.Empty, result.Message);
            }

            var vm = await BuildAsync(lectureId.Value, ct);
            if (vm is null)
                return NotFound();

            // إعادة ما أدخله الأدمن فوق الصفوف المبنية من الخادم (العناوين المعروضة لا تُقرأ من الطلب)
            var posted = chapters.GroupBy(c => c.LessonId).ToDictionary(g => g.Key, g => g.First());
            foreach (var row in vm.Chapters)
            {
                if (!posted.TryGetValue(row.LessonId, out var p))
                    continue;
                row.Include = p.Include;
                row.Title = p.Title;
                row.StartText = p.StartText;
                row.EndText = p.EndText;
            }
            vm.ActiveTab = "content";
            return View(nameof(Edit), vm);
        }

        // ═══════════════════════════ OLC-S3.2 — فيديوهات المؤشرات ═══════════════════════════

        // POST: إضافة فيديو مؤشر — R5 + R6 + R7 + R8
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("LectureContents", "Edit")]
        public async Task<IActionResult> AddItem(int id, [Bind(Prefix = "AddInput")] AddIndicatorVideoVm input, CancellationToken ct)
        {
            var lectureId = await _service.GetLectureIdOfContentAsync(id, ct);
            if (lectureId is null)
                return NotFound();
            if (await PartnerGuardAsync(lectureId.Value, ct) is { } denied)
                return denied;

            if (ModelState.IsValid)
            {
                var result = await _service.AddIndicatorVideoAsync(id, input, CurrentUserId(), ct);
                if (result.NotFound)
                    return NotFound();
                if (result.Success)
                    return BackToContent(lectureId.Value, result.Message);

                ModelState.AddModelError(string.Empty, result.Message);
            }

            var vm = await BuildAsync(lectureId.Value, ct);
            if (vm is null)
                return NotFound();

            vm.AddInput = input;
            vm.ActiveTab = "content";
            return View(nameof(Edit), vm);
        }

        // POST: حذف ناعم لعنصر (فصل أو فيديو)
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("LectureContents", "Delete")]
        public async Task<IActionResult> RemoveItem(int itemId, CancellationToken ct)
        {
            var lectureId = await _service.GetLectureIdOfItemAsync(itemId, ct);
            if (lectureId is null)
                return NotFound();
            if (await PartnerGuardAsync(lectureId.Value, ct) is { } denied)
                return denied;

            var result = await _service.RemoveItemAsync(itemId, CurrentUserId(), ct);
            return result.NotFound ? NotFound() : BackToContent(lectureId.Value, result);
        }

        // POST: إعادة ترتيب قائمة التشغيل (القائمة الكاملة بالترتيب الجديد)
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("LectureContents", "Edit")]
        public async Task<IActionResult> Reorder(int id, [FromForm] List<int>? orderedItemIds, CancellationToken ct)
        {
            var lectureId = await _service.GetLectureIdOfContentAsync(id, ct);
            if (lectureId is null)
                return NotFound();
            if (await PartnerGuardAsync(lectureId.Value, ct) is { } denied)
                return denied;

            var result = await _service.ReorderItemsAsync(id, orderedItemIds ?? new List<int>(), CurrentUserId(), ct);
            return result.NotFound ? NotFound() : BackToContent(lectureId.Value, result);
        }

        // POST: «تحديث من المصدر» — R6
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("LectureContents", "Edit")]
        public async Task<IActionResult> RefreshItem(int itemId, CancellationToken ct)
        {
            var lectureId = await _service.GetLectureIdOfItemAsync(itemId, ct);
            if (lectureId is null)
                return NotFound();
            if (await PartnerGuardAsync(lectureId.Value, ct) is { } denied)
                return denied;

            var result = await _service.RefreshItemFromSourceAsync(itemId, CurrentUserId(), ct);
            return result.NotFound ? NotFound() : BackToContent(lectureId.Value, result);
        }

        // ═══════════════════════════ OLC-S3.3 — النشر ═══════════════════════════

        // POST: Draft/Hidden → Published بعد تحقق R4/R5
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("LectureContents", "Edit")]
        public async Task<IActionResult> Publish(int id, CancellationToken ct)
        {
            var lectureId = await _service.GetLectureIdOfContentAsync(id, ct);
            if (lectureId is null)
                return NotFound();
            if (await PartnerGuardAsync(lectureId.Value, ct) is { } denied)
                return denied;

            var result = await _service.PublishAsync(id, CurrentUserId(), ct);
            return result.NotFound ? NotFound() : BackToBasic(lectureId.Value, result);
        }

        // POST: Published → Hidden
        [HttpPost, ValidateAntiForgeryToken]
        [AdminPermission("LectureContents", "Edit")]
        public async Task<IActionResult> Hide(int id, CancellationToken ct)
        {
            var lectureId = await _service.GetLectureIdOfContentAsync(id, ct);
            if (lectureId is null)
                return NotFound();
            if (await PartnerGuardAsync(lectureId.Value, ct) is { } denied)
                return denied;

            var result = await _service.HideAsync(id, CurrentUserId(), ct);
            return result.NotFound ? NotFound() : BackToBasic(lectureId.Value, result);
        }

        // ═══════════════════════════ OLC-S7.3 — تقرير المشاهدة ═══════════════════════════

        // GET /Admin/LectureContents/Report?lectureId=5 — قراءة فقط من بيانات التقدّم الحقيقية
        [HttpGet]
        [AdminPermission("LectureContents", "Read")]
        public async Task<IActionResult> Report(int lectureId, CancellationToken ct)
        {
            if (await PartnerGuardAsync(lectureId, ct) is { } denied)
                return denied;

            var vm = await _service.GetReportAsync(lectureId, ct);
            if (vm is null)
                return NotFound();

            vm.FeatureEnabled = await _feature.IsEnabledAsync(ct);
            return View(vm);
        }

        // ═══════════════════════════ OLC-S4.2 — «الدفعات الأونلاين» ═══════════════════════════

        // GET /Admin/LectureContents/Batches?audience=2&partnerIds=1&partnerIds=2
        // «المنصة» لكل من له Read، و«الشركاء» يتطلب ManagePartners (R18) — الطلب المصطنع بدونها 403.
        [HttpGet]
        [AdminPermission("LectureContents", "Read")]
        public async Task<IActionResult> Batches([FromQuery] OnlineBatchesFilterVm filter, CancellationToken ct)
        {
            filter ??= new OnlineBatchesFilterVm();
            if (!Enum.IsDefined(filter.Audience))
                filter.Audience = DispatchAudience.Platform;

            var canManagePartners = await CanAsync(AdminPermissionPolicies.LectureContents_ManagePartners);
            if (filter.Audience == DispatchAudience.Partners && !canManagePartners)
                return Forbid();

            var vm = await _service.GetOnlineBatchesAsync(filter, canManagePartners, ct);
            vm.FeatureEnabled = await _feature.IsEnabledAsync(ct);
            return View(vm);
        }

        // ───────────────────────── helpers ─────────────────────────

        /// <summary>
        /// R18 — Helper واحد لكل Action: محاضرة دفعة شريك تتطلب LectureContents:ManagePartners فوق صلاحية الـ Action.
        /// null = مسموح (أو المحاضرة غير موجودة فيتولاها الـ Action بـ 404).
        /// </summary>
        private async Task<IActionResult?> PartnerGuardAsync(int lectureId, CancellationToken ct)
        {
            var partnerId = await _service.GetLecturePartnerIdAsync(lectureId, ct);
            if (partnerId is null)
                return null;

            return await CanAsync(AdminPermissionPolicies.LectureContents_ManagePartners) ? null : Forbid();
        }

        private async Task<LectureContentEditVm?> BuildAsync(int lectureId, CancellationToken ct)
        {
            var vm = await _service.GetForLectureAsync(lectureId, ct);
            if (vm is null)
                return null;

            vm.FeatureEnabled = await _feature.IsEnabledAsync(ct);
            vm.CanCreate = await CanAsync(AdminPermissionPolicies.LectureContents_Create);
            vm.CanEdit = await CanAsync(AdminPermissionPolicies.LectureContents_Edit);
            vm.CanDelete = await CanAsync(AdminPermissionPolicies.LectureContents_Delete);
            if (TempData["OlcTab"] is string tab && (tab == "basic" || tab == "content"))
                vm.ActiveTab = tab;
            return vm;
        }

        private IActionResult BackToContent(int lectureId, string message)
            => BackTo(lectureId, "content", LectureContentResult.Ok(message));

        private IActionResult BackToContent(int lectureId, LectureContentResult result)
            => BackTo(lectureId, "content", result);

        private IActionResult BackToBasic(int lectureId, LectureContentResult result)
            => BackTo(lectureId, "basic", result);

        private IActionResult BackTo(int lectureId, string tab, LectureContentResult result)
        {
            TempData[result.Success ? "OlcSuccess" : "OlcError"] = result.Message;
            TempData["OlcTab"] = tab;
            return RedirectToAction(nameof(Edit), new { lectureId });
        }

        private async Task<bool> CanAsync(string policy)
            => (await _authorization.AuthorizeAsync(User, policy)).Succeeded;

        private string CurrentUserId()
            => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    }
}
