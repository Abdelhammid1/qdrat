using Microsoft.AspNetCore.Mvc;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.Services.MinistrySim;
using QdratNew.Services.MinistrySim.PartnerScope;

namespace QdratNew.Areas.Partner.Controllers
{
    // APD-S5.2: متابعة معمل القياس الذي أسنده المعهد لهذه المدرسة. الإنشاء والإسناد من الأدمن فقط.
    // القراءة مسموحة حتى AccessUntilDate بعد انتهاء العقد، والإدارة (POST) تتطلب عقدًا نشطًا.
    // الملكية تُفحص داخل الخدمات (IMinistrySimOwnershipQuery)، والرد 404 لأي اختبار أو طالب خارج نطاق المدرسة.
    // صفحات النتيجة والمراجعة والتحليلات والتقرير تعيد استخدام Views الأدمن (MinistrySimResultRoutes يبدّل الروابط والتخطيط).
    [Area("Partner")]
    [PartnerFeature(PartnerFeatureType.MinistrySimExams)]
    public class MinistrySimExamsController : PartnerBaseController
    {
        private const string AdminViews = "~/Areas/Admin/Views/MinistrySimExam/";

        private readonly IPartnerMinistrySimExamService _exams;
        private readonly IMinistrySimExamResultService _results;
        private readonly IMinistrySimExamBatchAnalyticsService _analytics;
        private readonly IMinistrySimParentReportService _parentReports;

        public MinistrySimExamsController(
            IPartnerSubscriptionService subscriptionService,
            ApplicationDbContext context,
            IPartnerMinistrySimExamService exams,
            IMinistrySimExamResultService results,
            IMinistrySimExamBatchAnalyticsService analytics,
            IMinistrySimParentReportService parentReports)
            : base(subscriptionService, context)
        {
            _exams = exams;
            _results = results;
            _analytics = analytics;
            _parentReports = parentReports;
        }

        private bool CanManage => _subscriptionService.CanUseMinistrySimExams(ActivePartnerId);

        private CancellationToken Aborted => HttpContext.RequestAborted;

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.MinistrySimExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> Index(bool hidden = false)
            => View(await _exams.GetIndexAsync(ActivePartnerId, hidden, CanManage, Aborted));

        // حالة طلاب المدرسة في الاختبار، أو طلاب دفعة واحدة مُسنَدة لها
        [HttpGet]
        [PartnerFeature(PartnerFeatureType.MinistrySimExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> BatchStudents(int id, int? batchId = null)
        {
            var vm = await _exams.GetStudentsAsync(ActivePartnerId, id, batchId, Aborted);
            return vm == null ? NotFound() : View(vm);
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.MinistrySimExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> StudentResult(int id, int studentId)
        {
            if (!await _exams.OwnsStudentAsync(ActivePartnerId, id, studentId, Aborted))
                return NotFound();

            var lookup = await _results.GetResultAsync(id, studentId);

            switch (lookup.Status)
            {
                case MinistrySimExamResultStatus.ExamNotFound:
                    return NotFound();

                case MinistrySimExamResultStatus.AttemptNotFound:
                    TempData["Info"] = "لم يبدأ الطالب هذا الاختبار بعد.";
                    return RedirectToAction(nameof(BatchStudents), new { id });

                case MinistrySimExamResultStatus.NotCompleted:
                    TempData["Info"] = "النتيجة تظهر بعد أن ينهي الطالب أقسام الاختبار.";
                    return RedirectToAction(nameof(BatchStudents), new { id });

                default:
                    return View(AdminViews + "StudentResult.cshtml", lookup.Vm);
            }
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.MinistrySimExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> StudentQuestionReview(int id, int studentId, int stageNumber)
        {
            if (!await _exams.OwnsStudentAsync(ActivePartnerId, id, studentId, Aborted))
                return NotFound();

            var lookup = await _results.GetQuestionReviewAsync(id, studentId, stageNumber);

            switch (lookup.Status)
            {
                case MinistrySimExamQuestionReviewStatus.StageNotFound:
                    return NotFound();

                case MinistrySimExamQuestionReviewStatus.AttemptNotFound:
                case MinistrySimExamQuestionReviewStatus.NotCompleted:
                    TempData["Info"] = "مراجعة الإجابات تُفتح بعد أن ينهي الطالب أقسام الاختبار.";
                    return RedirectToAction(nameof(BatchStudents), new { id });

                default:
                    return View(AdminViews + "StudentQuestionReview.cshtml", lookup.Vm);
            }
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.MinistrySimExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> BatchAnalytics(int examId, int batchId)
        {
            var lookup = await _analytics.GetBatchAnalyticsAsync(examId, batchId, ActivePartnerId);

            return lookup.Status == MinistrySimExamBatchAnalyticsStatus.Ok
                ? View(AdminViews + "BatchAnalytics.cshtml", lookup.Vm)
                : NotFound();
        }

        // ترويسة المدرسة + «بالتعاون مع معهد القدرات». لا PDF من الخادم: الطباعة من المتصفح.
        [HttpGet]
        [PartnerFeature(PartnerFeatureType.MinistrySimExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> ParentReport(int examId, int studentId, bool print = false)
        {
            if (!await _exams.OwnsStudentAsync(ActivePartnerId, examId, studentId, Aborted))
                return NotFound();

            var lookup = await _parentReports.BuildAsync(examId, studentId, ActivePartnerId, print, Aborted);

            switch (lookup.Status)
            {
                case MinistrySimParentReportStatus.Ok:
                    break;

                case MinistrySimParentReportStatus.AttemptNotFound:
                case MinistrySimParentReportStatus.NotCompleted:
                    TempData["Info"] = "تقرير ولي الأمر يُتاح بعد أن ينهي الطالب أقسام الاختبار.";
                    return RedirectToAction(nameof(BatchStudents), new { id = examId });

                default:
                    return NotFound();
            }

            var vm = lookup.Vm!;
            vm.Letterhead = await _exams.GetLetterheadAsync(ActivePartnerId, Aborted);

            // ملاحظات المدرّس تُحفظ بعقد نشط فقط؛ بعد انتهائه تُعرض للقراءة
            vm.CanEditNote = CanManage;

            return View(AdminViews + "ParentReport.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveParentReportNote(int attemptId, string note)
        {
            if (!await _exams.SaveParentReportNoteAsync(ActivePartnerId, attemptId, note, Aborted))
                return NotFound();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hide(int id)
        {
            if (!await _exams.SetHiddenAsync(ActivePartnerId, id, true, Aborted))
                return NotFound();

            TempData["Success"] = "✅ أُخفي الاختبار من قائمتك. لا يتأثر الطلاب ونتائجهم.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            if (!await _exams.SetHiddenAsync(ActivePartnerId, id, false, Aborted))
                return NotFound();

            TempData["Success"] = "✅ عاد الاختبار إلى قائمتك.";
            return RedirectToAction(nameof(Index), new { hidden = true });
        }
    }
}
