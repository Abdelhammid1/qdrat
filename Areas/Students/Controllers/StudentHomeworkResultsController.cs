using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
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
    public class StudentHomeworkResultsController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStudentHomeworkAnalyticsService _homeworkAnalyticsService;
        private readonly IHomeworkRecommendationService _homeworkRecommendationService;

        public StudentHomeworkResultsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            IStudentHomeworkAnalyticsService homeworkAnalyticsService,
            IHomeworkRecommendationService homeworkRecommendationService)
        {
            _contextFactory = contextFactory;
            _userManager = userManager;
            _homeworkAnalyticsService = homeworkAnalyticsService;
            _homeworkRecommendationService = homeworkRecommendationService;
        }

        private async Task<int?> GetCurrentStudentIdAsync()
        {
            using var db = _contextFactory.CreateDbContext();

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return null;

            var student = await db.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);

            return student?.StudentID;
        }

        // =====================================================
        // ✅ HomeworkReportUnified (منقول كما هو)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> HomeworkReportUnified(int homeworkSetId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // =====================================================
            // 1️⃣ نتيجة الواجب
            // =====================================================
            var summary = await db.HomeworkSetStudents
                .AsNoTracking()
                .Where(x =>
                    x.StudentId == studentId &&
                    x.HomeworkSetId == homeworkSetId &&
                    x.IsSubmitted)
                .Select(x => x.Score)
                .FirstOrDefaultAsync();

            // =====================================================
            // 2️⃣ بيانات الطالب
            // =====================================================
            var studentInfo = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new
                {
                    s.FullName,
                    BatchName = s.BatchEnrollments
                        .Select(e => e.Batch.Name)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();

            // =====================================================
            // 3️⃣ الأسئلة المرتبطة بالواجب (المرجع الأساسي)
            // =====================================================
            var homeworkQuestions = await (
                from h in db.Homeworks.AsNoTracking()
                join q in db.Questions.AsNoTracking() on h.QuestionId equals q.Id
                join l in db.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in db.Sections.AsNoTracking() on l.SectionId equals s.Id
                where h.StudentId == studentId
                      && h.HomeworkSetId == homeworkSetId
                select new
                {
                    h.QuestionId,
                    QuestionTitle = q.Title,
                    q.CorrectAnswer,
                    IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true, // ✅
                    LessonId = l.Id,
                    LessonTitle = l.Title,
                    SectionId = s.Id,
                    SectionTitle = s.Title
                }
            ).ToListAsync();

            // =====================================================
            // 4️⃣ آخر محاولة لكل سؤال
            // =====================================================
            var attempts = await db.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            var questions = homeworkQuestions
                .Select(q =>
                {
                    var lastAttempt = attempts
                        .Where(a => a.QuestionId == q.QuestionId)
                        .OrderByDescending(a => a.AttemptedAt)
                        .FirstOrDefault();

                    return new HomeworkQuestionAnalyticsVm
                    {
                        QuestionId = q.QuestionId,
                        QuestionTitle = q.QuestionTitle,
                        StudentAnswer = lastAttempt?.SelectedAnswer,
                        IsRTL = q.IsRTL, // ✅
                        CorrectAnswer = q.CorrectAnswer,
                        IsCorrect = lastAttempt?.IsCorrect ?? false,
                        TimeTakenSeconds = lastAttempt?.TimeTakenSeconds ?? 0
                    };
                })
                .ToList();

            // =====================================================
            // 5️⃣ الحسابات العامة (مصدر الكروت)
            // =====================================================
            int totalQuestions = questions.Count;
            int correctCount = questions.Count(x => x.IsCorrect);
            int wrongCount = questions.Count(x => !x.IsCorrect && !string.IsNullOrWhiteSpace(x.StudentAnswer));
            int skippedCount = totalQuestions - correctCount - wrongCount;

            double totalSeconds = questions.Sum(x => x.TimeTakenSeconds);
            double timeSpentMinutes = totalSeconds > 0
                ? Math.Round(totalSeconds / 60.0, 1)
                : 0;

            // =====================================================
            // 6️⃣ ViewModel الأساسي
            // =====================================================
            var vm = new HomeworkAnalyticsViewModel
            {
                HomeworkSetId = homeworkSetId,
                StudentName = studentInfo?.FullName ?? "الطالب",
                BatchName = studentInfo?.BatchName ?? "غير محددة",
                Questions = questions,
                ScorePercentage = summary ?? 0,
                TimeSpentMinutes = timeSpentMinutes,

             
            };

            // =====================================================
            // 7️⃣ SectionsPerformance (التشارت)
            // =====================================================
            vm.SectionsPerformance = homeworkQuestions
                .GroupBy(x => new { x.SectionId, x.SectionTitle })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(q =>
                        questions.Any(a => a.QuestionId == q.QuestionId && a.IsCorrect));

                    return new SectionPerformanceVm
                    {
                        SectionId = g.Key.SectionId,
                        SectionTitle = g.Key.SectionTitle,
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
            // 8️⃣ LessonsPerformance (الجدول)
            // =====================================================
            vm.LessonsPerformance = homeworkQuestions
                .GroupBy(x => new { x.LessonId, x.LessonTitle })
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(q =>
                        questions.Any(a => a.QuestionId == q.QuestionId && a.IsCorrect));

                    var seconds = questions
                        .Where(a => g.Any(q => q.QuestionId == a.QuestionId))
                        .Sum(a => a.TimeTakenSeconds);

                    return new LessonPerformanceVm
                    {
                        LessonId = g.Key.LessonId,
                        LessonTitle = g.Key.LessonTitle,
                        TotalQuestions = total,
                        SuccessRate = total > 0
                            ? Math.Round(correct * 100.0 / total, 1)
                            : 0,
                        TimeSpentMinutes = seconds > 0
                            ? Math.Round(seconds / 60.0, 1)
                            : 0
                    };
                })
                .ToList();

            // =====================================================
            // 9️⃣ التوصيات
            // =====================================================
            vm.Recommendation =
                _homeworkRecommendationService.GenerateRecommendation(
                    new HomeworkAnalyticsVm
                    {
                        ScorePercentage = vm.ScorePercentage,
                        TimeSpentMinutes = vm.TimeSpentMinutes,
                        QuestionsCount = totalQuestions,
                        CorrectAnswers = correctCount
                    });

            return View("HomeworkReportUnified", vm);
        }


        [HttpGet]
        public async Task<IActionResult> Review(int homeworkSetId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var attempts = await db.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Options)
                .Include(a => a.Question.VerbalPassage)
                .Include(a => a.Question.Curriculum)
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            if (!attempts.Any())
                return Content("⚠️ لا توجد محاولات مسجلة لهذا الواجب.");

            var grouped = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToList();

            var vm = new HomeworkReviewViewModel
            {
                HomeworkSetId = homeworkSetId,
                TotalQuestions = grouped.Count,
                CorrectAnswers = grouped.Count(x => x.IsCorrect),
                WrongAnswers = grouped.Count(x => !x.IsCorrect),

                Questions = grouped.Select(x => new HomeworkQuestionReviewItems
                {
                    QuestionId = x.QuestionId,
                    QuestionText = x.Question.Title,
                    StudentAnswer = x.SelectedAnswer,
                    CorrectAnswer = x.Question.CorrectAnswer,
                    IsCorrect = x.IsCorrect,
                    // 🔥 الجديد
                    Explanation = x.Question.Explanation,
                    VideoUrl = x.Question.VideoUrl,

                    TimeTakenSeconds = x.TimeTakenSeconds,
                    ImageUrl = x.Question.ImageUrl,
                    IsQuantitative = x.Question.IsQuantitative,
                    ComparisonValue1 = x.Question.ValueA,
                    ComparisonValue2 = x.Question.ValueB,

                    // 🔥 القطعة
                    VerbalPassageContent = x.Question.VerbalPassage?.Content,
                    VerbalPassageMediaUrl = x.Question.VerbalPassage?.MediaUrl,
                    VerbalPassageType = x.Question.VerbalPassage != null
                        ? (QdratNew.Enums.PassageType?)x.Question.VerbalPassage.Type
                        : null,
                    VerbalPassageTitle = x.Question.VerbalPassage?.Title,

                    // الاتجاه
                    IsRTL = x.Question.Curriculum != null
                        ? x.Question.Curriculum.IsRTL
                        : true,

                    DisplayType = x.Question.Template switch
                    {
                        QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                        QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                        _ => QuestionDisplayType.WithImage
                    },

                    Options = x.Question.Options.Select(o => new HomeworkOptionReviewItem
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()

                }).ToList()
            };

            return View("Review", vm);
        }


        [HttpGet]
        public async Task<IActionResult> ReviewLesson(int homeworkSetId, int lessonId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // =====================================================
            // 1️⃣ كل أسئلة الواجب داخل هذا المؤشر
            // =====================================================
            var homeworkQuestions = await (
                from h in db.Homeworks.AsNoTracking()
                join q in db.Questions
                    .Include(q => q.Options)
                    .Include(q => q.VerbalPassage)
                    .Include(q => q.Curriculum)
                    on h.QuestionId equals q.Id
                where h.StudentId == studentId
                      && h.HomeworkSetId == homeworkSetId
                      && q.LessonId == lessonId
                select q
            ).Distinct().ToListAsync();

            if (!homeworkQuestions.Any())
            {
                // ✅ تحديد اللغة من أول سؤال لو موجود (أو default)
                bool isRTL = true;

                return RedirectToAction(
                    "ErrorMessage",
                    "StudentHomeworkDashboard",
                    new
                    {
                        area = "Students",
                        msg = isRTL
                            ? "⚠️ لا توجد أسئلة لهذا المؤشر."
                            : "No questions found for this lesson"
                    }
                );
            }

            // =====================================================
            // 2️⃣ تحديد اتجاه الصفحة (مهم جدًا)
            // =====================================================
            bool pageIsRTL = homeworkQuestions.FirstOrDefault()?.Curriculum?.IsRTL ?? true;

            // =====================================================
            // 3️⃣ عنوان المؤشر
            // =====================================================
            var lessonTitle = await db.Lessons
                .Where(l => l.Id == lessonId)
                .Select(l => l.Title)
                .FirstOrDefaultAsync();

            // =====================================================
            // 4️⃣ المحاولات
            // =====================================================
            var attempts = await db.QuestionAttemptNew
                .Where(a =>
                    a.StudentId == studentId &&
                    a.HomeworkSetId == homeworkSetId)
                .AsNoTracking()
                .ToListAsync();

            var lastAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.AttemptedAt).First()
                );

            // =====================================================
            // 5️⃣ بناء الأسئلة
            // =====================================================
            var questionItems = homeworkQuestions.Select(q =>
            {
                lastAttempts.TryGetValue(q.Id, out var attempt);

                bool hasAnswer =
                    attempt != null &&
                    !string.IsNullOrWhiteSpace(attempt.SelectedAnswer);

                return new HomeworkQuestionReviewItems
                {
                    QuestionId = q.Id,
                    QuestionText = q.Title,
                    StudentAnswer = attempt?.SelectedAnswer,
                    CorrectAnswer = q.CorrectAnswer,
                    Explanation = q.Explanation,
                    VideoUrl = q.VideoUrl,
                    IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true,

                    IsCorrect = hasAnswer && attempt!.IsCorrect,
                    TimeTakenSeconds = attempt?.TimeTakenSeconds ?? 0,

                    ImageUrl = q.ImageUrl,
                    IsQuantitative = q.IsQuantitative,
                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,

                    // 🔥 القطعة
                    VerbalPassageContent = q.VerbalPassage?.Content,
                    VerbalPassageTitle = q.VerbalPassage?.Title,
                    VerbalPassageMediaUrl = q.VerbalPassage?.MediaUrl,
                    VerbalPassageType = q.VerbalPassage != null
          ? (QdratNew.Enums.PassageType?)q.VerbalPassage.Type
          : null,

                    DisplayType = q.Template switch
                    {
                        QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                        QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                        _ => QuestionDisplayType.WithImage
                    },

                    Options = q.Options.Select(o => new HomeworkOptionReviewItem
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                };
            }).ToList();

            // =====================================================
            // 6️⃣ الإحصائيات
            // =====================================================
            int total = questionItems.Count;
            int correct = questionItems.Count(x => x.StudentAnswer != null && x.IsCorrect);
            int wrong = questionItems.Count(x => x.StudentAnswer != null && !x.IsCorrect);
            int skipped = questionItems.Count(x => string.IsNullOrWhiteSpace(x.StudentAnswer));

            double totalSeconds = questionItems.Sum(x => x.TimeTakenSeconds);
            int minutes = (int)(totalSeconds / 60);
            int seconds = (int)(totalSeconds % 60);

            // =====================================================
            // 7️⃣ ViewModel
            // =====================================================
            var vm = new HomeworkReviewViewModel
            {
                HomeworkSetId = homeworkSetId,
                LessonId = lessonId,
                LessonTitle = lessonTitle ?? "",
                TotalQuestions = total,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                SkippedAnswers = skipped,
                TimeSpentMinutes = minutes,
                TimeSpentSeconds = seconds,
                TimeSpentFormatted = $"{minutes} {(pageIsRTL ? "دقيقة" : "min")} {seconds} {(pageIsRTL ? "ثانية" : "sec")}",

                // ✅ أهم إضافة
                IsRTL = pageIsRTL,

                Questions = questionItems
            };

            return View("Review", vm);
        }


        [HttpGet]
        public async Task<IActionResult> Result(int homeworkSetId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var analytics = await _homeworkAnalyticsService
                .AnalyzeHomeworkAsync(studentId.Value, homeworkSetId);

            var vm = new HomeworkResultViewModel
            {
                HomeworkSetId = homeworkSetId,
                TotalQuestions = analytics.Questions.Count,
                CorrectAnswers = analytics.Questions.Count(q => q.IsCorrect),
                WrongAnswers = analytics.Questions.Count(q => !q.IsCorrect),
                ScorePercentage = analytics.ScorePercentage,
                TimeSpentMinutes = analytics.TimeSpentMinutes,
                IsQuantitative = analytics.IsQuantitative,
                Questions = analytics.Questions.Select(q => new HomeworkResultItemViewModel
                {
                    QuestionId = q.QuestionId,
                    QuestionTitle = q.QuestionTitle,
                    StudentAnswer = q.StudentAnswer,
                    CorrectAnswer = q.CorrectAnswer,
                    IsCorrect = q.IsCorrect
                }).ToList()
            };

            return View("Result", vm);
        }






    }
}
