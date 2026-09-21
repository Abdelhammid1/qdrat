using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using QdratNew.Constants;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Question;
using QdratNew.ViewModels.Reports;
using QdratNew.ViewModels.Students;
using System.Text.Json;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class PlacementExamsController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ITimeZoneService _timeZoneService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IExamRecommendationService _recommendationService;
        private readonly IMemoryCache _cache;
        private readonly IStudentExamStatisticsService _studentExamStatisticsService;
        private readonly ILogger<PlacementExamsController> _logger;
        private readonly IIntegrityGuardService _integrityGuard;

        public PlacementExamsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ITimeZoneService timeZoneService,
            UserManager<ApplicationUser> userManager,
            IExamRecommendationService recommendationService, IMemoryCache cache, IStudentExamStatisticsService studentExamStatisticsService,
            ILogger<PlacementExamsController> logger,
            IIntegrityGuardService integrityGuard)
        {
            _contextFactory = contextFactory;
            _timeZoneService = timeZoneService;
            _userManager = userManager;
            _recommendationService = recommendationService;
            _cache = cache;
            _studentExamStatisticsService = studentExamStatisticsService;
            _logger = logger;
            _integrityGuard = integrityGuard;
        }

        // ✅ بدء اختبار تحديد المستوى
        [HttpGet]
        public async Task<IActionResult> StartPlacementExam(int assignmentId, Guid? q = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            if (await _integrityGuard.IsBlockedAsync(studentId.Value, IntegrityAttemptType.Placement, assignmentId))
                return RedirectToAction("Blocked", "IntegrityGuard", new { area = "Students", attemptType = (int)IntegrityAttemptType.Placement, attemptEntityId = assignmentId, returnUrl = $"{Request.Path}{Request.QueryString}" });

            // 🟢 جلب بيانات تعيين اختبار تحديد المستوى للطالب
            var assignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId);

            if (assignment == null || assignment.Exam == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار.");

            var exam = assignment.Exam;

            if (!string.IsNullOrWhiteSpace(exam.ReferenceCode))
            {
                var verifiedKey = $"VerifiedPlacementExam_{assignmentId}";
                if (HttpContext.Session.GetString(verifiedKey) != "true")
                {
                    return RedirectToAction(nameof(VerifyPlacementExamCode), new { assignmentId });
                }
            }

            // 🧩 جلب الأسئلة الخاصة بالاختبار
            var examQuestions = await _context.ExamQuestions
                .Where(eq => eq.ExamId == exam.Id)
                .OrderBy(eq => eq.Order)
                .ToListAsync();

            var allQuestionIds = examQuestions.Select(eq => eq.QuestionId).ToList();
            if (!allQuestionIds.Any())
                return NotFound("⚠️ لا يوجد أسئلة لهذا الاختبار.");

            // 🧠 جلب أو إنشاء حالة الطالب في الاختبار
            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.ExamId == exam.Id);



            // ✅ ✅ أضف هذا الشرط هنا مباشرة
            if (status != null && status.IsSubmitted)
            {
                // الطالب أنهى الاختبار سابقًا، نحوله مباشرة إلى صفحة النتيجة
                return RedirectToAction("ResultPlacementExam", new { assignmentId });
            }




            if (status != null && (status.StartedAt == null || status.EndAt == null))
            {
                // 🧩 تحديث سجل ناقص قديم (مثل حالة 901)
                status.StartedAt ??= DateTime.UtcNow;
                status.EndAt ??= DateTime.UtcNow.AddMinutes(exam.DurationMinutes);
                status.Status = ExamStatus.InProgress;
                status.IsSubmitted = false;

                _context.ExamStudentStatuses.Update(status);
                await _context.SaveChangesAsync();
            }
            else if (status == null)
            {
                // 🟢 أول دخول للطالب على الاختبار
                status = new ExamStudentStatus
                {
                    StudentId = studentId.Value,
                    ExamId = exam.Id,
                    ExamAssignmentId = null, // ✅ لا يوجد سجل في ToBatches
                    StartedAt = DateTime.UtcNow,
                    EndAt = DateTime.UtcNow.AddMinutes(exam.DurationMinutes),
                    Status = ExamStatus.InProgress,
                    IsSubmitted = false
                };

                _context.ExamStudentStatuses.Add(status);
                await _context.SaveChangesAsync();
            }

            // ⏰ تحقق من انتهاء الوقت
            if (status.EndAt.HasValue && DateTime.UtcNow > status.EndAt.Value)
            {
                status.IsSubmitted = true;
                status.Status = ExamStatus.Completed;
                status.SubmittedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Content(@"
<script src='https://cdn.jsdelivr.net/npm/sweetalert2@11'></script>
<script>
Swal.fire({
    icon: 'warning',
    title: 'انتهى وقت الاختبار',
    text: 'تم إنهاء اختبار تحديد المستوى تلقائيًا لأن الوقت المحدد انتهى.',
    confirmButtonText: 'عرض النتيجة'
}).then(() => {
    window.location.href = '/Students/PlacementExams/ResultPlacementExam/" + assignmentId + @"';
});
</script>", "text/html");
            }

            // 🧭 تحديد السؤال الحالي
            Guid currentQuestionId = q.HasValue && allQuestionIds.Contains(q.Value)
                ? q.Value
                : allQuestionIds.First();

            // 🧩 تحميل السؤال الحالي
            var question = await _context.Questions
                .Include(qq => qq.Options)
                .Include(qq => qq.Lesson).ThenInclude(l => l.Section)
                .Include(q => q.Curriculum)
                .Include(qq => qq.VerbalPassage)
                .FirstOrDefaultAsync(qq => qq.Id == currentQuestionId);

            // 📝 محاولات الطالب السابقة في نفس الاختبار فقط
            var allAttempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.ExamId == exam.Id)
                .ToListAsync();

            var attempt = allAttempts.FirstOrDefault(a => a.QuestionId == currentQuestionId);

            // 🔖 الأسئلة المحددة للمراجعة
            var marked = TempData["ReviewMarked"] as string ?? "";
            TempData.Keep("ReviewMarked");
            var reviewIds = marked.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(x => Guid.TryParse(x, out _))
                .Select(Guid.Parse)
                .ToList();

            bool isLast = currentQuestionId == allQuestionIds.Last();
            bool isManualJump = q.HasValue && allQuestionIds.Contains(q.Value) && currentQuestionId != allQuestionIds.First();

            // 🕒 حساب الوقت المتبقي
            int remainingSeconds = 0;
            if (status.EndAt.HasValue)
            {
                remainingSeconds = (int)(status.EndAt.Value - DateTime.UtcNow).TotalSeconds;
                if (remainingSeconds < 0)
                    remainingSeconds = 0;
            }

            // ✅ بناء ViewModel النهائي
            var vm = new ExamSolveViewModel
            {
                ExamId = exam.Id,
                ExamAssignmentId = assignment.Id,
                CurrentQuestionId = currentQuestionId,
                AllQuestionIds = allQuestionIds,
                Question = question?.ToDisplayModel(),
                SelectedAnswer = attempt?.SelectedAnswer,
                AnswersMap = allAttempts.ToDictionary(a => a.QuestionId, a => a.SelectedAnswer),
                DurationMinutes = exam.DurationMinutes,
                StartTime = _timeZoneService.GetNowUtc(),
                IsSubmitted = status.IsSubmitted,
                ReviewMarkedIds = reviewIds,
                RemainingSeconds = remainingSeconds,
                ForceReviewVisible = isManualJump || isLast,
                ExamTitle = exam.Title,
                SubmitAnswerUrl = "/Students/PlacementExams/SubmitAnswerFetch",
                FinalSubmitUrl = "/Students/PlacementExams/SubmitFinal",
                ExamType = "Placement"
            };

            // 🆕 إضافة خيار "لا أعرف الإجابة" ديناميكيًا للعرض فقط (بدون تخزينه كـ QuestionOption حقيقي)
            // النص يتبع اتجاه المنهج (Curriculum.IsRTL): عربي لو مفعّل، إنجليزي لو غير مفعّل
            if (exam.AllowDontKnowOption && vm.Question != null)
            {
                bool curriculumIsRTL = question?.Curriculum?.IsRTL ?? true;
                string dontKnowText = ExamAnswerConstants.GetDontKnowOptionText(curriculumIsRTL);

                vm.Question.Options.Add(new QdratNew.ViewModels.Question.QuestionOptionDisplayViewModel
                {
                    Text = dontKnowText,
                    IsSelected = ExamAnswerConstants.IsDontKnowOption(vm.SelectedAnswer)
                });
            }

            // 🧠 تخزين قائمة الأسئلة في الجلسة
            string sessionKey = $"PlacementExamQs_{assignmentId}_{studentId}";
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(allQuestionIds));

            return View("StartPlacementExam", vm);
        }

        [HttpGet]
        public async Task<IActionResult> VerifyPlacementExamCode(int assignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var assignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId);

            if (assignment == null || assignment.Exam == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار.");

            if (string.IsNullOrWhiteSpace(assignment.Exam.ReferenceCode))
                return RedirectToAction(nameof(StartPlacementExam), new { assignmentId });

            return View("VerifyPlacementExamCode", assignmentId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyPlacementExamCode(int assignmentId, string code)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var assignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId);

            if (assignment == null || assignment.Exam == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار.");

            if (string.IsNullOrWhiteSpace(assignment.Exam.ReferenceCode))
                return RedirectToAction(nameof(StartPlacementExam), new { assignmentId });

            if (!string.Equals(assignment.Exam.ReferenceCode.Trim(), code?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "❌ الكود المرجعي غير صحيح.";
                return View("VerifyPlacementExamCode", assignmentId);
            }

            HttpContext.Session.SetString($"VerifiedPlacementExam_{assignmentId}", "true");
            return RedirectToAction(nameof(StartPlacementExam), new { assignmentId });
        }

        // ✅ حفظ إجابة الطالب والتنقل

        [HttpGet]
        public async Task<IActionResult> PlacementExamDashboard()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // =========================================
            // 1️⃣ تحميل الطالب
            // =========================================
            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("لم يتم العثور على الطالب.");

            // =========================================
            // 2️⃣ تحميل اختبارات تحديد المستوى فقط
            // =========================================
            var assignments = await (
                from ea in _context.ExamAssignments.AsNoTracking()
                join e in _context.Exams.AsNoTracking() on ea.ExamId equals e.Id
                where ea.StudentId == studentId
                      && e.Type == ExamType.LevelAssessment
                select new
                {
                    ea.Id,
                    ea.ExamId,
                    e.Title,
                    ea.AssignedAt
                }
            )
            .OrderByDescending(x => x.AssignedAt)
            .ToListAsync();

            // =========================================
            // 3️⃣ تحميل الحالات (مرة واحدة)
            // =========================================
            var statuses = await _context.ExamStudentStatuses
                .AsNoTracking()
                .Where(s => s.StudentId == studentId)
                .ToListAsync();

            // =========================================
            // 4️⃣ تحميل المحاولات (مرة واحدة)
            // =========================================
            var attempts = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.StudentId == studentId)
                .ToListAsync();

            // =========================================
            // 5️⃣ بناء البيانات
            // =========================================
            var exams = assignments.Select(a =>
            {
                var status = statuses
                    .Where(s => s.ExamId == a.ExamId)
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefault();

                var examAttempts = attempts
                    .Where(x => x.ExamAssignmentId == a.Id)
                    .ToList();

                int totalAnswered = examAttempts.Count;
                int correct = examAttempts.Count(x => x.IsCorrect);

                double score = totalAnswered > 0
                    ? Math.Round(correct * 100.0 / totalAnswered, 2)
                    : 0;

                return new PlacementExamVm
                {
                    ExamAssignmentId = a.Id,
                    ExamId = a.ExamId,
                    Title = a.Title,
                    AssignedAt = a.AssignedAt,

                    // الحالة الحقيقية
                    IsSubmitted = status?.IsSubmitted ?? false,

                    // إضافة حالة وسطية
                    IsInProgress = status != null && !status.IsSubmitted,

                    Score = score
                };
            })
            .OrderByDescending(x => x.AssignedAt)
            .ToList();

            // =========================================
            // 6️⃣ ViewModel
            // =========================================
            var vm = new PlacementExamDashboardViewModel
            {
                StudentName = student.FullName,
                TotalPlacementExams = exams.Count,
                PlacementExams = exams,

                InitialScore = exams.FirstOrDefault()?.Score ?? 0,
                LatestScore = exams.LastOrDefault(x => x.Score > 0)?.Score ?? 0,
                Improvement = (exams.LastOrDefault(x => x.Score > 0)?.Score ?? 0)
                              - (exams.FirstOrDefault()?.Score ?? 0),

                ProgressTimeline = exams.Select(x => new PlacementExamProgressEntry
                {
                    ExamDate = x.AssignedAt,
                    Score = x.Score
                }).ToList(),

                Strengths = new(),
                Weaknesses = new(),
                Recommendation = "📊 سيتم تحليل الأداء بعد إكمال الاختبارات"
            };

            return View(vm);
        }


        [HttpPost]
        public async Task<IActionResult> SubmitAnswerFetch(
        int id, Guid q, string nav, string? SelectedOption, int? timeTakenSeconds = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            try
            {
                var studentId = await GetCurrentStudentIdAsync();
                if (studentId == null)
                    return Json(new { success = false, message = "❌ لم يتم التعرف على الطالب." });

                // 🟢 جلب ExamId سواء من ExamAssignments أو ExamAssignmentsToBatches
                var examId = await _context.ExamAssignments
                    .Where(a => a.Id == id)
                    .Select(a => a.ExamId)
                    .FirstOrDefaultAsync();

                if (examId == 0)
                {
                    examId = await _context.ExamAssignmentsToBatches
                        .Where(a => a.Id == id)
                        .Select(a => a.ExamId ?? 0)
                        .FirstOrDefaultAsync();
                }

                if (examId == 0)
                    return Json(new { success = false, message = "⚠️ لم يتم العثور على الاختبار." });

                // 🟢 جلب الأسئلة من Session أو قاعدة البيانات
                string listKey = $"PlacementExamQs_{id}_{studentId}";
                List<Guid> allQuestions;

                if (HttpContext.Session.TryGetValue(listKey, out var bytes))
                    allQuestions = JsonSerializer.Deserialize<List<Guid>>(bytes) ?? new();
                else
                {
                    allQuestions = await _context.ExamQuestions
                        .Where(eq => eq.ExamAssignmentId == id || eq.ExamId == examId)
                        .OrderBy(eq => eq.Order)
                        .Select(eq => eq.QuestionId)
                        .ToListAsync();

                    HttpContext.Session.SetString(listKey, JsonSerializer.Serialize(allQuestions));
                }

                if (!allQuestions.Any())
                    return Json(new { success = false, message = "⚠️ لا توجد أسئلة مرتبطة بالاختبار." });

                // ✅ رفض أي سؤال ليس ضمن أسئلة هذا التكليف تحديدًا
                if (!allQuestions.Contains(q))
                {
                    _logger.LogWarning(
                        "PlacementExamsController: محاولة تسجيل سؤال {QuestionId} غير منتمٍ لتكليف {AssignmentId} " +
                        "(طالب {StudentId}) — تم الرفض.", q, id, studentId);

                    return Json(new { success = false, message = "⚠️ هذا السؤال لا ينتمي لهذا الاختبار." });
                }

                // كشف التلاعب الزمني
                int safeTimeTaken = (timeTakenSeconds.HasValue && timeTakenSeconds.Value > 0) ? timeTakenSeconds.Value : 1;
                var _qStartKey = $"QStart_{id}_{studentId}_{q}";
                var _qStartStr = HttpContext.Session.GetString(_qStartKey);
                if (!string.IsNullOrEmpty(_qStartStr) && DateTime.TryParse(_qStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var _qStartTime))
                {
                    double serverTimeTaken = (DateTime.UtcNow - _qStartTime).TotalSeconds;
                    if (Math.Abs(serverTimeTaken - safeTimeTaken) > 30)
                    {
                        _context.SuspiciousActivities.Add(new SuspiciousActivity
                        {
                            StudentId = studentId.Value,
                            ExamContextId = id,
                            QuestionId = q,
                            ClientTimeTakenSeconds = safeTimeTaken,
                            ServerTimeTakenSeconds = serverTimeTaken,
                            ExamType = "Placement",
                            DetectedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                // 🟢 حفظ الإجابة
                if (!string.IsNullOrWhiteSpace(SelectedOption))
                {
                    var question = await _context.Questions.FirstOrDefaultAsync(x => x.Id == q);
                    if (question != null)
                    {
                        // 🆕 "لا أعرف الإجابة" (بالعربي أو الإنجليزي حسب اتجاه المنهج): تُحتسب دائمًا كإجابة خاطئة، وتُميَّز بعلم منفصل للإحصائيات
                        bool isDontKnowAnswer = ExamAnswerConstants.IsDontKnowOption(SelectedOption);

                        bool isCorrect = !isDontKnowAnswer && string.Equals(
                            SelectedOption.Trim(),
                            question.CorrectAnswer?.Trim(),
                            StringComparison.OrdinalIgnoreCase);

                        var attempt = await _context.QuestionAttemptNew
                            .FirstOrDefaultAsync(a =>
                                a.StudentId == studentId &&
                                a.QuestionId == q &&
                                (a.ExamAssignmentId == id || a.ExamId == examId));

                        if (attempt == null)
                        {
                            attempt = new QuestionAttemptNew
                            {
                                StudentId = studentId.Value,
                                QuestionId = q,
                                ExamId = examId,
                                ExamAssignmentId = id,
                                AttemptedAt = DateTime.UtcNow,
                                SelectedAnswer = SelectedOption,
                                IsCorrect = isCorrect,
                                IsDontKnowAnswer = isDontKnowAnswer,
                                TimeTakenSeconds = timeTakenSeconds ?? 0,
                                LessonId = question.LessonId,
                                SectionId = question.SectionId
                            };
                            _context.QuestionAttemptNew.Add(attempt);
                        }
                        else
                        {
                            attempt.SelectedAnswer = SelectedOption;
                            attempt.IsCorrect = isCorrect;
                            attempt.IsDontKnowAnswer = isDontKnowAnswer;
                            attempt.TimeTakenSeconds = timeTakenSeconds ?? attempt.TimeTakenSeconds;
                            attempt.AttemptedAt = DateTime.UtcNow;
                            _context.QuestionAttemptNew.Update(attempt);
                        }

                        await _context.SaveChangesAsync();
                    }
                }


                // 🟦 معالجة القفز المباشر من لوحة المراجعة
                // 🟦 معالجة القفز المباشر من لوحة المراجعة
                // 🟦 القفز لسؤال من لوحة المراجعة
                if (nav == "goto")
                {
                    var targetStr = HttpContext.Request.Form["target"].ToString();

                    if (Guid.TryParse(targetStr, out Guid targetQ) && allQuestions.Contains(targetQ))
                    {
                        var vmGoto = await GetExamSolveViewModel(id, targetQ, studentId.Value, examId);

                        vmGoto.IsReviewMode = true;
                        vmGoto.ForceReviewVisible = true;
                        vmGoto.SubmitAnswerUrl = "/Students/PlacementExams/SubmitAnswerFetch";
                        vmGoto.FinalSubmitUrl  = "/Students/PlacementExams/SubmitFinal";
                        vmGoto.ExamType        = "Placement";
                        HttpContext.Session.SetString($"QStart_{id}_{studentId}_{targetQ}", DateTime.UtcNow.ToString("O"));
                        return PartialView("_SolveUnifiedExamPartial", vmGoto);
                    }
                }




                // 🧭 التنقل بين الأسئلة
                int currentIndex = allQuestions.FindIndex(x => x == q);
                int nextIndex = currentIndex;

                if (nav == "next" && currentIndex < allQuestions.Count - 1)
                    nextIndex++;
                else if (nav == "prev" && currentIndex > 0)
                    nextIndex--;

                var nextQuestionId = allQuestions[nextIndex];

                var nextVm = await GetExamSolveViewModel(id, nextQuestionId, studentId.Value, examId);
                nextVm.SubmitAnswerUrl = "/Students/PlacementExams/SubmitAnswerFetch";
                nextVm.FinalSubmitUrl  = "/Students/PlacementExams/SubmitFinal";
                nextVm.ExamType        = "Placement";
                HttpContext.Session.SetString($"QStart_{id}_{studentId}_{nextQuestionId}", DateTime.UtcNow.ToString("O"));
                return PartialView("_SolveUnifiedExamPartial", nextVm);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"⚠️ خطأ أثناء حفظ الإجابة: {ex.Message}" });
            }
        }



        [HttpPost]
        public async Task<IActionResult> SaveReviewBehavior(
            [FromBody] SaveReviewBehaviorDto dto)
        {
            using var _context = _contextFactory.CreateDbContext();
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null) return Json(new { success = false });

            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    (s.ExamAssignmentId == dto.ExamAssignmentId || s.ExamId == dto.ExamAssignmentId));

            if (status != null)
            {
                status.ReviewBehaviorJson = dto.ReviewNavLog;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> ResultPlacementExam(int assignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
            {
                // 🔒 لتجنب حلقة التوجيه إلى نفس الصفحة باستمرار
                if (HttpContext.User.Identity?.IsAuthenticated == true)
                    return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لم يتم العثور على بيانات الطالب الحالية.</div>", "text/html");

                return RedirectToAction("Login", "Account", new { area = "" });
            }

            // 🟩 من هنا يبدأ المنطق الآمن
            var assignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId);

            if (assignment == null)
                return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لم يتم العثور على بيانات الاختبار.</div>", "text/html");

            var exam = assignment.Exam;
            if (exam == null)
                return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لم يتم العثور على بيانات الاختبار.</div>", "text/html");

            // ✅ استدعاء الخدمة الموحدة كما في ExamResult
            var summary = await _studentExamStatisticsService.GetExamResultAsync(assignmentId, studentId.Value);

            if (summary == null || summary.TotalQuestions == 0)
                return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لا توجد نتائج متاحة بعد.</div>", "text/html");

            var vm = new QdratNew.ViewModels.Students.PlacementExamResultVm
            {
                ExamTitle = exam.Title,
                TotalQuestions = summary.TotalQuestions,
                CorrectAnswers = summary.CorrectAnswers,
                AnsweredQuestions = summary.AnsweredQuestions,
                WrongAnswers = summary.WrongAnswers,            // ✅ أضف هذا السطر
                DontKnowAnswers = summary.DontKnowAnswers,       // 🆕 إحصائية "لا أعرف الإجابة"

                SkippedQuestions = summary.SkippedQuestions,
                Score = summary.ScorePercentage,
                TimeSpentFormatted = $"{summary.SolveMinutes} دقيقة تقريبًا",
                AssignedAt = assignment.AssignedAt,
                SubmittedAt = DateTime.UtcNow
            };

            return View("~/Areas/Students/Views/PlacementExams/ResultPlacementExam.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> ReviewLessonQuestions(int lessonId, int assignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var assignment = await _context.ExamAssignments
                .AsNoTracking()
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId);

            if (assignment == null)
                return NotFound("⚠️ لم يتم العثور على بيانات التعيين.");

            var exam = assignment.Exam;
            if (exam == null)
                return NotFound("⚠️ لم يتم العثور على بيانات الاختبار.");

            // 🔥 جلب الأسئلة مع كل البيانات المطلوبة
            var raw = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions
                    .Include(q => q.Options)
                    .Include(q => q.Curriculum)
                    .Include(q => q.VerbalPassage)
                on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId
                      && (a.ExamAssignmentId == assignmentId || a.ExamId == exam.Id)
                      && l.Id == lessonId
                select new
                {
                    q.Id,
                    q.Title,
                    a.IsCorrect,
                    a.IsDontKnowAnswer,
                    a.SelectedAnswer,
                    q.CorrectAnswer,
                    a.TimeTakenSeconds,
                    q.ImageUrl,
                    q.Template,
                    q.ValueA,
                    q.ValueB,
                    q.IsQuantitative,

                    // 🔥 RTL
                    IsRTL = q.Curriculum.IsRTL,

                    // 🔥 القطعة
                    PassageTitle = q.VerbalPassage != null ? q.VerbalPassage.Title : null,
                    PassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                    PassageType = (PassageType?)q.VerbalPassage.Type,
                    PassageMediaUrl = q.VerbalPassage != null ? q.VerbalPassage.MediaUrl : null,

                    // 🔥 الشرح
                    Explanation = q.Explanation,
                    VideoUrl = q.VideoUrl,

                    Options = q.Options.Select(opt => new
                    {
                        opt.Text,
                        opt.ImageUrl
                    }).ToList()
                }
            ).ToListAsync();

            if (!raw.Any())
                return NotFound("❌ لا توجد أسئلة مرتبطة بهذا الدرس.");

            // 🆕 هل هذا الاختبار مفعّل فيه خيار "لا أعرف الإجابة"؟
            bool allowDontKnowOptionLesson = exam.AllowDontKnowOption;

            // 🔥 بناء ViewModel في الذاكرة
            var questions = raw.Select(x => new QdratNew.ViewModels.Exam.ExamReviewQuestionVm
            {
                QuestionId = x.Id,
                QuestionTitle = x.Title,
                IsCorrect = x.IsCorrect,
                IsDontKnowAnswer = x.IsDontKnowAnswer,
                StudentAnswer = x.SelectedAnswer,
                CorrectAnswer = x.CorrectAnswer ?? "",
                TimeTakenSeconds = x.TimeTakenSeconds,
                ImageUrl = x.ImageUrl,

                DisplayType = x.Template == QuestionTemplate.CompareValues
                    ? QuestionDisplayType.ComparisonText
                    : x.Template == QuestionTemplate.CompareWithImage
                        ? QuestionDisplayType.ComparisonWithImage
                        : QuestionDisplayType.WithImage,

                ComparisonValue1 = x.ValueA,
                ComparisonValue2 = x.ValueB,

                // 🔥 RTL
                IsRTL = x.IsRTL,

                // 🔥 القطعة
                VerbalPassageTitle = x.PassageTitle,
                VerbalPassageContent = x.PassageContent,
                VerbalPassageType = x.PassageType,
                VerbalPassageMediaUrl = x.PassageMediaUrl,

                // 🔥 الشرح
                Explanation = x.Explanation,
                VideoUrl = x.VideoUrl,

                Options = x.Options.Select(opt => new QdratNew.ViewModels.Homework.QuestionOptionVm
                {
                    Text = opt.Text,
                    ImageUrl = opt.ImageUrl
                }).ToList(),

                IsQuantitative = x.IsQuantitative
            }).ToList();

            // 🆕 إضافة خيار "لا أعرف الإجابة" ديناميكيًا لعرض المراجعة فقط
            // النص يتبع اتجاه المنهج الخاص بكل سؤال (qvm.IsRTL): عربي لو مفعّل، إنجليزي لو غير مفعّل
            if (allowDontKnowOptionLesson)
            {
                foreach (var qvm in questions)
                {
                    qvm.Options.Add(new QdratNew.ViewModels.Homework.QuestionOptionVm
                    {
                        Text = ExamAnswerConstants.GetDontKnowOptionText(qvm.IsRTL),
                        ImageUrl = null
                    });
                }
            }

            // 🔹 بيانات الدرس
            var lessonTitle = await _context.Lessons
                .Where(x => x.Id == lessonId)
                .Select(x => x.Title)
                .FirstOrDefaultAsync();

            var sectionTitle = await (
                from l in _context.Lessons
                join s in _context.Sections on l.SectionId equals s.Id
                where l.Id == lessonId
                select s.Title
            ).FirstOrDefaultAsync();

            var vm = new QdratNew.ViewModels.Exam.ExamReviewViewModel
            {
                ExamTitle = exam.Title ?? "اختبار تحديد المستوى",
                LessonTitle = lessonTitle,
                SectionTitle = sectionTitle,
                LessonId = lessonId,
                ExamAssignmentId = assignmentId,
                Questions = questions,
                TotalQuestions = questions.Count,
                TimeSpentMinutes = Math.Round(questions.Sum(q => q.TimeTakenSeconds) / 60.0, 1),

                // 🔥 مهم جدًا
                IsRTL = questions.First().IsRTL
            };

            return View("~/Areas/Students/Views/PlacementExams/ReviewLessonQuestions.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> PlacementReport(int assignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ✅ التحقق من هوية الطالب
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🧩 جلب بيانات الطالب مع ولي الأمر مرة واحدة
            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("لم يتم العثور على بيانات الطالب.");

            // 🧩 جلب بيانات التعيين والاختبار مرة واحدة
            var assignment = await _context.ExamAssignments
                .AsNoTracking()
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId);

            if (assignment == null || assignment.Exam == null)
                return NotFound("❌ اختبار المستوى غير موجود.");

            var examId = assignment.ExamId;
            var exam = assignment.Exam;

            // 🟢 جلب كل البيانات اللازمة (أسئلة + محاولات + دروس + محاور + مناهج) باستعلام واحد موحد
            var fullData = await (
                from eq in _context.ExamQuestions.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on eq.QuestionId equals q.Id
                join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in _context.Sections.AsNoTracking() on l.SectionId equals s.Id
                join c in _context.Curriculums.AsNoTracking() on s.CurriculumId equals c.Id
                join att in _context.QuestionAttemptNew.AsNoTracking()
                    .Where(a => a.StudentId == studentId &&
                                (a.ExamId == examId || a.ExamAssignmentId == assignmentId))
                    on q.Id equals att.QuestionId into gj
                from attempt in gj.DefaultIfEmpty()
                where eq.ExamId == examId
                select new
                {
                    QuestionId = q.Id,
                    q.Title,
                    SectionId = s.Id,
                    SectionName = s.Title,
                    LessonId = l.Id,
                    LessonName = l.Title,
                    CurriculumTitle = c.Title,
                    IsCorrect = attempt != null && attempt.IsCorrect,
                    IsDontKnowAnswer = attempt != null && attempt.IsDontKnowAnswer,
                    HasAttempt = attempt != null
                }

            ).ToListAsync();

            if (!fullData.Any())
                return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لا توجد بيانات للأسئلة.</div>", "text/html");

            // 🧮 حساب النتائج العامة دفعة واحدة
            int totalQuestions = fullData.Count;
            int correctAnswers = fullData.Count(x => x.IsCorrect);
            int answeredQuestions = fullData.Count(x => x.HasAttempt);
            int skippedQuestions = totalQuestions - answeredQuestions;
            int wrongAnswers = fullData.Count(x => x.HasAttempt && !x.IsCorrect);
            // 🆕 من ضمن wrongAnswers: عدد إجابات "لا أعرف" تحديدًا (تظل محتسبة ضمن الخطأ، لكنها تُعرض كإحصائية مستقلة)
            int dontKnowAnswers = fullData.Count(x => x.IsDontKnowAnswer);
            double overall = totalQuestions > 0 ? Math.Round(correctAnswers * 100.0 / totalQuestions, 1) : 0.0;

            // 🕒 حساب زمن الحل من الحالة أو المحاولات
            var status = await _context.ExamStudentStatuses.AsNoTracking()
                .Where(s => s.StudentId == studentId &&
                           (s.ExamId == examId || s.ExamAssignmentId == assignmentId))
                .OrderByDescending(s => s.SubmittedAt)
                .FirstOrDefaultAsync();

            int solveMinutes;
            if (status?.StartedAt != null && status?.SubmittedAt != null)
            {
                solveMinutes = (int)Math.Round((status.SubmittedAt.Value - status.StartedAt.Value).TotalMinutes);
            }
            else
            {
                var totalSeconds = await _context.QuestionAttemptNew.AsNoTracking()
                    .Where(a => a.StudentId == studentId &&
                                (a.ExamId == examId || a.ExamAssignmentId == assignmentId))
                    .SumAsync(a => (int?)a.TimeTakenSeconds) ?? 0;

                solveMinutes = totalSeconds > 0 ? (int)Math.Round(totalSeconds / 60.0) : exam.DurationMinutes;
            }

            double totalMinutes = exam.DurationMinutes > 0 ? exam.DurationMinutes : 60;
            double percentTime = Math.Round((solveMinutes / totalMinutes) * 100, 1);
            var recommendation = _recommendationService.GetRecommendation(overall, solveMinutes, totalMinutes);

            // 🧠 تحليل المحاور كمي ولفظي في استعلامين موحدين (بدون GroupJoin مزدوج)
            var groupedSections = fullData
                .GroupBy(x => new { x.SectionName, x.CurriculumTitle })
                .Select(g => new
                {
                    g.Key.SectionName,
                    g.Key.CurriculumTitle,
                    TotalQuestions = g.Count(),
                    CorrectCount = g.Count(x => x.IsCorrect),
                    WrongCount = g.Count(x => x.HasAttempt && !x.IsCorrect),
                    Percent = g.Any(x => x.HasAttempt)
                        ? Math.Round(g.Count(x => x.IsCorrect) * 100.0 / g.Count(x => x.HasAttempt), 1)
                        : 0.0
                })
                .ToList();

            var quantGroups = groupedSections
                .Where(x => x.CurriculumTitle.Contains("كمي"))
                .ToList();

            var verbalGroups = groupedSections
                .Where(x => x.CurriculumTitle.Contains("لفظي"))
                .ToList();

            // 🧾 إنشاء ViewModel الرئيسي
            var model = new PlacementReportViewModel
            {
                Student = new StudentMiniVm
                {
                    FullName = student.FullName,
                    StudentID = student.StudentID,
                    Level = student.Level,
                    ParentName = student.Parent?.FullName ?? "—",
                    ParentPhone = student.WhatsAppNumber ?? "—"
                },
                ExamDate = assignment.AssignedAt.ToString("yyyy-MM-dd"),
                SolveMinutes = solveMinutes,
                OverallPercent = overall,
                Recommendation = recommendation,

                QuantLabels = quantGroups.Select(x => x.SectionName).ToList(),
                QuantScores = quantGroups.Select(x => x.Percent).ToList(),
                QuantCorrectCounts = quantGroups.Select(x => x.CorrectCount).ToList(),
                QuantWrongCounts = quantGroups.Select(x => x.WrongCount).ToList(),

                VerbalLabels = verbalGroups.Select(x => x.SectionName).ToList(),
                VerbalScores = verbalGroups.Select(x => x.Percent).ToList(),
                VerbalCorrectCounts = verbalGroups.Select(x => x.CorrectCount).ToList(),
                VerbalWrongCounts = verbalGroups.Select(x => x.WrongCount).ToList(),

                // 🔥 الجديد
                HasQuantChart = quantGroups.Any(),
                HasVerbalChart = verbalGroups.Any(),


                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                WrongAnswers = wrongAnswers,
                DontKnowAnswers = dontKnowAnswers,
                AnsweredQuestions = answeredQuestions,
                SkippedQuestions = skippedQuestions,
                TotalMinutes = totalMinutes,
                PercentTime = percentTime,
                AssignmentId = assignment.Id
            };

            // 🧩 تجميع المؤشرات داخل المحاور
            model.SectionLessonReports = fullData
                .GroupBy(x => new { x.SectionId, x.SectionName })
                .Select(g => new QdratNew.ViewModels.Exam.SectionLessonReportVm
                {
                    SectionId = g.Key.SectionId,
                    SectionName = g.Key.SectionName,
                    Lessons = g.GroupBy(l => new { l.LessonId, l.LessonName })
                        .Select(lessonGroup => new QdratNew.ViewModels.Exam.LessonPerformanceVm
                        {
                            LessonId = lessonGroup.Key.LessonId,
                            LessonName = lessonGroup.Key.LessonName,
                            TotalQuestions = lessonGroup.Count(),
                            CorrectCount = lessonGroup.Count(x => x.IsCorrect),
                            WrongCount = lessonGroup.Count(x => x.HasAttempt && !x.IsCorrect),
                            SkippedCount = lessonGroup.Count(x => !x.HasAttempt),
                            Percent = lessonGroup.Any(x => x.HasAttempt)
                                ? Math.Round(lessonGroup.Count(x => x.IsCorrect) * 100.0 / lessonGroup.Count(x => x.HasAttempt), 1)
                                : 0.0
                        }).OrderBy(x => x.LessonName).ToList()
                })
                .OrderBy(x => x.SectionName)
                .ToList();

            return View(model);
        }



        [HttpGet]
        public async Task<IActionResult> PlacementReview(int assignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ✅ جلب بيانات التعيين
            var assignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId);

            if (assignment == null || assignment.Exam == null)
                return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لم يتم العثور على بيانات هذا الاختبار.</div>", "text/html");

            var exam = assignment.Exam;

            // ✅ استدعاء الخدمة الموحدة لحساب الإحصائيات
            var summary = await _studentExamStatisticsService.GetExamResultAsync(assignmentId, studentId.Value);

            // ✅ جلب الأسئلة ومحاولات الطالب + الخيارات + القطعة اللفظية
            var rawAttempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions
                    .Include(q => q.Lesson).ThenInclude(l => l.Section)
                    .Include(q => q.Curriculum)
                    .Include(q => q.Options)
                    .Include(q => q.VerbalPassage)
                on a.QuestionId equals q.Id
                where a.StudentId == studentId && a.ExamAssignmentId == assignmentId
                select new
                {
                    q.Id,
                    q.Title,
                    a.SelectedAnswer,
                    a.IsCorrect,
                    a.IsDontKnowAnswer,
                    q.CorrectAnswer,
                    q.IsQuantitative,
                    q.ImageUrl,
                    q.Template,
                    q.ValueA,
                    q.ValueB,
                    Explanation = q.Explanation,
                    VideoUrl = q.VideoUrl,
                    LessonTitle = q.Lesson.Title,
                    SectionTitle = q.Lesson.Section.Title,
                    a.TimeTakenSeconds,
                    VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                    VerbalPassageTitle = q.VerbalPassage != null ? q.VerbalPassage.Title : null,
                    VerbalPassageType = (PassageType?)q.VerbalPassage.Type,
                    VerbalPassageMediaUrl = q.VerbalPassage != null ? q.VerbalPassage.MediaUrl : null,
                    VerbalPassageDurationSeconds = q.VerbalPassage != null ? q.VerbalPassage.DurationSeconds : null,
                    VerbalPassageRequireFullListen = q.VerbalPassage != null ? q.VerbalPassage.RequireFullListen : false,
                    Options = q.Options.Select(o => new
                    {
                        o.Text,
                        o.ImageUrl
                    }).ToList()
                }
            ).ToListAsync();

            if (!rawAttempts.Any())
                return Content("<div class='alert alert-warning text-center'>⚠️ لا توجد محاولات مسجلة لهذا الاختبار.</div>", "text/html");

            // 🆕 هل هذا الاختبار مفعّل فيه خيار "لا أعرف الإجابة"؟
            bool allowDontKnowOption = exam.AllowDontKnowOption;

            // ✅ بعد جلب البيانات من EF نقوم ببناء الـ ViewModel في الذاكرة (خارج الـ Expression Tree)
            var questions = rawAttempts.Select(x => new ExamReviewQuestionVm
            {
                QuestionId = x.Id,
                QuestionTitle = x.Title,
                StudentAnswer = x.SelectedAnswer ?? "—",
                CorrectAnswer = x.CorrectAnswer ?? "—",
                IsCorrect = x.IsCorrect,
                IsDontKnowAnswer = x.IsDontKnowAnswer,
                IsQuantitative = x.IsQuantitative,
                ImageUrl = x.ImageUrl,
                Explanation = x.Explanation,
                VideoUrl = x.VideoUrl,
                DisplayType = x.Template == QuestionTemplate.CompareValues
                    ? QuestionDisplayType.ComparisonText
                    : x.Template == QuestionTemplate.CompareWithImage
                        ? QuestionDisplayType.ComparisonWithImage
                        : QuestionDisplayType.WithImage,
                ComparisonValue1 = x.ValueA,
                ComparisonValue2 = x.ValueB,
                LessonTitle = x.LessonTitle,
                SectionTitle = x.SectionTitle,
                TimeTakenSeconds = x.TimeTakenSeconds,
                VerbalPassageTitle = x.VerbalPassageTitle,
                VerbalPassageContent = x.VerbalPassageContent,
                VerbalPassageType = x.VerbalPassageType,
                VerbalPassageMediaUrl = x.VerbalPassageMediaUrl,
                VerbalPassageDurationSeconds = x.VerbalPassageDurationSeconds,
                VerbalPassageRequireFullListen = x.VerbalPassageRequireFullListen,
                Options = x.Options.Select(o => new QdratNew.ViewModels.Homework.QuestionOptionVm
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList()
            }).ToList();

            // =========================================
            // 🔥 تحديد اتجاه المنهج
            // =========================================
            var firstQuestionId = rawAttempts.First().Id;

            var curriculumIsRTL = await (
                from q in _context.Questions
                join c in _context.Curriculums on q.CurriculumId equals c.Id
                where q.Id == firstQuestionId
                select c.IsRTL
            ).FirstOrDefaultAsync();

            // 🆕 إضافة خيار "لا أعرف الإجابة" ديناميكيًا لعرض المراجعة فقط (بدون تخزينه كـ QuestionOption حقيقي)
            // النص يتبع اتجاه المنهج (Curriculum.IsRTL): عربي لو مفعّل، إنجليزي لو غير مفعّل
            if (allowDontKnowOption)
            {
                string dontKnowTextReview = ExamAnswerConstants.GetDontKnowOptionText(curriculumIsRTL);
                foreach (var qvm in questions)
                {
                    qvm.Options.Add(new QdratNew.ViewModels.Homework.QuestionOptionVm
                    {
                        Text = dontKnowTextReview,
                        ImageUrl = null
                    });
                }
            }

            // ✅ صياغة الوقت بصيغة موحدة
            string formattedTime = summary.SolveMinutes > 0
                ? $"{summary.SolveMinutes} دقيقة تقريبًا"
                : "غير محدد";

            // ✅ بناء ViewModel النهائي كما في الاختبارات العامة
            var vm = new ExamReviewViewModel
            {
                ExamAssignmentId = assignmentId,
                ExamTitle = exam.Title,
                TotalQuestions = summary.TotalQuestions,
                CorrectAnswers = summary.CorrectAnswers,
                WrongAnswers = summary.WrongAnswers,
                DontKnowAnswers = summary.DontKnowAnswers,
                SkippedQuestions = summary.SkippedQuestions,
                ScorePercentage = summary.ScorePercentage,
                AverageTimePerQuestion = summary.AverageTimePerQuestion,
                TimeSpentFormatted = formattedTime,

                // 🔥 أهم سطر
                IsRTL = curriculumIsRTL,
                Questions = questions
            };

            return View("~/Areas/Students/Views/PlacementExams/PlacementReview.cshtml", vm);
        }

        // ✅ تسليم الاختبار
        [HttpPost]
        public async Task<IActionResult> SubmitFinal(int id, bool forceSubmit = false)
        {
            using var _context = _contextFactory.CreateDbContext();

            try
            {
                var studentId = await GetCurrentStudentIdAsync();
                if (studentId == null)
                    return Json(new { success = false, message = "⚠️ لم يتم التعرف على الطالب." });

                // 🔹 جلب بيانات التعيين
                var examAssignment = await _context.ExamAssignments
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == id && a.StudentId == studentId);

                if (examAssignment == null)
                    return Json(new { success = false, message = "⚠️ لم يتم العثور على الاختبار." });

                var exam = examAssignment.Exam;
                var examId = exam.Id;

                // 🔹 جلب جميع الأسئلة
                var questionIds = await _context.ExamQuestions
                    .Where(eq => eq.ExamId == examId)
                    .Select(eq => eq.QuestionId)
                    .ToListAsync();

                if (!questionIds.Any())
                    return Json(new { success = false, message = "⚠️ لا توجد أسئلة في هذا الاختبار." });

                // 🔹 تحقق من إجابات الطالب
                var attempts = await _context.QuestionAttemptNew
                    .Where(a => a.StudentId == studentId && a.ExamId == examId)
                    .ToListAsync();

                int notAnswered = questionIds.Count(qid => !attempts.Any(a => a.QuestionId == qid));

                if (notAnswered > 0 && !forceSubmit)
                {
                    return Json(new
                    {
                        success = false,
                        notAnswered,
                        message = $"لديك {notAnswered} أسئلة لم تجب عنها. هل ترغب بإنهاء الاختبار؟"
                    });
                }

                // ✅ إنهاء الاختبار رسميًا
                var status = await _context.ExamStudentStatuses
                    .FirstOrDefaultAsync(s => s.StudentId == studentId && s.ExamId == examId);

                // ⚠️ قد لا يوجد سجل حالة مسبق (مثلاً دفعة أُنشئت بدون تعيين فردي أو طالب انضم متأخرًا)
                // بدون إنشائه هنا، يظل الاختبار "لم يبدأ بعد" في لوحة الإدارة رغم أن الطالب أنهاه فعليًا
                if (status == null)
                {
                    status = new ExamStudentStatus
                    {
                        StudentId = studentId.Value,
                        ExamId = examId,
                        AssignedAt = examAssignment.AssignedAt
                    };
                    _context.ExamStudentStatuses.Add(status);
                }

                status.IsSubmitted = true;
                status.Status = ExamStatus.Completed;
                status.SubmittedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                // 🧹 إزالة الكاش الخاص بالطالب بعد التسليم لضمان تحديث فوري
                _cache.Remove($"PlacementExamDashboard_{studentId}");
                _cache.Remove($"ExamResult_{exam.Id}_{studentId}");

                return Json(new
                {
                    success = true,
                    message = "✅ تم إنهاء الاختبار بنجاح.",
                    redirectUrl = Url.Action("ResultPlacementExam", "PlacementExams", new { area = "Students", assignmentId = id })

                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"⚠️ حدث خطأ أثناء التسليم: {ex.Message}" });
            }
        }


        // ✅ تحميل السؤال - نسخة نهائية تعتمد على Session لحفظ واسترجاع الإجابات (مثل الاختبار العام)
        private async Task<ExamSolveViewModel> GetExamSolveViewModel(
         int assignmentId, Guid questionId, int studentId, int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧠 تحميل السؤال الحالي + القطعة اللفظية
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.Lesson)

                .Include(q => q.Curriculum)
                .Include(q => q.VerbalPassage)    // ✅ هذا هو التعديل الضروري
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                throw new Exception("⚠️ لم يتم العثور على السؤال المحدد.");

            // 🆕 هل هذا الاختبار مفعّل فيه خيار "لا أعرف الإجابة"؟
            bool allowDontKnowOption = await _context.Exams
                .Where(e => e.Id == examId)
                .Select(e => e.AllowDontKnowOption)
                .FirstOrDefaultAsync();

            // 🧠 تحميل محاولات الطالب لجميع الأسئلة
            var allAttempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId &&
                            (a.ExamAssignmentId == assignmentId || a.ExamId == examId))
                .ToListAsync();

            var attempt = allAttempts.FirstOrDefault(a => a.QuestionId == questionId);

            // 📦 جلب جميع الأسئلة من الجلسة أو قاعدة البيانات
            var sessionKey = $"PlacementExamQs_{assignmentId}_{studentId}";
            List<Guid> allQuestions;

            if (HttpContext.Session.TryGetValue(sessionKey, out var bytes))
            {
                allQuestions = JsonSerializer.Deserialize<List<Guid>>(bytes) ?? new();
            }
            else
            {
                allQuestions = await _context.ExamQuestions
                    .Where(eq => eq.ExamId == examId || eq.ExamAssignmentId == assignmentId)
                    .OrderBy(eq => eq.Order)
                    .Select(eq => eq.QuestionId)
                    .ToListAsync();

                HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(allQuestions));
            }

            var currentIndex = allQuestions.FindIndex(q => q == questionId);
            bool isLast = currentIndex == allQuestions.Count - 1;

            // ⚡ بناء ViewModel بالكامل
            var vm = new ExamSolveViewModel
            {
                ExamId = examId,
                ExamAssignmentId = assignmentId,
                CurrentQuestionId = question.Id,
                AllQuestionIds = allQuestions,

                // 🎯 هنا سيتم تحميل القطعة داخل QuestionDisplayViewModel تلقائيًا
                Question = question.ToDisplayModel(),

                // 🔹 إجابة الطالب
                SelectedAnswer = attempt?.SelectedAnswer,
                TimeTakenSeconds = attempt?.TimeTakenSeconds ?? 0,

                // 🔹 هل السؤال الأخير؟
                ForceReviewVisible = isLast,

                // 🔹 خريطة الإجابات
                AnswersMap = allAttempts.ToDictionary(a => a.QuestionId, a => a.SelectedAnswer),

                // 🔹 هذه مطلوبة لعرض البيانات داخل الواجهة إذا رغبت لاحقًا
                VerbalPassageTitle = question.VerbalPassage?.Title,
                VerbalPassageContent = question.VerbalPassage?.Content,
                VerbalPassageType = question.VerbalPassage?.Type,
                VerbalPassageMediaUrl = question.VerbalPassage?.MediaUrl,
                VerbalPassageDurationSeconds = question.VerbalPassage?.DurationSeconds,
                VerbalPassageRequireFullListen = question.VerbalPassage?.RequireFullListen ?? false
            };

            // 🆕 إضافة خيار "لا أعرف الإجابة" ديناميكيًا للعرض فقط (بدون تخزينه كـ QuestionOption حقيقي)
            // النص يتبع اتجاه المنهج (Curriculum.IsRTL): عربي لو مفعّل، إنجليزي لو غير مفعّل
            if (allowDontKnowOption)
            {
                bool curriculumIsRTLSolve = question.Curriculum?.IsRTL ?? true;

                vm.Question.Options.Add(new QdratNew.ViewModels.Question.QuestionOptionDisplayViewModel
                {
                    Text = ExamAnswerConstants.GetDontKnowOptionText(curriculumIsRTLSolve),
                    IsSelected = ExamAnswerConstants.IsDontKnowOption(vm.SelectedAnswer)
                });
            }

            return vm;
        }

        // ✅ جلب الطالب الحالي
        private async Task<int?> GetCurrentStudentIdAsync()
        {
            using var _context = _contextFactory.CreateDbContext();
            var userId = _userManager.GetUserId(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
            return student?.StudentID;
        }
    }
}
