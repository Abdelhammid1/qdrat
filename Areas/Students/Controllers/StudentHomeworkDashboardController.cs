using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Analytics;
using QdratNew.Data;
using QdratNew.DTOs.Homework;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services;
using QdratNew.Services.Homework.Interfaces;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.Services.StudentProgress;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Instructor;
using QdratNew.ViewModels.Question;
using QdratNew.ViewModels.Reports;
using QdratNew.ViewModels.Students;
using Rotativa.AspNetCore;
using System.Linq;
using System.Text.Json;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class StudentHomeworkDashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHomeworkAssignmentService _homeworkAssignmentService;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<StudentHomeworkDashboardController> _logger;
        private readonly IStudentAnalyticsService _analyticsService;
        private readonly IStudentAnalyticsService _studentAnalyticsService;
        private readonly IStudentRankingService _studentRankingService;
        private readonly StudentAnalyticsHelper _analyticsHelper;
        private readonly IStudentHomeworkAnalyticsService _homeworkAnalyticsService;
        private readonly IHomeworkRecommendationService _homeworkRecommendationService;
        private readonly IStudentProgressService _studentProgressService;
        private readonly IHomeworkWriteService _homeworkWriteService;
        private readonly IMemoryCache _cache;

        public StudentHomeworkDashboardController(
            UserManager<ApplicationUser> userManager,
            IHomeworkAssignmentService homeworkAssignmentService,
    IDbContextFactory<ApplicationDbContext> contextFactory,
            ILogger<StudentHomeworkDashboardController> logger, IStudentAnalyticsService analyticsService, IStudentAnalyticsService studentAnalyticsService, IStudentRankingService studentRankingService, StudentAnalyticsHelper analyticsHelper, IStudentHomeworkAnalyticsService homeworkAnalyticsService, IHomeworkRecommendationService homeworkRecommendationService, IStudentProgressService studentProgressService, IHomeworkWriteService homeworkWriteService, IMemoryCache cache)
        {
            _userManager = userManager;
            _homeworkAssignmentService = homeworkAssignmentService;
            _contextFactory = contextFactory;
            _logger = logger;
            _analyticsService = analyticsService;
            _studentAnalyticsService = studentAnalyticsService;
            _studentRankingService = studentRankingService;
            _analyticsHelper = analyticsHelper;
            _homeworkAnalyticsService = homeworkAnalyticsService;
            _homeworkRecommendationService = homeworkRecommendationService;
            _studentProgressService = studentProgressService;
            _homeworkWriteService = homeworkWriteService; // 🔴 إضافة فقط
            _cache = cache;

        }
        public async Task<IActionResult> Recommendations(int curriculumId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            var analytics = await _analyticsService.AnalyzeStudentAsync(studentId.Value, curriculumId);

            return Json(analytics);
        }
        private async Task<int?> GetCurrentStudentIdAsync()
        {
            using var _context = _contextFactory.CreateDbContext();

            var userId = _userManager.GetUserId(User);

            if (string.IsNullOrEmpty(userId))
                return null;

            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            if (student == null)
            {
                _logger.LogWarning("لم يتم العثور على طالب مرتبط بالمستخدم ذو الـ ID: " + userId);
                return null;
            }

            return student.StudentID;
        }


        [HttpGet]
        public IActionResult ErrorMessage(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg))
                msg = "حدث خطأ غير متوقع أثناء معالجة الطلب.";

            ViewBag.ErrorMessage = msg;

            // منع التخزين في المتصفح
            Response.Headers["Cache-Control"] = "no-cache,no-store,must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // =====================================================
            // 1) جلب جميع HomeworkSets الخاصة بدفعات الطالب (مرة واحدة)
            // =====================================================
            var homeworkSets = await (
                from hs in _context.HomeworkSets.AsNoTracking()
                join sbe in _context.StudentBatchEnrollments.AsNoTracking()
                    on hs.BatchId equals sbe.BatchId
                where sbe.StudentID == studentId
                select new
                {
                    hs.Id,
                    hs.StartAt,
                    hs.EndAt
                }
            ).Distinct().ToListAsync();

            if (!homeworkSets.Any())
            {
                return View(new StudentHomeworkDashboardViewModel
                {
                    TotalAssigned = 0,
                    Completed = 0,
                    Pending = 0,
                    Late = 0
                });
            }

            // =====================================================
            // 2) جلب حالات الطالب من HomeworkSetStudents
            // =====================================================
            var submittedStates = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Where(x => x.StudentId == studentId && x.IsSubmitted)
                .Select(x => new
                {
                    x.HomeworkSetId,
                    x.SubmittedAt
                })
                .ToListAsync();

            // HashSet لتسريع البحث (O(1))
            var submittedSetIds = new HashSet<int>(
                submittedStates.Select(x => x.HomeworkSetId)
            );

            // =====================================================
            // 3) الحسابات النهائية (تمرير واحد فقط)
            // =====================================================
            int totalAssigned = homeworkSets.Count;
            int completed = submittedSetIds.Count;

            int pending = 0;
            int late = 0;

            var now = DateTime.Now;

            foreach (var hw in homeworkSets)
            {
                if (submittedSetIds.Contains(hw.Id))
                    continue;

                if (hw.EndAt.HasValue)
                {
                    if (hw.EndAt.Value < now)
                        late++;
                    else
                        pending++;
                }
            }

            var vm = new StudentHomeworkDashboardViewModel
            {
                TotalAssigned = totalAssigned,
                Completed = completed,
                Pending = pending,
                Late = late
            };

            // =====================================================
            // 4) آخر واجب محلول (للتحليل)
            // =====================================================
            var lastSolvedSetId = submittedStates
                .OrderByDescending(x => x.SubmittedAt)
                .Select(x => x.HomeworkSetId)
                .FirstOrDefault();

            if (lastSolvedSetId > 0)
            {
                vm.LastHomeworkAnalysis =
                    await _homeworkAnalyticsService.AnalyzeHomeworkAsync(
                        studentId.Value,
                        lastSolvedSetId
                    );

                if (vm.LastHomeworkAnalysis != null)
                {
                    vm.Recommendation =
                        _homeworkRecommendationService.GenerateRecommendation(
                            vm.LastHomeworkAnalysis
                        );
                }
            }

            return View(vm);
        }




        [HttpGet]
        public async Task<IActionResult> GetCurriculumData(int curriculumId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { error = "Student not found" });

            // =====================================================
            // 1) دفعة الطالب
            // =====================================================
            var batchId = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(x => x.StudentID == studentId)
                .Select(x => x.BatchId)
                .FirstOrDefaultAsync();

            if (batchId == 0)
                return Json(new { error = "Batch not found" });

            // =====================================================
            // 2) المحاور (مرة واحدة)
            // =====================================================
            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => curriculumId == 0 || s.CurriculumId == curriculumId)
                .Select(s => new { s.Id, s.Title })
                .ToListAsync();

            // =====================================================
            // 3) أداء الطالب (StudentPerformance) – مرة واحدة
            // =====================================================
            var studentPerformances = await _context.StudentPerformances
                .AsNoTracking()
                .Where(sp =>
                    sp.StudentID == studentId &&
                    (curriculumId == 0 || sp.CurriculumId == curriculumId))
                .ToListAsync();

            // =====================================================
            // 4) أداء الدفعة (StudentPerformance) – مرة واحدة
            // =====================================================
            var batchPerformances = await (
                from sp in _context.StudentPerformances.AsNoTracking()
                join sbe in _context.StudentBatchEnrollments.AsNoTracking()
                    on sp.StudentID equals sbe.StudentID
                where sbe.BatchId == batchId &&
                      (curriculumId == 0 || sp.CurriculumId == curriculumId)
                select sp
            ).ToListAsync();

            // =====================================================
            // 5) بناء الرادار (بدون أي Query داخل Loop)
            // =====================================================
            var sectionLabels = new List<string>();
            var studentScores = new List<double>();
            var batchAverages = new List<double>();

            foreach (var sec in sections)
            {
                sectionLabels.Add(sec.Title ?? $"محور {sec.Id}");

                var stuScores = studentPerformances
                    .Where(x => x.SectionId == sec.Id)
                    .Select(x => x.Score)
                    .ToList();

                studentScores.Add(stuScores.Any() ? Math.Round(stuScores.Average(), 2) : 0);

                var batchScores = batchPerformances
                    .Where(x => x.SectionId == sec.Id)
                    .Select(x => x.Score)
                    .ToList();

                batchAverages.Add(batchScores.Any() ? Math.Round(batchScores.Average(), 2) : 0);
            }

            // =====================================================
            // 6) بيانات تطور الواجبات (من HomeworkSetStudents فقط)
            // =====================================================
            var homeworkStats = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking()
                    on hss.HomeworkSetId equals hs.Id
                where hss.StudentId == studentId && hss.IsSubmitted
                select new
                {
                    hs.Id,
                    hs.CompletionTitle,
                    hss.Score,
                    hss.SubmittedAt
                }
            ).OrderBy(x => x.SubmittedAt).ToListAsync();

            var progressLabels = homeworkStats.Select(x => x.CompletionTitle).ToList();
            var studentHomeworkScores = homeworkStats.Select(x => x.Score ?? 0).ToList();
            var homeworkTitles = homeworkStats.Select(x => x.CompletionTitle).ToList();

            // =====================================================
            // 7) متوسط الدفعة للواجبات (محسوب مرة واحدة)
            // =====================================================
            var batchHomeworkAverages = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking()
                    on hss.HomeworkSetId equals hs.Id
                where hss.IsSubmitted && hs.BatchId == batchId
                group hss by hss.HomeworkSetId into g
                select g.Average(x => x.Score ?? 0)
            ).ToListAsync();


            // =====================================================
            // 8) بيانات الشارت (نِسَب صحيحة / خاطئة)
            // =====================================================
            var homeworkStatsDetailed = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking()
                    on hss.HomeworkSetId equals hs.Id
                where hss.StudentId == studentId && hss.IsSubmitted
                select new
                {
                    hs.CompletionTitle,
                    Score = hss.Score ?? 0
                }
            ).OrderBy(x => x.CompletionTitle).ToListAsync();

            // الصحيح = النسبة
            var correctAnswers = homeworkStatsDetailed
                .Select(x => Math.Round(x.Score, 2))
                .ToList();

            // الخطأ = 100 - النسبة
            var remainingQuestions = homeworkStatsDetailed
                .Select(x => Math.Round(100 - x.Score, 2))
                .ToList();




            return Json(new
            {
                sectionLabels,
                studentScores,
                batchAverages,
                progressLabels,
                homeworkTitles,
                studentHomeworkScores,
                batchHomeworkAverages,
                correctAnswers,
                remainingQuestions
            });


        }


       


        private async Task<string> GetUserName(string? userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return "إداري";

            var user = await _userManager.FindByIdAsync(userId);
            return user?.FullName ?? "غير معروف";
        }


        [HttpGet]
        public async Task<IActionResult> Start(int homeworkSetId, Guid? q = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ----------------------------------------------------
            // 🟢 [1] تحقق من التسليم (خفيف)
            // ----------------------------------------------------
            bool isSubmitted = await _context.HomeworkSetStudents
                .AsNoTracking()
                .AnyAsync(x =>
                    x.StudentId == studentId &&
                    x.HomeworkSetId == homeworkSetId &&
                    x.IsSubmitted);

            if (isSubmitted)
            {
                return RedirectToAction(
                    "ErrorMessage",
                    new { msg = "لقد انتهى هذا الواجب بالفعل ولا يمكن الدخول مرة أخرى." }
                );
            }

            // ----------------------------------------------------
            // 🟢 [2] IDs الأسئلة (Session أولًا)
            // ----------------------------------------------------
            var questionsCacheKey = $"HomeworkQuestions_{homeworkSetId}_{studentId}";
            List<Guid> allQuestions;

            var cachedIds = HttpContext.Session.GetString(questionsCacheKey);
            if (!string.IsNullOrEmpty(cachedIds))
            {
                allQuestions = JsonSerializer.Deserialize<List<Guid>>(cachedIds)!;
            }
            else
            {
                var homeworks = await _context.Homeworks
                    .AsNoTracking()
                    .Where(h => h.HomeworkSetId == homeworkSetId && h.StudentId == studentId)
                    .OrderBy(h => h.Id)
                    .Select(h => h.QuestionId)
                    .Distinct()
                    .ToListAsync();

                if (!homeworks.Any())
                    return RedirectToAction("ErrorMessage", new { msg = "⚠️ لا توجد أسئلة لهذا الواجب." });

                allQuestions = homeworks;

                HttpContext.Session.SetString(
                    questionsCacheKey,
                    JsonSerializer.Serialize(allQuestions)
                );
            }

            // ----------------------------------------------------
            // 🟢 [3] تحديد السؤال الحالي
            // ----------------------------------------------------
            Guid currentQuestionId =
                q.HasValue && allQuestions.Contains(q.Value)
                    ? q.Value
                    : allQuestions.First();

            // ----------------------------------------------------
            // 🟢 [4] تحميل السؤال الحالي فقط (بدل كل الأسئلة)
            // ----------------------------------------------------
            QuestionDisplayViewModel questionVm;
            bool isQuantitative;

            var questionCacheKey = $"Question_{currentQuestionId}";
            if (HttpContext.Session.TryGetValue(questionCacheKey, out var cachedQuestion))
            {
                questionVm = JsonSerializer.Deserialize<QuestionDisplayViewModel>(cachedQuestion)!;

                isQuantitative = await _context.Questions
                    .AsNoTracking()
                    .Where(qx => qx.Id == currentQuestionId)
                    .Select(qx => qx.Lesson.Section.Curriculum.IsQuantitative)
                    .FirstOrDefaultAsync();
            }
            else
            {
                var questionEntity = await _context.Questions
                    .Include(q => q.Options)
                    .Include(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                    .Include(q => q.VerbalPassage)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(qx => qx.Id == currentQuestionId);

                if (questionEntity == null)
                    return RedirectToAction("ErrorMessage", new { msg = "⚠️ فشل تحميل السؤال." });

                isQuantitative =
                    questionEntity.Lesson?.Section?.Curriculum?.IsQuantitative ?? false;

                questionVm = questionEntity.ToDisplayModel();

                HttpContext.Session.Set(
                    questionCacheKey,
                    JsonSerializer.SerializeToUtf8Bytes(questionVm)
                );
            }

            // ----------------------------------------------------
            // 🟢 [5] المحاولات
            // ----------------------------------------------------
            var allAttempts = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            var latestAttempt = allAttempts
                .Where(a => a.QuestionId == currentQuestionId)
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefault();

            // ----------------------------------------------------
            // 🟢 [6] المراجعة
            // ----------------------------------------------------
            var reviewKey = $"ReviewMarked_{homeworkSetId}_{studentId}";
            var reviewListJson = HttpContext.Session.GetString(reviewKey);
            var reviewList = string.IsNullOrEmpty(reviewListJson)
                ? new List<Guid>()
                : JsonSerializer.Deserialize<List<Guid>>(reviewListJson)!;

            // ----------------------------------------------------
            // 🟢 [7] ViewModel النهائي (بدون تغيير)
            // ----------------------------------------------------
            var vm = new HomeworkSolveViewModel
            {
                HomeworkSetId = homeworkSetId,
                CurrentQuestionId = currentQuestionId,
                AllQuestionIds = allQuestions,
                Question = questionVm,
                SelectedAnswer = latestAttempt?.SelectedAnswer,
                AnswersMap = allAttempts
                    .GroupBy(a => a.QuestionId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderByDescending(a => a.AttemptedAt)
                              .FirstOrDefault()?.SelectedAnswer ?? ""
                    ),
                ReviewMarkedIds = reviewList,
                IsHomeworkSubmitted = false,
                IsReviewMode = false,
                ForceReviewVisible = false,
                IsQuantitative = isQuantitative
            };

            return View("Start", vm);
        }


        [HttpGet]
        public async Task<IActionResult> Report(int homeworkSetId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // تحليل الواجب
            var analytics = await _homeworkAnalyticsService.AnalyzeHomeworkAsync(studentId.Value, homeworkSetId);

            // جلب بيانات الطالب والدفعة
            var student = await _context.Students
                .Include(s => s.BatchEnrollments)
                .ThenInclude(e => e.Batch)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            // تحديد اسم الدفعة
            var batchName = student?.BatchEnrollments
                .Select(e => e.Batch.Name)
                .FirstOrDefault() ?? "غير محددة";

            // تجهيز الـ ViewModel للعرض
            analytics.BatchName = batchName;
            analytics.StudentName = student?.FullName ?? "الطالب";

            // التأكد من أن الوقت محسوب بشكل صحيح
            if (analytics.TimeSpentMinutes == 0)
            {
                var totalSeconds = analytics.Questions.Sum(q => (double)(q.TimeTakenSeconds ?? 0));
                analytics.TimeSpentMinutes = Math.Round(totalSeconds / 60.0, 1);
            }

            return View("HomeworkReportUnified", analytics);
        }


        [HttpGet]
        public async Task<IActionResult> HomeworkReportUnified(int homeworkSetId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // =====================================================
            // 1️⃣ النتيجة العامة للواجب
            // =====================================================
            var summary = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.HomeworkSetId == homeworkSetId &&
                    x.IsSubmitted)
                .Select(x => new
                {
                    x.Score
                })
                .FirstOrDefaultAsync();

            if (summary == null)
                return RedirectToAction("ErrorMessage", new { msg = "⚠️ لم يتم العثور على نتيجة هذا الواجب." });

            // =====================================================
            // 2️⃣ بيانات الطالب
            // =====================================================
            var studentInfo = await _context.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new
                {
                    StudentName = s.FullName,
                    BatchName = s.BatchEnrollments
                        .Select(e => e.Batch.Name)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            // =====================================================
            // 3️⃣ جلب آخر محاولة لكل سؤال
            // =====================================================
            var rawAttempts = await (
                from a in _context.QuestionAttemptNew.AsNoTracking()
                join q in _context.Questions.AsNoTracking()
                    on a.QuestionId equals q.Id
                where a.StudentId == studentId
                      && a.HomeworkSetId == homeworkSetId
                select new
                {
                    a.QuestionId,
                    a.AttemptedAt,
                    a.SelectedAnswer,
                    a.IsCorrect,
                    a.TimeTakenSeconds,
                    q.LessonId,
                    QuestionTitle = q.Title,
                    CorrectAnswer = q.CorrectAnswer
                }
            ).ToListAsync();

            var questions = rawAttempts
                .GroupBy(x => x.QuestionId)
                .Select(g =>
                {
                    var last = g.OrderByDescending(x => x.AttemptedAt).First();

                    return new HomeworkQuestionAnalyticsVm
                    {
                        QuestionId = g.Key,
                        QuestionTitle = last.QuestionTitle,
                        StudentAnswer = last.SelectedAnswer,
                        CorrectAnswer = last.CorrectAnswer,
                        IsCorrect = last.IsCorrect,
                        TimeTakenSeconds = last.TimeTakenSeconds
                    };
                })
                .ToList();

            // =====================================================
            // 4️⃣ حساب الوقت
            // =====================================================
            var totalSeconds = questions.Sum(q => q.TimeTakenSeconds);
            var timeSpentMinutes = totalSeconds > 0
                ? Math.Round(totalSeconds / 60.0, 1)
                : 0;

            // =====================================================
            // 5️⃣ بناء ViewModel الأساسي
            // =====================================================
            var vm = new HomeworkAnalyticsViewModel
            {
                HomeworkSetId = homeworkSetId,
                StudentName = studentInfo?.StudentName ?? "الطالب",
                BatchName = studentInfo?.BatchName ?? "غير محددة",
                Questions = questions,
                ScorePercentage = summary.Score ?? 0,
                TimeSpentMinutes = timeSpentMinutes
            };

            // =====================================================
            // 6️⃣ تحليل الأداء حسب المحاور (SectionsPerformance)
            // =====================================================
            var sectionRaw = await (
                from h in _context.Homeworks.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on h.QuestionId equals q.Id
                join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in _context.Sections.AsNoTracking() on l.SectionId equals s.Id
                where h.StudentId == studentId
                      && h.HomeworkSetId == homeworkSetId
                select new
                {
                    h.QuestionId,
                    s.Id,
                    s.Title
                }
            ).ToListAsync();

            vm.SectionsPerformance = sectionRaw
                .GroupBy(x => new { x.Id, x.Title })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(x =>
                        questions.Any(q => q.QuestionId == x.QuestionId && q.IsCorrect));

                    return new ViewModels.Homework.SectionPerformanceVm
                    {
                        SectionId = g.Key.Id,
                        SectionTitle = g.Key.Title,
                        Accuracy = total > 0
                            ? Math.Round(correct * 100.0 / total, 1)
                            : 0
                    };
                })
                .ToList();

            vm.BestSection = vm.SectionsPerformance
                .OrderByDescending(x => x.Accuracy)
                .Select(x => x.SectionTitle)
                .FirstOrDefault();

            // =====================================================
            // 7️⃣ تحليل المؤشرات (LessonsPerformance)
            // =====================================================
            var lessonIds = rawAttempts.Select(x => x.LessonId).Distinct().ToList();
            var lessonTitles = await _context.Lessons
                .AsNoTracking()
                .Where(l => lessonIds.Contains(l.Id))
                .Select(l => new { l.Id, l.Title })
                .ToDictionaryAsync(l => l.Id, l => l.Title);

            vm.LessonsPerformance = rawAttempts
        .GroupBy(x => x.LessonId)
        .Select(g =>
        {
            var totalQuestions = g
                .Select(x => x.QuestionId)
                .Distinct()
                .Count();

            var correct = g.Count(x => x.IsCorrect);

            var totalSecondsLesson = g.Sum(x => x.TimeTakenSeconds);

            return new ViewModels.Homework.LessonPerformanceVm
            {
                LessonId = g.Key,
                LessonTitle = lessonTitles.TryGetValue(g.Key, out var lessonTitle) ? lessonTitle : $"مؤشر {g.Key}",
                TotalQuestions = totalQuestions,
                SuccessRate = totalQuestions > 0
                    ? Math.Round(correct * 100.0 / totalQuestions, 1)
                    : 0,

                // ✅ الزمن لكل مؤشر
                TimeSpentMinutes = totalSecondsLesson > 0
                    ? Math.Round(totalSecondsLesson / 60.0, 1)
                    : 0
            };
        })
        .ToList();


            // =====================================================
            // 8️⃣ التوصيات
            // =====================================================
            var recommendationInput = new HomeworkAnalyticsVm
            {
                ScorePercentage = vm.ScorePercentage,
                TimeSpentMinutes = vm.TimeSpentMinutes,
                QuestionsCount = vm.TotalQuestions,
                CorrectAnswers = vm.CorrectCount
            };

            vm.Recommendation =
                _homeworkRecommendationService.GenerateRecommendation(recommendationInput);

            return View("HomeworkReportUnified", vm);
        }



        [HttpGet]
        public async Task<IActionResult> ReviewLesson(int homeworkSetId, int lessonId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🧭 جلب بيانات الواجب
            var homeworkSet = await _context.HomeworkSets
                .Include(hs => hs.Batch)
                .FirstOrDefaultAsync(hs => hs.Id == homeworkSetId);

            
            if (homeworkSet == null)
            {
                return RedirectToAction("ErrorMessage", new { msg = "⚠️ لا توجد أسئلة لهذا الواجب." });
            }
            // ✅ جلب المحاولات الخاصة بالمؤشر فقط
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Options)
                .Include(a => a.Question.VerbalPassage)
                .Where(a =>
                    a.StudentId == studentId &&
                    a.HomeworkSetId == homeworkSetId &&
                    a.Question.LessonId == lessonId)
                .AsNoTracking()
                .ToListAsync();



            if (!attempts.Any())
            {
                return RedirectToAction("ErrorMessage", new { msg = "⚠️ لا توجد أسئلة لهذا المؤشر داخل الواجب." });
            }
            // ✅ الوقت الفعلي المحسوب من أول محاولة لآخر محاولة
            DateTime? startedAt = attempts.Min(a => a.AttemptedAt);
            DateTime? finishedAt = attempts.Max(a => a.AttemptedAt);

            int actualMinutesSpent = 0;
            string formattedTime = "غير محدد";
            string explanation = "";

            if (startedAt.HasValue && finishedAt.HasValue)
            {
                actualMinutesSpent = (int)Math.Round((finishedAt.Value - startedAt.Value).TotalMinutes);
                if (actualMinutesSpent <= 0)
                    actualMinutesSpent = 1;

                formattedTime = $"{actualMinutesSpent} دقيقة";
                explanation = $"⏱ الوقت المستغرق فعليًا من {startedAt:HH:mm} حتى {finishedAt:HH:mm}";
            }
            else
            {
                // fallback باستخدام TimeTakenSeconds
                double totalSeconds = attempts.Sum(a => a.TimeTakenSeconds);
                int minutes = (int)(totalSeconds / 60);
                formattedTime = $"{minutes} دقيقة (تقديري)";
                explanation = "⏱ لم يتم تسجيل زمن البداية والنهاية، تم حساب الزمن تقديريًا.";
            }

            // ✅ تجهيز الأسئلة الخاصة بالمؤشر
            var questionVms = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .Select(x => new HomeworkReviewQuestionVm
                {
                    QuestionId = x.Question.Id,
                    QuestionText = x.Question.Title,
                    StudentAnswer = x.SelectedAnswer,
                    CorrectAnswer = x.Question.CorrectAnswer,
                    IsCorrect = x.IsCorrect,
                    TimeTakenSeconds = x.TimeTakenSeconds,

                    ImageUrl = x.Question.ImageUrl,
                    IsQuantitative = x.Question.IsQuantitative,
                    ComparisonValue1 = x.Question.ValueA,
                    ComparisonValue2 = x.Question.ValueB,
                    VerbalPassageContent = x.Question.VerbalPassage != null ? x.Question.VerbalPassage.Content : null,

                    DisplayType = x.Question.Template switch
                    {
                        QuestionTemplate.CompareValues => Enums.QuestionDisplayType.ComparisonText,
                        QuestionTemplate.CompareWithImage => Enums.QuestionDisplayType.ComparisonWithImage,
                        _ => Enums.QuestionDisplayType.WithImage
                    },

                    Options = x.Question.Options.Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                })
                .ToList();

            // ✅ جلب اسم المؤشر
            var lessonTitle = await _context.Lessons
                .Where(l => l.Id == lessonId)
                .Select(l => l.Title)
                .FirstOrDefaultAsync() ?? "مؤشر غير معروف";

            var vm = new HomeworkReviewViewModel
            {
                HomeworkSetId = homeworkSetId,
                TotalQuestions = questionVms.Count,
                CorrectAnswers = questionVms.Count(q => q.IsCorrect),
                WrongAnswers = questionVms.Count(q => !q.IsCorrect),

                // ✅ الوقت الكلي
                TimeSpentMinutes = actualMinutesSpent,
                TimeSpentFormatted = formattedTime, // 🔹 أضف هذا السطر لعرض الوقت في الصفحة
                TimeExplanation = explanation,

                Questions = questionVms.Select(q => new HomeworkQuestionReviewItems
                {
                    QuestionId = q.QuestionId,
                    QuestionText = q.QuestionText,
                    ImageUrl = q.ImageUrl,
                    IsCorrect = q.IsCorrect,
                    IsQuantitative = q.IsQuantitative,
                    StudentAnswer = q.StudentAnswer,
                    CorrectAnswer = q.CorrectAnswer,
                    TimeTakenSeconds = q.TimeTakenSeconds,
                    VerbalPassageContent = q.VerbalPassageContent,
                    ComparisonValue1 = q.ComparisonValue1,
                    ComparisonValue2 = q.ComparisonValue2,
                    DisplayType = q.DisplayType,
                    Options = q.Options.Select(o => new HomeworkOptionReviewItem
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                }).ToList()
            };


            return View("Review", vm); // 👈 نفس صفحة العرض الأصلية
        }




        [HttpPost]
        public async Task<IActionResult> RefreshRank(int batchId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
          
            if (studentId == null)
            {
                return RedirectToAction("ErrorMessage", new { msg = "⚠️ لم يتم العثور على الطالب " });
            }
            await _studentRankingService.RecordStudentRankAsync(studentId.Value, batchId);

            var lastRank = await _context.StudentRankHistories
                .Where(r => r.StudentId == studentId && r.BatchId == batchId)
                .OrderByDescending(r => r.RecordedAt)
                .FirstOrDefaultAsync();

            return Json(new
            {
                success = true,
                rank = lastRank?.Rank ?? 0,
                total = lastRank?.TotalStudents ?? 0,
                updated = lastRank?.RecordedAt.ToString("yyyy-MM-dd HH:mm")
            });
        }



        [HttpPost]
        public async Task<IActionResult> SubmitAnswer(int id, Guid q, string? SelectedOption, string nav, string AllIds, int? timeTakenSeconds = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var allIds = AllIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                               .Where(g => g != Guid.Empty)
                               .ToList();

            var currentIndex = allIds.IndexOf(q);
            if (currentIndex == -1)
                return RedirectToAction("Start", new { id });

            var question = await _context.Questions
                .FirstOrDefaultAsync(qs => qs.Id == q);

            if (question == null)
                return RedirectToAction("Start", new { id });

            try
            {
                var isCorrect = SelectedOption == question.CorrectAnswer;

                var attempt = await _context.QuestionAttemptNew
                    .FirstOrDefaultAsync(a => a.StudentId == studentId && a.QuestionId == q);

                if (attempt == null)
                {
                    attempt = new QuestionAttemptNew
                    {
                        StudentId = studentId.Value,
                        HomeworkSetId = id,   // ✅ أضف هذا السطر
                        QuestionId = q,
                        AttemptedAt = DateTime.Now,
                        SelectedAnswer = SelectedOption ?? "",
                        IsCorrect = isCorrect,
                        LessonId = question.LessonId,
                        SectionId = question.SectionId,
                        TimeTakenSeconds = timeTakenSeconds ?? 0
                    };

                    _context.QuestionAttemptNew.Add(attempt);
                }
                else
                {
                    attempt.SelectedAnswer = SelectedOption ?? "";
                    attempt.IsCorrect = isCorrect;
                    attempt.AttemptedAt = DateTime.Now;
                    attempt.TimeTakenSeconds = timeTakenSeconds ?? attempt.TimeTakenSeconds;

                }

                // وضع علامة للمراجعة
                if (nav == "review")
                {
                    var reviewList = TempData["ReviewMarked"] as string ?? "";
                    var ids = new HashSet<string>(reviewList.Split(',', StringSplitOptions.RemoveEmptyEntries));
                    ids.Add(q.ToString());
                    TempData["ReviewMarked"] = string.Join(",", ids);
                }

                // ✅ تسجيل النشاط
                var existingLog = await _context.StudentActivityLogs
                    .FirstOrDefaultAsync(l => l.StudentId == studentId && l.QuestionId == q);

                if (existingLog == null)
                {
                    var activity = new StudentActivityLog
                    {
                        StudentId = studentId.Value,
                        ActivityType = "Homework",
                        ActivityTitle = "حل سؤال: " + question.Title,
                        Timestamp = DateTime.Now,
                        WasCorrect = isCorrect,
                        Source = "Homework",
                        QuestionId = question.Id,
                        LessonId = question.LessonId,
                        SectionId = question.SectionId
                    };

                    _context.StudentActivityLogs.Add(activity);
                }

                await _context.SaveChangesAsync();
                await _analyticsHelper.SafeRecordAnalyticsAsync(studentId.Value, q, isCorrect);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "⚠️ الخطأ: " + ex.GetType().Name + " - " + ex.Message;
                return RedirectToAction("Start", new { id, q });
            }

            // التنقل بين الأسئلة
            if (nav == "next" && currentIndex < allIds.Count - 1)
                return RedirectToAction("Start", new { id, q = allIds[currentIndex + 1] });

            if (nav == "prev" && currentIndex > 0)
                return RedirectToAction("Start", new { id, q = allIds[currentIndex - 1] });

            return RedirectToAction("Start", new { id, q });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitAnswerFetch(
          int id,
          Guid q,
          string nav,
          string? SelectedOption,
          int? timeTakenSeconds = null)
        {
            using var _context = _contextFactory.CreateDbContext();
            HttpContext.RequestAborted.ThrowIfCancellationRequested();

            try
            {
                var studentId = await GetCurrentStudentIdAsync();
                if (studentId == null)
                    return Json(new { success = false, message = "❌ لم يتم التعرف على الطالب. الرجاء تسجيل الدخول." });

                // ✅ تحميل الأسئلة من Session (كما هو)
                var allQuestions = await GetHomeworkQuestionIdsAsync(id, _context);

                if (allQuestions == null || !allQuestions.Any())
                    return RedirectToAction("ErrorMessage", new { msg = "⚠️ لا توجد أسئلة مرتبطة بهذا الواجب." });

                // ======================================================
                // ✅ (1) حفظ المحاولة (إجابة أو زمن فقط)
                // ======================================================

                // 🟢 جلب آخر محاولة لنفس السؤال داخل نفس الواجب
                var existingAttempt = await _context.QuestionAttemptNew
                    .OrderByDescending(a => a.AttemptedAt)
                    .FirstOrDefaultAsync(a =>
                        a.StudentId == studentId &&
                        a.HomeworkSetId == id &&
                        a.QuestionId == q);

                bool hasAnswer = !string.IsNullOrWhiteSpace(SelectedOption);

                // 🟩 الحالة الأولى: الطالب اختار إجابة
                if (hasAnswer)
                {
                    var question = await _context.Questions
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
                                StudentId = studentId.Value,
                                HomeworkSetId = id,
                                QuestionId = q,
                                SelectedAnswer = SelectedOption,
                                IsCorrect = isCorrect,
                                TimeTakenSeconds = timeTakenSeconds ?? 0,
                                IsMarkedForReview = nav == "review"
                            });
                    }
                }
                // 🟨 الحالة الثانية: لا توجد إجابة لكن يوجد زمن (تنقل / مراجعة)
                else if (existingAttempt == null && timeTakenSeconds.HasValue && timeTakenSeconds > 0)
                {
                    await _homeworkWriteService.SaveQuestionAttemptAsync(
                        new HomeworkQuestionAttemptInput
                        {
                            StudentId = studentId.Value,
                            HomeworkSetId = id,
                            QuestionId = q,
                            SelectedAnswer = string.Empty,
                            IsCorrect = false,
                            TimeTakenSeconds = timeTakenSeconds.Value,
                            IsMarkedForReview = false
                        });
                }


                // ======================================================
                // ✅ (2) المراجعة — بدون أي تعديل
                // ======================================================
                if (nav == "review")
                {
                    var reviewKey = $"ReviewMarked_{id}_{studentId}";
                    var existing = HttpContext.Session.GetString(reviewKey);
                    var list = string.IsNullOrEmpty(existing)
                        ? new List<Guid>()
                        : JsonSerializer.Deserialize<List<Guid>>(existing)!;

                    if (!list.Contains(q))
                        list.Add(q);

                    HttpContext.Session.SetString(reviewKey, JsonSerializer.Serialize(list));
                }

                // ======================================================
                // ✅ (3) القفز من المراجعة — كما هو
                // ======================================================
                if (nav == "goto" || nav == "jump")
                {
                    var targetStr = HttpContext.Request.Form["target"].FirstOrDefault();
                    if (!Guid.TryParse(targetStr, out var targetQuestion) || targetQuestion == Guid.Empty)
                        return Json(new { success = false, message = "⚠️ لم يتم العثور على السؤال المطلوب." });

                    if (!allQuestions.Contains(targetQuestion))
                        return Json(new { success = false, message = "⚠️ السؤال المحدد غير موجود ضمن هذا الواجب." });

                    var jumpVm = await GetSolveViewModel(id, targetQuestion, studentId.Value, _context);
                    jumpVm.IsReviewMode = true;
                    jumpVm.ForceReviewVisible = true;

                    return PartialView("_SolveHomeworkQuestionPartial", jumpVm);
                }

                // ======================================================
                // ✅ (4) عرض المراجعة — كما هو
                // ======================================================
                if (nav == "showReview")
                {
                    var reviewVm = await GetSolveViewModel(id, q, studentId.Value, _context);
                    reviewVm.IsReviewMode = true;
                    reviewVm.ForceReviewVisible = true;
                    return PartialView("_SolveHomeworkQuestionPartial", reviewVm);
                }

                // ======================================================
                // ✅ (5) التنقل — كما هو
                // ======================================================
                int currentIndex = allQuestions.FindIndex(x => x == q);
                int nextIndex = currentIndex;

                if (nav == "next" && currentIndex < allQuestions.Count - 1)
                    nextIndex++;
                else if (nav == "prev" && currentIndex > 0)
                    nextIndex--;

                var nextQuestionId = allQuestions[nextIndex];

                var nextVm = await GetSolveViewModel(id, nextQuestionId, studentId.Value, _context);
                nextVm.IsReviewMode = false;
                nextVm.ForceReviewVisible = false;

                return PartialView("_SolveHomeworkQuestionPartial", nextVm);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SubmitAnswerFetch: {ex.Message}");
                return Json(new { success = false, message = "⚠️ حدث خطأ أثناء حفظ الإجابة أو التنقل." });
            }
        }


        private async Task<List<Guid>> GetHomeworkQuestionIdsAsync(int homeworkSetId, ApplicationDbContext _context)
        {
            string cacheKey = $"HomeworkQuestions_{homeworkSetId}_{await GetCurrentStudentIdAsync()}";

            if (HttpContext.Session.TryGetValue(cacheKey, out var bytes))
            {
                var cachedList = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(bytes);
                if (cachedList != null && cachedList.Any())
                {
                    Console.WriteLine($"[CACHE] Loaded {cachedList.Count} question IDs for Homework {homeworkSetId}");
                    return cachedList;
                }
            }

            // 🟢 تحميل من قاعدة البيانات عند أول مرة فقط
            var ids = await _context.Homeworks
                .AsNoTracking()
                .Where(h => h.HomeworkSetId == homeworkSetId)
                .OrderBy(h => h.Id)
                .Select(h => h.QuestionId)
                .ToListAsync();

            // 🟢 حفظها في الكاش
            var serialized = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(ids);
            HttpContext.Session.Set(cacheKey, serialized);

            Console.WriteLine($"[CACHE] Stored {ids.Count} question IDs for Homework {homeworkSetId}");
            return ids;
        }


        private async Task SaveStudentAnswerAsync(
      ApplicationDbContext _context,
      int homeworkSetId,
      Guid questionId,
      int studentId,
      string selectedAnswer)
        {
            // 🟢 اجلب السؤال لتحديد الإجابة الصحيحة
            var question = await _context.Questions
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                return;

            bool isCorrect = string.Equals(
                selectedAnswer?.Trim(),
                question.CorrectAnswer?.Trim(),
                StringComparison.OrdinalIgnoreCase);

            var attempt = await _context.QuestionAttemptNew
                .FirstOrDefaultAsync(a =>
                    a.StudentId == studentId &&
                    a.QuestionId == questionId &&
                    a.HomeworkSetId == homeworkSetId);

            if (attempt == null)
            {
                attempt = new QuestionAttemptNew
                {
                    StudentId = studentId,
                    QuestionId = questionId,
                    HomeworkSetId = homeworkSetId,
                    SelectedAnswer = selectedAnswer,
                    IsCorrect = isCorrect,
                    LessonId = question.LessonId,
                    SectionId = question.SectionId,
                    AttemptedAt = DateTime.Now
                };
                _context.QuestionAttemptNew.Add(attempt);
            }
            else
            {
                attempt.SelectedAnswer = selectedAnswer;
                attempt.IsCorrect = isCorrect;
                attempt.AttemptedAt = DateTime.Now;
                _context.QuestionAttemptNew.Update(attempt);
            }

            await _context.SaveChangesAsync();

            Console.WriteLine($"[ANSWER] Saved answer for Q:{questionId} | S:{studentId} | Correct:{isCorrect}");
        }




        [HttpPost]
        public async Task<IActionResult> SubmitFinal(int id, bool forceSubmit = false)
        {
            using var _context = _contextFactory.CreateDbContext();

            try
            {
                Response.ContentType = "application/json";

                var studentId = await GetCurrentStudentIdAsync();
                if (studentId == null)
                    return Json(new { success = false, message = "لم يتم التعرف على الطالب." });

                // ======================================================
                // 1) تأكيد وجود أسئلة للواجب
                // ======================================================
                var questionIds = await _context.Homeworks
                    .Where(h => h.HomeworkSetId == id && h.StudentId == studentId)
                    .Select(h => h.QuestionId)
                    .ToListAsync();

                if (!questionIds.Any())
                    return Json(new { success = false, message = "لا توجد أسئلة لهذا الواجب." });

                // ======================================================
                // 2) 🔴 تأكيد وجود HomeworkSetStudent (جذور المشكلة)
                // ======================================================
                var hss = await _context.HomeworkSetStudents
                    .FirstOrDefaultAsync(x =>
                        x.HomeworkSetId == id &&
                        x.StudentId == studentId.Value);

                if (hss == null)
                {
                    hss = new HomeworkSetStudent
                    {
                        HomeworkSetId = id,
                        StudentId = studentId.Value,
                        IsSubmitted = false,
                        AssignedAt = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow
                    };

                    _context.HomeworkSetStudents.Add(hss);
                    await _context.SaveChangesAsync();
                }

                // ======================================================
                // 3) تسليم الواجب (بدون اشتراط إجابة كل الأسئلة)
                // ======================================================
                await _homeworkWriteService.SubmitHomeworkAsync(id, studentId.Value);

                // ======================================================
                // 4) تسجيل التقدم
                // ======================================================
                await _studentProgressService.RecordHomeworkProgress(studentId.Value, id);

                // ======================================================
                // 5) Redirect للنتيجة
                // ======================================================
                var redirectUrl = Url.Action(
                    "HomeworkReportUnified",
                    "StudentHomeworkDashboard",
                    new { area = "Students", homeworkSetId = id });

                return Json(new
                {
                    success = true,
                    redirectUrl
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }


        [HttpGet]
        public async Task<IActionResult> AllHomeworks()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🟢 جميع الواجبات الخاصة بدفعات الطالب
            var allSets = await (
                from hs in _context.HomeworkSets.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on hs.BatchId equals b.Id
                join sbe in _context.StudentBatchEnrollments.AsNoTracking() on b.Id equals sbe.BatchId
                where sbe.StudentID == studentId
                select new
                {
                    hs.Id,
                    hs.CompletionTitle,
                    hs.StartAt,
                    hs.EndAt,
                    BatchName = b.Name,
                    BatchId = b.Id   // ✅ التصحيح هنا
                }
            )
            .OrderByDescending(hs => hs.StartAt)
            .ToListAsync();

            // 🟢 درجات الطالب من جدول HomeworkSetStudents
            var studentScores = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                where hss.StudentId == studentId && hss.IsSubmitted
                select new
                {
                    hss.HomeworkSetId,
                    Score = hss.Score ?? 0
                }
            ).ToListAsync();

            // 🟢 حساب متوسط الدفعة لكل واجب
            var batchAverages = await (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking() on hss.HomeworkSetId equals hs.Id
                join b in _context.Batches.AsNoTracking() on hs.BatchId equals b.Id
                where hss.IsSubmitted
                group hss by new { hss.HomeworkSetId, b.Id } into g
                select new
                {
                    HomeworkSetId = g.Key.HomeworkSetId,
                    BatchId = g.Key.Id,
                    AvgScore = g.Average(x => x.Score ?? 0)
                }
            ).ToListAsync();

            // 🟢 بناء القائمة
            var vm = allSets.Select(x =>
            {
                var studentScore = studentScores.FirstOrDefault(s => s.HomeworkSetId == x.Id)?.Score ?? 0;
                var batchAvg = batchAverages.FirstOrDefault(a => a.HomeworkSetId == x.Id && a.BatchId == x.BatchId)?.AvgScore ?? 0;

                return new HomeworkListVm
                {
                    HomeworkSetId = x.Id,
                    Title = x.CompletionTitle ?? "واجب",
                    CreatedAt = x.StartAt ?? DateTime.Now,
                    SectionName = x.BatchName,
                    IsSubmitted = studentScore > 0,
                    DelayLevel = studentScore > 0
                        ? "تم الحل"
                        : (x.EndAt.HasValue && x.EndAt.Value < DateTime.Now ? "متأخر" : "مطلوب"),
                    Score = Math.Round(studentScore, 1),
                    BatchAverageScore = Math.Round(batchAvg, 1)
                };
            }).ToList();

            // 🟢 حساب الإحصائيات العامة
            double avgScore = vm.Any(v => v.IsSubmitted)
                ? vm.Where(v => v.IsSubmitted).Average(v => v.Score)
                : 0;

            double weightedScore = vm.Any(v => v.IsSubmitted)
                ? vm.Where(v => v.IsSubmitted && v.BatchAverageScore > 0)
                    .Average(v => (v.Score / v.BatchAverageScore) * 100)
                : 0;

            // 🟢 تمرير القيم للواجهة
            ViewBag.TotalHomeworks = vm.Count;
            ViewBag.AverageScore = Math.Round(avgScore, 2);
            ViewBag.WeightedScore = Math.Round(weightedScore, 2);

            return View(vm);
        }


        private string GetDelayLevelFromWindow(DateTime startAt, DateTime? endAt)
        {
            var now = DateTime.Now;
            if (endAt.HasValue && now > endAt.Value)
                return "متأخر جدًا";

            var days = (now - startAt).Days;
            if (days < 1) return "لم يبدأ بعد";
            if (days < 2) return "قريب";
            if (days < 4) return "متوسط";
            return "متأخر";
        }



        private async Task<HomeworkReportViewModel> BuildReportViewModel(int homeworkSetId, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 بيانات الواجب العامة
            var header = await (
                from hs in _context.HomeworkSets
                join b in _context.Batches on hs.BatchId equals b.Id
                join c in _context.Curriculums on hs.CurriculumId equals c.Id into cc
                from curriculum in cc.DefaultIfEmpty()
                join s in _context.Students on studentId equals s.StudentID
                where hs.Id == homeworkSetId
                select new
                {
                    hs.Id,
                    hs.Title,
                    hs.CreatedAt,
                    BatchName = b.Name,
                    CurriculumTitle = curriculum != null ? curriculum.Title : "-",
                    StudentName = s.FullName
                }
            ).FirstOrDefaultAsync();

            if (header == null) return null;

            // 🟢 الأسئلة الخاصة بالواجب
            var homeworkQuestions = await (
                from h in _context.Homeworks
                join q in _context.Questions on h.QuestionId equals q.Id
                where h.HomeworkSetId == homeworkSetId && h.StudentId == studentId
                select new HomeworkReportQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionText = q.Title,
                    StudentAnswer = h.StudentAnswer,
                    IsCorrect = h.IsCorrect ?? false
                }
            ).ToListAsync();

            int total = homeworkQuestions.Count;

            // 🟢 آخر محاولة لكل سؤال (داخل الواجب)
            var lastAttempts = await (
                from a in _context.QuestionAttemptNew
                where a.StudentId == studentId && (a.HomeworkSetId == homeworkSetId || a.HomeworkSetId == null)
                join h in _context.Homeworks on new { a.StudentId, a.QuestionId } equals new { h.StudentId, h.QuestionId }
                where h.HomeworkSetId == homeworkSetId
                group a by a.QuestionId into g
                select g.OrderByDescending(x => x.AttemptedAt).FirstOrDefault()
            ).ToListAsync();

            // 🟢 تصحيح HomeworkSetId المفقود في المحاولات القديمة
            foreach (var att in lastAttempts.Where(a => a != null && a.HomeworkSetId == null))
                att.HomeworkSetId = homeworkSetId;

            await _context.SaveChangesAsync();

            // 🟢 حساب النتائج الأساسية
            int correct = lastAttempts.Count(a => a != null && a.IsCorrect);
            int wrong = total - correct;
            double percentage = total > 0 ? Math.Round((double)correct / total * 100, 2) : 0.0;

            // 🕒 حساب الوقت المستغرق
            double totalSeconds = lastAttempts.Where(a => a != null).Sum(a => (double)a.TimeTakenSeconds);
            int minutes = (int)Math.Round(totalSeconds / 60.0);
            double avgTimePerQuestion = total > 0 ? Math.Round(totalSeconds / total, 2) : 0;

            // 🧮 ترتيب الطالب
            var studentScores = await (
                from h in _context.Homeworks
                join a in _context.QuestionAttemptNew
                    on new { h.StudentId, h.QuestionId } equals new { a.StudentId, a.QuestionId }
                where h.HomeworkSetId == homeworkSetId
                group a by a.StudentId into g
                select new
                {
                    StudentId = g.Key,
                    Score = g.Count() > 0 ? (double)g.Count(x => x.IsCorrect) / g.Count() * 100 : 0.0
                }
            ).OrderByDescending(x => x.Score).ToListAsync();

            int rank = studentScores.FindIndex(x => x.StudentId == studentId) + 1;
            int totalStudents = studentScores.Count;

            // 🟢 أداء حسب المحاور
            var sectionScoresRaw = await (
                from h in _context.Homeworks
                join q in _context.Questions on h.QuestionId equals q.Id
                join a in _context.QuestionAttemptNew
                    on new { h.StudentId, h.QuestionId } equals new { a.StudentId, a.QuestionId }
                where h.HomeworkSetId == homeworkSetId && h.StudentId == studentId
                group a by q.SectionId into g
                select new
                {
                    SectionId = g.Key,
                    Accuracy = g.Count() > 0
                        ? Math.Round((double)g.Count(x => x.IsCorrect) * 100.0 / g.Count(), 2)
                        : 0.0
                }
            ).ToListAsync();

            var sectionScoreSectionIds = sectionScoresRaw
                .Where(x => x.SectionId != null)
                .Select(x => x.SectionId.Value)
                .Distinct()
                .ToList();

            var sectionScoreTitles = await _context.Sections
                .AsNoTracking()
                .Where(s => sectionScoreSectionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Title })
                .ToDictionaryAsync(s => s.Id, s => s.Title);

            var sectionScoresList = sectionScoresRaw
                .Select(x => new
                {
                    x.SectionId,
                    SectionName = x.SectionId != null && sectionScoreTitles.TryGetValue(x.SectionId.Value, out var title) ? title : null,
                    x.Accuracy
                })
                .ToList();

            var sectionScores = sectionScoresList.ToDictionary(x => x.SectionName, x => x.Accuracy);

            var bestSection = sectionScoresList.OrderByDescending(x => x.Accuracy).FirstOrDefault();
            var worstSection = sectionScoresList.OrderBy(x => x.Accuracy).FirstOrDefault();

            // 🟢 أسرع وأبطأ سؤال
            var allQuestionsOrdered = await _context.Homeworks
                .Where(h => h.HomeworkSetId == homeworkSetId && h.StudentId == studentId)
                .OrderBy(h => h.Id)
                .Select(h => h.QuestionId)
                .ToListAsync();

            var fastestQuestion = lastAttempts
                .Where(a => a != null && a.TimeTakenSeconds > 0)
                .OrderBy(a => a.TimeTakenSeconds)
                .FirstOrDefault();

            var slowestQuestion = lastAttempts
                .Where(a => a != null && a.TimeTakenSeconds > 0)
                .OrderByDescending(a => a.TimeTakenSeconds)
                .FirstOrDefault();

            var fastestQuestionIndex = fastestQuestion != null
                ? allQuestionsOrdered.FindIndex(q => q == fastestQuestion.QuestionId) + 1
                : (int?)null;

            var slowestQuestionIndex = slowestQuestion != null
                ? allQuestionsOrdered.FindIndex(q => q == slowestQuestion.QuestionId) + 1
                : (int?)null;

            string fastestText = fastestQuestion != null
                ? homeworkQuestions.FirstOrDefault(q => q.QuestionId == fastestQuestion.QuestionId)?.QuestionText
                : null;

            string slowestText = slowestQuestion != null
                ? homeworkQuestions.FirstOrDefault(q => q.QuestionId == slowestQuestion.QuestionId)?.QuestionText
                : null;

            // 🟢 بناء الـ ViewModel النهائي
            return new HomeworkReportViewModel
            {
                HomeworkSetId = header.Id,
                Title = header.Title,
                CreatedAt = header.CreatedAt,
                BatchName = header.BatchName,
                CurriculumTitle = header.CurriculumTitle,
                StudentName = header.StudentName,

                CorrectAnswers = correct,
                WrongAnswers = wrong,
                Percentage = percentage,
                TimeSpentMinutes = minutes,             // ⏱️ الوقت الكلي بالدقائق
                AverageSecondsPerQuestion = avgTimePerQuestion, // ⏱️ متوسط الزمن للسؤال الواحد
                Questions = homeworkQuestions,
                Rank = rank,
                TotalStudents = totalStudents,
                SectionScores = sectionScores,

                BestSection = bestSection?.SectionName,
                BestSectionScore = bestSection?.Accuracy ?? 0,
                WorstSection = worstSection?.SectionName,
                WorstSectionScore = worstSection?.Accuracy ?? 0,

                FastestQuestionText = fastestText,
                FastestQuestionIndex = fastestQuestionIndex,
                FastestQuestionTime = fastestQuestion?.TimeTakenSeconds ?? 0,
                FastestQuestionCorrect = fastestQuestion?.IsCorrect ?? false,

                SlowestQuestionText = slowestText,
                SlowestQuestionIndex = slowestQuestionIndex,
                SlowestQuestionTime = slowestQuestion?.TimeTakenSeconds ?? 0,
                SlowestQuestionCorrect = slowestQuestion?.IsCorrect ?? false
            };
        }

        [HttpGet]
        public async Task<IActionResult> CurriculumReport(
            int? curriculumId = null,
            int? sectionId = null,
            int? lessonId = null,
            int? studentId = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (!studentId.HasValue)
            {
                studentId = await GetCurrentStudentIdAsync();
                if (!studentId.HasValue)
                    return RedirectToAction("Login", "Account", new { area = "" });
            }

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);
            if (student == null) return NotFound();

            var vm = new CurriculumReportViewModel
            {
                StudentId = student.StudentID,   // 🟢 مرر StudentId هنا
                StudentName = student.FullName,
                Curriculums = await _context.Curriculums
                    .Select(c => new CurriculumViewModel { Id = c.Id, Title = c.Title })
                    .ToListAsync(),
                AvailableSections = new List<string>(),
                AvailableLessons = new List<string>(),
                SectionScores = new Dictionary<string, double>()
            };

            if (curriculumId.HasValue)
            {
                // جلب المحاولات بناءً على الفلتر (منهج + محور + مؤشر)
                var attemptsQuery = _context.QuestionAttemptNew
                    .Include(a => a.Question)
                        .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                    .Where(a => a.StudentId == studentId &&
                                a.Question.Lesson.Section.CurriculumId == curriculumId);

                if (sectionId.HasValue)
                    attemptsQuery = attemptsQuery.Where(a => a.Question.SectionId == sectionId);

                if (lessonId.HasValue)
                    attemptsQuery = attemptsQuery.Where(a => a.Question.LessonId == lessonId);

                var attempts = await attemptsQuery.ToListAsync();

                // ✅ إجابات صحيحة/خاطئة/النسبة
                vm.CorrectAnswers = attempts.Count(a => a.IsCorrect);
                vm.WrongAnswers = attempts.Count(a => !a.IsCorrect);
                vm.Percentage = attempts.Any()
                    ? Math.Round((double)vm.CorrectAnswers / attempts.Count * 100, 2)
                    : 0;

                // ✅ الوقت المستغرق
                vm.TimeSpentMinutes = (int)Math.Round(attempts.Sum(a => a.TimeTakenSeconds) / 60.0);

                // ✅ توزيع حسب المحاور
                vm.SectionScores = attempts
                    .GroupBy(a => a.Question.Lesson.Section.Title)
                    .ToDictionary(
                        g => g.Key,
                        g => Math.Round((double)g.Count(x => x.IsCorrect) * 100 / g.Count(), 2)
                    );

                // ✅ قائمة الأسئلة
                vm.Questions = attempts.Select(a => new HomeworkReportQuestionVm
                {
                    QuestionId = a.QuestionId,
                    QuestionText = a.Question.Title,
                    StudentAnswer = a.SelectedAnswer,
                    IsCorrect = a.IsCorrect
                }).ToList();

                // ✅ ترتيب الطالب داخل دفعته
                var batchId = await _context.StudentBatchEnrollments
                    .Where(sbe => sbe.StudentID == studentId)
                    .Select(sbe => sbe.BatchId)
                    .FirstOrDefaultAsync();

                var studentScores = await (
                    from a in _context.QuestionAttemptNew
                    join q in _context.Questions on a.QuestionId equals q.Id
                    join l in _context.Lessons on q.LessonId equals l.Id
                    join s in _context.Sections on l.SectionId equals s.Id
                    where s.CurriculumId == curriculumId && a.StudentId != null
                    group a by a.StudentId into g
                    select new
                    {
                        StudentId = g.Key,
                        Score = g.Count() > 0 ? (double)g.Count(x => x.IsCorrect) / g.Count() * 100 : 0
                    }
                ).OrderByDescending(x => x.Score).ToListAsync();

                vm.Rank = studentScores.FindIndex(x => x.StudentId == studentId) + 1;
                vm.TotalStudents = studentScores.Count;

                // ✅ جلب المحاور المرتبطة بالمنهج
                vm.AvailableSections = await _context.Sections
                    .Where(s => s.CurriculumId == curriculumId)
                    .Select(s => s.Title)
                    .ToListAsync();

                // ✅ جلب المؤشرات الفعالة (دروس فيها محاولات واجبات فقط)
                if (sectionId.HasValue)
                {
                    vm.AvailableLessons = await (
                        from l in _context.Lessons
                        join q in _context.Questions on l.Id equals q.LessonId
                        join a in _context.QuestionAttemptNew on q.Id equals a.QuestionId
                        where l.SectionId == sectionId && a.StudentId == studentId
                        select l.Title
                    ).Distinct().ToListAsync();
                }
            }

            return View(vm);
        }



        [HttpGet]
        public async Task<IActionResult> GetLessonsBySection(int sectionId, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var lessons = await (
                from l in _context.Lessons
                join h in _context.Homeworks on l.Id equals h.LessonId
                where l.SectionId == sectionId && h.StudentId == studentId
                select l.Title
            ).Distinct().ToListAsync();

            return Json(lessons);
        }




        private async Task<CurriculumReportViewModel> BuildCurriculumReportViewModel(int curriculumId, int? sectionId, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 الأسئلة الخاصة بالطالب في هذا المنهج/المحور
            var homeworksQuery = _context.Homeworks
                .Include(h => h.Question)
                .Where(h => h.StudentId == studentId && h.HomeworkSet.CurriculumId == curriculumId);

            if (sectionId.HasValue)
                homeworksQuery = homeworksQuery.Where(h => h.Question.SectionId == sectionId);

            var homeworks = await homeworksQuery.ToListAsync();
            if (!homeworks.Any()) return null;

            // 🟢 آخر محاولة لكل سؤال
            var lastAttempts = await (
                from a in _context.QuestionAttemptNew
                where a.StudentId == studentId
                join h in homeworks on a.QuestionId equals h.QuestionId
                group a by a.QuestionId into g
                select g.OrderByDescending(x => x.AttemptedAt).FirstOrDefault()
            ).ToListAsync();

            int correct = lastAttempts.Count(a => a != null && a.IsCorrect);
            int wrong = lastAttempts.Count(a => a != null) - correct;
            double percentage = lastAttempts.Count > 0
                ? Math.Round((double)correct / lastAttempts.Count * 100, 2)
                : 0.0;

            double totalSecondsDouble = lastAttempts.Where(a => a != null).Sum(a => a.TimeTakenSeconds);
            int minutes = (int)Math.Round(totalSecondsDouble / 60);

            // 🟢 حساب الأداء حسب المحاور
            var attemptSectionIds = lastAttempts
                .Where(a => a != null && a.SectionId != null)
                .Select(a => a.SectionId.Value)
                .Distinct()
                .ToList();

            var sectionTitles = await _context.Sections
                .AsNoTracking()
                .Where(s => attemptSectionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Title })
                .ToDictionaryAsync(s => s.Id, s => s.Title);

            var sectionScoresList = lastAttempts
                .Where(a => a != null && a.SectionId != null)
                .GroupBy(a => a.SectionId)
                .Select(g => new
                {
                    SectionId = g.Key.Value,
                    SectionName = sectionTitles.TryGetValue(g.Key.Value, out var title) ? title : null,
                    Accuracy = g.Count() > 0
                        ? Math.Round((double)g.Count(x => x.IsCorrect) * 100.0 / g.Count(), 2)
                        : 0.0
                })
                .ToList();

            var sectionScores = sectionScoresList.ToDictionary(x => x.SectionName, x => x.Accuracy);

            var bestSection = sectionScoresList.OrderByDescending(x => x.Accuracy).FirstOrDefault();
            var worstSection = sectionScoresList.OrderBy(x => x.Accuracy).FirstOrDefault();

            // 🟢 أسرع وأبطأ سؤال
            var fastestQuestion = lastAttempts
                .Where(a => a != null && a.TimeTakenSeconds > 0)
                .OrderBy(a => a.TimeTakenSeconds)
                .FirstOrDefault();

            var slowestQuestion = lastAttempts
                .Where(a => a != null && a.TimeTakenSeconds > 0)
                .OrderByDescending(a => a.TimeTakenSeconds)
                .FirstOrDefault();

            string fastestText = fastestQuestion != null
                ? await _context.Questions.Where(q => q.Id == fastestQuestion.QuestionId).Select(q => q.Title).FirstOrDefaultAsync()
                : null;

            string slowestText = slowestQuestion != null
                ? await _context.Questions.Where(q => q.Id == slowestQuestion.QuestionId).Select(q => q.Title).FirstOrDefaultAsync()
                : null;

            // 🟢 قائمة المناهج للفلتر
            var curriculums = await (
                from cc in _context.CourseCurriculums
                join cur in _context.Curriculums on cc.CurriculumId equals cur.Id
                select new CurriculumViewModel { Id = cur.Id, Title = cur.Title }
            ).ToListAsync();

            // 🟢 المحاور المتاحة للفلتر
            var availableSections = await _context.Sections
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => s.Title)
                .ToListAsync();

            return new CurriculumReportViewModel
            {

                CurriculumId = curriculumId,
                CurriculumTitle = _context.Curriculums.FirstOrDefault(c => c.Id == curriculumId)?.Title,
                SectionTitle = sectionId.HasValue ? _context.Sections.FirstOrDefault(s => s.Id == sectionId)?.Title : null,
                StudentName = await _context.Students.Where(s => s.StudentID == studentId).Select(s => s.FullName).FirstOrDefaultAsync(),
                BatchName = await (
                    from s in _context.Students
                    join e in _context.StudentBatchEnrollments on s.StudentID equals e.StudentID
                    join b in _context.Batches on e.BatchId equals b.Id
                    where s.StudentID == studentId
                    select b.Name
                ).FirstOrDefaultAsync(),

                CorrectAnswers = correct,
                WrongAnswers = wrong,
                Percentage = percentage,
                TimeSpentMinutes = minutes,
                Questions = homeworks.Select(h => new HomeworkReportQuestionVm
                {
                    QuestionId = h.QuestionId,
                    QuestionText = h.Question.Title,
                    StudentAnswer = h.StudentAnswer,
                    IsCorrect = h.IsCorrect ?? false
                }).ToList(),

                Rank = 0, // ممكن نضيف ترتيب لو عايزين مقارنة مع باقي الطلاب
                TotalStudents = 0,

                SectionScores = sectionScores,

                BestSection = bestSection?.SectionName,
                BestSectionScore = bestSection?.Accuracy ?? 0,
                WorstSection = worstSection?.SectionName,
                WorstSectionScore = worstSection?.Accuracy ?? 0,

                FastestQuestionText = fastestText,
                FastestQuestionIndex = null,
                FastestQuestionTime = fastestQuestion?.TimeTakenSeconds ?? 0,
                FastestQuestionCorrect = fastestQuestion?.IsCorrect ?? false,

                SlowestQuestionText = slowestText,
                SlowestQuestionIndex = null,
                SlowestQuestionTime = slowestQuestion?.TimeTakenSeconds ?? 0,
                SlowestQuestionCorrect = slowestQuestion?.IsCorrect ?? false,

                Curriculums = curriculums,
                AvailableSections = availableSections
            };
        }




        public async Task<IActionResult> DownloadReportPdf(int homeworkSetId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var vm = await BuildReportViewModel(homeworkSetId, studentId.Value);
            if (vm == null) return NotFound();

            return new Rotativa.AspNetCore.ViewAsPdf("Report", vm)
            {
                FileName = $"HomeworkReport_{homeworkSetId}.pdf",
                PageSize = Rotativa.AspNetCore.Options.Size.A4
            };
        }


        // ================================================================
        // 🔒 Security Endpoints — منع التلاعب وحماية الجلسة
        // ================================================================

        /// <summary>
        /// POST: تسجيل بداية جلسة الحل + قفل التبويب
        /// يُستدعى فور تحميل صفحة الواجب
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RecordSessionStart([FromBody] DTOs.Homework.SessionStartRequest request)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { success = false, message = "❌ يجب تسجيل الدخول أولاً." });

            bool isSubmitted = await _context.HomeworkSetStudents
                .AsNoTracking()
                .AnyAsync(x =>
                    x.StudentId == studentId &&
                    x.HomeworkSetId == request.HomeworkSetId &&
                    x.IsSubmitted);

            if (isSubmitted)
                return Json(new { success = false, isSubmitted = true, message = "تم تسليم هذا الواجب مسبقاً." });

            // ✅ إلغاء تفعيل أي تبويب سابق للجلسة نفسها
            var existing = await _context.HomeworkSessionLogs
                .Where(s =>
                    s.StudentId == studentId &&
                    s.HomeworkSetId == request.HomeworkSetId &&
                    s.IsActive)
                .ToListAsync();

            foreach (var old in existing)
                old.IsActive = false;

            // ✅ إنشاء جلسة جديدة
            var session = new Entities.HomeworkSessionLog
            {
                HomeworkSetId   = request.HomeworkSetId,
                StudentId       = studentId.Value,
                TabId           = request.TabId ?? Guid.NewGuid().ToString(),
                SessionStartedAt = DateTime.UtcNow,
                LastActiveAt    = DateTime.UtcNow,
                IsActive        = true,
                IpAddress       = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent       = HttpContext.Request.Headers["User-Agent"].ToString()[..Math.Min(512, HttpContext.Request.Headers["User-Agent"].ToString().Length)]
            };

            _context.HomeworkSessionLogs.Add(session);
            await _context.SaveChangesAsync();

            // ✅ تخزين وقت بداية الجلسة في Session لحساب الوقت الكلي لاحقاً
            var sessionTimeKey = $"SessionStart_{request.HomeworkSetId}_{studentId}";
            HttpContext.Session.SetString(sessionTimeKey, DateTime.UtcNow.ToString("O"));

            return Json(new
            {
                success = true,
                sessionId = session.Id,
                tabId = session.TabId,
                serverTime = DateTime.UtcNow
            });
        }

        /// <summary>
        /// POST: التحقق من صحة الوقت المُرسَل من الـ Client
        /// يُستدعى عند كل إجابة لكشف التلاعب الزمني
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ValidateQuestionTime([FromBody] DTOs.Homework.TimeValidationRequest request)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { success = false });

            // ✅ احسب الوقت الفعلي المتاح منذ بداية الجلسة
            var sessionTimeKey = $"SessionStart_{request.HomeworkSetId}_{studentId}";
            var sessionStartStr = HttpContext.Session.GetString(sessionTimeKey);

            if (!string.IsNullOrEmpty(sessionStartStr) &&
                DateTime.TryParse(sessionStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var sessionStart))
            {
                double maxAllowedSecs = (DateTime.UtcNow - sessionStart).TotalSeconds + 30; // +30s تسامح

                if (request.ClientTimeSecs > maxAllowedSecs)
                {
                    var discrepancy = request.ClientTimeSecs - maxAllowedSecs;

                    _context.HomeworkTimeTamperLogs.Add(new Entities.HomeworkTimeTamperLog
                    {
                        HomeworkSetId   = request.HomeworkSetId,
                        StudentId       = studentId.Value,
                        QuestionId      = request.QuestionId,
                        ClientTimeSecs  = request.ClientTimeSecs,
                        MaxAllowedSecs  = maxAllowedSecs,
                        DiscrepancySecs = discrepancy,
                        LoggedAt        = DateTime.UtcNow,
                        Note            = $"الفارق: {discrepancy:F0}ث | وقت الجلسة: {(DateTime.UtcNow - sessionStart).TotalSeconds:F0}ث"
                    });

                    await _context.SaveChangesAsync();

                    _logger.LogWarning(
                        "[TIME_TAMPER] Student:{StudentId} Homework:{HomeworkSetId} Question:{QuestionId} ClientTime:{ClientTime}s MaxAllowed:{MaxAllowed}s",
                        studentId, request.HomeworkSetId, request.QuestionId, request.ClientTimeSecs, maxAllowedSecs);

                    return Json(new { success = true, suspicious = true });
                }
            }

            return Json(new { success = true, suspicious = false });
        }

        /// <summary>
        /// GET: التحقق من أن هذا التبويب هو التبويب النشط
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> CheckTabLock(int homeworkSetId, string tabId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { isActive = false });

            var activeTab = await _context.HomeworkSessionLogs
                .AsNoTracking()
                .Where(s =>
                    s.StudentId == studentId &&
                    s.HomeworkSetId == homeworkSetId &&
                    s.IsActive)
                .OrderByDescending(s => s.LastActiveAt)
                .Select(s => s.TabId)
                .FirstOrDefaultAsync();

            return Json(new { isActive = activeTab == tabId });
        }

        /// <summary>
        /// POST: تحديث وقت آخر نشاط للتبويب (Heartbeat)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> TabHeartbeat([FromBody] DTOs.Homework.TabHeartbeatRequest request)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { success = false });

            var session = await _context.HomeworkSessionLogs
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.HomeworkSetId == request.HomeworkSetId &&
                    s.TabId == request.TabId &&
                    s.IsActive);

            if (session != null)
            {
                session.LastActiveAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = session != null });
        }

        // ================================================================
        // ✅ JSON API Endpoints — للاستخدام مع الواجهة الجديدة (SPA-style)
        // ================================================================

        /// <summary>
        /// GET: جلب بيانات الواجب الكاملة مع جميع الأسئلة وتفاصيلها
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetHomeworkData(int homeworkSetId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { success = false, message = "❌ يجب تسجيل الدخول أولاً." });

            bool isSubmitted = await _context.HomeworkSetStudents
                .AsNoTracking()
                .AnyAsync(x =>
                    x.StudentId == studentId &&
                    x.HomeworkSetId == homeworkSetId &&
                    x.IsSubmitted);

            if (isSubmitted)
                return Json(new { success = false, message = "تم تسليم هذا الواجب مسبقاً.", isSubmitted = true });

            var orderedIds = await _context.Homeworks
                .AsNoTracking()
                .Where(h => h.HomeworkSetId == homeworkSetId && h.StudentId == studentId)
                .OrderBy(h => h.Id)
                .Select(h => h.QuestionId)
                .Distinct()
                .ToListAsync();

            if (!orderedIds.Any())
                return Json(new { success = false, message = "⚠️ لا توجد أسئلة لهذا الواجب." });

            var questionsDict = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                        .ThenInclude(s => s.Curriculum)
                .Where(q => orderedIds.Contains(q.Id))
                .ToDictionaryAsync(q => q.Id);

            var attemptsMap = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .GroupBy(a => a.QuestionId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.OrderByDescending(a => a.AttemptedAt).First().SelectedAnswer ?? "");

            var reviewKey = $"ReviewMarked_{homeworkSetId}_{studentId}";
            var reviewJson = HttpContext.Session.GetString(reviewKey);
            var reviewMarked = string.IsNullOrEmpty(reviewJson)
                ? new List<Guid>()
                : JsonSerializer.Deserialize<List<Guid>>(reviewJson)!;

            var mappedQuestions = orderedIds
                .Select((id, index) =>
                {
                    if (!questionsDict.TryGetValue(id, out var q)) return null;

                    var curriculum = q.Lesson?.Section?.Curriculum;

                    string questionType = q.Template switch
                    {
                        Enums.QuestionTemplate.TextOnly         => "text",
                        Enums.QuestionTemplate.WithImage        => "text_with_image",
                        Enums.QuestionTemplate.CompareValues    => "comparison",
                        Enums.QuestionTemplate.CompareWithImage => "comparison_with_image",
                        Enums.QuestionTemplate.ImageOptions     => "image_only",
                        _ => "text"
                    };

                    var opts = q.Options?.ToList() ?? new List<Entities.QuestionOption>();
                    string optionsType = opts.All(o => !string.IsNullOrEmpty(o.ImageUrl) && string.IsNullOrEmpty(o.Text))
                        ? "image_only"
                        : opts.Any(o => !string.IsNullOrEmpty(o.ImageUrl))
                            ? "text_with_image"
                            : "text";

                    return (object)new
                    {
                        id = q.Id,
                        order = index + 1,
                        type = questionType,
                        title = q.Title,
                        imageUrl = q.ImageUrl,
                        comparisonValue1 = q.ValueA,
                        comparisonValue2 = q.ValueB,
                        passage = q.VerbalPassage != null ? new
                        {
                            title          = q.VerbalPassage.Title,
                            content        = q.VerbalPassage.Content,
                            type           = q.VerbalPassage.Type.ToString().ToLower(),
                            mediaUrl       = q.VerbalPassage.MediaUrl,
                            durationSeconds = q.VerbalPassage.DurationSeconds,
                            requireFullListen = q.VerbalPassage.RequireFullListen,
                            startSeconds   = q.PassageStartSeconds,
                            endSeconds     = q.PassageEndSeconds
                        } : null,
                        curriculumSettings = new
                        {
                            isRTL           = curriculum?.IsRTL ?? true,
                            useIndicNumbers = q.IsQuantitative,
                            isQuantitative  = q.IsQuantitative
                        },
                        options = opts.Select(o => new { text = o.Text, imageUrl = o.ImageUrl }).ToList(),
                        optionsType,
                        selectedAnswer    = attemptsMap.TryGetValue(q.Id, out var ans) ? ans : null,
                        isMarkedForReview = reviewMarked.Contains(q.Id)
                    };
                })
                .Where(q => q != null)
                .ToList();

            return Json(new
            {
                success = true,
                data = new
                {
                    homeworkSetId,
                    totalQuestions  = orderedIds.Count,
                    questions       = mappedQuestions,
                    reviewMarkedIds = reviewMarked.Select(id => id.ToString()).ToList()
                }
            });
        }

        /// <summary>
        /// POST: حفظ استجابة الطالب لسؤال محدد
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SaveAnswerJson([FromBody] DTOs.Homework.SaveAnswerRequest request)
        {
            using var _context = _contextFactory.CreateDbContext();

            try
            {
                var studentId = await GetCurrentStudentIdAsync();
                if (studentId == null)
                    return Json(new { success = false, message = "❌ يجب تسجيل الدخول أولاً." });

                bool hasAnswer = !string.IsNullOrWhiteSpace(request.SelectedAnswer);

                if (hasAnswer)
                {
                    var question = await _context.Questions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(q => q.Id == request.QuestionId);

                    if (question == null)
                        return Json(new { success = false, message = "⚠️ السؤال غير موجود." });

                    bool isCorrect = string.Equals(
                        request.SelectedAnswer!.Trim(),
                        question.CorrectAnswer?.Trim(),
                        StringComparison.OrdinalIgnoreCase);

                    await _homeworkWriteService.SaveQuestionAttemptAsync(new HomeworkQuestionAttemptInput
                    {
                        StudentId        = studentId.Value,
                        HomeworkSetId    = request.HomeworkSetId,
                        QuestionId       = request.QuestionId,
                        SelectedAnswer   = request.SelectedAnswer,
                        IsCorrect        = isCorrect,
                        TimeTakenSeconds = request.TimeTakenSeconds,
                        IsMarkedForReview = request.IsMarkedForReview
                    });
                }
                else if (request.TimeTakenSeconds > 0)
                {
                    bool alreadyExists = await _context.QuestionAttemptNew.AnyAsync(a =>
                        a.StudentId == studentId &&
                        a.QuestionId == request.QuestionId &&
                        a.HomeworkSetId == request.HomeworkSetId);

                    if (!alreadyExists)
                    {
                        await _homeworkWriteService.SaveQuestionAttemptAsync(new HomeworkQuestionAttemptInput
                        {
                            StudentId        = studentId.Value,
                            HomeworkSetId    = request.HomeworkSetId,
                            QuestionId       = request.QuestionId,
                            SelectedAnswer   = string.Empty,
                            IsCorrect        = false,
                            TimeTakenSeconds = request.TimeTakenSeconds,
                            IsMarkedForReview = false
                        });
                    }
                }

                if (request.IsMarkedForReview)
                {
                    var reviewKey = $"ReviewMarked_{request.HomeworkSetId}_{studentId}";
                    var existing = HttpContext.Session.GetString(reviewKey);
                    var list = string.IsNullOrEmpty(existing)
                        ? new List<Guid>()
                        : JsonSerializer.Deserialize<List<Guid>>(existing)!;

                    if (!list.Contains(request.QuestionId))
                        list.Add(request.QuestionId);

                    HttpContext.Session.SetString(reviewKey, JsonSerializer.Serialize(list));
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في SaveAnswerJson للسؤال {QuestionId}", request.QuestionId);
                return Json(new { success = false, message = "⚠️ فشل حفظ الإجابة. يرجى المحاولة مرة أخرى." });
            }
        }

        /// <summary>
        /// POST: تسجيل بيانات جلسة المراجعة (وقت الدخول + سجل التنقلات)
        /// </summary>
        [HttpPost]
        public IActionResult SaveReviewSession([FromBody] DTOs.Homework.SaveReviewSessionRequest request)
        {
            var sessionKey = $"ReviewSession_{request.HomeworkSetId}_{_userManager.GetUserId(User)}";

            var sessionData = new
            {
                homeworkSetId  = request.HomeworkSetId,
                enteredAt      = request.EnteredAt,
                navigationLog  = request.NavigationLog,
                savedAt        = DateTime.UtcNow
            };

            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(sessionData));

            return Json(new { success = true });
        }

        /// <summary>
        /// POST: إنهاء الواجب نهائياً مع حفظ الوقت الكلي
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> FinalSubmitJson([FromBody] DTOs.Homework.FinalSubmitRequest request)
        {
            using var _context = _contextFactory.CreateDbContext();

            try
            {
                var studentId = await GetCurrentStudentIdAsync();
                if (studentId == null)
                    return Json(new { success = false, message = "❌ يجب تسجيل الدخول أولاً." });

                bool hasQuestions = await _context.Homeworks.AnyAsync(h =>
                    h.HomeworkSetId == request.HomeworkSetId &&
                    h.StudentId == studentId);

                if (!hasQuestions)
                    return Json(new { success = false, message = "⚠️ لا توجد أسئلة لهذا الواجب." });

                var hss = await _context.HomeworkSetStudents
                    .FirstOrDefaultAsync(x =>
                        x.HomeworkSetId == request.HomeworkSetId &&
                        x.StudentId == studentId.Value);

                if (hss == null)
                {
                    hss = new Entities.HomeworkSetStudent
                    {
                        HomeworkSetId = request.HomeworkSetId,
                        StudentId     = studentId.Value,
                        IsSubmitted   = false,
                        AssignedAt    = DateTime.UtcNow,
                        LastUpdated   = DateTime.UtcNow
                    };
                    _context.HomeworkSetStudents.Add(hss);
                    await _context.SaveChangesAsync();
                }

                await _homeworkWriteService.SubmitHomeworkAsync(request.HomeworkSetId, studentId.Value);
                await _studentProgressService.RecordHomeworkProgress(studentId.Value, request.HomeworkSetId);

                var redirectUrl = Url.Action(
                    "HomeworkReportUnified",
                    "StudentHomeworkDashboard",
                    new { area = "Students", homeworkSetId = request.HomeworkSetId });

                return Json(new { success = true, redirectUrl });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في FinalSubmitJson للواجب {HomeworkSetId}", request.HomeworkSetId);
                return Json(new { success = false, message = "⚠️ حدث خطأ أثناء تسليم الواجب. يرجى المحاولة مرة أخرى." });
            }
        }

        private string GetDelayLevel(int days)
        {
            if (days >= 5) return "مرتفع";
            if (days >= 2 && days <= 3) return "متوسط";
            return "منخفض";
        }

        [HttpGet]
        public async Task<IActionResult> Review(int homeworkSetId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🧭 جلب بيانات الواجب
            var homeworkSet = await _context.HomeworkSets
                .Include(hs => hs.Batch)
                .FirstOrDefaultAsync(hs => hs.Id == homeworkSetId);

            if (homeworkSet == null)
                return NotFound("❌ لم يتم العثور على هذا الواجب.");

            // ✅ جلب كل المحاولات الخاصة بالطالب لهذا الواجب
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Options)
                .Include(a => a.Question.VerbalPassage)
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .AsNoTracking()
                .ToListAsync();

            if (!attempts.Any())
                return Content("<div class='alert alert-warning text-center'>⚠️ لا توجد محاولات مسجلة لهذا الواجب.</div>", "text/html");

            // ✅ الوقت الفعلي المحسوب من أول محاولة لآخر محاولة
            DateTime? startedAt = attempts.Min(a => a.AttemptedAt);
            DateTime? finishedAt = attempts.Max(a => a.AttemptedAt);

            int actualMinutesSpent = 0;
            string formattedTime = "غير محدد";
            string explanation = "";

            if (startedAt.HasValue && finishedAt.HasValue)
            {
                actualMinutesSpent = (int)Math.Round((finishedAt.Value - startedAt.Value).TotalMinutes);
                if (actualMinutesSpent <= 0)
                    actualMinutesSpent = 1;

                formattedTime = $"{actualMinutesSpent} دقيقة";
                explanation = $"⏱ الوقت المستغرق فعليًا من {startedAt:HH:mm} حتى {finishedAt:HH:mm}";
            }
            else
            {
                // fallback باستخدام TimeTakenSeconds
                double totalSeconds = attempts.Sum(a => a.TimeTakenSeconds);
                int minutes = (int)(totalSeconds / 60);
                formattedTime = $"{minutes} دقيقة (تقديري)";
                explanation = "⏱ لم يتم تسجيل زمن البداية والنهاية، تم حساب الزمن تقديريًا.";
            }

            // ✅ تجهيز الأسئلة
            var questionVms = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .Select(x => new HomeworkReviewQuestionVm
                {
                    QuestionId = x.Question.Id,
                    QuestionText = x.Question.Title,
                    StudentAnswer = x.SelectedAnswer,
                    CorrectAnswer = x.Question.CorrectAnswer,
                    IsCorrect = x.IsCorrect,
                    TimeTakenSeconds = x.TimeTakenSeconds,

                    ImageUrl = x.Question.ImageUrl,
                    IsQuantitative = x.Question.IsQuantitative,
                    ComparisonValue1 = x.Question.ValueA,
                    ComparisonValue2 = x.Question.ValueB,
                    VerbalPassageContent = x.Question.VerbalPassage != null ? x.Question.VerbalPassage.Content : null,

                    DisplayType = x.Question.Template switch
                    {
                        QuestionTemplate.CompareValues => Enums.QuestionDisplayType.ComparisonText,
                        QuestionTemplate.CompareWithImage => Enums.QuestionDisplayType.ComparisonWithImage,
                        _ => Enums.QuestionDisplayType.WithImage
                    },

                    Options = x.Question.Options.Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                })
                .ToList();

            // ✅ بناء ViewModel النهائي
            // ✅ بناء ViewModel النهائي
            // ✅ بناء ViewModel النهائي
            var vm = new HomeworkReviewViewModel
            {
                HomeworkSetId = homeworkSetId,
                TotalQuestions = questionVms.Count,
                CorrectAnswers = questionVms.Count(q => q.IsCorrect),
                WrongAnswers = questionVms.Count(q => !q.IsCorrect),

                // ✅ الوقت الكلي
                TimeSpentMinutes = actualMinutesSpent,
                TimeSpentFormatted = formattedTime, // 🔹 أضف هذا السطر لعرض الوقت في الصفحة
                TimeExplanation = explanation,

                // ✅ التحويل اليدوي إلى نوع HomeworkQuestionReviewItem
                Questions = questionVms.Select(q => new HomeworkQuestionReviewItems
                {
                    QuestionId = q.QuestionId,
                    QuestionText = q.QuestionText,
                    StudentAnswer = q.StudentAnswer,
                    CorrectAnswer = q.CorrectAnswer,
                    IsCorrect = q.IsCorrect,
                    TimeTakenSeconds = q.TimeTakenSeconds,
                    ImageUrl = q.ImageUrl,
                    IsQuantitative = q.IsQuantitative,
                    ComparisonValue1 = q.ComparisonValue1,
                    ComparisonValue2 = q.ComparisonValue2,
                    VerbalPassageContent = q.VerbalPassageContent,
                    DisplayType = q.DisplayType,
                    Options = q.Options.Select(o => new HomeworkOptionReviewItem
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                }).ToList()
            };


            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> AdminHomeworkReport(int studentId, int homeworkSetId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧭 جلب بيانات الطالب
            var student = await _context.Students
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("❌ لم يتم العثور على بيانات الطالب.");

            // 🧭 جلب بيانات الواجب
            var homeworkSet = await _context.HomeworkSets
                .Include(h => h.Batch)
                .FirstOrDefaultAsync(h => h.Id == homeworkSetId);

            if (homeworkSet == null)
                return NotFound("❌ لم يتم العثور على بيانات الواجب.");

            // 🔹 جلب الأسئلة ومحاولات الطالب
            var questions = await (
                from h in _context.Homeworks
                join q in _context.Questions on h.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                join c in _context.Curriculums on s.CurriculumId equals c.Id
                join att in _context.QuestionAttemptNew
                    .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                    on q.Id equals att.QuestionId into gj
                from attempt in gj.DefaultIfEmpty()
                where h.HomeworkSetId == homeworkSetId && h.StudentId == studentId
                select new
                {
                    q.Id,
                    q.Title,
                    SectionId = s.Id,
                    SectionName = s.Title,
                    CurriculumTitle = c.Title,
                    IsCorrect = attempt != null && attempt.IsCorrect,
                    HasAttempt = attempt != null,
                    TimeTakenSeconds = attempt != null ? attempt.TimeTakenSeconds : 0.0
                }
            ).ToListAsync();

            int totalQuestions = questions.Count;
            int correctAnswers = questions.Count(x => x.IsCorrect);
            int answeredQuestions = questions.Count(x => x.HasAttempt);
            int skippedQuestions = totalQuestions - answeredQuestions;

            double overall = totalQuestions > 0
                ? Math.Round(correctAnswers * 100.0 / totalQuestions, 1)
                : 0.0;

            // 🕒 حساب الوقت الكلي من أول محاولة إلى آخر محاولة
            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            DateTime? start = attempts.Min(a => a.AttemptedAt);
            DateTime? end = attempts.Max(a => a.AttemptedAt);

            int totalSeconds = (int)attempts.Sum(a => a.TimeTakenSeconds);
            int solveMinutes = 0;

            string formattedTime = "غير محدد";
            string timeExplanation = "";

            if (start.HasValue && end.HasValue)
            {
                solveMinutes = (int)Math.Round((end.Value - start.Value).TotalMinutes);
                if (solveMinutes <= 0) solveMinutes = 1;
                formattedTime = $"{solveMinutes} دقيقة";
                timeExplanation = $"من {start:HH:mm} إلى {end:HH:mm}";
            }
            else if (totalSeconds > 0)
            {
                solveMinutes = totalSeconds / 60;
                formattedTime = $"{solveMinutes} دقيقة (تقديري)";
                timeExplanation = "تم احتساب الزمن من مجموع وقت الأسئلة.";
            }

            // ⚡ التحليل الزمني التفصيلي
            double avgSecondsPerQuestion = answeredQuestions > 0
                ? Math.Round(totalSeconds / (double)answeredQuestions, 1)
                : 0;

            var fastest = questions
                .Where(q => q.TimeTakenSeconds > 0)
                .OrderBy(q => q.TimeTakenSeconds)
                .FirstOrDefault();

            var slowest = questions
                .Where(q => q.TimeTakenSeconds > 0)
                .OrderByDescending(q => q.TimeTakenSeconds)
                .FirstOrDefault();

            string fastestQuestion = fastest != null ? fastest.Title : "—";
            string slowestQuestion = slowest != null ? slowest.Title : "—";
            double fastestTime = fastest?.TimeTakenSeconds ?? 0;
            double slowestTime = slowest?.TimeTakenSeconds ?? 0;

            // 🔹 تحليل المحاور
            var homeworkSections = questions
                .GroupBy(q => new { q.SectionName, q.CurriculumTitle })
                .Select(g => new
                {
                    g.Key.SectionName,
                    g.Key.CurriculumTitle,
                    TotalQuestions = g.Count(),
                    CorrectCount = g.Count(x => x.IsCorrect),
                    WrongCount = g.Count(x => !x.IsCorrect),
                    Percent = g.Any() ? Math.Round(g.Count(x => x.IsCorrect) * 100.0 / g.Count(), 1) : 0.0
                })
                .ToList();

            var quantGroups = homeworkSections
                .Where(x => x.CurriculumTitle.Contains("كمي"))
                .ToList();

            var verbalGroups = homeworkSections
                .Where(x => x.CurriculumTitle.Contains("لفظي"))
                .ToList();

            // ✅ بناء ViewModel
            var model = new PlacementReportViewModel
            {
                Student = new StudentMiniVm
                {
                    FullName = student.FullName,
                    StudentID = student.StudentID,
                    Level = student.Level,
                    ParentName = student.Parent?.FullName ?? "—",
                    ParentPhone = student.Parent?.PhoneNumber ?? "—"
                },
                ExamDate = homeworkSet.CreatedAt.ToString("yyyy-MM-dd"),
                SolveMinutes = solveMinutes,
                OverallPercent = overall,
                Recommendation = null,

                QuantLabels = quantGroups.Select(x => x.SectionName).ToList(),
                QuantScores = quantGroups.Select(x => x.Percent).ToList(),
                VerbalLabels = verbalGroups.Select(x => x.SectionName).ToList(),
                VerbalScores = verbalGroups.Select(x => x.Percent).ToList(),

                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                AnsweredQuestions = answeredQuestions,
                SkippedQuestions = skippedQuestions,

                // 🆕 إضافات زمنية
                AverageSecondsPerQuestion = avgSecondsPerQuestion,
                FastestQuestion = fastestQuestion,
                FastestTimeSeconds = fastestTime,
                SlowestQuestion = slowestQuestion,
                SlowestTimeSeconds = slowestTime,
                TimeExplanation = timeExplanation,
                TimeSpentFormatted = formattedTime
            };

            return View("AdminHomeworkReport", model);
        }


        [HttpGet]
        public async Task<IActionResult> SolvedHomeworks()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var solved = await (
                from h in _context.Homeworks
                join hs in _context.HomeworkSets on h.HomeworkSetId equals hs.Id
                where h.StudentId == studentId && h.Status == HomeworkStatus.Submitted
                group h by new { h.HomeworkSetId, hs.CompletionTitle, hs.StartAt } into g
                select new
                {
                    HomeworkSetId = g.Key.HomeworkSetId,
                    Title = g.Key.CompletionTitle,
                    StartAt = g.Key.StartAt,
                    TotalQuestions = g.Count()
                }
            ).OrderByDescending(x => x.StartAt).ToListAsync();

            var vm = solved.Select(x => new HomeworkListVm
            {
                HomeworkSetId = x.HomeworkSetId,
                Title = x.Title ?? "واجب",
                CreatedAt = x.StartAt ?? DateTime.Now,
                IsSubmitted = true,
                SectionName = "-",
                DelayLevel = "تم الحل",
                Score = 0,
                BatchAverageScore = 0
            }).ToList();

            ViewBag.TotalSolved = vm.Count;
            return View(vm);
        }



        [HttpGet]
        public async Task<IActionResult> RequiredHomeworks()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🟢 1. جلب دفعات الطالب
            var batchIds = await _context.StudentBatchEnrollments
                .Where(e => e.StudentID == studentId)
                .Select(e => e.BatchId)
                .ToListAsync();

            // 🟢 2. جلب جميع الواجبات في النظام (بشكل آمن)
            var allSetsDb = await (
                from hs in _context.HomeworkSets
                join b in _context.Batches on hs.BatchId equals b.Id
                select new
                {
                    hs.Id,
                    hs.CompletionTitle,
                    hs.StartAt,
                    hs.EndAt,
                    BatchId = b.Id,
                    BatchName = b.Name
                }
            ).ToListAsync();

            // 🟢 3. الفلترة داخل الذاكرة (آمنة 100%)
            var allSets = allSetsDb
                .Where(x => batchIds.Contains(x.BatchId))
                .OrderBy(x => x.StartAt)
                .ToList();

            // 🟢 4. الواجبات المحلولة
            var solvedIds = await _context.Homeworks
                .Where(h => h.StudentId == studentId && h.Status == HomeworkStatus.Submitted)
                .Select(h => h.HomeworkSetId)
                .Distinct()
                .ToListAsync();

            // 🟢 5. استبعاد المحلولة
            var required = allSets
                .Where(x => !solvedIds.Contains(x.Id))
                .ToList();

            var vm = required.Select(x => new HomeworkListVm
            {
                HomeworkSetId = x.Id,
                Title = x.CompletionTitle ?? "واجب",
                CreatedAt = x.StartAt ?? DateTime.Now,
                SectionName = x.BatchName,
                IsSubmitted = false,
                DelayLevel = (x.EndAt.HasValue && x.EndAt.Value < DateTime.Now)
                    ? "متأخر"
                    : "مطلوب",
                Score = 0,
                BatchAverageScore = 0
            }).ToList();

            ViewBag.TotalRequired = vm.Count;
            return View(vm);
        }


        [HttpGet]
        public async Task<IActionResult> LateHomeworks()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🟢 1. جلب دفعات الطالب
            var batchIds = await _context.StudentBatchEnrollments
                .Where(e => e.StudentID == studentId)
                .Select(e => e.BatchId)
                .ToListAsync();

            // 🟢 2. جلب كل الواجبات من قاعدة البيانات دون أي شرط Any (آمن تمامًا)
            var allSetsDb = await (
                from hs in _context.HomeworkSets
                join b in _context.Batches on hs.BatchId equals b.Id
                select new
                {
                    hs.Id,
                    hs.CompletionTitle,
                    hs.StartAt,
                    hs.EndAt,
                    BatchId = b.Id,
                    BatchName = b.Name
                }
            ).ToListAsync();

            // 🟢 3. فلترة داخل الذاكرة فقط (لا علاقة لها بـ SQL)
            var allSets = allSetsDb
                .Where(x => batchIds.Contains(x.BatchId))
                .ToList();

            // 🟢 4. الواجبات التي حلها الطالب
            var solvedIds = await _context.Homeworks
                .Where(h => h.StudentId == studentId && h.Status == HomeworkStatus.Submitted)
                .Select(h => h.HomeworkSetId)
                .Distinct()
                .ToListAsync();

            // 🟢 5. المتأخرة فقط (انتهى وقتها ولم تُحل)
            var late = allSets
                .Where(x => x.EndAt.HasValue && x.EndAt.Value < DateTime.Now && !solvedIds.Contains(x.Id))
                .OrderByDescending(x => x.EndAt)
                .ToList();

            var vm = late.Select(x => new HomeworkListVm
            {
                HomeworkSetId = x.Id,
                Title = x.CompletionTitle ?? "واجب",
                CreatedAt = x.StartAt ?? DateTime.Now,
                SectionName = x.BatchName,
                IsSubmitted = false,
                DelayLevel = "متأخر",
                Score = 0,
                BatchAverageScore = 0
            }).ToList();

            ViewBag.TotalLate = vm.Count;
            return View(vm);
        }


        public async Task<IActionResult> Result(int homeworkSetId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account");

            var analytics = await _homeworkAnalyticsService
                .AnalyzeHomeworkAsync(studentId.Value, homeworkSetId);

            await _homeworkAnalyticsService.UpdateHomeworkSubmissionAsync(
                studentId.Value, homeworkSetId, analytics.ScorePercentage, true);

            int total = analytics.Questions.Count;
            int correct = analytics.Questions.Count(q => q.IsCorrect);
            int wrong = total - correct;

            // ===========================
            //  جلب IsQuantitative من DB
            // ===========================
            bool isQuantitative = false;

            if (analytics.Questions.Any())
            {
                var firstQid = analytics.Questions.First().QuestionId;

                using var _context = _contextFactory.CreateDbContext();
                isQuantitative = await _context.Questions
                    .Where(q => q.Id == firstQid)
                    .Select(q => q.IsQuantitative)
                    .FirstOrDefaultAsync();
            }

            // ===========================
            //  تحويل الأسئلة إلى نوع Result
            // ===========================
            var questionsVm = analytics.Questions.Select(q => new HomeworkResultItemViewModel
            {
                QuestionId = q.QuestionId,
                QuestionTitle = q.QuestionTitle,
                StudentAnswer = q.StudentAnswer,
                CorrectAnswer = q.CorrectAnswer,
                IsCorrect = q.IsCorrect
            }).ToList();

            // ===========================
            //  بناء الـ ViewModel النهائي
            // ===========================
            var vm = new HomeworkResultViewModel
            {
                HomeworkSetId = homeworkSetId,
                TotalQuestions = total,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                ScorePercentage = analytics.ScorePercentage,
                TimeSpentMinutes = analytics.TimeSpentMinutes,
                IsQuantitative = isQuantitative,
                Questions = questionsVm
            };

            return View("Result", vm);
        }


        private async Task<HomeworkSolveViewModel> GetSolveViewModel(
         int homeworkSetId,
         Guid questionId,
         int studentId,
         ApplicationDbContext _context)
        {
            // 🟢 1. IDs
            var allQuestions = await GetHomeworkQuestionIdsAsync(homeworkSetId, _context);
            if (!allQuestions.Any())
                return new HomeworkSolveViewModel { ErrorMessage = "⚠️ لا توجد أسئلة." };

            // 🟢 2. تحميل السؤال + تحديد IsQuantitative دائمًا
            QuestionDisplayViewModel? qVm = null;
            string cacheKey = $"Question_{questionId}";

            bool isQuantitative;

            if (HttpContext.Session.TryGetValue(cacheKey, out var bytes))
            {
                // 🔹 تحميل من Session
                qVm = System.Text.Json.JsonSerializer.Deserialize<QuestionDisplayViewModel>(bytes);

                // 🔴 مهم جدًا: استنتاج IsQuantitative حتى مع وجود Cache
                isQuantitative = await _context.Questions
                    .AsNoTracking()
                    .Where(q => q.Id == questionId)
                    .Select(q => q.Lesson.Section.Curriculum.IsQuantitative)
                    .FirstOrDefaultAsync();
            }
            else
            {
                // 🔹 تحميل كامل من DB
                var qEntity = await _context.Questions
                    .Include(q => q.Options)
                    .Include(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                    .Include(q => q.VerbalPassage)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == questionId);

                if (qEntity == null)
                    return new HomeworkSolveViewModel { ErrorMessage = "⚠️ فشل تحميل السؤال." };

                isQuantitative =
                    qEntity.Lesson?.Section?.Curriculum?.IsQuantitative ?? false;

                qVm = qEntity.ToDisplayModel();

                HttpContext.Session.Set(
                    cacheKey,
                    System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(qVm)
                );
            }

            if (qVm == null)
                return new HomeworkSolveViewModel { ErrorMessage = "⚠️ فشل تحميل السؤال." };

            // 🟢 3. الإجابة الحالية
            var attempt = await _context.QuestionAttemptNew
                .AsNoTracking()
                .FirstOrDefaultAsync(a =>
                    a.StudentId == studentId &&
                    a.QuestionId == questionId &&
                    a.HomeworkSetId == homeworkSetId);

            var selectedAnswer = attempt?.SelectedAnswer ?? "";

            foreach (var opt in qVm.Options)
                opt.IsSelected = (opt.Text == selectedAnswer);

            // 🟢 4. جميع الإجابات
            var answersMap = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .GroupBy(a => a.QuestionId)
                .ToDictionaryAsync(
                    g => g.Key,
                    g => g.OrderByDescending(a => a.AttemptedAt)
                          .FirstOrDefault()?.SelectedAnswer ?? ""
                );

            // 🟢 5. المراجعة
            var reviewKey = $"ReviewMarked_{homeworkSetId}_{studentId}";
            var reviewList = HttpContext.Session.TryGetValue(reviewKey, out var rb)
                ? System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(rb) ?? new List<Guid>()
                : new List<Guid>();

            // 🟢 6. ViewModel النهائي
            return new HomeworkSolveViewModel
            {
                HomeworkSetId = homeworkSetId,
                Question = qVm,
                CurrentQuestionId = questionId,
                AllQuestionIds = allQuestions,
                SelectedAnswer = selectedAnswer,
                AnswersMap = answersMap,
                ReviewMarkedIds = reviewList,

                // ⭐⭐⭐ السطر الحاسم
                IsQuantitative = isQuantitative,

                IsReviewMode = true,
                IsHomeworkSubmitted = false,
                ForceReviewVisible = false
            };
        }


    }
}