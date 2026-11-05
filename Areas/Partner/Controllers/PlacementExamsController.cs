using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.Exams.Placement;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Partner;

namespace QdratNew.Areas.Partner.Controllers
{
    // PPL-S4: كل المنطق وفحص الملكية داخل IPlacementExamManagementService بنطاق الشريك.
    // PPL-S6: التقارير عبر IPlacementExamReportService، ومسموحة للقراءة حتى AccessUntilDate.
    [Area("Partner")]
    [PartnerFeature(PartnerFeatureType.PlacementExams)]
    public class PlacementExamsController : PartnerBaseController
    {
        private readonly IPlacementExamManagementService _managementService;
        private readonly IPlacementExamReportService _reportService;

        public PlacementExamsController(
            IPartnerSubscriptionService subscriptionService,
            ApplicationDbContext context,
            IPlacementExamManagementService managementService,
            IPlacementExamReportService reportService)
            : base(subscriptionService, context)
        {
            _managementService = managementService;
            _reportService = reportService;
        }

        private PlacementScope Scope => PlacementScope.Partner(
            ActivePartnerId,
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name);

        private void SetResultMessage(PlacementOperationResult result)
        {
            var key = result.Outcome switch
            {
                PlacementOperationOutcome.Success => "Success",
                PlacementOperationOutcome.Info => "Info",
                _ => "Error"
            };
            TempData[key] = result.Message;
        }

        // =====================================================
        // Index / BatchExams
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var ct = HttpContext.RequestAborted;
            var data = await _managementService.GetIndexAsync(Scope, ct);
            var contract = await _managementService.GetContractSummaryAsync(Scope, ct);

            return View(new PartnerPlacementIndexVm
            {
                Batches = data.ActiveBatches,
                SingleStudentExams = await _managementService.GetSingleStudentExamsAsync(Scope, ct),
                ContractEndDate = contract?.EndDate,
                CanUseProfessionalModels = contract?.CanUseProfessionalModels ?? false,
                HasBankCourses = AllowedCourses.Any(c => c.CanUsePlatformQuestionBank)
            });
        }

        [HttpGet]
        public async Task<IActionResult> BatchExams(int batchId)
        {
            var ct = HttpContext.RequestAborted;
            var exams = await _managementService.GetBatchExamsAsync(Scope, batchId, ct);
            if (exams == null)
                return NotFound();

            return View(new PartnerPlacementBatchExamsVm
            {
                BatchId = batchId,
                BatchName = await _managementService.GetBatchNameAsync(Scope, batchId, ct) ?? string.Empty,
                Exams = exams
            });
        }

        // =====================================================
        // قوائم صفحات الإنشاء
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> GetSectionsByCourse(int courseId)
            => Json(await _managementService.GetSectionsByCourseAsync(Scope, courseId, HttpContext.RequestAborted));

        [HttpGet]
        public async Task<IActionResult> GetBatchesByCourse(int courseId)
            => Json(await _managementService.GetBatchesByCourseAsync(Scope, courseId, HttpContext.RequestAborted));

        [HttpGet]
        public async Task<IActionResult> GetModelSummary(int modelId)
        {
            var summary = await _managementService.GetModelSummaryAsync(Scope, modelId, HttpContext.RequestAborted);
            return summary == null ? NotFound() : Json(summary);
        }

