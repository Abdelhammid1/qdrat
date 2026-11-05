using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.PerformanceIndicator.PartnerScope;
using QdratNew.ViewModels.Partner.PerformanceIndicator;

namespace QdratNew.Areas.Partner.Controllers
{
    // APD-S4.2: متابعة اختبارات مؤشر الأداء التي أرسلها المعهد لهذه المدرسة. الإنشاء من الأدمن فقط.
    // القراءة مسموحة حتى AccessUntilDate بعد انتهاء العقد، والإدارة (POST) تتطلب عقدًا نشطًا.
    // الملكية تُفحص داخل الخدمة، والرد 404 لأي اختبار أو طالب خارج نطاق المدرسة.
    [Area("Partner")]
    [PartnerFeature(PartnerFeatureType.PerformanceIndicatorExams)]
    public class PerformanceIndicatorExamsController : PartnerBaseController
    {
        private readonly IPartnerPerformanceExamService _exams;

        public PerformanceIndicatorExamsController(
            IPartnerSubscriptionService subscriptionService,
            ApplicationDbContext context,
            IPartnerPerformanceExamService exams)
            : base(subscriptionService, context)
        {
            _exams = exams;
        }

        private bool CanManage => _subscriptionService.CanUsePerformanceIndicatorExams(ActivePartnerId);

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PerformanceIndicatorExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> Index(bool hidden = false)
            => View(await _exams.GetIndexAsync(ActivePartnerId, hidden, CanManage, HttpContext.RequestAborted));

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PerformanceIndicatorExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> ExamStudents(int id)
        {
            var vm = await _exams.GetStudentsAsync(ActivePartnerId, id, HttpContext.RequestAborted);
            if (vm == null)
                return NotFound();

            vm.CanManage = CanManage;
            return View(vm);
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PerformanceIndicatorExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> StudentExamQuestions(int examId, int studentId)
        {
            var vm = await _exams.GetStudentQuestionsAsync(ActivePartnerId, examId, studentId, HttpContext.RequestAborted);
            if (vm == null)
                return NotFound();

            if (!vm.StudentCompleted || vm.Report == null)
            {
                TempData["Info"] = "مراجعة الإجابات تُفتح بعد أن ينهي الطالب الاختبار.";
                return RedirectToAction(nameof(ExamStudents), new { id = examId });
            }

            return View(vm.Report);
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PerformanceIndicatorExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> BatchReport(int examId, int batchId)
        {
            var vm = await _exams.GetBatchReportAsync(ActivePartnerId, examId, batchId, HttpContext.RequestAborted);
            return vm == null ? NotFound() : View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendToStudent(int examId, int studentId)
        {
            var outcome = await _exams.ResendToStudentAsync(ActivePartnerId, examId, studentId, HttpContext.RequestAborted);

            switch (outcome)
            {
                case PartnerPerformanceResendOutcome.NotFound:
                    return NotFound();
                case PartnerPerformanceResendOutcome.Added:
                    TempData["Success"] = "✅ أُرسل الاختبار للطالب.";
                    break;
                case PartnerPerformanceResendOutcome.Reopened:
                    TempData["Success"] = "✅ أُعيد فتح الاختبار للطالب بوقته كاملًا، وإجاباته السابقة محفوظة.";
                    break;
                case PartnerPerformanceResendOutcome.AlreadyAvailable:
                    TempData["Info"] = "الاختبار متاح للطالب بالفعل ولم يبدأه بعد.";
                    break;
                case PartnerPerformanceResendOutcome.AlreadyCompleted:
                    TempData["Info"] = "أنهى الطالب الاختبار، ونتيجته محفوظة ولا تُعاد.";
                    break;
                default:
                    TempData["Error"] = "⚠️ لا يمكن الإرسال: الطالب ليس طالبًا نشطًا في دفعات هذا الاختبار.";
                    break;
            }

            return RedirectToAction(nameof(ExamStudents), new { id = examId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Hide(int id)
        {
            if (!await _exams.SetHiddenAsync(ActivePartnerId, id, true, User.FindFirstValue(ClaimTypes.NameIdentifier), HttpContext.RequestAborted))
                return NotFound();

            TempData["Success"] = "✅ أُخفي الاختبار من قائمتك. لا يتأثر الطلاب ونتائجهم.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restore(int id)
        {
            if (!await _exams.SetHiddenAsync(ActivePartnerId, id, false, null, HttpContext.RequestAborted))
                return NotFound();

            TempData["Success"] = "✅ عاد الاختبار إلى قائمتك.";
            return RedirectToAction(nameof(Index), new { hidden = true });
        }
    }
}
