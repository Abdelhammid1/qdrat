using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.PerformanceIndicator;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class PerformanceIndicatorExamsController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPerformanceIndicatorExamService _examService;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IPerformanceInsightService _performanceInsightService;
        private readonly IPerformanceIndicatorAnalysisService _analysisService;
        private readonly IStudentExamStatisticsService _studentExamStatisticsService;
        private readonly ITimeCalculationService _timeService;
        private readonly IPerformanceExamAutoCloseService _autoCloseService;
        private readonly ILogger<PerformanceIndicatorExamsController> _logger;
        private readonly IIntegrityGuardService _integrityGuard;

        public PerformanceIndicatorExamsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IPerformanceIndicatorExamService examService,
            ITimeZoneService timeZoneService,
            IPerformanceInsightService performanceInsightService,
            IPerformanceIndicatorAnalysisService analysisService,
            IStudentExamStatisticsService studentExamStatisticsService,
            ITimeCalculationService timeService,
            IPerformanceExamAutoCloseService autoCloseService,
            ILogger<PerformanceIndicatorExamsController> logger,
            IIntegrityGuardService integrityGuard)
        {
            _contextFactory = contextFactory;
            _userManager = userManager;
            _examService = examService;
            _timeZoneService = timeZoneService;
            _performanceInsightService = performanceInsightService;
            _analysisService = analysisService;
            _studentExamStatisticsService = studentExamStatisticsService;
            _timeService = timeService;
            _autoCloseService = autoCloseService;
            _logger = logger;
            _integrityGuard = integrityGuard;
        }

        private async Task<int?> GetCurrentStudentIdAsync()
        {
            using var _context = _contextFactory.CreateDbContext();
            var userId = _userManager.GetUserId(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
            return student?.StudentID;
        }


        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ======================================================
            // 🟩 1. فحص أي اختبارات تم بدءها ولم تُغلق بعد
            // ======================================================
            var activeExams = await _context.PerformanceIndicatorExamStudents
                .Where(x =>
                    x.StudentId == studentId &&
                    !x.IsCompleted &&
                    x.StartedAt != null
                )
                .Select(x => new
                {
                    x.PerformanceIndicatorExamId,
                    x.StartedAt
                })
                .ToListAsync();

            foreach (var ex in activeExams)
            {
                await _autoCloseService.AutoCloseIfExpiredAsync(studentId.Value, ex.PerformanceIndicatorExamId);
            }

            // ======================================================
            // 🟩 2. تحميل الاختبارات الخاصة بالطالب
            // ======================================================
            var examsData = await (
                from es in _context.PerformanceIndicatorExamStudents.AsNoTracking()
                join e in _context.PerformanceIndicatorExams.AsNoTracking()
                    on es.PerformanceIndicatorExamId equals e.Id
                join c in _context.Curriculums.AsNoTracking()
                    on e.CurriculumId equals c.Id
                where es.StudentId == studentId
                select new
                {
                    ExamId = e.Id,
                    e.Title,
                    e.CreatedAt,
                    CurriculumTitle = c.Title
                }
            ).ToListAsync();

            if (!examsData.Any())
            {
                return View("Dashboard", new PerformanceIndicatorDashboardVm
                {
                    TotalExams = 0,
                    CompletedExams = 0,
                    AverageScore = 0,
                    Exams = new List<PerformanceIndicatorExamCardVm>()
                });
            }

            // ======================================================
            // 🟩 NEW — جلب الاختبارات المكتملة من سجل الطالب مباشرة
            // ======================================================
            var finishedExams = await _context.PerformanceIndicatorExamStudents
                .AsNoTracking()
                .Where(x => x.StudentId == studentId && x.IsCompleted)
                .Select(x => x.PerformanceIndicatorExamId)
                .ToListAsync();

            // ======================================================
            // 🟩 3. جلب نتائج الطالب لكل اختبار
            // ======================================================
            var studentResults = await _context.StudentIndicatorResults.AsNoTracking()
                .Where(r => r.StudentId == studentId)
                .GroupBy(r => r.PerformanceIndicatorExamId)
                .Select(g => new
                {
                    ExamId = g.Key,
                    Average = g.Average(x => x.ScorePercent)
                })
                .ToListAsync();

            // ======================================================
            // 🟩 4. دمج بيانات الاختبارات + نتائج الطالب + حالة الإغلاق
            // ======================================================
            var examCards = examsData
                .Select(exam =>
                {
                    var result = studentResults.FirstOrDefault(r => r.ExamId == exam.ExamId);

                    // 🟩 التعديل المهم:
                    bool isCompleted =
                        (result != null)                     // توجد نتائج
                        || finishedExams.Contains(exam.ExamId);  // أو تم إغلاقه تلقائيًا

                    double score = result?.Average ?? 0;

                    return new PerformanceIndicatorExamCardVm
                    {
                        ExamId = exam.ExamId,
                        Title = exam.Title ?? "—",
                        Curriculum = exam.CurriculumTitle,
                        CreatedAt = exam.CreatedAt,
                        IsCompleted = isCompleted,
                        ScorePercent = Math.Round(score, 1),
                        StatusText = isCompleted ? "تم الحل" : "جاهز للاختبار"
                    };
                })
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            // ======================================================
            // 🟩 5. حساب الإحصائيات العامة
            // ======================================================
            int completedCount = examCards.Count(x => x.IsCompleted);

            double avgScore = completedCount > 0
                ? Math.Round(examCards.Where(x => x.IsCompleted).Average(x => x.ScorePercent), 1)
                : 0;

            var vm = new PerformanceIndicatorDashboardVm
            {
                TotalExams = examCards.Count,
                CompletedExams = completedCount,
                AverageScore = avgScore,
                Exams = examCards
            };

            return View("Dashboard", vm);
        }


        // 🟢 بدء اختبار مؤشر الأداء
        [HttpGet]
        public async Task<IActionResult> StartPerformanceExam(int id, Guid? q = null)
        {
            var verifiedKey = $"VerifiedExam_{id}";
            if (HttpContext.Session.GetString(verifiedKey) != "true")
            {
                return RedirectToAction("VerifyReferenceCode", new { id });
            }

            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            if (await _integrityGuard.IsBlockedAsync(studentId.Value, IntegrityAttemptType.PerformanceIndicator, id))
                return RedirectToAction("Blocked", "IntegrityGuard", new { area = "Students", attemptType = (int)IntegrityAttemptType.PerformanceIndicator, attemptEntityId = id, returnUrl = $"{Request.Path}{Request.QueryString}" });

            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Sections).ThenInclude(s => s.Section)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار.");

            // 🧠 سجل الطالب الأصلي
            var studentExam = await _context.PerformanceIndicatorExamStudents
                .FirstOrDefaultAsync(s => s.PerformanceIndicatorExamId == id && s.StudentId == studentId);

            // 🔥 تنفيذ الغلق التلقائي قبل أي فحص
            await _autoCloseService.AutoCloseIfExpiredAsync(studentId.Value, id);

            // 🟢 إعادة تحميل سجل الطالب بعد الغلق التلقائي
            studentExam = await _context.PerformanceIndicatorExamStudents
                .FirstOrDefaultAsync(s => s.PerformanceIndicatorExamId == id && s.StudentId == studentId);

            // 🛑 لو الاختبار أصبح مكتمل — لا نفتح الأسئلة
            if (studentExam != null && studentExam.IsCompleted)
            {
                TempData["ExamCompletedMessage"] =
                    "لقد أتممت هذا الاختبار مسبقًا. إذا لم تظهر نتيجتك، اضغط على زر (عرض النتيجة) أدناه.";

                return RedirectToAction("Result", new { id });
            }

            // 🧹 تنظيف الجلسة
            string sessionKeyX = $"PerformanceExamQs_{id}_{studentId}";
            HttpContext.Session.Remove(sessionKeyX);
            TempData.Remove("ReviewMarked");

            // 📝 إنشاء سجل جديد إذا لم يوجد
            if (studentExam == null)
            {
                studentExam = new PerformanceIndicatorExamStudent
                {
                    PerformanceIndicatorExamId = id,
                    StudentId = studentId.Value,
                    IsCompleted = false,
                    StartedAt = DateTime.UtcNow
                };

                await _context.PerformanceIndicatorExamStudents.AddAsync(studentExam);
                await _context.SaveChangesAsync();
            }
            else if (studentExam.StartedAt == null)
            {
                studentExam.StartedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            // 🕒 الزمن المتبقي
            var startAt = studentExam.StartedAt ?? DateTime.UtcNow;
            int totalSeconds = exam.DurationMinutes * 60;
            int elapsed = (int)(DateTime.UtcNow - startAt).TotalSeconds;
            int remainingSeconds = Math.Max(0, totalSeconds - elapsed);

            if (remainingSeconds <= 0)
            {
                studentExam.IsCompleted = true;
                studentExam.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                HttpContext.Session.Remove($"PerformanceExamQs_{exam.Id}_{studentId}");
                TempData.Remove("ReviewMarked");

                return RedirectToAction("Result", new { id = exam.Id });
            }

            // 🧩 تحميل الأسئلة
            var questionIds = await _context.PerformanceIndicatorExamQuestions
                .Where(eq => eq.PerformanceIndicatorExamId == exam.Id)
                .OrderBy(eq => eq.OrderNumber)
                .Select(eq => eq.QuestionId)
                .ToListAsync();

            if (!questionIds.Any())
                return NotFound("⚠️ لا توجد أسئلة في هذا الاختبار.");

            // 🧠 حفظ الأسئلة في Session
            string sessionKey = $"PerformanceExamQs_{exam.Id}_{studentId}";
            HttpContext.Session.SetString(sessionKey,
                System.Text.Json.JsonSerializer.Serialize(questionIds));

            // 🧭 تحديد السؤال الحالي
            Guid currentQ = q.HasValue && questionIds.Contains(q.Value)
                ? q.Value
                : questionIds.First();

            var question = await _context.Questions
       .Include(qn => qn.Options)
       .Include(qn => qn.Lesson).ThenInclude(l => l.Section)
       .Include(qn => qn.VerbalPassage)
       .Include(qn => qn.Curriculum) // ✅ مهم جدًا
       .FirstOrDefaultAsync(qn => qn.Id == currentQ);

            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.PerformanceIndicatorExamId == exam.Id)
                .ToListAsync();

            // 🟢 منع تكرار الإجابات — أخذ آخر محاولة فقط
            var answersMap = attempts
                .GroupBy(a => a.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(a => a.AttemptedAt).First().SelectedAnswer
                );

            var vm = new ExamSolveViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                CurrentQuestionId = currentQ,
                AllQuestionIds = questionIds,
                Question = question?.ToDisplayModel(),

                SelectedAnswer = attempts
                    .Where(a => a.QuestionId == currentQ)
                    .OrderByDescending(a => a.AttemptedAt)
                    .Select(a => a.SelectedAnswer)
                    .FirstOrDefault(),

                AnswersMap = answersMap,
                DurationMinutes = exam.DurationMinutes,
                RemainingSeconds = remainingSeconds,
                StartTime = DateTime.Now,
                ExamAssignmentId = id,
                ForceReviewVisible = false,
                IsReviewMode = false,
                ReviewMarkedIds = new List<Guid>(),
                SubmitAnswerUrl = "/Students/PerformanceIndicatorExams/SubmitPerformanceAnswerFetch",
                FinalSubmitUrl = "/Students/PerformanceIndicatorExams/SubmitFinalPerformanceExam",
                ExamType = "Performance"
            };

            return View("~/Areas/Students/Views/PerformanceIndicatorExams/StartExam.cshtml", vm);
        }



        [HttpPost]
        public async Task<IActionResult> SubmitPerformanceAnswerFetch(
          int id, Guid q, string nav,
          string? SelectedOption, string? target,
          string? keepReview, int timeTakenSeconds)
        {
            using var _context = _contextFactory.CreateDbContext();






            try
            {
                var studentId = await GetCurrentStudentIdAsync();
                if (studentId == null)
                    return Json(new { success = false, message = "❌ لم يتم التعرف على الطالب." });


                var studentExam = await _context.PerformanceIndicatorExamStudents
                                 .FirstOrDefaultAsync(x => x.PerformanceIndicatorExamId == id && x.StudentId == studentId);

                // 🛑 منع الطالب من فتح السؤال بعد انتهاء الوقت
                var examX = await _context.PerformanceIndicatorExams.FirstOrDefaultAsync(e => e.Id == id);
                if (studentExam.StartedAt != null)
                {
                    var endTime = studentExam.StartedAt.Value.AddMinutes(examX.DurationMinutes);
                    if (DateTime.UtcNow >= endTime)
                    {
                        studentExam.IsCompleted = true;
                        studentExam.CompletedAt = endTime;
                        await _context.SaveChangesAsync();

                        return Json(new
                        {
                            success = false,
                            redirectUrl = Url.Action("Result", new { id })
                        });
                    }
                }



                if (studentExam == null || studentExam.IsCompleted)
                {
                    return Json(new { success = false, message = "❌ لا يمكنك تعديل الإجابات بعد إنهاء الاختبار." });
                }





                var exam = await _context.PerformanceIndicatorExams.FirstOrDefaultAsync(e => e.Id == id);
                if (exam == null)
                    return Json(new { success = false, message = "❌ لم يتم العثور على بيانات الاختبار." });

                // 🧠 جلب الأسئلة من الـ Session أو من قاعدة البيانات إذا لم تكن موجودة
                string listKey = $"PerformanceExamQs_{id}_{studentId}";
                List<Guid> allIds;

                if (HttpContext.Session.TryGetValue(listKey, out var bytes))
                {
                    allIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(bytes) ?? new();
                }
                else
                {
                    allIds = await _context.PerformanceIndicatorExamQuestions
                        .Where(eq => eq.PerformanceIndicatorExamId == exam.Id)
                        .OrderBy(eq => eq.OrderNumber)
                        .Select(eq => eq.QuestionId)
                        .ToListAsync();

                    HttpContext.Session.SetString(listKey, System.Text.Json.JsonSerializer.Serialize(allIds));
                }

                if (!allIds.Any())
                    return Json(new { success = false, message = "⚠️ لا توجد أسئلة في هذا الاختبار." });

                // ✅ رفض أي سؤال ليس ضمن أسئلة هذا الاختبار تحديدًا
                if (!allIds.Contains(q))
                {
                    _logger.LogWarning(
                        "SubmitPerformanceAnswerFetch: محاولة تسجيل سؤال {QuestionId} غير منتمٍ لاختبار المؤشر {ExamId} " +
                        "(طالب {StudentId}) — تم الرفض.", q, id, studentId);

                    return Json(new { success = false, message = "⚠️ هذا السؤال لا ينتمي لهذا الاختبار." });
                }

                // كشف التلاعب الزمني
                if (timeTakenSeconds > 0)
                {
                    var _qStartKey = $"QStart_{id}_{studentId}_{q}";
                    var _qStartStr = HttpContext.Session.GetString(_qStartKey);
                    if (!string.IsNullOrEmpty(_qStartStr) && DateTime.TryParse(_qStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var _qStartTime))
                    {
                        double serverTimeTaken = (DateTime.UtcNow - _qStartTime).TotalSeconds;
                        if (Math.Abs(serverTimeTaken - timeTakenSeconds) > 30)
                        {
                            _context.SuspiciousActivities.Add(new SuspiciousActivity
                            {
                                StudentId = studentId.Value,
                                ExamContextId = id,
                                QuestionId = q,
                                ClientTimeTakenSeconds = timeTakenSeconds,
                                ServerTimeTakenSeconds = serverTimeTaken,
                                ExamType = "Performance",
                                DetectedAt = DateTime.UtcNow
                            });
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                // 🟩 حفظ الإجابة
                if (!string.IsNullOrWhiteSpace(SelectedOption))
                {
                    var question = await _context.Questions.FirstOrDefaultAsync(x => x.Id == q);
                    if (question != null)
                    {
                        bool isCorrect = string.Equals(
                            SelectedOption.Trim(),
                            question.CorrectAnswer?.Trim(),
                            StringComparison.OrdinalIgnoreCase
                        );

                        var attempt = await _context.QuestionAttemptNew.FirstOrDefaultAsync(a =>
                            a.StudentId == studentId &&
                            a.QuestionId == q &&
                            a.PerformanceIndicatorExamId == exam.Id);

                        if (attempt == null)
                        {
                            attempt = new QuestionAttemptNew
                            {
                                StudentId = studentId.Value,
                                QuestionId = q,
                                PerformanceIndicatorExamId = exam.Id,
                                SelectedAnswer = SelectedOption,
                                IsCorrect = isCorrect,
                                AttemptedAt = DateTime.UtcNow,
                                LessonId = question.LessonId,
                                SectionId = question.SectionId,
                                TimeTakenSeconds = timeTakenSeconds
                            };
                            _context.QuestionAttemptNew.Add(attempt);
                        }
                        else
                        {
                            attempt.SelectedAnswer = SelectedOption;
                            attempt.IsCorrect = isCorrect;
                            attempt.AttemptedAt = DateTime.UtcNow;
                            attempt.TimeTakenSeconds = timeTakenSeconds;
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                // ✅ إدارة قائمة المراجعة
                var reviewJsonExisting = TempData["ReviewMarked"] as string;
                var reviewList = !string.IsNullOrEmpty(reviewJsonExisting)
                    ? System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(reviewJsonExisting)
                    : new List<Guid>();

                if (nav == "review" && !reviewList.Contains(q))
                    reviewList.Add(q);
                else if (nav == "unmark" && reviewList.Contains(q))
                    reviewList.Remove(q);

                TempData["ReviewMarked"] = System.Text.Json.JsonSerializer.Serialize(reviewList);
                TempData.Keep("ReviewMarked");

                // 🧭 التنقل
                int idx = allIds.IndexOf(q);
                if (idx == -1 && allIds.Any())
                    q = allIds.First();

                if (nav == "next" && idx + 1 < allIds.Count)
                    q = allIds[idx + 1];
                else if (nav == "prev" && idx > 0)
                    q = allIds[idx - 1];
                else if (nav == "goto" && Guid.TryParse(target, out var t) && allIds.Contains(t))
                    q = t;

                var vm = await BuildPerformanceSolveViewModel(exam.Id, q, studentId.Value);
                vm.ReviewMarkedIds = reviewList;

                if (nav == "showReview" || keepReview == "1" || nav == "goto")
                {
                    vm.ForceReviewVisible = true;
                    vm.IsReviewMode = true;
                }

                vm.SubmitAnswerUrl = "/Students/PerformanceIndicatorExams/SubmitPerformanceAnswerFetch";
                vm.FinalSubmitUrl  = "/Students/PerformanceIndicatorExams/SubmitFinalPerformanceExam";
                vm.ExamType        = "Performance";
                HttpContext.Session.SetString($"QStart_{id}_{studentId}_{q}", DateTime.UtcNow.ToString("O"));
                return PartialView("_SolveUnifiedExamPartial", vm);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "⚠️ خطأ داخلي: " + ex.Message });
            }
        }



        [HttpGet]
        public async Task<IActionResult> Report(int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧭 تحديد الطالب الحالي
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 📘 تحليل الأداء عبر الخدمة الجديدة
            var insight = await _performanceInsightService.AnalyzeStudentPerformanceAsync(studentId.Value, examId);

            // 🚫 منع إعادة إدخال الأسئلة وتأمين التقرير من العبث
            await _autoCloseService.AutoCloseIfExpiredAsync(studentId.Value, examId);



            if (insight == null)
                return NotFound("⚠️ لا توجد بيانات متاحة لتحليل هذا الاختبار بعد.");

            // 📊 جلب بيانات الطالب والاختبار
            var student = await _context.Students
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .Include(e => e.Batch)
                .FirstOrDefaultAsync(e => e.Id == examId);

            var examBatch = await _context.PerformanceIndicatorExamToBatch
    .Include(x => x.Batch)
        .ThenInclude(b => b.Course)
    .Where(x => x.PerformanceIndicatorExamId == examId)
    .Select(x => x.Batch)
    .FirstOrDefaultAsync();



            if (exam == null || student == null)
                return NotFound("❌ لم يتم العثور على بيانات الاختبار أو الطالب.");
            // 🕒 حساب الوقت الفعلي من محاولات الطالب أو من ExamStudents
            // 🕒 حساب الوقت الفعلي باستخدام أول وآخر محاولة فعلية (أدق طريقة)
            string elapsedTimeFormatted = "غير محدد";

            var firstAttempt = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.PerformanceIndicatorExamId == examId)
                .OrderBy(a => a.AttemptedAt)
                .FirstOrDefaultAsync();

            var lastAttempt = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.PerformanceIndicatorExamId == examId)
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefaultAsync();

            if (firstAttempt != null && lastAttempt != null && firstAttempt.AttemptedAt != null && lastAttempt.AttemptedAt != null)
            {
                var duration = lastAttempt.AttemptedAt - firstAttempt.AttemptedAt;


                if (duration.TotalSeconds > 0)
                {
                    int minutes = (int)duration.TotalMinutes;
                    int seconds = (int)(duration.TotalSeconds % 60);
                    elapsedTimeFormatted = $"{minutes} دقيقة و {seconds} ثانية";
                }
            }
            else
            {
                // 🟢 كخطة احتياطية لو لم توجد محاولات
                var studentExam = await _context.PerformanceIndicatorExamStudents
                    .FirstOrDefaultAsync(s => s.PerformanceIndicatorExamId == examId && s.StudentId == studentId);

                if (studentExam != null && studentExam.StartedAt.HasValue && studentExam.CompletedAt.HasValue)
                {
                    var duration = studentExam.CompletedAt.Value - studentExam.StartedAt.Value;
                    int minutes = (int)duration.TotalMinutes;
                    int seconds = (int)(duration.TotalSeconds % 60);
                    elapsedTimeFormatted = $"{minutes} دقيقة و {seconds} ثانية";
                }
            }


            foreach (var section in insight.Sections)
            {
                // === جلب SectionId الحقيقي بناءً على الاسم ===
                int realSectionId = await _context.Sections
                    .Where(s => s.Title == section.SectionTitle)
                    .Select(s => s.Id)
                    .FirstOrDefaultAsync();

                // إذا لم نجد المحور — اعتبر البيانات صفرية
                if (realSectionId == 0)
                {
                    section.CorrectCount = 0;
                    section.WrongCount = 0;
                    section.SkippedCount = 0;
                    section.Score = 0;
                    continue;
                }

                // === جلب الأسئلة الخاصة بالمحور ===
                var examQuestionsForSection = await _context.PerformanceIndicatorExamQuestions
                    .Where(eq => eq.PerformanceIndicatorExamId == examId
                                 && eq.SectionId == realSectionId)
                    .Select(eq => eq.QuestionId)
                    .ToListAsync();

                int totalQuestions = examQuestionsForSection.Count;

                if (totalQuestions == 0)
                {
                    section.CorrectCount = 0;
                    section.WrongCount = 0;
                    section.SkippedCount = 0;
                    section.Score = 0;
                    continue;
                }

                // === جلب المحاولات باستخدام JOIN صحيح ===
                var attemptsForSection = await (
                    from a in _context.QuestionAttemptNew
                    join eq in _context.PerformanceIndicatorExamQuestions
                        on a.QuestionId equals eq.QuestionId
                    where a.StudentId == studentId
                          && a.PerformanceIndicatorExamId == examId
                          && eq.PerformanceIndicatorExamId == examId
                          && eq.SectionId == realSectionId
                    select a
                ).ToListAsync();

                // === الصحيحة ===
                section.CorrectCount = attemptsForSection.Count(a => a.IsCorrect);

                // === الخاطئة ===
                section.WrongCount = attemptsForSection.Count(a =>
                    !a.IsCorrect &&
                    !string.IsNullOrWhiteSpace(a.SelectedAnswer) &&
                    a.SelectedAnswer != "—"
                );

                // === المجاب فعليًا ===
                int answeredReal = attemptsForSection.Count(a =>
                    !string.IsNullOrWhiteSpace(a.SelectedAnswer) &&
                    a.SelectedAnswer != "—"
                );

                // === المتخطاة ===
                section.SkippedCount = totalQuestions - answeredReal;

                // === النسبة ===
                section.Score = Math.Round(
                    totalQuestions == 0 ? 0 :
                    (section.CorrectCount * 100.0 / totalQuestions), 1);
            }

            await _autoCloseService.AutoCloseIfExpiredAsync(studentId.Value, examId);



            // 🧮 بناء ViewModel للعرض
            // 🧮 بناء ViewModel للعرض مع تمرير SectionId لكل محور
            var model = new QdratNew.ViewModels.Analytics.PerformanceInsightResult
            {
                StudentId = studentId.Value,
                ExamId = examId,
                OverallScore = insight.OverallScore,
                TimeUsagePercent = insight.TimeUsagePercent,
                SpeedAccuracyFeedback = insight.SpeedAccuracyFeedback,
                ScoreBand = insight.ScoreBand,
                MotivationalMessage = insight.MotivationalMessage,
                ElapsedTimeFormatted = elapsedTimeFormatted,
                CheatingFlagMessage = insight.CheatingFlagMessage,
                SkippedQuestions = insight.SkippedQuestions,

                Sections = insight.Sections.Select(s => new QdratNew.ViewModels.Analytics.SectionInsight
                {
                    SectionId = _context.Sections
                        .Where(sec => sec.Title == s.SectionTitle)
                        .Select(sec => sec.Id)
                        .FirstOrDefault(),   // 🆕 يجلب الـ Id الفعلي

                    SectionTitle = s.SectionTitle,
                    CorrectCount = s.CorrectCount,
                    WrongCount = s.WrongCount,
                    SkippedCount = s.SkippedCount,
                    Score = s.Score,
                    Guidance = s.Guidance
                }).ToList()
            };
            var studentCourse = await _context.StudentCourses
    .Include(sc => sc.Course)
    .FirstOrDefaultAsync(sc => sc.StudentID == studentId);

            ViewBag.CourseTitle = studentCourse?.Course?.Name ?? "غير محددة";


            ViewBag.StudentName = student.FullName;
            ViewBag.BatchName = examBatch?.Name ?? "غير محددة";
            ViewBag.CourseTitle = examBatch?.Course?.Name ?? "غير محددة";
            ViewBag.CurriculumTitle = exam.Curriculum?.Title ?? "غير محدد";

            ViewBag.ExamTitle = exam.Title;


            ViewBag.ExamDate = exam.CreatedAt; // أو StartedAt إن أردت
            ViewBag.ExamType = exam.IsOnline ? "اختبار إلكتروني" : "اختبار حضوري";
            ViewBag.ExamReference = string.IsNullOrWhiteSpace(exam.ReferenceCode)
                ? $"PI-{exam.Id}"
                : exam.ReferenceCode;





            return View("PerformanceIndicatorReport", model);
        }



        [HttpGet]
        public async Task<IActionResult> ReviewSectionQuestions(int examId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ===============================
            // 1) بيانات الاختبار
            // ===============================
            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لم يتم العثور على بيانات الاختبار.</div>", "text/html");

            // ===============================
            // 2) اسم المحور
            // ===============================
            var sectionTitle = await _context.Sections
                .Where(s => s.Id == sectionId)
                .Select(s => s.Title)
                .FirstOrDefaultAsync() ?? "محور غير معروف";

            // ===============================
            // 3) جلب الأسئلة + LEFT JOIN على المحاولات (بدون Contains)
            // ===============================
            var data = await (
                from eq in _context.PerformanceIndicatorExamQuestions
                join q in _context.Questions
                    .Include(q => q.Options)
                    .Include(q => q.Lesson).ThenInclude(l => l.Section)
                    .Include(q => q.VerbalPassage)
                    .Include(q => q.Curriculum)
                    on eq.QuestionId equals q.Id
                join a in _context.QuestionAttemptNew
                    .Where(a =>
                        a.StudentId == studentId &&
                        a.PerformanceIndicatorExamId == examId)
                    on q.Id equals a.QuestionId into attempts
                from attempt in attempts
                    .OrderByDescending(a => a.AttemptedAt)
                    .Take(1)
                    .DefaultIfEmpty()
                where
                    eq.PerformanceIndicatorExamId == examId &&
                    eq.SectionId == sectionId
                select new
                {
                    q.Id,
                    q.Title,
                    q.CorrectAnswer,
                    IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true, // ✅
                    StudentAnswer = attempt != null && !string.IsNullOrWhiteSpace(attempt.SelectedAnswer)
                        ? attempt.SelectedAnswer
                        : "—",
                    IsCorrect = attempt != null && attempt.IsCorrect,
                    TimeTakenSeconds = attempt != null ? attempt.TimeTakenSeconds : 0,
                    q.ImageUrl,
                    q.ValueA,
                    q.ValueB,
                    q.Template,
                    q.IsQuantitative,
                    VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                    Options = q.Options.Select(o => new
                    {
                        o.Text,
                        o.ImageUrl
                    }).ToList()
                }
            ).ToListAsync();

            if (!data.Any())
                return Content($"<div class='alert alert-warning text-center mt-5'>⚠️ لا توجد أسئلة لهذا المحور ({sectionTitle}).</div>", "text/html");

            // ===============================
            // 4) بناء الأسئلة
            // ===============================
            var questions = data.Select(x => new ExamReviewQuestionVm
            {
                QuestionId = x.Id,
                QuestionTitle = x.Title,
                CorrectAnswer = x.CorrectAnswer ?? "—",
                StudentAnswer = x.StudentAnswer,
                IsCorrect = x.IsCorrect,
                TimeTakenSeconds = x.TimeTakenSeconds,
                ImageUrl = x.ImageUrl,
                IsRTL = x.IsRTL, // ✅ مهم جدًا
                ComparisonValue1 = x.ValueA,
                ComparisonValue2 = x.ValueB,
                IsQuantitative = x.IsQuantitative,
                DisplayType =
                    x.Template == QuestionTemplate.CompareValues
                        ? QuestionDisplayType.ComparisonText
                        : x.Template == QuestionTemplate.CompareWithImage
                            ? QuestionDisplayType.ComparisonWithImage
                            : QuestionDisplayType.WithImage,
                VerbalPassageContent = x.VerbalPassageContent,
                Options = x.Options.Select(o => new QdratNew.ViewModels.Homework.QuestionOptionVm
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList()
            }).ToList();

            // ===============================
            // 5) الإحصائيات
            // ===============================
            int total = questions.Count;
            int correct = questions.Count(q => q.IsCorrect);
            int wrong = questions.Count(q => !q.IsCorrect && q.StudentAnswer != "—");
            int skipped = questions.Count(q => q.StudentAnswer == "—");

            double percent = total > 0
                ? Math.Round(correct * 100.0 / total, 1)
                : 0;

            double avgTime = total > 0
                ? Math.Round(questions.Average(q => q.TimeTakenSeconds), 1)
                : 0;

            var vm = new ViewModels.Exam.ExamReviewViewModel
            {
                ExamAssignmentId = exam.Id,
                ExamTitle = exam.Title,
                TotalQuestions = total,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                SkippedQuestions = skipped,
                ScorePercentage = percent,
                AverageTimePerQuestion = avgTime,
                TimeSpentFormatted = $"{Math.Round(questions.Sum(q => q.TimeTakenSeconds) / 60.0, 1)} دقيقة تقريبًا",
                Questions = questions
            };

            return View("~/Areas/Students/Views/PerformanceIndicatorExams/ReviewSectionQuestions.cshtml", vm);
        }


        [HttpGet]
        public async Task<IActionResult> GetExamProgress(int id)
        {
            using var _context = _contextFactory.CreateDbContext();
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { success = false, message = "لم يتم تحديد الطالب." });

            var totalQuestions = await _context.PerformanceIndicatorExamQuestions
                .CountAsync(eq => eq.PerformanceIndicatorExamId == id);

            var answeredCount = await _context.QuestionAttemptNew
                .CountAsync(a => a.PerformanceIndicatorExamId == id && a.StudentId == studentId && a.SelectedAnswer != null);

            return Json(new
            {
                success = true,
                totalQuestions,
                answeredCount
            });
        }

        private async Task<ExamSolveViewModel> GetSolveViewModel(int assignmentId, Guid q, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧩 جلب بيانات الاختبار
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null || assignment.Exam == null)
                throw new Exception("⚠️ لم يتم العثور على بيانات الاختبار.");

            var exam = assignment.Exam;

            // 🟢 جلب الأسئلة المرتبطة بالاختبار
            var examQuestions = await _context.ExamQuestions
                .Where(eq => eq.ExamAssignmentId == assignment.Id)
                .OrderBy(eq => eq.Order)
                .ToListAsync();

            var allQuestionIds = examQuestions.Select(eq => eq.QuestionId).ToList();
            if (!allQuestionIds.Any())
                throw new Exception("⚠️ لا توجد أسئلة في هذا الاختبار.");

            // 🧠 جلب السؤال الحالي
            var question = await _context.Questions
          .Include(qq => qq.Options)
          .Include(qq => qq.Lesson).ThenInclude(l => l.Section)
          .Include(qq => qq.VerbalPassage)
          .Include(qq => qq.Curriculum) // ✅ مهم جدًا
          .FirstOrDefaultAsync(qq => qq.Id == q);

            if (question == null)
                throw new Exception("⚠️ السؤال المحدد غير موجود.");

            // 🧩 آخر محاولة لهذا الطالب
            var attempt = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.QuestionId == q && a.ExamAssignmentId == assignmentId)
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefaultAsync();

            // 🧩 كل المحاولات لتلوين لوحة المراجعة
            var allAttempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.ExamAssignmentId == assignmentId)
                .ToListAsync();

            // 🔖 الأسئلة المعلمة للمراجعة
            var marked = TempData["ReviewMarked"] as string ?? "";
            TempData.Keep("ReviewMarked");
            var reviewIds = marked
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(x => Guid.TryParse(x, out _))
                .Select(Guid.Parse)
                .ToList();

            // 🧭 خريطة الإجابات السابقة
            var answersMap = allAttempts
                .GroupBy(a => a.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(a => a.AttemptedAt).First().SelectedAnswer ?? ""
                );

            bool isLast = q == allQuestionIds.LastOrDefault();

            // ✅ بناء ViewModel النهائي
            return new ExamSolveViewModel
            {
                ExamId = exam.Id,
                ExamAssignmentId = assignment.Id,
                ExamTitle = exam.Title,
                CurrentQuestionId = q,
                AllQuestionIds = allQuestionIds,
                Question = question.ToDisplayModel(),
                SelectedAnswer = attempt?.SelectedAnswer,
                AnswersMap = answersMap,
                DurationMinutes = exam.DurationMinutes,
                StartTime = DateTime.Now,
                IsExamSubmitted = false,
                ReviewMarkedIds = reviewIds,
                QuestionStartTime = DateTime.Now,
                ForceReviewVisible = isLast
            };
        }



        // 🟢 بناء ViewModel
        private async Task<ExamSolveViewModel> BuildPerformanceSolveViewModel(int examId, Guid q, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();
            var exam = await _context.PerformanceIndicatorExams.FirstOrDefaultAsync(e => e.Id == examId);
            var question = await _context.Questions
     .Include(qq => qq.Options)
     .Include(qq => qq.Lesson).ThenInclude(l => l.Section)
     .Include(qq => qq.VerbalPassage)
     .Include(qq => qq.Curriculum) // ✅ مهم جدًا
     .FirstOrDefaultAsync(qq => qq.Id == q);


            var allQ = await _context.PerformanceIndicatorExamQuestions
                .Where(eq => eq.PerformanceIndicatorExamId == examId)
                .OrderBy(eq => eq.OrderNumber)
                .Select(eq => eq.QuestionId)
                .ToListAsync();

            var attempt = await _context.QuestionAttemptNew
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.QuestionId == q && a.PerformanceIndicatorExamId == examId);

            // 🟢 الأسئلة المحددة للمراجعة
            var marked = TempData["ReviewMarked"] as string ?? "";
            TempData.Keep("ReviewMarked");
            var reviewIds = marked
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Where(x => Guid.TryParse(x, out _))
                .Select(Guid.Parse)
                .ToList();

            bool isLast = q == allQ.LastOrDefault();

            var allAttempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.PerformanceIndicatorExamId == examId)
                .ToListAsync();

            var answersMap = allAttempts
                .GroupBy(a => a.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(a => a.AttemptedAt)
                          .First()
                          .SelectedAnswer ?? ""
                );

            return new ExamSolveViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                CurrentQuestionId = q,
                AllQuestionIds = allQ,
                Question = question?.ToDisplayModel(),

                SelectedAnswer = attempt?.SelectedAnswer,
                DurationMinutes = exam.DurationMinutes,
                ExamAssignmentId = exam.Id,
                RemainingSeconds = (int)(exam.DurationMinutes * 60),
                ReviewMarkedIds = reviewIds,
                ForceReviewVisible = isLast,
                IsReviewMode = false,
                AnswersMap = answersMap,
                QuestionStartTime = DateTime.Now
            };
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

        // 🟢 تسليم اختبار المؤشر
        [HttpPost]
        public async Task<IActionResult> SubmitFinalPerformanceExam(int id, bool forceSubmit = false)
        {
            try
            {
                using var _context = _contextFactory.CreateDbContext();

                var studentId = await GetCurrentStudentIdAsync();
                if (studentId == null)
                    return Json(new { success = false, message = "لم يتم تحديد الطالب." });

                // ============================
                //   🧭 تحميل بيانات الاختبار
                // ============================
                var exam = await _context.PerformanceIndicatorExams
                    .Include(e => e.Curriculum)
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (exam == null)
                    return Json(new { success = false, message = "لم يتم العثور على الاختبار." });

                // ============================
                //   🟦 جلب الأسئلة
                // ============================
                var examQuestions = await _context.PerformanceIndicatorExamQuestions
                    .Where(eq => eq.PerformanceIndicatorExamId == exam.Id)
                    .Select(eq => new { eq.SectionId, eq.QuestionId })
                    .ToListAsync();

                if (!examQuestions.Any())
                    return Json(new { success = false, message = "⚠️ لا توجد أسئلة في هذا الاختبار." });

                // ============================
                //   🟩 جلب محاولات الطالب
                // ============================
                var attempts = await _context.QuestionAttemptNew
                    .Where(a => a.StudentId == studentId && a.PerformanceIndicatorExamId == exam.Id)
                    .ToListAsync();

                // ============================
                //   🧮 حساب النتيجة لكل محور
                // ============================
                var groupedSections = examQuestions.GroupBy(eq => eq.SectionId).ToList();

                foreach (var sectionGroup in groupedSections)
                {
                    int sectionId = sectionGroup.Key ?? 0;
                    var questionIds = sectionGroup.Select(q => q.QuestionId).ToList();
                    int totalQuestions = questionIds.Count;

                    int correct = attempts.Count(a =>
                        a.SectionId == sectionId &&
                        questionIds.Contains(a.QuestionId) &&
                        a.IsCorrect
                    );

                    int answered = attempts.Count(a =>
                        a.SectionId == sectionId &&
                        questionIds.Contains(a.QuestionId)
                    );

                    int skipped = totalQuestions - answered;

                    double percent = totalQuestions > 0
                        ? Math.Round((correct * 100.0 / totalQuestions), 2)
                        : 0.0;

                    double avgTime = attempts
                        .Where(a => a.SectionId == sectionId && questionIds.Contains(a.QuestionId))
                        .Select(a => (double?)a.TimeTakenSeconds ?? 0)
                        .DefaultIfEmpty(0)
                        .Average();

                    // 🟢 تسجيل النتيجة
                    await _examService.RecordStudentResultAsync(
                        studentId.Value,
                        exam.Id,
                        sectionId,
                        percent,
                        avgTime,
                        false,
                        false
                    );
                }

                // ============================
                //   🟦 تحديث حالة الاختبار
                // ============================
                var studentExam = await _context.PerformanceIndicatorExamStudents
                    .FirstOrDefaultAsync(e => e.PerformanceIndicatorExamId == exam.Id && e.StudentId == studentId);

                if (studentExam != null)
                {
                    studentExam.IsCompleted = true;
                    studentExam.CompletedAt = DateTime.Now;
                    await _context.SaveChangesAsync();

                    // ============================
                    //   🕒 حساب الزمن الفعلي
                    // ============================
                    int allowedSeconds = exam.DurationMinutes * 60;

                    var duration = _timeService.CalculateActualDuration(
                        studentExam.StartedAt,
                        studentExam.CompletedAt,
                        attempts,
                        allowedSeconds
                    );

                    double solveMinutes = Math.Round(duration.TotalMinutes, 2);

                    // ============================
                    //   📝 حفظ StudentPerformance
                    // ============================
                    var scores = await _context.StudentIndicatorResults
                        .Where(r => r.StudentId == studentId && r.PerformanceIndicatorExamId == exam.Id)
                        .Select(r => r.ScorePercent)
                        .ToListAsync();

                    double overallScore = scores.Any() ? scores.Average() : 0.0;

                    var perf = new StudentPerformance
                    {
                        StudentID = studentId.Value,
                        Score = Math.Round(overallScore, 2),
                        ExamDate = DateTime.UtcNow,

                        ActivityType = PerformanceActivityType.PerformanceIndicatorExam,
                        ExamType = ExamType.PerformanceScale,  // ⬅️ المهم جداً

                        ExamId = null,                       // ✅ الصحيح
                        CurriculumId = exam.CurriculumId,
                        SectionId = null,

                        EngagementScore = 100,
                        StudyHours = (float)solveMinutes
                    };

                    _context.StudentPerformances.Add(perf);
                    await _context.SaveChangesAsync();
                }

                // ============================
                //   🧠 توليد الخطة العلاجية
                // ============================
                try { await _analysisService.GenerateRemedialPlansAsync(exam.Id); } catch { }

                // ============================
                //   🧹 حذف Session
                // ============================
                string key = $"PerformanceExamQs_{id}_{studentId}";
                HttpContext.Session.Remove(key);

                // ============================
                //   🎯 إرجاع النتيجة
                // ============================
                return Json(new
                {
                    success = true,
                    redirectUrl = Url.Action("Result", "PerformanceIndicatorExams", new { id = exam.Id })
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    inner = ex.InnerException?.Message,
                    stack = ex.StackTrace
                });
            }

        }



        // 🟢 يعرض فورم إدخال الكود المرجعي
        [HttpGet]
        public IActionResult VerifyReferenceCode(int id)
        {
            // 🟢 تحقق من الـ Session
            var verifiedKey = $"VerifiedExam_{id}";
            var isVerified = HttpContext.Session.GetString(verifiedKey);

            if (isVerified == "true")
            {
                // ✅ إذا سبق التحقق، انتقل مباشرة للاختبار
                return RedirectToAction("StartPerformanceExam", new { id });
            }

            // 🧾 عرض نموذج إدخال الكود المرجعي
            return View("VerifyReferenceCode", id);
        }

        // 🟢 يتحقق من صحة الكود المرجعي
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyReferenceCode(int id, string referenceCode)
        {
            using var _context = _contextFactory.CreateDbContext();
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🔹 جلب الاختبار باستخدام الـ Id
            var exam = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على هذا الاختبار.";
                return View("VerifyReferenceCode", id);
            }

            if (string.IsNullOrWhiteSpace(exam.ReferenceCode))
            {
                TempData["Error"] = "⚠️ لم يتم تعيين كود مرجعي لهذا الاختبار. تواصل مع المشرف.";
                return View("VerifyReferenceCode", id);
            }

            // 🔍 تحقق من الكود المرجعي (غير حساس للحروف)
            if (!string.Equals(exam.ReferenceCode.Trim(), referenceCode.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "❌ الكود المرجعي غير صحيح. الرجاء التحقق من المشرف.";
                return View("VerifyReferenceCode", id);
            }

            // ✅ نجاح التحقق — حفظ Session وليس TempData
            HttpContext.Session.SetString($"VerifiedExam_{exam.Id}", "true");

            // 🟢 توجيه الطالب مباشرة إلى الاختبار
            return RedirectToAction("StartPerformanceExam", new { id = exam.Id });
        }


        [HttpPost]
        public async Task<IActionResult> ToggleReviewMark(Guid qid)
        {
            using var _context = _contextFactory.CreateDbContext();
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { success = false, message = "لم يتم التعرف على الطالب." });

            var attempt = await _context.QuestionAttemptNew
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.QuestionId == qid);

            if (attempt == null)
                return Json(new { success = false, message = "لم يتم العثور على المحاولة الخاصة بهذا السؤال." });

            attempt.IsMarkedForReview = !attempt.IsMarkedForReview;
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                marked = attempt.IsMarkedForReview,
                questionId = qid
            });
        }


        // 🟢 مراجعة اختبار مؤشر الأداء (Review)

        [HttpGet]
        public async Task<IActionResult> Review(int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ===============================
            // 1) بيانات الاختبار
            // ===============================
            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لم يتم العثور على بيانات هذا الاختبار.</div>", "text/html");

            // ===============================
            // 2) جلب كل أسئلة الاختبار + LEFT JOIN المحاولات
            // ===============================
            var data = await (
                from eq in _context.PerformanceIndicatorExamQuestions
                join q in _context.Questions
                                  .Include(q => q.Lesson).ThenInclude(l => l.Section)
                                  .Include(q => q.Options)
                                  .Include(q => q.VerbalPassage)
                                  .Include(q => q.Curriculum) // ✅ مهم جدًا
                      on eq.QuestionId equals q.Id
                join a in _context.QuestionAttemptNew
                    .Where(a => a.StudentId == studentId && a.PerformanceIndicatorExamId == exam.Id)
                    on q.Id equals a.QuestionId into attempts
                from attempt in attempts
                    .OrderByDescending(a => a.AttemptedAt)
                    .Take(1)
                    .DefaultIfEmpty()
                where eq.PerformanceIndicatorExamId == exam.Id
                select new
                {
                    q.Id,
                    q.Title,
                    IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true, // ✅
                    StudentAnswer = attempt != null && !string.IsNullOrWhiteSpace(attempt.SelectedAnswer)
                        ? attempt.SelectedAnswer
                        : "—",
                    IsCorrect = attempt != null && attempt.IsCorrect,
                    q.CorrectAnswer,
                    q.IsQuantitative,
                    q.ImageUrl,
                    q.Template,
                    q.ValueA,
                    q.ValueB,
                    LessonTitle = q.Lesson.Title,
                    SectionTitle = q.Lesson.Section.Title,
                    TimeTakenSeconds = attempt != null ? attempt.TimeTakenSeconds : 0,
                    VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                    Options = q.Options.Select(o => new
                    {
                        o.Text,
                        o.ImageUrl
                    }).ToList()
                }
            ).ToListAsync();

            if (!data.Any())
                return Content("<div class='alert alert-warning text-center'>⚠️ لا توجد أسئلة لهذا الاختبار.</div>", "text/html");

            // ===============================
            // 3) بناء الأسئلة
            // ===============================
            var questions = data.Select(x => new ExamReviewQuestionVm
            {
                QuestionId = x.Id,
                QuestionTitle = x.Title,
                StudentAnswer = x.StudentAnswer,
                CorrectAnswer = x.CorrectAnswer ?? "—",
                IsCorrect = x.IsCorrect,
                IsQuantitative = x.IsQuantitative,
                ImageUrl = x.ImageUrl,
                IsRTL = x.IsRTL, // ✅ أهم سطر
                DisplayType =
                    x.Template == QuestionTemplate.CompareValues
                        ? QuestionDisplayType.ComparisonText
                        : x.Template == QuestionTemplate.CompareWithImage
                            ? QuestionDisplayType.ComparisonWithImage
                            : QuestionDisplayType.WithImage,
                ComparisonValue1 = x.ValueA,
                ComparisonValue2 = x.ValueB,
                LessonTitle = x.LessonTitle,
                SectionTitle = x.SectionTitle,
                TimeTakenSeconds = x.TimeTakenSeconds,
                VerbalPassageContent = x.VerbalPassageContent,
                Options = x.Options.Select(o => new QdratNew.ViewModels.Homework.QuestionOptionVm
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList()
            }).ToList();

            // ===============================
            // 4) الإحصائيات — من نفس البيانات المعروضة
            // ===============================
            int total = questions.Count;
            int correct = questions.Count(q => q.IsCorrect);
            int wrong = questions.Count(q =>
                !q.IsCorrect &&
                q.StudentAnswer != "—");
            int skipped = questions.Count(q => q.StudentAnswer == "—");

            double percent = total > 0
                ? Math.Round(correct * 100.0 / total, 1)
                : 0;

            double avgTime = total > 0
                ? Math.Round(questions.Average(q => q.TimeTakenSeconds), 1)
                : 0;

            string formattedTime = $"{Math.Round(questions.Sum(q => q.TimeTakenSeconds) / 60.0, 1)} دقيقة تقريبًا";

            // ===============================
            // 5) ViewModel النهائي
            // ===============================
            var vm = new ViewModels.Exam.ExamReviewViewModel
            {
                ExamAssignmentId = examId,
                ExamTitle = exam.Title,
                TotalQuestions = total,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                SkippedQuestions = skipped,
                ScorePercentage = percent,
                AverageTimePerQuestion = avgTime,
                TimeSpentFormatted = formattedTime,
                Questions = questions
            };

            return View("~/Areas/Students/Views/PerformanceIndicatorExams/Review.cshtml", vm);
        }



        [HttpGet]
        public async Task<IActionResult> Result(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ================================
            // 1) تأكيد أن الاختبار اختبار مؤشر أداء
            // ================================
            var exam = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Include(e => e.Curriculum)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            // ================================
            // 2) سجل الطالب
            // ================================
            var studentExam = await _context.PerformanceIndicatorExamStudents
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.PerformanceIndicatorExamId == id &&
                    s.StudentId == studentId);

            // ================================
            // 3) المحاولات (مصدر الحقيقة)
            // ================================
            var attempts = await _context.QuestionAttemptNew
                .Where(a =>
                    a.StudentId == studentId &&
                    a.PerformanceIndicatorExamId == id)
                .ToListAsync();

            int totalQuestions = await _context.PerformanceIndicatorExamQuestions
                .CountAsync(q => q.PerformanceIndicatorExamId == id);

            int correct = attempts.Count(a => a.IsCorrect);
            int answered = attempts.Count(a =>
                !string.IsNullOrWhiteSpace(a.SelectedAnswer));

            int skipped = totalQuestions - answered;
            int wrong = answered - correct;

            double scorePercent = totalQuestions > 0
                ? Math.Round((correct * 100.0) / totalQuestions, 1)
                : 0;

            double avgTime = attempts.Any()
                ? Math.Round(attempts.Average(a => a.TimeTakenSeconds), 1)
                : 0;

            double solveMinutes = attempts.Any()
                ? Math.Round(attempts.Sum(a => a.TimeTakenSeconds) / 60.0, 1)
                : 0;

            // ================================
            // 4) نتائج المحاور (Section)
            // ================================
            var sectionGroups = await (
                from q in _context.PerformanceIndicatorExamQuestions
                join a in _context.QuestionAttemptNew
                    on q.QuestionId equals a.QuestionId
                where
                    q.PerformanceIndicatorExamId == id &&
                    a.StudentId == studentId
                select new
                {
                    q.SectionId,
                    a.IsCorrect,
                    a.SelectedAnswer,
                    a.TimeTakenSeconds
                }
            ).ToListAsync();

            var sections = sectionGroups
                .GroupBy(x => x.SectionId)
                .Select(g =>
                {
                    int sectionId = g.Key ?? 0;

                    int secTotal = _context.PerformanceIndicatorExamQuestions
                        .Count(q =>
                            q.PerformanceIndicatorExamId == id &&
                            q.SectionId == sectionId);

                    int secCorrect = g.Count(x => x.IsCorrect);
                    int secAnswered = g.Count(x =>
                        !string.IsNullOrWhiteSpace(x.SelectedAnswer));

                    return new SectionResultVm
                    {
                        SectionName = _context.Sections
                            .Where(s => s.Id == sectionId)
                            .Select(s => s.Title)
                            .FirstOrDefault() ?? "—",

                        ScorePercent = secTotal > 0
                            ? Math.Round((secCorrect * 100.0) / secTotal, 1)
                            : 0,

                        AverageTime = g.Any()
                            ? Math.Round(g.Average(x => x.TimeTakenSeconds), 1)
                            : 0
                    };
                })
                .ToList();

            // ================================
            // 5) ViewModel النهائي
            // ================================
            var vm = new PerformanceIndicatorResultVm
            {
                ExamId = id,
                ExamTitle = exam.Title,

                TotalQuestions = totalQuestions,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                SkippedQuestions = skipped,

                TotalAverage = scorePercent,
                AverageTimePerQuestion = avgTime,
                TotalTimeMinutes = solveMinutes,

                AssignedAt = studentExam?.StartedAt ?? DateTime.Now,
                SubmittedAt = studentExam?.CompletedAt,

                Sections = sections
            };

            return View("Result", vm);
        }


    }
}
