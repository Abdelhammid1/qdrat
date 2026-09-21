using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs.Homework;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Homework.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.Services.StudentProgress;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Question;
using System.Text.Json;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class StudentHomeworkFlowController : StudentBaseController
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IHomeworkWriteService _homeworkWriteService;
        private readonly IStudentProgressService _studentProgressService;
        private readonly ITimeZoneService _time;
        private readonly IHomeworkAnalyticsService _homeworkAnalyticsService;
        private readonly IIntegrityGuardService _integrityGuard;
        public StudentHomeworkFlowController(
      IDbContextFactory<ApplicationDbContext> contextFactory,
      UserManager<ApplicationUser> userManager,
      IHomeworkWriteService homeworkWriteService,
      IStudentProgressService studentProgressService,
      ITimeZoneService time,
      IHomeworkAnalyticsService homeworkAnalyticsService,
      IIntegrityGuardService integrityGuard
  ) : base(contextFactory, userManager)
        {
            _contextFactory = contextFactory;
            _homeworkWriteService = homeworkWriteService;
            _studentProgressService = studentProgressService;
            _time = time;
            _homeworkAnalyticsService = homeworkAnalyticsService;
            _integrityGuard = integrityGuard;
        }
        // ======================================================
        // Start (نفس التوقيع)
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> Start(int homeworkSetId, Guid? q = null)
        {
            using var db = _contextFactory.CreateDbContext();

            int studentId = StudentId;

            if (await _integrityGuard.IsBlockedAsync(studentId, IntegrityAttemptType.Homework, homeworkSetId))
                return RedirectToAction("Blocked", "IntegrityGuard", new { area = "Students", attemptType = (int)IntegrityAttemptType.Homework, attemptEntityId = homeworkSetId, returnUrl = $"{Request.Path}{Request.QueryString}" });

            bool isSubmitted = await db.HomeworkSetStudents
                .AsNoTracking()
                .AnyAsync(x =>
                    x.StudentId == studentId &&
                    x.HomeworkSetId == homeworkSetId &&
                    x.IsSubmitted);

            if (isSubmitted)
            {
                return RedirectToAction(
                    "ErrorMessage",
                    "StudentHomeworkDashboard",
                    new { area = "Students", msg = "لقد انتهى هذا الواجب بالفعل." }
                );
            }

            // IDs من Session
            var cacheKey = $"HomeworkQuestions_{homeworkSetId}_{studentId}";
            List<Guid> allQuestionIds;

            var cached = HttpContext.Session.GetString(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                allQuestionIds = JsonSerializer.Deserialize<List<Guid>>(cached)!;
            }
            else
            {
                allQuestionIds = await db.Homeworks
                    .AsNoTracking()
                    .Where(h => h.HomeworkSetId == homeworkSetId && h.StudentId == studentId)
                    .OrderBy(h => h.Id)
                    .Select(h => h.QuestionId)
                    .Distinct()
                    .ToListAsync();

                if (!allQuestionIds.Any())
                    return RedirectToAction("ErrorMessage", "StudentHomeworkDashboard",
                        new { area = "Students", msg = "لا توجد أسئلة لهذا الواجب." });

                HttpContext.Session.SetString(cacheKey, JsonSerializer.Serialize(allQuestionIds));
            }

            Guid currentQuestionId =
                q.HasValue && allQuestionIds.Contains(q.Value)
                    ? q.Value
                    : allQuestionIds.First();

            var vm = await GetSolveViewModel(
                homeworkSetId,
                currentQuestionId,
                studentId,
                db
            );

            return View("Start", vm);
        }

        // ======================================================
        // SubmitAnswerFetch (نفس التوقيع)
        // ======================================================
        [HttpPost]
        public async Task<IActionResult> SubmitAnswerFetch(
            int id,
            Guid q,
            string nav,
            string? SelectedOption,
            int? timeTakenSeconds = null)
        {
            using var db = _contextFactory.CreateDbContext();
            HttpContext.RequestAborted.ThrowIfCancellationRequested();

            int studentId = StudentId;

            var allQuestions = await GetHomeworkQuestionIdsAsync(id, studentId, db);
            if (!allQuestions.Any())
                return Json(new { success = false });

            bool hasAnswer = !string.IsNullOrWhiteSpace(SelectedOption);

            if (hasAnswer)
            {
                var question = await db.Questions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == q);

                if (question != null)
                {
                    bool isCorrect = string.Equals(
                        SelectedOption.Trim(),
                        question.CorrectAnswer?.Trim(),
                        StringComparison.OrdinalIgnoreCase);

                    await _homeworkWriteService.SaveQuestionAttemptAsync(
                        new HomeworkQuestionAttemptInput
                        {
                            StudentId = studentId,
                            HomeworkSetId = id,
                            QuestionId = q,
                            SelectedAnswer = SelectedOption,
                            IsCorrect = isCorrect,
                            TimeTakenSeconds = timeTakenSeconds ?? 0,
                            IsMarkedForReview = nav == "review"
                        });

                    // ✅ تحديث answersMap في Session بدلاً من إعادة استعلامه
                    var mapCacheKey = $"AnswersMap_{id}_{studentId}";
                    var cachedMapJson = HttpContext.Session.GetString(mapCacheKey);
                    if (cachedMapJson != null)
                    {
                        var map = JsonSerializer.Deserialize<Dictionary<Guid, string>>(cachedMapJson)
                                  ?? new Dictionary<Guid, string>();
                        map[q] = SelectedOption;
                        HttpContext.Session.SetString(mapCacheKey, JsonSerializer.Serialize(map));
                    }
                }
            }
            else if (timeTakenSeconds.HasValue && timeTakenSeconds > 0)
            {
                await _homeworkWriteService.SaveQuestionAttemptAsync(
                    new HomeworkQuestionAttemptInput
                    {
                        StudentId = studentId,
                        HomeworkSetId = id,
                        QuestionId = q,
                        SelectedAnswer = string.Empty,
                        IsCorrect = false,
                        TimeTakenSeconds = timeTakenSeconds.Value,
                        IsMarkedForReview = false
                    });
            }

            // ✅ Auto-save فقط - لا داعي لـ GetSolveViewModel أو PartialView
            if (nav == "stay")
                return Json(new { success = true });

            // المراجعة
            if (nav == "review")
            {
                var reviewKey = $"ReviewMarked_{id}_{studentId}";
                var list = HttpContext.Session.GetString(reviewKey);
                var ids = string.IsNullOrEmpty(list)
                    ? new List<Guid>()
                    : JsonSerializer.Deserialize<List<Guid>>(list)!;

                if (!ids.Contains(q))
                    ids.Add(q);

                HttpContext.Session.SetString(reviewKey, JsonSerializer.Serialize(ids));
            }

            // Jump / Review
            if (nav == "goto" || nav == "jump" || nav == "showReview")
            {
                Guid target = q;

                if (nav == "goto" || nav == "jump")
                {
                    var targetStr = HttpContext.Request.Form["target"].FirstOrDefault();
                    if (!Guid.TryParse(targetStr, out target))
                        return Json(new { success = false });
                }

                var vm = await GetSolveViewModel(id, target, studentId, db);
                vm.IsReviewMode = true;
                vm.ForceReviewVisible = true;

                return PartialView("_SolveHomeworkQuestionPartial", vm);
            }

            // تنقل
            int index = allQuestions.FindIndex(x => x == q);
            if (nav == "next" && index < allQuestions.Count - 1)
                index++;
            else if (nav == "prev" && index > 0)
                index--;

            var nextVm = await GetSolveViewModel(
                id,
                allQuestions[index],
                studentId,
                db
            );

            return PartialView("_SolveHomeworkQuestionPartial", nextVm);
        }

        // ======================================================
        // SubmitFinal (نفس التوقيع)
        // ======================================================
        [HttpPost]
        public async Task<IActionResult> SubmitFinal(int id, bool forceSubmit = false)
        {
            using var db = _contextFactory.CreateDbContext();

            int studentId = StudentId;
            var now = _time.GetNowSaudi();

            var hss = await db.HomeworkSetStudents
                .FirstOrDefaultAsync(x =>
                    x.StudentId == studentId &&
                    x.HomeworkSetId == id);

            if (hss == null)
            {
                hss = new HomeworkSetStudent
                {
                    StudentId = studentId,
                    HomeworkSetId = id,
                    AssignedAt = now,
                    SubmittedAt = now // ✅ مهم
                };
                db.HomeworkSetStudents.Add(hss);
            }
            else if (hss.SubmittedAt == null)
            {
                // ✅ التحديث الحاسم
                hss.SubmittedAt = now;
            }

            await db.SaveChangesAsync();

            // منطق الحل الفعلي
            await _homeworkWriteService.SubmitHomeworkAsync(id, studentId);
            await _homeworkAnalyticsService.BuildAndStoreAsync(studentId, id);

            await _studentProgressService.RecordHomeworkProgress(studentId, id);

            var redirectUrl = Url.Action(
                "HomeworkReportUnified",
                "StudentHomeworkResults",
                new { area = "Students", homeworkSetId = id });

            return Json(new { success = true, redirectUrl });
        }

        // ======================================================
        // GetCurriculumData (المهم)
        // ======================================================
        [HttpGet]
        public async Task<IActionResult> GetCurriculumData(int curriculumId)
        {
            using var db = _contextFactory.CreateDbContext();

            int studentId = StudentId;
            int batchId = ActiveBatchId;
            int courseId = ActiveCourseId;

            var homeworkSetIds = await (
                from hs in db.HomeworkSets.AsNoTracking()
                join b in db.Batches.AsNoTracking()
                    on hs.BatchId equals b.Id
                where hs.BatchId == batchId && b.CourseId == courseId
                select hs.Id
            ).ToListAsync();

            if (!homeworkSetIds.Any())
                return Json(EmptyCharts());

            var attempts = await (
                from a in db.QuestionAttemptNew.AsNoTracking()
                join q in db.Questions.AsNoTracking() on a.QuestionId equals q.Id
                join l in db.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in db.Sections.AsNoTracking() on l.SectionId equals s.Id
                where a.StudentId == studentId && a.HomeworkSetId.HasValue
                select new
                {
                    a.HomeworkSetId,
                    a.QuestionId,
                    a.IsCorrect,
                    a.AttemptedAt,
                    SectionId = s.Id,
                    SectionTitle = s.Title,
                    CurriculumId = s.CurriculumId
                }
            ).ToListAsync();

            attempts = attempts
                .Where(a => homeworkSetIds.Any(id => id == a.HomeworkSetId))
                .ToList();

            if (curriculumId > 0)
                attempts = attempts.Where(a => a.CurriculumId == curriculumId).ToList();

            if (!attempts.Any())
                return Json(EmptyCharts());

            var finalAttempts = attempts
                .GroupBy(x => new { x.HomeworkSetId, x.QuestionId })
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToList();

            // Radar
            var sectionLabels = new List<string>();
            var studentScores = new List<double>();
            var batchAverages = new List<double>();

            foreach (var g in finalAttempts.GroupBy(x => new { x.SectionId, x.SectionTitle }))
            {
                int total = g.Count();
                int correct = g.Count(x => x.IsCorrect);

                double score = total == 0 ? 0 : Math.Round(correct * 100.0 / total, 1);

                sectionLabels.Add(g.Key.SectionTitle);
                studentScores.Add(score);
                batchAverages.Add(score); // مؤقتًا
            }

            // Progress
            var homeworkTitles = new List<string>();
            var correctAnswers = new List<int>();
            var remainingQuestions = new List<int>();
            var studentHomeworkScores = new List<double>();
            var batchHomeworkAverages = new List<double>();

            int i = 1;
            foreach (var hw in finalAttempts.GroupBy(x => x.HomeworkSetId))
            {
                int total = hw.Count();
                int correct = hw.Count(x => x.IsCorrect);

                homeworkTitles.Add($"واجب {i++}");
                correctAnswers.Add(correct);
                remainingQuestions.Add(Math.Max(0, total - correct));

                double score = total == 0 ? 0 : Math.Round(correct * 100.0 / total, 1);
                studentHomeworkScores.Add(score);
                batchHomeworkAverages.Add(score);
            }

            return Json(new
            {
                sectionLabels,
                studentScores,
                batchAverages,
                homeworkTitles,
                correctAnswers,
                remainingQuestions,
                studentHomeworkScores,
                batchHomeworkAverages
            });
        }

        // ======================================================
        private object EmptyCharts() => new
        {
            sectionLabels = new List<string>(),
            studentScores = new List<double>(),
            batchAverages = new List<double>(),
            homeworkTitles = new List<string>(),
            correctAnswers = new List<int>(),
            remainingQuestions = new List<int>(),
            studentHomeworkScores = new List<double>(),
            batchHomeworkAverages = new List<double>()
        };




        private async Task<HomeworkSolveViewModel> GetSolveViewModel(
    int homeworkSetId,
    Guid questionId,
    int studentId,
    ApplicationDbContext _context)
        {
            // ======================================================
            // 1) IDs الأسئلة
            // ======================================================
            var allQuestions = await GetHomeworkQuestionIdsAsync(
                homeworkSetId,
                studentId,
                _context);

            if (!allQuestions.Any())
                return new HomeworkSolveViewModel
                {
                    ErrorMessage = "⚠️ لا توجد أسئلة لهذا الواجب."
                };

            // ======================================================
            // 2) تحميل السؤال + IsQuantitative (حتى مع الكاش)
            // ======================================================
            QuestionDisplayViewModel? qVm = null;
            bool isQuantitative;

            string cacheKey = $"Question_{questionId}";

            if (HttpContext.Session.TryGetValue(cacheKey, out var bytes))
            {
                qVm = JsonSerializer.Deserialize<QuestionDisplayViewModel>(bytes);
                // IsRTL و IsQuantitative محفوظان بالفعل في الـ ViewModel المخزّن - لا حاجة لاستعلام DB
                isQuantitative = qVm!.IsQuantitative;
            }
            else
            {
                var qEntity = await _context.Questions
                    .Include(q => q.Options)
                    .Include(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                    .Include(q => q.VerbalPassage)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == questionId);

                if (qEntity == null)
                    return new HomeworkSolveViewModel
                    {
                        ErrorMessage = "⚠️ فشل تحميل السؤال."
                    };

                isQuantitative =
                    qEntity.Lesson?.Section?.Curriculum?.IsQuantitative ?? false;

                qVm = qEntity.ToDisplayModel();
                qVm.IsRTL = qEntity.Lesson?.Section?.Curriculum?.IsRTL ?? true;
                qVm.IsQuantitative = isQuantitative; // ✅ حفظ IsQuantitative مع الكاش
                HttpContext.Session.Set(
                    cacheKey,
                    JsonSerializer.SerializeToUtf8Bytes(qVm));
            }

            if (qVm == null)
                return new HomeworkSolveViewModel
                {
                    ErrorMessage = "⚠️ فشل تحميل السؤال."
                };

            // ======================================================
            // 3) آخر إجابة لهذا السؤال
            // ======================================================
            var attempt = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    a.HomeworkSetId == homeworkSetId &&
                    a.QuestionId == questionId)
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefaultAsync();

            var selectedAnswer = attempt?.SelectedAnswer ?? "";

            foreach (var opt in qVm.Options)
                opt.IsSelected = opt.Text == selectedAnswer;

            // ======================================================
            // 4) جميع الإجابات داخل الواجب (Session cache)
            // ======================================================
            var mapCacheKey = $"AnswersMap_{homeworkSetId}_{studentId}";
            Dictionary<Guid, string> answersMap;
            var cachedMapJson = HttpContext.Session.GetString(mapCacheKey);
            if (cachedMapJson != null)
            {
                answersMap = JsonSerializer.Deserialize<Dictionary<Guid, string>>(cachedMapJson)
                             ?? new Dictionary<Guid, string>();
            }
            else
            {
                // أول تحميل: استعلام من DB ثم تخزين في Session
                answersMap = await _context.QuestionAttemptNew
                    .AsNoTracking()
                    .Where(a =>
                        a.StudentId == studentId &&
                        a.HomeworkSetId == homeworkSetId)
                    .GroupBy(a => a.QuestionId)
                    .ToDictionaryAsync(
                        g => g.Key,
                        g => g.OrderByDescending(a => a.AttemptedAt)
                              .FirstOrDefault()?.SelectedAnswer ?? "");
                HttpContext.Session.SetString(mapCacheKey, JsonSerializer.Serialize(answersMap));
            }

            // ======================================================
            // 5) المراجعة
            // ======================================================
            var reviewKey = $"ReviewMarked_{homeworkSetId}_{studentId}";
            var reviewList = HttpContext.Session.TryGetValue(reviewKey, out var rb)
                ? JsonSerializer.Deserialize<List<Guid>>(rb) ?? new()
                : new List<Guid>();

            // ======================================================
            // 6) ViewModel النهائي
            // ======================================================
            return new HomeworkSolveViewModel
            {
                HomeworkSetId = homeworkSetId,
                Question = qVm,
                CurrentQuestionId = questionId,
                AllQuestionIds = allQuestions,
                SelectedAnswer = selectedAnswer,
                AnswersMap = answersMap,
                ReviewMarkedIds = reviewList,

                // ⭐ حاسم
                IsQuantitative = isQuantitative,

                IsReviewMode = false,
                ForceReviewVisible = false,
                IsHomeworkSubmitted = false
            };
        }





        // ======================================================
        private async Task<List<Guid>> GetHomeworkQuestionIdsAsync(
            int homeworkSetId,
            int studentId,
            ApplicationDbContext db)
        {
            var cacheKey = $"HomeworkQuestions_{homeworkSetId}_{studentId}";
            var cached = HttpContext.Session.GetString(cacheKey);
            if (!string.IsNullOrEmpty(cached))
                return JsonSerializer.Deserialize<List<Guid>>(cached)!;

            var ids = await db.Homeworks
                .AsNoTracking()
                .Where(h => h.HomeworkSetId == homeworkSetId && h.StudentId == studentId)
                .OrderBy(h => h.Id)
                .Select(h => h.QuestionId)
                .ToListAsync();

            HttpContext.Session.SetString(cacheKey, JsonSerializer.Serialize(ids));
            return ids;
        }
    }
}
