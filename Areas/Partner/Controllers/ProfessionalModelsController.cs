using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.Services.ProfessionalModels;
using QdratNew.ViewModels.Partner;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [PartnerFeature(PartnerFeatureType.ProfessionalModels)] // PPL-S1.4
    public class ProfessionalModelsController : PartnerBaseController
    {
        private const int DefaultHomeworkDays = 7;
        private const int DefaultExamDays = 1;

        private readonly ApplicationDbContext _context;
        private readonly IProfessionalModelSendService _sendService;
        private readonly IProfessionalModelAvailabilityService _availabilityService;
        private readonly ITimeZoneService _timeZoneService;

        public ProfessionalModelsController(
            ApplicationDbContext context,
            IProfessionalModelSendService sendService,
            IProfessionalModelAvailabilityService availabilityService,
            ITimeZoneService timeZoneService,
            IPartnerSubscriptionService subscriptionService)
            : base(subscriptionService, context)
        {
            _context = context;
            _sendService = sendService;
            _availabilityService = availabilityService;
            _timeZoneService = timeZoneService;
        }

        // =========================================
        // 📄 قائمة نماذج الواجب
        // =========================================
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            return View(new PartnerHomeworkModelsVm
            {
                CanCreateHomework = _subscriptionService.CanUseHomework(ActivePartnerId),
                Models = await _sendService.GetModelsAsync(Scope(), ProfessionalModelType.Homework, ct)
            });
        }

        // =========================================
        // 📄 PPM-S5.2: قائمة نماذج الاختبار
        // =========================================
        public async Task<IActionResult> Exams(CancellationToken ct)
        {
            return View(new PartnerExamModelsVm
            {
                CanCreateExams = _subscriptionService.CanUseExams(ActivePartnerId),
                CanUsePlacementExams = _subscriptionService.CanUsePlacementExams(ActivePartnerId),
                Models = await _sendService.GetModelsAsync(Scope(), ProfessionalModelType.Exam, ct)
            });
        }

        // =========================================
        // 📤 PPM-S4.1: إرسال واجب مباشرة من نموذج
        // =========================================
        [HttpGet]
        public async Task<IActionResult> SendHomework(int id, CancellationToken ct)
        {
            if (!_subscriptionService.CanUseHomework(ActivePartnerId))
                return FeatureNotAllowed(PartnerFeatureType.Homework);

            var now = _timeZoneService.ConvertToSaudi(_timeZoneService.GetNowUtc());

            return await RenderSendFormAsync(new SendModelHomeworkVm
            {
                ModelId = id,
                StartAtLocal = now,
                EndAtLocal = now.AddDays(DefaultHomeworkDays)
            }, ProfessionalModelType.Homework, ct);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendHomework(SendModelHomeworkVm vm, CancellationToken ct)
        {
            if (!_subscriptionService.CanUseHomework(ActivePartnerId))
                return FeatureNotAllowed(PartnerFeatureType.Homework);

            return await HandleSendPostAsync(
                vm,
                ProfessionalModelType.Homework,
                () => _sendService.SendHomeworkAsync(Scope(), FillRequest(new SendModelHomeworkRequest(), vm), ct),
                RedirectToAction("Index", "SentHomeworks"),
                ct);
        }

        // =========================================
        // 📤 PPM-S5.2: إرسال اختبار مباشرة من نموذج
        // =========================================
        [HttpGet]
        public async Task<IActionResult> SendExam(int id, CancellationToken ct)
        {
            if (!_subscriptionService.CanUseExams(ActivePartnerId))
                return FeatureNotAllowed(PartnerFeatureType.Exams);

            var now = _timeZoneService.ConvertToSaudi(_timeZoneService.GetNowUtc());

            return await RenderSendFormAsync(new SendModelExamVm
            {
                ModelId = id,
                StartAtLocal = now,
                EndAtLocal = now.AddDays(DefaultExamDays)
            }, ProfessionalModelType.Exam, ct);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendExam(SendModelExamVm vm, CancellationToken ct)
        {
            if (!_subscriptionService.CanUseExams(ActivePartnerId))
                return FeatureNotAllowed(PartnerFeatureType.Exams);

            return await HandleSendPostAsync(
                vm,
                ProfessionalModelType.Exam,
                () => _sendService.SendExamAsync(Scope(), ToRequest(vm), ct),
                RedirectToAction("SentExams", "PartnerExam"),
                ct);
        }

        private async Task<IActionResult> HandleSendPostAsync(
            SendFromModelVm vm,
            ProfessionalModelType type,
            Func<Task<ModelSendResult>> sendAsync,
            IActionResult onSuccess,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return await RenderSendFormAsync(vm, type, ct);

            if (vm.Intent == SendFromModelVm.IntentSend)
            {
                var result = await sendAsync();

                if (result.IsNotFound)
                    return NotFound();

                if (result.Succeeded)
                {
                    TempData["Success"] = result.Message;
                    return onSuccess;
                }

                vm.ErrorMessage = result.Message;
                vm.Preview = result.Preview;
                return await RenderSendFormAsync(vm, type, ct);
            }

            var scope = Scope();
            var request = ToRequest(vm);

            var error = vm.BatchIds.Count == 0
                ? "⚠️ اختر دفعة واحدة على الأقل."
                : _sendService.ValidateSchedule(scope, request);

            if (error != null)
            {
                vm.ErrorMessage = error;
                return await RenderSendFormAsync(vm, type, ct);
            }

            var preview = await _sendService.PreviewAsync(scope, type, request, ct);
            if (preview == null)
                return NotFound();

            if (preview.ValidationError != null)
                vm.ErrorMessage = preview.ValidationError;
            else
                vm.Preview = preview;

            return await RenderSendFormAsync(vm, type, ct);
        }

        // =========================================
        // 🔁 بدائل سؤال من نفس الدرس (Partial للنافذة)
        // =========================================
        [HttpGet]
        public async Task<IActionResult> ReplacementCandidates(int id, Guid questionId, CancellationToken ct)
        {
            if (!_subscriptionService.CanUseHomework(ActivePartnerId))
                return FeatureNotAllowed(PartnerFeatureType.Homework);

            var candidates = await _sendService.GetReplacementCandidatesAsync(Scope(), id, questionId, ct);

            return PartialView("_ReplacementCandidates", candidates);
        }

        [HttpGet]
        public async Task<IActionResult> ExamReplacementCandidates(int id, Guid questionId, CancellationToken ct)
        {
            if (!_subscriptionService.CanUseExams(ActivePartnerId))
                return FeatureNotAllowed(PartnerFeatureType.Exams);

            var candidates = await _sendService.GetReplacementCandidatesAsync(Scope(), id, questionId, ct);

            return PartialView("_ReplacementCandidates", candidates);
        }

        private async Task<IActionResult> RenderSendFormAsync(SendFromModelVm vm, ProfessionalModelType type, CancellationToken ct)
        {
            var scope = Scope();

            var original = await _sendService.PreviewAsync(
                scope,
                type,
                new SendModelHomeworkRequest { ModelId = vm.ModelId },
                ct);

            if (original == null)
                return NotFound();

            vm.ModelTitle = original.ModelTitle;
            vm.ModelQuestions = original.Questions;
            vm.EligibleBatches = await _sendService.GetEligibleBatchesAsync(scope, vm.ModelId, ct);

            if (vm.Replacements.Count > 0)
            {
                var resolved = vm.Preview ?? await _sendService.PreviewAsync(scope, type, ToRequest(vm), ct);

                vm.ChosenSubstitutes = (resolved?.Questions ?? new List<PreviewQuestionVm>())
                    .Where(q => q.ReplacesQuestionId.HasValue)
                    .ToDictionary(q => q.ReplacesQuestionId!.Value);
            }

            return View(vm.FormAction, vm);
        }

        private ModelSendScope Scope()
            => ModelSendScope.Partner(ActivePartnerId, User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);

        private static SendFromModelRequestBase ToRequest(SendFromModelVm vm)
            => vm is SendModelExamVm exam ? ToRequest(exam) : FillRequest(new SendModelHomeworkRequest(), vm);

        private static SendModelExamRequest ToRequest(SendModelExamVm vm)
        {
            var request = FillRequest(new SendModelExamRequest(), vm);
            request.DurationMinutes = vm.DurationMinutes;
            return request;
        }

        private static TRequest FillRequest<TRequest>(TRequest request, SendFromModelVm vm)
            where TRequest : SendFromModelRequestBase
        {
            request.ModelId = vm.ModelId;
            request.BatchIds = vm.BatchIds;
            request.Title = vm.Title;
            request.StartAtLocal = vm.StartAtLocal;
            request.EndAtLocal = vm.EndAtLocal;
            request.ExcludedQuestionIds = vm.ExcludedQuestionIds;
            request.Replacements = vm.Replacements;
            request.ConfirmWarnings = vm.ConfirmWarnings;
            return request;
        }

        // الـ Controller محكوم بميزة النماذج، والإرسال يتطلب ميزة نوعه أيضًا
        private ViewResult FeatureNotAllowed(PartnerFeatureType feature)
        {
            var result = View("~/Views/Shared/FeatureNotAllowed.cshtml");
            result.StatusCode = StatusCodes.Status403Forbidden;
            result.ViewData["FeatureName"] = feature;
            return result;
        }

        // =========================================
        // 👁️ معاينة النموذج
        // =========================================
        public async Task<IActionResult> Preview(int id)
        {
            if (!await IsModelAvailableForPartnerAsync(id))
                return NotFound();

            var model = await _context.ProfessionalModels
                .Include(m => m.Questions)
                .ThenInclude(q => q.Question)
                .FirstOrDefaultAsync(m => m.Id == id && !m.IsArchived);

            if (model == null)
                return NotFound();

            return View(model);
        }

        // =========================================
        // ↪️ PPM-S4.2: المسارات القديمة تُحوَّل لإصدار واحد ثم تُحذف
        // =========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Send(int modelId)
            => RedirectToAction(nameof(SendHomework), new { id = modelId });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SendCustomized(int modelId)
            => RedirectToAction(nameof(SendHomework), new { id = modelId });

        // =========================================
        // 🔒 PPL-S1.6: نفس قواعد الإتاحة في Index
        // =========================================
        private Task<bool> IsModelAvailableForPartnerAsync(int modelId)
            => _availabilityService.IsAvailableForPartnerAsync(modelId, ActivePartnerId, null);
    }
}
