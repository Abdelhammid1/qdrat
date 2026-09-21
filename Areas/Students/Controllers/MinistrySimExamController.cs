using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    // اختبار محاكاة اختبار الوزارة — Sprint 9 (MSE-F): شاشة الترحيب بالمرحلة (F1) + شاشة حل الأسئلة (F2).
    // Sprint 10 (MSE-F): مؤقت 26 دقيقة بتغيير لون (F3) + LockStageAsync والفرض الفعلي للقفل أحادي الاتجاه +
    // الانتقال الإجباري عند انتهاء الوقت (F4) — عبر EnforceStageTimeLimitAsync المُستدعاة في بداية كل طلب على مرحلة مفتوحة،
    // وزر "إنهاء المرحلة" الذي كان مؤجَّلاً من Sprint 9 لاعتماده على LockStageAsync.
    // Sprint 11 (F5): next/prev/flag مقيّدة بنيويًا بأسئلة نفس المرحلة أصلاً (StageOrder داخل examId+stageNumber) —
    // أُضيف تحقق دفاعي صريح في SubmitAnswer يرفض أي stageQuestionId لا ينتمي فعليًا لـ (examId, stageNumber) المُرسَلين،
    // لمنع أي تلاعب مباشر بمعاملات الرابط/النموذج قبل الوصول لأي منطق آخر.
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class MinistrySimExamController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMinistrySimExamAttemptService _attemptService;
        private readonly IMinistrySimExamResultService _resultService;
        private readonly IIntegrityGuardService _integrityGuard;

        public MinistrySimExamController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IMinistrySimExamAttemptService attemptService,
            IMinistrySimExamResultService resultService,
            IIntegrityGuardService integrityGuard)
        {
            _context = context;
            _userManager = userManager;
            _attemptService = attemptService;
            _resultService = resultService;
            _integrityGuard = integrityGuard;
        }

        private async Task<int?> GetCurrentStudentIdAsync()
        {
            var userId = _userManager.GetUserId(User);
            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            return student?.StudentID;
        }

        // Sprint 17 (MSE-J / J3): مفتاح Session موحّد لتخزين نتيجة التحقق من الرمز المرجعي لهذا الطالب في هذا الاختبار
        private static string GetCodeGateSessionKey(int examId, int studentId) => $"MinistrySimExamCodeVerified_{examId}_{studentId}";

        // نقطة الدخول من قائمة الطالب (لم تكن موجودة في أي Sprint سابق — لا يوجد رابط دخول لهذه الميزة من واجهة
        // الطالب قبل هذا). تعرض كل اختبارات محاكاة الوزارة المنشورة والمُسنَدة فعليًا لهذا الطالب (مباشرة أو عبر
        // دفعة)، بحالة كل اختبار (لم يبدأ / قيد التقدّم / مكتمل) ورابط مناسب لكل حالة.
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var assignedDirectExamIds = await _context.MinistrySimExamAssignmentsToStudents
                .AsNoTracking()
                .Where(x => x.StudentId == studentId.Value)
                .Select(x => x.MinistrySimExamId)
                .ToListAsync();

            var studentBatchIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == studentId.Value && e.Status == "Active")
                .Select(e => e.BatchId)
                .ToListAsync();

            var assignedViaBatchExamIds = await _context.MinistrySimExamAssignmentsToBatches
                .AsNoTracking()
                .Where(x => EF.Constant(studentBatchIds).Contains(x.BatchId))
                .Select(x => x.MinistrySimExamId)
                .ToListAsync();

            var assignedExamIds = assignedDirectExamIds.Union(assignedViaBatchExamIds).Distinct().ToList();

            var exams = await _context.MinistrySimExams
                .AsNoTracking()
                .Include(e => e.Course)
                .Where(e => e.IsPublished && EF.Constant(assignedExamIds).Contains(e.Id))
                .OrderByDescending(e => e.PublishedAt)
                .ToListAsync();

            var attempts = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId.Value && EF.Constant(assignedExamIds).Contains(a.MinistrySimExamId))
                .ToListAsync();

            var vm = exams.Select(e =>
            {
                var attempt = attempts.FirstOrDefault(a => a.MinistrySimExamId == e.Id);
                return new MinistrySimExamDashboardItemVm
                {
                    MinistrySimExamId = e.Id,
                    Title = e.Title,
                    CourseName = e.Course?.Name,
                    IsCompleted = attempt?.IsCompleted ?? false,
                    IsStarted = attempt != null,
                    TotalScorePercent = attempt?.TotalScorePercent
                };
            }).ToList();

            return View(vm);
        }

        // F1: شاشة الترحيب بالمرحلة — تُنشئ/تستأنف محاولة الطالب وتحدّد المرحلة التالية المطلوب عرضها،
        // ثم تُعرض قبل استدعاء StartStageAsync (لا يبدأ العدّاد الفعلي للمرحلة إلا بعد ضغط الطالب على "ابدأ المرحلة").
        [HttpGet]
        public async Task<IActionResult> Welcome(int examId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // Sprint 17 (MSE-J / J3): بوابة الدخول للاختبار الحضوري — قبل أي وصول لمحتوى الاختبار (حتى قبل بدء/استئناف
            // المحاولة نفسها). Session تُستخدم كتخزين بسيط للتحقق المُنجَز (مرة واحدة لكل محاولة طالما جلسته مستمرة) —
            // اختبار أونلاين لا يُكلّف هذا الفحص أي شيء عمليًا (ValidateReferenceCodeAsync تُرجع true فورًا).
            var codeGateKey = GetCodeGateSessionKey(examId, studentId.Value);
            if (HttpContext.Session.GetString(codeGateKey) != "true")
            {
                var verified = await _attemptService.ValidateReferenceCodeAsync(examId, studentId.Value, null);
                if (!verified)
                    return RedirectToAction(nameof(EnterReferenceCode), new { examId });

                HttpContext.Session.SetString(codeGateKey, "true");
            }

            MinistrySimExamStudentAttempt attempt;
            try
            {
                attempt = await _attemptService.StartOrResumeAttemptAsync(examId, studentId.Value);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Index", "Dashboard");
            }

            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار.");

            var stageProgress = await _context.MinistrySimExamStudentStageProgresses
                .AsNoTracking()
                .Where(p => p.MinistrySimExamStudentAttemptId == attempt.Id)
                .ToListAsync();

            var currentUnlocked = stageProgress.FirstOrDefault(p => !p.IsLocked);
            int nextStageNumber = currentUnlocked != null
                ? currentUnlocked.StageNumber
                : stageProgress.Where(p => p.IsLocked).Select(p => p.StageNumber).DefaultIfEmpty(0).Max() + 1;

            var vm = new MinistrySimExamStageWelcomeVm
            {
                MinistrySimExamId = exam.Id,
                ExamTitle = exam.Title,
                StageNumber = nextStageNumber,
                TotalStages = exam.TotalStages
            };

            if (attempt.IsCompleted || nextStageNumber > exam.TotalStages)
            {
                vm.IsExamFinished = true;
                return View(vm);
            }

            var stage = await _context.MinistrySimExamStages
                .AsNoTracking()
                .Include(s => s.QuantSection)
                .Include(s => s.VerbalSection)
                .FirstOrDefaultAsync(s => s.MinistrySimExamId == examId && s.StageNumber == nextStageNumber);

            if (stage == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على هذه المرحلة ضمن الاختبار.";
                return RedirectToAction("Index", "Dashboard");
            }

            vm.QuantSectionTitle = stage.QuantSection?.Title ?? "المحور الكمي";
            vm.VerbalSectionTitle = stage.VerbalSection?.Title ?? "المحور اللفظي";
            vm.QuantQuestionCount = stage.QuantQuestionCount;
            vm.VerbalQuestionCount = stage.VerbalQuestionCount;
            vm.DurationMinutes = stage.DurationMinutes;
            vm.IsResuming = currentUnlocked != null;

            return View(vm);
        }

        // Sprint 17 (MSE-J / J3): شاشة إدخال الرمز المرجعي — تُعرض فقط لطالب مُسنَد له اختبار حضوري لم يتحقق منه بعد
        // في هذه الجلسة (Welcome يُحوِّل إليها). لا تُنشئ/تستأنف أي محاولة قبل التحقق.
        [HttpGet]
        public async Task<IActionResult> EnterReferenceCode(int examId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            if (HttpContext.Session.GetString(GetCodeGateSessionKey(examId, studentId.Value)) == "true")
                return RedirectToAction(nameof(Welcome), new { examId });

            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار.");

            return View(new MinistrySimExamEnterCodeVm { MinistrySimExamId = examId, ExamTitle = exam.Title });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnterReferenceCode(int examId, string referenceCode)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var verified = await _attemptService.ValidateReferenceCodeAsync(examId, studentId.Value, referenceCode);
            if (!verified)
            {
                TempData["Error"] = "❌ الكود المرجعي غير صحيح — تأكد منه مع المشرف في المعمل وحاول مرة أخرى.";
                return RedirectToAction(nameof(EnterReferenceCode), new { examId });
            }

            HttpContext.Session.SetString(GetCodeGateSessionKey(examId, studentId.Value), "true");
            return RedirectToAction(nameof(Welcome), new { examId });
        }

        // يُنفَّذ عند ضغط الطالب على زر "ابدأ المرحلة" من شاشة الترحيب — يستدعي StartStageAsync (يرفض تجاوز مراحل
        // سابقة غير مقفلة) ثم يحوّل الطالب لشاشة الحل.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartStage(int examId, int stageNumber)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var attempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.MinistrySimExamId == examId && a.StudentId == studentId);

            if (attempt == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على محاولتك لهذا الاختبار — ابدأ من شاشة الترحيب أولاً.";
                return RedirectToAction("Welcome", new { examId });
            }

            try
            {
                await _attemptService.StartStageAsync(attempt.Id, stageNumber);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Welcome", new { examId });
            }

            return RedirectToAction("Solve", new { examId, stageNumber });
        }

        // F2: شاشة حل الأسئلة — تعرض أسئلة المرحلة الحالية المفتوحة فقط، بترتيب StageOrder، سؤالاً واحدًا في كل مرة
        [HttpGet]
        public async Task<IActionResult> Solve(int examId, int stageNumber, Guid? q = null, bool review = false)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            if (await _integrityGuard.IsBlockedAsync(studentId.Value, IntegrityAttemptType.MinistrySim, examId))
                return RedirectToAction("Blocked", "IntegrityGuard", new { area = "Students", attemptType = (int)IntegrityAttemptType.MinistrySim, attemptEntityId = examId, returnUrl = $"{Request.Path}{Request.QueryString}" });

            var attempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.MinistrySimExamId == examId && a.StudentId == studentId);

            if (attempt == null)
                return RedirectToAction("Welcome", new { examId });

            var progress = await _context.MinistrySimExamStudentStageProgresses
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.MinistrySimExamStudentAttemptId == attempt.Id && p.StageNumber == stageNumber);

            if (progress == null)
            {
                TempData["ErrorMessage"] = "يجب بدء هذه المرحلة أولاً من شاشة الترحيب.";
                return RedirectToAction("Welcome", new { examId });
            }

            if (progress.IsLocked)
            {
                return await ContinueAfterStageAsync(examId, attempt.Id);
            }

            var timeExpired = await _attemptService.EnforceStageTimeLimitAsync(attempt.Id, stageNumber);
            if (timeExpired)
            {
                return await ContinueAfterStageAsync(examId, attempt.Id);
            }

            var vm = await BuildSolveViewModelAsync(examId, attempt.Id, stageNumber, q);
            if (vm == null)
                return NotFound("⚠️ لا توجد أسئلة في هذه المرحلة.");

            var stage = await _context.MinistrySimExamStages
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.MinistrySimExamId == examId && s.StageNumber == stageNumber);

            var deadline = progress.StartedAt.Value.AddMinutes(stage?.DurationMinutes ?? 26);
            var remaining = (int)Math.Floor((deadline - DateTime.Now).TotalSeconds);
            vm.RemainingSeconds = remaining > 0 ? remaining : 0;
            vm.IsReviewVisible = review;

            return View(vm);
        }

        // يحفظ إجابة/علامة مراجعة السؤال الحالي (إن وُجد تغيير) ثم ينتقل تاليًا/سابقًا ضمن نفس المرحلة فقط —
        // لا يوجد أي مسار هنا يفتح سؤالاً من مرحلة أخرى.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitAnswer(
            int examId, int stageNumber, int stageQuestionId, Guid currentQuestionId,
            int? selectedOptionIndex, string nav, bool review = false)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var attempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.MinistrySimExamId == examId && a.StudentId == studentId);

            if (attempt == null)
                return RedirectToAction("Welcome", new { examId });

            // F5: تحقق دفاعي ضد تلاعب مباشر بمعاملات الرابط/النموذج — stageQuestionId يجب أن ينتمي فعلاً
            // لنفس (examId, stageNumber) المُرسَلين، وإلا يُرفض الطلب فورًا قبل أي تأثير على next/prev/flag أو المؤقت.
            var realStage = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(sq => sq.Id == stageQuestionId)
                .Select(sq => new { sq.QuestionId, sq.MinistrySimExamStage.MinistrySimExamId, sq.MinistrySimExamStage.StageNumber })
                .FirstOrDefaultAsync();

            if (realStage == null || realStage.MinistrySimExamId != examId || realStage.StageNumber != stageNumber
                || realStage.QuestionId != currentQuestionId)
            {
                TempData["ErrorMessage"] = "طلب غير صالح — بيانات السؤال لا تطابق المرحلة الحالية.";
                return RedirectToAction("Welcome", new { examId });
            }

            var timeExpired = await _attemptService.EnforceStageTimeLimitAsync(attempt.Id, stageNumber);
            if (timeExpired)
            {
                return await ContinueAfterStageAsync(examId, attempt.Id);
            }

            var orderedIds = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(sq => sq.MinistrySimExamStage.MinistrySimExamId == examId && sq.MinistrySimExamStage.StageNumber == stageNumber)
                .OrderBy(sq => sq.StageOrder)
                .Select(sq => sq.QuestionId)
                .ToListAsync();

            Guid? reviewQuestionId = null;
            if (nav?.StartsWith("review:", StringComparison.Ordinal) == true)
            {
                if (!Guid.TryParse(nav.Substring("review:".Length), out var targetId) || !orderedIds.Contains(targetId))
                {
                    TempData["ErrorMessage"] = "يمكن مراجعة أسئلة المرحلة الحالية فقط.";
                    return RedirectToAction("Solve", new { examId, stageNumber, q = currentQuestionId });
                }

                reviewQuestionId = targetId;
            }

            var existingFlag = await _context.MinistrySimExamStudentAnswers
                .AsNoTracking()
                .Where(a => a.MinistrySimExamStudentAttemptId == attempt.Id && a.MinistrySimExamStageQuestionId == stageQuestionId)
                .Select(a => (bool?)a.IsFlaggedForReview)
                .FirstOrDefaultAsync();

            bool flagForReview = nav == "flag" ? true
                : nav == "unflag" ? false
                : existingFlag ?? false;

            try
            {
                if (selectedOptionIndex.HasValue || nav == "flag" || nav == "unflag")
                {
                    await _attemptService.SubmitAnswerAsync(attempt.Id, stageQuestionId, selectedOptionIndex, flagForReview);
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Welcome", new { examId });
            }

            int idx = orderedIds.IndexOf(currentQuestionId);
            if (idx == -1) idx = 0;

            Guid nextQuestionId = reviewQuestionId ?? currentQuestionId;
            if (nav == "next" && idx + 1 < orderedIds.Count)
                nextQuestionId = orderedIds[idx + 1];
            else if (nav == "prev" && idx > 0)
                nextQuestionId = orderedIds[idx - 1];

            return RedirectToAction("Solve", new
            {
                examId,
                stageNumber,
                q = nextQuestionId,
                review = review || nav == "review" || reviewQuestionId.HasValue
            });
        }

        // Sprint 10 (F4): إنهاء المرحلة اختياريًا قبل انتهاء الوقت — كان مؤجَّلاً من Sprint 9 لاعتماده على LockStageAsync.
        // بعد هذا الاستدعاء لا يمكن الرجوع لهذه المرحلة إطلاقًا (لا رجوع في IsLocked من أي مسار كود).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FinishStage(int examId, int stageNumber)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var attempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.MinistrySimExamId == examId && a.StudentId == studentId);

            if (attempt == null)
                return RedirectToAction("Welcome", new { examId });

            try
            {
                await _attemptService.LockStageAsync(attempt.Id, stageNumber, timeExpired: false);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Welcome", new { examId });
            }

            var isCompleted = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .AnyAsync(a => a.Id == attempt.Id && a.IsCompleted);

            // Manual completion shows the next welcome screen without starting its timer.
            return RedirectToAction(isCompleted ? "Result" : "Welcome", new { examId });
        }

        // Automatic timeout continues in solving mode without resetting an existing timer.
        private async Task<IActionResult> ContinueAfterStageAsync(int examId, int attemptId)
        {
            var nextStageNumber = await _context.MinistrySimExamStages
                .AsNoTracking()
                .Where(s => s.MinistrySimExamId == examId
                    && !_context.MinistrySimExamStudentStageProgresses.Any(p =>
                        p.MinistrySimExamStudentAttemptId == attemptId
                        && p.StageNumber == s.StageNumber && p.IsLocked))
                .OrderBy(s => s.StageNumber)
                .Select(s => (int?)s.StageNumber)
                .FirstOrDefaultAsync();

            if (!nextStageNumber.HasValue)
                return RedirectToAction("Result", new { examId });

            try
            {
                await _attemptService.StartStageAsync(attemptId, nextStageNumber.Value);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction("Welcome", new { examId });
            }

            return RedirectToAction("Solve", new { examId, stageNumber = nextStageNumber.Value });
        }

        // Sprint 12 (G2): شاشة نتيجة بسيطة — نتيجة إجمالية + نتيجة كل مرحلة، متاحة فقط بعد اكتمال المحاولة (IsCompleted).
        // Sprint 14 (MSE-H / H1): منطق الحساب انتقل بالكامل إلى IMinistrySimExamResultService — هذا الـ Action الآن
        // فقط يترجم كل حالة من Status إلى نفس سلوك التوجيه/الرسائل الذي كان مكتوبًا هنا مباشرة قبل الاستخراج.
        [HttpGet]
        public async Task<IActionResult> Result(int examId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var lookup = await _resultService.GetResultAsync(examId, studentId.Value);

            switch (lookup.Status)
            {
                case MinistrySimExamResultStatus.AttemptNotFound:
                    return RedirectToAction("Welcome", new { examId });

                case MinistrySimExamResultStatus.NotCompleted:
                    TempData["ErrorMessage"] = "لم تُتم مراحل هذا الاختبار بعد.";
                    return RedirectToAction("Welcome", new { examId });

                case MinistrySimExamResultStatus.ExamNotFound:
                    return NotFound("❌ لم يتم العثور على هذا الاختبار.");

                default:
                    return View(lookup.Vm);
            }
        }

        // Sprint 13 (MSE-G / G6): مراجعة أسئلة/إجابات مرحلة واحدة سؤالاً بسؤال — نفس نمط
        // StudentHomeworkResultsController.Review، لكن المصدر هنا MinistrySimExamStudentAnswer +
        // MinistrySimExamStageQuestion (بدل QuestionAttemptNew)، وGlobalOrder يُعرض كما هو محفوظ من
        // مرحلة التوليد دون إعادة حساب. متاحة فقط بعد اكتمال المحاولة (نفس شرط شاشة النتيجة).
        // Sprint 14 (MSE-H / H1): منطق الحساب انتقل بالكامل إلى IMinistrySimExamResultService.
        [HttpGet]
        public async Task<IActionResult> QuestionReview(int examId, int stageNumber)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var lookup = await _resultService.GetQuestionReviewAsync(examId, studentId.Value, stageNumber);

            switch (lookup.Status)
            {
                case MinistrySimExamQuestionReviewStatus.AttemptNotFound:
                    return RedirectToAction("Welcome", new { examId });

                case MinistrySimExamQuestionReviewStatus.NotCompleted:
                    TempData["ErrorMessage"] = "لم تُتم مراحل هذا الاختبار بعد.";
                    return RedirectToAction("Welcome", new { examId });

                case MinistrySimExamQuestionReviewStatus.StageNotFound:
                    return NotFound("❌ لم يتم العثور على مرحلة اختبار معمل القياس المطلوبة.");

                default:
                    return View(lookup.Vm);
            }
        }

        private async Task<MinistrySimExamSolveVm> BuildSolveViewModelAsync(int examId, int attemptId, int stageNumber, Guid? currentQuestionId)
        {
            var stageQuestions = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(sq => sq.MinistrySimExamStage.MinistrySimExamId == examId && sq.MinistrySimExamStage.StageNumber == stageNumber)
                .OrderBy(sq => sq.StageOrder)
                .Select(sq => new { sq.Id, sq.QuestionId, sq.StageOrder })
                .ToListAsync();

            if (!stageQuestions.Any())
                return null;

            var currentEntry = currentQuestionId.HasValue
                ? stageQuestions.FirstOrDefault(x => x.QuestionId == currentQuestionId.Value)
                : null;
            currentEntry ??= stageQuestions.First();

            var question = await _context.Questions
                .AsNoTracking()
                .Include(qn => qn.Options)
                .Include(qn => qn.Lesson).ThenInclude(l => l.Section)
                .Include(qn => qn.VerbalPassage)
                .Include(qn => qn.Curriculum)
                .FirstOrDefaultAsync(qn => qn.Id == currentEntry.QuestionId);

            if (question == null)
                return null;

            var stageAnswers = await _context.MinistrySimExamStudentAnswers
                .AsNoTracking()
                .Where(a => a.MinistrySimExamStudentAttemptId == attemptId
                    && a.StageQuestion.MinistrySimExamStage.MinistrySimExamId == examId
                    && a.StageQuestion.MinistrySimExamStage.StageNumber == stageNumber)
                .Select(a => new { a.MinistrySimExamStageQuestionId, a.SelectedOptionId, a.IsFlaggedForReview })
                .ToDictionaryAsync(a => a.MinistrySimExamStageQuestionId);

            stageAnswers.TryGetValue(currentEntry.Id, out var currentAnswer);

            return new MinistrySimExamSolveVm
            {
                MinistrySimExamId = examId,
                StageNumber = stageNumber,
                StageQuestionId = currentEntry.Id,
                CurrentQuestionId = currentEntry.QuestionId,
                CurrentIndex = currentEntry.StageOrder,
                Total = stageQuestions.Count,
                Question = question.ToDisplayModel(),
                SelectedOptionIndex = currentAnswer?.SelectedOptionId,
                IsFlaggedForReview = currentAnswer?.IsFlaggedForReview ?? false,
                ReviewQuestions = stageQuestions.Select(sq =>
                {
                    stageAnswers.TryGetValue(sq.Id, out var answer);
                    return new MinistrySimExamStageNavigationVm
                    {
                        QuestionId = sq.QuestionId,
                        Number = sq.StageOrder,
                        IsAnswered = answer?.SelectedOptionId.HasValue ?? false,
                        IsFlaggedForReview = answer?.IsFlaggedForReview ?? false
                    };
                }).ToList()
            };
        }
    }
}