        // =====================================================
        // CreateAdvanced
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> CreateAdvanced()
        {
            var model = await _managementService.GetCreateAdvancedAsync(Scope, HttpContext.RequestAborted);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdvancedPost(PlacementExamCreateAdvancedViewModel model)
        {
            var ct = HttpContext.RequestAborted;

            if (!ModelState.IsValid)
            {
                await _managementService.ReloadCreateAdvancedLookupsAsync(Scope, model, ct);
                return View("CreateAdvanced", model);
            }

            var result = await _managementService.CreateAdvancedAsync(Scope, model, ct);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Message;
                await _managementService.ReloadCreateAdvancedLookupsAsync(Scope, model, ct);
                return View("CreateAdvanced", model);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Review), new { examId = result.ExamId });
        }

        // =====================================================
        // CreateFromProfessionalModel
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> CreateFromProfessionalModel(int? modelId = null)
        {
            if (SubscriptionContext?.CanUseProfessionalModels != true)
            {
                TempData["Error"] = "النماذج الاحترافية غير مشمولة في عقدكم الحالي.";
                return RedirectToAction(nameof(Index));
            }

            var vm = await _managementService.GetCreateFromModelAsync(Scope, HttpContext.RequestAborted);

            // PPM-S5.2: رابط «تحديد مستوى من هذا النموذج». القائمة نفسها هي حد الإتاحة
            if (modelId.HasValue && vm.AvailableModels.Any(m => m.Value == modelId.Value.ToString()))
                vm.SelectedModelId = modelId.Value;

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromProfessionalModel(PlacementExamFromModelVm vm)
        {
            var ct = HttpContext.RequestAborted;

            if (SubscriptionContext?.CanUseProfessionalModels != true)
            {
                TempData["Error"] = "النماذج الاحترافية غير مشمولة في عقدكم الحالي.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _managementService.CreateFromProfessionalModelAsync(Scope, vm, ct);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Message;
                await _managementService.ReloadCreateFromModelLookupsAsync(Scope, vm, ct);
                return View(nameof(CreateFromProfessionalModel), vm);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // Review / ToggleDontKnowOption / PreviewQuestion
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> Review(int examId)
        {
            var model = await _managementService.GetReviewAsync(Scope, examId, HttpContext.RequestAborted);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleDontKnowOption(int examId, bool enabled)
        {
            var result = await _managementService.ToggleDontKnowOptionAsync(Scope, examId, enabled, HttpContext.RequestAborted);

            if (!result.Succeeded)
                return Json(new { success = false, message = result.Message });

            return Json(new { success = true, allowDontKnowOption = enabled, message = result.Message });
        }

        [HttpGet]
        public async Task<IActionResult> PreviewQuestion(int examId, Guid questionId)
        {
            var model = await _managementService.GetQuestionPreviewAsync(Scope, examId, questionId, HttpContext.RequestAborted);
            return model == null
                ? NotFound()
                : PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", model);
        }

        // =====================================================
        // ReplaceQuestion / AddQuestionFromBank
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ReplaceQuestion(int examId, Guid questionId)
        {
            var model = await _managementService.GetReplaceQuestionAsync(Scope, examId, questionId, HttpContext.RequestAborted);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplaceQuestion(int examId, Guid oldQuestionId, Guid newQuestionId)
        {
            var result = await _managementService.ReplaceQuestionAsync(Scope, examId, oldQuestionId, newQuestionId, HttpContext.RequestAborted);

            if (result.Outcome == PlacementOperationOutcome.NotFound)
                return NotFound();

            SetResultMessage(result);
            return RedirectToAction(nameof(Review), new { examId });
        }

        [HttpGet]
        public async Task<IActionResult> AddQuestionFromBank(int examId, int sectionId)
        {
            var (result, model) = await _managementService.GetAddQuestionFromBankAsync(Scope, examId, sectionId, HttpContext.RequestAborted);

            if (result.Outcome == PlacementOperationOutcome.NotFound)
                return NotFound();

            if (result.Outcome == PlacementOperationOutcome.BadRequest)
            {
                SetResultMessage(result);
                return RedirectToAction(nameof(Review), new { examId });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuestionFromBank(int examId, int sectionId, Guid questionId)
        {
            var result = await _managementService.AddQuestionFromBankAsync(Scope, examId, sectionId, questionId, HttpContext.RequestAborted);

            if (result.Outcome == PlacementOperationOutcome.NotFound)
                return NotFound();

            SetResultMessage(result);
            return RedirectToAction(nameof(Review), new { examId });
        }

        // =====================================================
        // PPL-S5: الإرسال
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ConfirmSend(int examId)
        {
            var summary = await _managementService.GetSendSummaryAsync(Scope, examId, HttpContext.RequestAborted);
            if (summary == null)
                return NotFound();

            if (!summary.IsDraft)
            {
                TempData["Info"] = "تم إرسال هذا الاختبار بالفعل، وتجد خيارات الطلاب الجدد وإعادة الإرسال هنا.";
                return RedirectToAction(nameof(PlacementDetails), new { id = examId });
            }

            return View(summary);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendExamToStudents(int examId)
            => DeliveryResult(await _managementService.SendToStudentsAsync(Scope, examId, HttpContext.RequestAborted), examId);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendExamToNewStudents(int examId, int batchId)
            => DeliveryResult(await _managementService.SendToNewStudentsAsync(Scope, examId, batchId, HttpContext.RequestAborted), examId);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BackfillMissingAssignments(int examId, int batchId)
            => DeliveryResult(await _managementService.BackfillMissingAssignmentsAsync(Scope, examId, batchId, HttpContext.RequestAborted), examId);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendExamToStudent(int examId, int studentId)
            => DeliveryResult(await _managementService.ResendToStudentAsync(Scope, examId, studentId, HttpContext.RequestAborted), examId);

        // الخطأ يعود للمراجعة لأن التفاصيل لا تعرض الاختبار المخفي
        private IActionResult DeliveryResult(PlacementOperationResult result, int examId)
        {
            if (result.Outcome == PlacementOperationOutcome.NotFound)
                return NotFound();

            SetResultMessage(result);

            return result.Outcome == PlacementOperationOutcome.Error
                ? RedirectToAction(nameof(Review), new { examId })
                : RedirectToAction(nameof(PlacementDetails), new { id = examId });
        }

        // =====================================================
        // Hide / Restore / Delete
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HideExam(int id, int? batchId)
        {
            SetResultMessage(await _managementService.HideAsync(Scope, id, HttpContext.RequestAborted));
            return RedirectToBatchOrIndex(batchId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreExam(int id, int? batchId)
        {
            SetResultMessage(await _managementService.RestoreAsync(Scope, id, HttpContext.RequestAborted));
            return RedirectToBatchOrIndex(batchId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExamPermanently(int examId, int batchId)
        {
            SetResultMessage(await _managementService.DeletePermanentlyAsync(Scope, examId, batchId, HttpContext.RequestAborted));
            return RedirectToBatchOrIndex(batchId);
        }

        private IActionResult RedirectToBatchOrIndex(int? batchId)
            => batchId.HasValue
                ? RedirectToAction(nameof(BatchExams), new { batchId })
                : RedirectToAction(nameof(Index));

        // =====================================================
        // PlacementDetails
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> PlacementDetails(int id)
        {
            var vm = await _managementService.GetPlacementDetailsAsync(Scope, id, HttpContext.RequestAborted);
            return vm == null ? NotFound() : View(vm);
        }

        // =====================================================
        // PPL-S6: التقارير والمراجعات (قراءة فقط)
        // =====================================================
        private bool IsReadOnly => SubscriptionContext == null;

        private PartnerPlacementReportPageVm<TReport> ReportPage<TReport>(TReport report, int examId, int batchId = 0, int studentId = 0, int assignmentId = 0)
            where TReport : class
            => new()
            {
                Report = report,
                ExamId = examId,
                BatchId = batchId,
                StudentId = studentId,
                AssignmentId = assignmentId,
                IsReadOnly = IsReadOnly
            };

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PlacementExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> BatchStudents(int examId, int batchId)
        {
            var data = await _reportService.GetBatchStudentsAsync(Scope, examId, batchId, HttpContext.RequestAborted);
            return data == null ? NotFound() : View(ReportPage(data, examId, batchId));
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PlacementExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> StudentExamDetails(int examId, int studentId, int batchId = 0)
        {
            var model = await _reportService.GetStudentExamDetailsAsync(Scope, examId, studentId, HttpContext.RequestAborted);
            return model == null ? NotFound() : View(ReportPage(model, examId, batchId, studentId));
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PlacementExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> ExamSectionAnalysis(int assignmentId, int studentId, int batchId = 0)
        {
            var result = await _reportService.GetSectionAnalysisAsync(Scope, assignmentId, studentId, HttpContext.RequestAborted);

            return result.Outcome == PlacementReportOutcome.Success
                ? View(ReportPage(result.Model!, result.Model!.ExamId, batchId, studentId, assignmentId))
                : NotFound();
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PlacementExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> ReviewLessonQuestions(int assignmentId, int studentId, int lessonId, int batchId = 0)
        {
            var result = await _reportService.GetReviewLessonQuestionsAsync(Scope, assignmentId, studentId, lessonId, HttpContext.RequestAborted);

            return result.Outcome switch
            {
                PlacementReportOutcome.Success => View(ReportPage(result.Model!, 0, batchId, studentId, assignmentId)),
                PlacementReportOutcome.NoData => View("ReportNoData", result.Message),
                _ => NotFound()
            };
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PlacementExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> ReviewLessonQuestionsBatch(int examId, int lessonId, int batchId = 0)
        {
            var model = await _reportService.GetReviewLessonQuestionsBatchAsync(Scope, examId, lessonId, HttpContext.RequestAborted);
            return model == null ? NotFound() : View(ReportPage(model, examId, batchId));
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PlacementExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> BatchPerformanceReport(int examId, int batchId)
        {
            var result = await _reportService.GetBatchPerformanceReportAsync(Scope, examId, batchId, HttpContext.RequestAborted);

            return result.Outcome switch
            {
                PlacementReportOutcome.Success => View(ReportPage(result.Model!, examId, batchId)),
                PlacementReportOutcome.NoData => View("ReportNoData", result.Message),
                _ => NotFound()
            };
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PlacementExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> StudentPlacementReport(int studentId, int examId, int assignmentId = 0, int batchId = 0)
        {
            if (examId <= 0)
                return NotFound();

            var ct = HttpContext.RequestAborted;
            var result = await _reportService.GetStudentPlacementReportAsync(Scope, studentId, assignmentId, examId, batchId, ct);

            if (result.Outcome == PlacementReportOutcome.NotFound)
                return NotFound();

            if (result.Outcome == PlacementReportOutcome.NoData)
                return View("ReportNoData", result.Message);

            var page = ReportPage(result.Model!, examId, batchId, studentId, result.Model!.AssignmentId);
            page.Branding = await _reportService.GetReportBrandingAsync(Scope, ct);
            return View(page);
        }

        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PlacementExams, AllowReadAfterExpiry = true)]
        public async Task<IActionResult> StudentPlacementReportPrint(int examId, int studentId)
        {
            var ct = HttpContext.RequestAborted;
            var result = await _reportService.GetStudentPlacementReportPrintAsync(Scope, examId, studentId, ct);

            if (result.Outcome == PlacementReportOutcome.NotFound)
                return NotFound();

            if (result.Outcome == PlacementReportOutcome.NoData)
                return View("ReportNoData", result.Message);

            var page = ReportPage(result.Model!, examId, result.BatchId ?? 0, studentId);
            page.Branding = await _reportService.GetReportBrandingAsync(Scope, ct);
            return View(page);
        }

        // DownloadReportChrome غير مكشوف عمدًا: يفتح Chrome بلا Cookies فيطبع صفحة الدخول
        [HttpGet]
        [PartnerFeature(PartnerFeatureType.PlacementExams, AllowReadAfterExpiry = true)]
        [EnableRateLimiting("ppl-report-pdf")] // PPL-S7.1: توليد PDF يستهلك المعالج
        public async Task<IActionResult> DownloadStudentReport(int examId, int studentId)
        {
            var file = await _reportService.BuildStudentReportPdfAsync(Scope, examId, studentId, HttpContext.RequestAborted);
            return file == null
                ? NotFound()
                : File(file.Value.Pdf, "application/pdf", file.Value.FileName);
        }
    }
}
