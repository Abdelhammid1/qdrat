using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Enums.Abstractions;
using QdratNew.Extensions;
using QdratNew.Helpers;
using QdratNew.Services;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Exams.Readers;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Question;
using QdratNew.ViewModels.Reports;
using QdratNew.ViewModels.Students;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class ExamsController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        //private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStudentRankingService _studentRankingService;
        private readonly ITimeZoneService _timeZoneService;
        private readonly IStudentExamStatisticsService _studentExamStatisticsService;
        private readonly IExamRecommendationService _recommendationService;
        private readonly StudentAnalyticsHelper _analyticsHelper;
        private readonly Services.Exams.Abstractions.IStudentExamDashboardService _studentExamDashboardService;
        private readonly IMemoryCache _cache;
        private readonly IExamAttendanceTracker _examAttendanceTracker; // ✅ الخدمة الجديدة
        private readonly IStudentExamStatusService _examStatusService;  // ⭐ الخدمة الجديدة
        private readonly IStudentProgressService _studentProgressService;
        private readonly IExamResultEngine _examResultEngine;
        private readonly IExamResultReader _examResultReader;
        private readonly ILogger<ExamsController> _logger;

        public object overallScore { get; private set; }
        public object avgSolveMinutes { get; private set; }
        public object timeUsagePercent { get; private set; }
        public double avgTotalMinutes { get; private set; }

        public ExamsController(IDbContextFactory<ApplicationDbContext> contextFactory, UserManager<ApplicationUser> userManager, IStudentRankingService studentRankingService, ITimeZoneService timeZoneService, IStudentExamStatisticsService studentExamStatisticsService, StudentAnalyticsHelper analyticsHelper, Services.Exams.Abstractions.IStudentExamDashboardService studentExamDashboardService, IMemoryCache cache, IExamAttendanceTracker examAttendanceTracker, IStudentProgressService studentProgressService, IExamResultEngine examResultEngine, IExamResultReader examResultReader, ILogger<ExamsController> logger)
        {
            _contextFactory = contextFactory;
            _userManager = userManager;
            _studentRankingService = studentRankingService;
            _timeZoneService = timeZoneService;
            _studentExamStatisticsService = studentExamStatisticsService;
            _analyticsHelper = analyticsHelper;
            _studentExamDashboardService = studentExamDashboardService;
            _cache = cache;
            _examAttendanceTracker = examAttendanceTracker;
            _examStatusService = new StudentExamStatusService(_contextFactory, _timeZoneService);
            _studentProgressService = studentProgressService;
            _examResultEngine = examResultEngine;
            _examResultReader = examResultReader;
            _logger = logger;
        }

        private async Task<int?> GetCurrentStudentIdAsync()
        {
            using var _context = _contextFactory.CreateDbContext();

            var userId = _userManager.GetUserId(User);
            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.UserId == userId);
            return student?.StudentID;
        }
        public async Task<IActionResult> Index()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var now = _timeZoneService.GetNowSaudi();

            // ✅ جلب التكليفات النشطة والمفتوحة زمنيًا فقط
            var rawData = await (
                from ea in _context.ExamAssignments
                join exam in _context.Exams on ea.ExamId equals exam.Id
                join batch in _context.ExamAssignmentsToBatches on ea.ExamId equals batch.ExamId
                where ea.StudentId == studentId
                      && exam.IsActive
                      && (batch.ScheduledDate == null
                          || (batch.ScheduledDate <= now
                              && batch.ScheduledDate.Value.AddMinutes(batch.DurationMinutes) >= now))
                orderby ea.AssignedAt descending
                select new
                {
                    Exam = exam,
                    Assignment = ea,
                    Batch = batch,
                    Status = _context.ExamStudentStatuses
                        .Where(s => s.ExamAssignmentId == batch.Id && s.StudentId == studentId)
                        .Select(s => s.IsSubmitted)
                        .FirstOrDefault()
                }
            ).ToListAsync();



            var viewModels = rawData.Select(x => new StudentExamCardViewModel
            {
                ExamId = x.Exam.Id,
                ExamAssignmentId = x.Batch.Id,
                Title = x.Exam.Title,
                AssignedAt = x.Assignment.AssignedAt,
                IsOnline = x.Batch.IsOnline,
                IsCompleted = x.Status,
                RequiresPassword = !x.Batch.IsOnline
            }).ToList();

            return View(viewModels);
        }


        [HttpGet]
        public async Task<IActionResult> LessonQuestions(int lessonId, int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (lessonId <= 0 || examAssignmentId <= 0)
                return NotFound();

            // 🔹 تحقق من صلاحية الطالب الحالي
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🔹 اجلب بيانات الأسئلة
            var questions = await (
     from a in _context.QuestionAttemptNew
     join q in _context.Questions on a.QuestionId equals q.Id
     join l in _context.Lessons on q.LessonId equals l.Id
     join s in _context.Sections on l.SectionId equals s.Id
     where a.StudentId == studentId
           && l.Id == lessonId
           && a.ExamAssignmentId == examAssignmentId
     select new QdratNew.ViewModels.Exam.ExamReviewQuestionVm
     {
         QuestionId = q.Id,
         QuestionTitle = q.Title,
         IsCorrect = a.IsCorrect,
         StudentAnswer = a.SelectedAnswer,

         // ✅ الحل الصحيح المعتمد على الكيان الحقيقي
         CorrectAnswer = q.CorrectAnswer ?? "",

         TimeTakenSeconds = a.TimeTakenSeconds,
         ImageUrl = q.ImageUrl,
         DisplayType = (QdratNew.Enums.QuestionDisplayType)
    ((int)q.Template == (int)QdratNew.Enums.QuestionTemplate.CompareValues ? 1 :
     (int)q.Template == (int)QdratNew.Enums.QuestionTemplate.CompareWithImage ? 2 : 0),



         ComparisonValue1 = q.ValueA,
         ComparisonValue2 = q.ValueB,
         VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
         Options = q.Options.Select(opt => new QdratNew.ViewModels.Homework.QuestionOptionVm
         {
             Text = opt.Text,
             ImageUrl = opt.ImageUrl
         }).ToList(),
         IsQuantitative = q.IsQuantitative
     }
 ).ToListAsync();



            if (!questions.Any())
                return NotFound("لا توجد أسئلة لهذا المؤشر في هذا الاختبار.");

            // 🔹 إعداد الـ ViewModel النهائي
            var vm = new QdratNew.ViewModels.Exam.ExamReviewViewModel
            {
                LessonTitle = await _context.Lessons
                    .Where(x => x.Id == lessonId)
                    .Select(x => x.Title)
                    .FirstOrDefaultAsync(),
                SectionTitle = await (
                    from l in _context.Lessons
                    join s in _context.Sections on l.SectionId equals s.Id
                    where l.Id == lessonId
                    select s.Title
                ).FirstOrDefaultAsync(),
                LessonId = lessonId,
                ExamAssignmentId = examAssignmentId,
                Questions = questions,
                TotalQuestions = questions.Count,
                TimeSpentMinutes = Math.Round(questions.Sum(q => q.TimeTakenSeconds) / 60.0, 1)
            };

            return View(vm);
        }



        [HttpGet]
        public async Task<IActionResult> AllExams()
        {
            var studentId = await GetCurrentStudentIdAsync();
            var vm = await _examStatusService.GetAllExamsAsync(studentId.Value);
            return View(vm);
        }


        [HttpGet]
        public async Task<IActionResult> ExamSectionsReport(int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🔵 تحديد هل الاختبار فردي أم دفعة
            bool isIndividual = await _context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId.Value);

            // 🔵 جلب جميع الأسئلة لهذا التعيين
            var allQuestions = await (
                from eq in _context.ExamQuestions
                join q in _context.Questions on eq.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where
                    (!isIndividual && eq.ExamAssignmentId == examAssignmentId) ||
                    (isIndividual && eq.ExamAssignmentToStudentId == examAssignmentId)
                select new
                {
                    SectionId = s.Id,
                    SectionTitle = s.Title
                }
            ).ToListAsync();

            // 🔵 جلب محاولات الطالب
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where
                    a.StudentId == studentId &&
                    (
                        (!isIndividual && a.ExamAssignmentId == examAssignmentId) ||
                        (isIndividual && a.ExamAssignmentToStudentId == examAssignmentId)
                    )
                select new
                {
                    SectionId = s.Id,
                    SectionTitle = s.Title,
                    IsCorrect = a.IsCorrect,
                    HasAttempt = true
                }
            ).ToListAsync();

            // 🔵 دمج الأسئلة مع المحاولات ومعالجة السكّيب
            var merged = allQuestions
                .GroupJoin(
                    attempts,
                    q => q.SectionId,
                    a => a.SectionId,
                    (q, a) => new { q.SectionId, q.SectionTitle, Attempts = a.ToList() }
                )
                .ToList();

            // 🔵 التجميع النهائي
            var grouped = merged
                .GroupBy(x => new { x.SectionId, x.SectionTitle })
                .Select(g => new ExamSectionSummaryVm
                {
                    SectionId = g.Key.SectionId,
                    SectionTitle = g.Key.SectionTitle,
                    TotalQuestions = g.Count(),
                    Correct = g.Sum(y => y.Attempts.Count(z => z.IsCorrect)),
                    Wrong = g.Sum(y => y.Attempts.Count(z => z.HasAttempt && !z.IsCorrect)),
                    Skipped = g.Count() - g.Sum(y => y.Attempts.Count())
                })
                .OrderBy(x => x.SectionTitle)
                .ToList();

            ViewBag.ExamAssignmentId = examAssignmentId;
            ViewBag.IsIndividual = isIndividual;

            return View(grouped);
        }


        [HttpGet]
        public async Task<IActionResult> ExamSectionLessonsReport(int examAssignmentId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            bool isIndividual = await _context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId.Value);

            // 🔵 الأسئلة داخل هذا المحور
            var questions = await (
                from eq in _context.ExamQuestions
                join q in _context.Questions on eq.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join attempt in _context.QuestionAttemptNew
                    .Where(x =>
                        x.StudentId == studentId &&
                        (
                            (!isIndividual && x.ExamAssignmentId == examAssignmentId) ||
                            (isIndividual && x.ExamAssignmentToStudentId == examAssignmentId)
                        )
                    )
                    on q.Id equals attempt.QuestionId into gj
                from att in gj.DefaultIfEmpty()
                where
                    (
                        (!isIndividual && eq.ExamAssignmentId == examAssignmentId) ||
                        (isIndividual && eq.ExamAssignmentToStudentId == examAssignmentId)
                    )
                    && l.SectionId == sectionId
                select new
                {
                    LessonId = l.Id,
                    LessonTitle = l.Title,
                    IsCorrect = att != null && att.IsCorrect,
                    HasAttempt = att != null
                }
            ).ToListAsync();

            var grouped = questions
                .GroupBy(q => new { q.LessonId, q.LessonTitle })
                .Select(g => new ExamLessonSummaryVm
                {
                    LessonId = g.Key.LessonId,
                    LessonTitle = g.Key.LessonTitle,
                    TotalQuestions = g.Count(),
                    Correct = g.Count(x => x.IsCorrect),
                    Wrong = g.Count(x => x.HasAttempt && !x.IsCorrect),
                    Skipped = g.Count(x => !x.HasAttempt),
                })
                .OrderBy(x => x.LessonTitle)
                .ToList();

            ViewBag.SectionTitle = await _context.Sections
                .Where(s => s.Id == sectionId)
                .Select(s => s.Title)
                .FirstOrDefaultAsync();

            ViewBag.ExamAssignmentId = examAssignmentId;
            ViewBag.IsIndividual = isIndividual;

            return View(grouped);
        }



        [HttpGet]
        public async Task<IActionResult> LoadReviewSection(string section, int assignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Unauthorized();

            section = section.Trim();

            var questions = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions.Include(q => q.Options).Include(q => q.VerbalPassage)
                    on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId
                      && a.ExamAssignmentId == assignmentId
                      && s.Title == section
                select new ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,
                    StudentAnswer = a.SelectedAnswer,
                    CorrectAnswer = q.CorrectAnswer,
                    IsCorrect = a.IsCorrect,
                    IsQuantitative = q.IsQuantitative,
                    ImageUrl = q.ImageUrl,
                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,
                    VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,

                    DisplayType =
                        q.Template == QuestionTemplate.CompareValues
                            ? QuestionDisplayType.ComparisonText
                        : q.Template == QuestionTemplate.CompareWithImage
                            ? QuestionDisplayType.ComparisonWithImage
                        : QuestionDisplayType.WithImage,

                    Options = q.Options.Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                }
            ).ToListAsync();

            // ➤ ترتيب الأسئلة داخل المحور
            int order = 1;
            foreach (var q in questions.OrderBy(x => x.QuestionTitle))
                q.QuestionOrder = order++;

            return PartialView("~/Views/Shared/_ExamReviewQuestionsButtons.cshtml", questions);
        }


        [HttpGet]
        public async Task<IActionResult> LoadReviewQuestion(int id, Guid qid)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Unauthorized();

            var vm = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions
                    .Include(q => q.Options)
                    .Include(q => q.VerbalPassage)
                    on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId
                      && a.ExamAssignmentId == id         // ✔ اختبارات الدفعة
                      && q.Id == qid
                select new ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title != null ? System.Net.WebUtility.HtmlDecode(q.Title) : "",
                    StudentAnswer = a.SelectedAnswer,
                    CorrectAnswer = q.CorrectAnswer,
                    IsCorrect = a.IsCorrect,
                    IsQuantitative = q.IsQuantitative,

                    ImageUrl = q.ImageUrl,
                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,

                    VerbalPassageContent = q.VerbalPassage != null
                        ? q.VerbalPassage.Content
                        : null,

                    DisplayType =
                        q.Template == QuestionTemplate.CompareValues
                            ? QuestionDisplayType.ComparisonText
                        : q.Template == QuestionTemplate.CompareWithImage
                            ? QuestionDisplayType.ComparisonWithImage
                        : QuestionDisplayType.WithImage,

                    Options = q.Options.Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                }
            ).FirstOrDefaultAsync();

            if (vm == null)
                return Content("خطأ: لم يتم تحميل بيانات السؤال.");

            return PartialView("~/Views/Shared/_ExamReviewQuestionPartial.cshtml", vm);
        }






        [HttpGet]
        public async Task<IActionResult> ExamReview(int id)
        {
            using var context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var summary = await _studentExamStatisticsService
                .GetExamResultAsync(id, studentId.Value);

            // ======================================================
            // 🔑 تحميل الأسئلة + Attempts (بما فيها المتخطاة)
            // ======================================================
            var questions = await (
                from eq in context.ExamQuestions

                join q in context.Questions
                    .Include(x => x.Options)
                    .Include(x => x.VerbalPassage)
                    .Include(x => x.Lesson)
                        .ThenInclude(l => l.Section)
                    on eq.QuestionId equals q.Id

                join a in context.QuestionAttemptNew
                    .Where(x =>
                        x.StudentId == studentId.Value &&
                        x.ExamAssignmentId == id)
                    on q.Id equals a.QuestionId into gj
                from attempt in gj
                    .OrderByDescending(x => x.AttemptedAt)
                    .Take(1)
                    .DefaultIfEmpty()

                where eq.ExamAssignmentId == id

                orderby eq.Id

                select new ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,

                    StudentAnswer = attempt != null ? attempt.SelectedAnswer : null,
                    CorrectAnswer = q.CorrectAnswer,

                    IsCorrect = attempt != null && attempt.IsCorrect,

                    LessonTitle = q.Lesson.Title,
                    SectionTitle = q.Lesson.Section.Title,
                    IsQuantitative = q.IsQuantitative,

                    TimeTakenSeconds = attempt != null
                        ? attempt.TimeTakenSeconds
                        : 0,

                    // القطعة اللفظية
                    VerbalPassageTitle = q.VerbalPassage != null
                        ? q.VerbalPassage.Title
                        : null,

                    VerbalPassageContent = q.VerbalPassage != null
                        ? q.VerbalPassage.Content
                        : null,

                    Options = q.Options.Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.Text == q.CorrectAnswer,
                        IsSelectedByStudent = attempt != null && o.Text == attempt.SelectedAnswer
                    }).ToList()
                }
            )
            .AsNoTracking()
            .ToListAsync();

            var examTitle = await context.ExamAssignmentsToBatches
                .Where(e => e.Id == id)
                .Select(e => e.Exam.Title)
                .FirstOrDefaultAsync() ?? "اختبار";

            var vm = new ExamReviewViewModel
            {
                ExamAssignmentId = id,
                ExamTitle = examTitle,

                TotalQuestions = summary.TotalQuestions,
                CorrectAnswers = summary.CorrectAnswers,
                WrongAnswers = summary.WrongAnswers,
                SkippedQuestions = summary.SkippedQuestions,

                ScorePercentage = summary.ScorePercentage,
                AverageTimePerQuestion = summary.AverageTimePerQuestion,
                TimeSpentFormatted = summary.SolveMinutes > 0
                    ? $"{summary.SolveMinutes} دقيقة تقريبًا"
                    : "غير محدد",

                Questions = questions
            };

            return View("ExamReview", vm);
        }


        [HttpGet]
        public async Task<IActionResult> ExamSectionQuestions(int examAssignmentId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🟦 جلب الأسئلة الخاصة بالمحور بناءً على محاولات الطالب
            var questions = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId
                      && a.ExamAssignmentId == examAssignmentId
                      && s.Id == sectionId
                select new ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,
                    StudentAnswer = a.SelectedAnswer,
                    CorrectAnswer = q.CorrectAnswer,
                    IsCorrect = a.IsCorrect,
                    ImageUrl = q.ImageUrl,
                    DisplayType = (QdratNew.Enums.QuestionDisplayType)
                        ((int)q.Template == (int)QdratNew.Enums.QuestionTemplate.CompareValues ? 1 :
                         (int)q.Template == (int)QdratNew.Enums.QuestionTemplate.CompareWithImage ? 2 : 0),
                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,
                    VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                    Options = q.Options.Select(o => new QdratNew.ViewModels.Homework.QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList(),
                    TimeTakenSeconds = a.TimeTakenSeconds,
                    IsQuantitative = q.IsQuantitative
                }
            ).ToListAsync();

            if (!questions.Any())
                return Content("لا توجد أسئلة لهذا المحور.");

            var sectionTitle = await _context.Sections
                .Where(x => x.Id == sectionId)
                .Select(x => x.Title)
                .FirstOrDefaultAsync();

            var vm = new ExamReviewViewModel
            {
                ExamAssignmentId = examAssignmentId,
                SectionTitle = sectionTitle,
                Questions = questions,
                TotalQuestions = questions.Count,
                TimeSpentMinutes = Math.Round(questions.Sum(q => q.TimeTakenSeconds) / 60.0, 1)
            };

            return View("ExamSectionQuestions", vm);
        }





        [HttpGet]
        public async Task<IActionResult> Review(int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var rawAttempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions.Include(q => q.Lesson).ThenInclude(l => l.Section)
                    on a.QuestionId equals q.Id
                where a.StudentId == studentId && a.ExamAssignmentId == examAssignmentId
                select new
                {
                    q.Id,
                    q.Title,
                    a.SelectedAnswer,
                    a.IsCorrect,
                    q.CorrectAnswer,
                    q.IsQuantitative,
                    LessonTitle = q.Lesson.Title,
                    SectionTitle = q.Lesson.Section.Title
                }
            ).ToListAsync();

            if (!rawAttempts.Any())
                return Content("<div class='alert alert-warning text-center'>⚠️ لا توجد محاولات لهذا الاختبار.</div>", "text/html");

            var vm = new ExamReviewViewModel
            {
                ExamAssignmentId = examAssignmentId,
                ExamTitle = await _context.ExamAssignmentsToBatches
                    .Where(e => e.Id == examAssignmentId)
                    .Select(e => e.Exam.Title)
                    .FirstOrDefaultAsync() ?? "اختبار",
                TotalQuestions = rawAttempts.Count,
                Questions = rawAttempts.Select(x => new ExamReviewQuestionVm
                {
                    QuestionId = x.Id,
                    QuestionTitle = x.Title,
                    StudentAnswer = x.SelectedAnswer,
                    CorrectAnswer = x.CorrectAnswer,
                    IsCorrect = x.IsCorrect,
                    IsQuantitative = x.IsQuantitative,
                    LessonTitle = x.LessonTitle,
                    SectionTitle = x.SectionTitle
                }).ToList()
            };

            return View("Review", vm);
        }

        [HttpGet]
        public async Task<IActionResult> ExamReport(int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ======================================================
            // 1) تحديد هل فردي أم دفعة (كما هو)
            // ======================================================
            bool isIndividual = await _context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId.Value);

            Exam exam;
            string examTitle;
            int durationMinutes;
            DateTime examDate;

            if (isIndividual)
            {
                var assign = await _context.ExamAssignmentsToStudents
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == examAssignmentId && a.StudentId == studentId.Value);

                if (assign == null)
                    return NotFound();

                exam = assign.Exam;
                examTitle = exam.Title;
                durationMinutes = assign.DurationMinutes;
                examDate = assign.ScheduledDate == default ? assign.CreatedAt : assign.ScheduledDate.Value;
            }
            else
            {
                var assign = await _context.ExamAssignmentsToBatches
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == examAssignmentId);

                if (assign == null)
                    return NotFound();

                exam = assign.Exam;
                examTitle = assign.Title ?? exam.Title;
                durationMinutes = assign.DurationMinutes;
                examDate = assign.ScheduledDate ?? assign.AssignedAt;
            }

            // ======================================================
            // 2) قراءة النتيجة الجاهزة (Snapshot)
            // ======================================================
            var status = await _context.ExamStudentStatuses
    .AsNoTracking()
    .FirstOrDefaultAsync(s =>
        s.StudentId == studentId.Value &&
        s.ExamAssignmentId == examAssignmentId &&
        (s.Status == ExamStatus.Completed || s.IsSubmitted));


            if (status == null)
            {
                return RedirectToAction("ErrorMessage",
                    new { msg = "لم يتم العثور على نتيجة لهذا الاختبار." });
            }

            // 🟡 حالة اختبار قديم بدون Snapshot
            if (string.IsNullOrEmpty(status.Note))
            {
                // نولّد Snapshot مرة واحدة فقط
                await _examResultEngine.GenerateSnapshotIfMissingAsync(
                    examAssignmentId,
                    studentId.Value
                );

                // إعادة تحميل الحالة بعد التوليد
                status = await _context.ExamStudentStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == studentId.Value &&
                        s.ExamAssignmentId == examAssignmentId);

                if (status == null || string.IsNullOrEmpty(status.Note))
                {
                    return RedirectToAction("ErrorMessage",
                        new { msg = "تعذر إنشاء تقرير لهذا الاختبار." });
                }
            }



            var snapshot = System.Text.Json.JsonSerializer
                .Deserialize<ExamFinalResultDto>(status.Note);

            if (snapshot == null)
                return RedirectToAction("ErrorMessage", new { msg = "بيانات التقرير غير صالحة." });

            // ======================================================
            // 3) الوقت والنسبة (محسوبة مسبقًا)
            // ======================================================
            double solveMinutes = snapshot.TotalTimeSeconds > 0
     ? Math.Round(snapshot.TotalTimeSeconds / 60.0, 1)
     : 0;


            double percentTime = durationMinutes > 0
                ? Math.Round((solveMinutes / durationMinutes) * 100, 1)
                : 0;

            // ======================================================
            // 4) توصيات الذكاء الاصطناعي (كما هي)
            // ======================================================
            var rec = _recommendationService.GetRecommendation(
                snapshot.ScorePercent,
                (int)solveMinutes,
                durationMinutes
            );
            var sectionTitles = await _context.Sections
    .Where(s => snapshot.Sections.Keys.Contains(s.Id))
    .Select(s => new { s.Id, s.Title })
    .ToDictionaryAsync(x => x.Id, x => x.Title);

            // ======================================================
            // 5) بناء أداء المحاور من Snapshot (بدون Attempts)
            // ======================================================
            var sectionStats = snapshot.Sections.Select(sec => new ExamSectionPerformancesVm
            {
                SectionId = sec.Key,
                SectionName = sectionTitles.TryGetValue(sec.Key, out var title)
    ? title
    : "محور غير معروف",
                TotalQuestions = sec.Value.Correct + sec.Value.Wrong + sec.Value.Skipped,
                CorrectAnswers = sec.Value.Correct,
                WrongAnswers = sec.Value.Wrong + sec.Value.Skipped,
                Skipped = sec.Value.Skipped,
                AccuracyPercent = (sec.Value.Correct + sec.Value.Wrong + sec.Value.Skipped) == 0
                    ? 0
                    : Math.Round(sec.Value.Correct * 100.0 /
                        (sec.Value.Correct + sec.Value.Wrong + sec.Value.Skipped), 1)
            }).ToList();



            // ======================================================
            // 📊 إعادة بناء بيانات الشارت (كمي / لفظي)
            // ======================================================
            var chartRows = await (
                from eq in _context.ExamQuestions
                join q in _context.Questions
                    .Include(x => x.Lesson)
                        .ThenInclude(l => l.Section)
                    on eq.QuestionId equals q.Id
                where eq.ExamAssignmentId == examAssignmentId
                select new
                {
                    q.IsQuantitative,
                    SectionTitle = q.Lesson.Section.Title
                }
            ).ToListAsync();

            var quantGroups = chartRows
                .Where(x => x.IsQuantitative)
                .GroupBy(x => x.SectionTitle)
                .ToList();

            var verbalGroups = chartRows
                .Where(x => !x.IsQuantitative)
                .GroupBy(x => x.SectionTitle)
                .ToList();

            bool hasQuantChart = quantGroups.Any();
            bool hasVerbalChart = verbalGroups.Any();



            // ======================================================
            // 6) بناء ViewModel النهائي
            // ======================================================
            var vm = new ExamDetailedReportViewModel
            {
                StudentId = studentId.Value,
                StudentName = await _context.Students
                    .Where(s => s.StudentID == studentId.Value)
                    .Select(s => s.FullName)
                    .FirstOrDefaultAsync(),

                ExamAssignmentId = examAssignmentId,
                ExamTitle = examTitle,
                ExamDate = examDate,
                IsIndividual = isIndividual,

                TotalQuestions = snapshot.TotalQuestions,
                TotalCorrect = snapshot.Correct,
                TotalWrong = snapshot.Wrong + snapshot.Skipped,
                TotalSkipped = snapshot.Skipped,

                TotalScore = snapshot.Correct,
                MaxScore = snapshot.TotalQuestions,

                SolveMinutes = solveMinutes,
                TotalMinutes = durationMinutes,
                PercentTime = percentTime,
                OverallPercent = snapshot.ScorePercent,

                SectionPerformances = sectionStats,

                HasQuantChart = hasQuantChart,
                HasVerbalChart = hasVerbalChart,

                QuantLabels = quantGroups.Select(g => g.Key).ToList(),
                QuantCorrectCounts = quantGroups.Select(g =>
                    snapshot.Sections.Values.Sum(s => s.Correct)
                        ).ToList(),
                QuantWrongCounts = quantGroups.Select(g =>
                    snapshot.Sections.Values.Sum(s => s.Wrong + s.Skipped)
).ToList(),

                VerbalLabels = verbalGroups.Select(g => g.Key).ToList(),
                VerbalCorrectCounts = verbalGroups.Select(g =>
                    snapshot.Sections.Values.Sum(s => s.Correct)
).ToList(),
                VerbalWrongCounts = verbalGroups.Select(g =>
                    snapshot.Sections.Values.Sum(s => s.Wrong + s.Skipped)
).ToList(),


                TrackCard1 = rec.TrackCard1,
                TrackCard1Desc = rec.TrackCard1Desc,
                TrackCard2 = rec.TrackCard2,
                TrackCard2Desc = rec.TrackCard2Desc,
                SpeedLabel = rec.SpeedLabel,
                SpeedNote = rec.SpeedNote
            };

            return View("ExamReport", vm);
        }



        [HttpGet]
        public async Task<IActionResult> Report(int examAssignmentId, int? studentId = null)
        {
            if (!studentId.HasValue)
            {
                studentId = await GetCurrentStudentIdAsync();
                if (!studentId.HasValue)
                    return RedirectToAction("Login", "Account", new { area = "" });
            }

            var vm = await BuildExamReportViewModel(examAssignmentId, studentId.Value);
            if (vm == null) return NotFound();

            return View("Report", vm); // 👈 هيستخدم نفس فيو التقرير بتاع الواجب
        }

        private async Task<HomeworkReportViewModel> BuildExamReportViewModel(int examAssignmentId, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var examAssignment = await _context.ExamAssignmentsToBatches
                .Include(e => e.Exam)
                .FirstOrDefaultAsync(e => e.Id == examAssignmentId);

            if (examAssignment == null) return null;

            // 🟢 هات المحاولات
            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.ExamAssignmentId == examAssignmentId)
                .Include(a => a.Question)
                .ToListAsync();

            int total = attempts.Count;
            int correct = attempts.Count(a => a.IsCorrect);
            int wrong = total - correct;
            double percentage = total > 0 ? Math.Round((double)correct / total * 100, 2) : 0;

            int minutes = (int)Math.Round(attempts.Sum(a => a.TimeTakenSeconds) / 60.0);

            // 🟢 أداء حسب المحاور
            var sectionScores = attempts
                .GroupBy(a => a.Question.SectionId)
                .Select(g => new
                {
                    SectionName = _context.Sections.FirstOrDefault(s => s.Id == g.Key)?.Title,
                    Accuracy = g.Count() > 0 ? Math.Round((double)g.Count(x => x.IsCorrect) * 100 / g.Count(), 2) : 0
                })
                .ToDictionary(x => x.SectionName ?? "-", x => x.Accuracy);

            return new HomeworkReportViewModel
            {
                HomeworkSetId = examAssignmentId,
                Title = examAssignment.Exam?.Title ?? "اختبار",
                CreatedAt = examAssignment.Exam?.CreatedAt ?? DateTime.Now,
                BatchName = "-", // ممكن تجيب اسم الدفعة لو عايز
                CurriculumTitle = "-", // ممكن تجيب المنهج لو عايز
                StudentName = await _context.Students
                    .Where(s => s.StudentID == studentId)
                    .Select(s => s.FullName)
                    .FirstOrDefaultAsync(),

                CorrectAnswers = correct,
                WrongAnswers = wrong,
                Percentage = percentage,
                TimeSpentMinutes = minutes,
                Questions = attempts.Select(a => new HomeworkReportQuestionVm
                {
                    QuestionId = a.QuestionId,
                    QuestionText = a.Question.Title,
                    StudentAnswer = a.SelectedAnswer,
                    IsCorrect = a.IsCorrect
                }).ToList(),
                SectionScores = sectionScores
            };
        }


        [HttpPost]
        public async Task<IActionResult> VerifyExamPassword(int examId, string password)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var assignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a => a.ExamId == examId);

            if (assignment == null || string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "❌ كلمة المرور غير صالحة أو لا يوجد اختبار.";
                return RedirectToAction("Index");
            }

            // 🔐 تحقق من كلمة المرور (قارن مع كلمة المرور المخزنة في جدول الاختبارات أو اجعلها ثابتة مؤقتًا)
            if (password.Trim() != "qdrat2025") // مثال: كلمة مرور ثابتة أو ديناميكية
            {
                TempData["ErrorMessage"] = "❌ كلمة المرور غير صحيحة.";
                return RedirectToAction("Index");
            }

            // ✅ إعادة التوجيه إلى StartExam
            return RedirectToAction("StartExam", new { examAssignmentId = examId });
        }

        [HttpGet]
        public async Task<IActionResult> StartExam(int examAssignmentId, Guid? q = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == examAssignmentId);

            if (assignment == null || assignment.Exam == null)
                return RedirectToAction("ErrorMessage", new { msg = "لم يتم العثور على بيانات الاختبار." });

            if (assignment.IsInLab)
            {
                var verifiedKey = $"VerifiedGeneralExam_{examAssignmentId}";
                if (HttpContext.Session.GetString(verifiedKey) != "true")
                    return RedirectToAction("VerifyGeneralExamCode", new { id = examAssignmentId });
            }

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            await _examAttendanceTracker.MarkStudentPresentAsync(examAssignmentId, studentId.Value);

            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId.Value &&
                    s.ExamAssignmentId == examAssignmentId);

            var now = DateTime.UtcNow;

            // ✅ إنشاء الحالة لأول مرة
            if (status == null)
            {
                status = new ExamStudentStatus
                {
                    StudentId = studentId.Value,
                    ExamId = assignment.Exam.Id,
                    ExamAssignmentId = examAssignmentId,
                    StartedAt = now,
                    EndAt = now.AddMinutes(assignment.DurationMinutes),
                    Status = ExamStatus.InProgress,
                    IsSubmitted = false
                };

                _context.ExamStudentStatuses.Add(status);
                await _context.SaveChangesAsync();
            }
            else
            {
                // ✅ معالجة الحالات القديمة (Migration Logic)
                if (!status.StartedAt.HasValue)
                {
                    status.StartedAt = now;
                }

                if (!status.EndAt.HasValue)
                {
                    status.EndAt = status.StartedAt.Value
                        .AddMinutes(assignment.DurationMinutes);

                    await _context.SaveChangesAsync();
                }
            }

            if (status.IsSubmitted || status.Status == ExamStatus.Completed)
            {
                return RedirectToAction("ExamResult", "StudentExamResults",
                    new { area = "Students", id = examAssignmentId });
            }

            if (now >= status.EndAt.Value)
            {
                try
                {
                    await _examResultEngine.FinalizeAsync(new ExamResultContext
                    {
                        StudentId = studentId.Value,
                        ExamAssignmentId = examAssignmentId,
                        Kind = ExamKind.General
                    }, reviewSeconds: 0);
                }
                catch { }

                return RedirectToAction("ExamResult", "StudentExamResults",
                    new { area = "Students", id = examAssignmentId });
            }

            var remainingSeconds = Math.Max(
                0,
                (int)(status.EndAt.Value - now).TotalSeconds
            );

            var questionIds = await _context.ExamQuestions
                .Where(eq => eq.ExamAssignmentId == examAssignmentId)
                .OrderBy(eq => eq.Order)
                .ThenBy(eq => eq.QuestionId)
                .Select(eq => eq.QuestionId)
                .ToListAsync();

            if (!questionIds.Any())
                return RedirectToAction("ErrorMessage", new { msg = "⚠️ لا توجد أسئلة لهذا الاختبار." });

            var sessionKey = $"ExamQuestions_{examAssignmentId}_{studentId.Value}";
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(questionIds));

            var currentQuestion = q ?? questionIds.First();

            var vm = await GetExamSolveViewModel(examAssignmentId, currentQuestion, studentId.Value);
            vm.ExamId = assignment.Exam.Id;
            vm.ExamTitle = assignment.Exam.Title;
            vm.RemainingSeconds = remainingSeconds;
            vm.IsIndividual = false;
            vm.SubmitAnswerUrl = "/Students/Exams/SubmitExamAnswerFetch";
            vm.FinalSubmitUrl = "/Students/Exams/SubmitFinalExam";
            vm.ExamType = "General";

            Response.Headers["Cache-Control"] = "no-cache,no-store,must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return View("StartExam", vm);
        }

        // =====================================================
        // إعادة محاولة اختبار عام/دفعة — يحتفظ بأفضل درجة سابقة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> RetakeExam(int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var assignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a => a.Id == examAssignmentId);

            if (assignment == null)
                return RedirectToAction("ErrorMessage", new { msg = "لم يتم العثور على بيانات الاختبار." });

            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId.Value &&
                    s.ExamAssignmentId == examAssignmentId);

            if (status == null || !status.IsSubmitted || status.Status != ExamStatus.Completed)
                return RedirectToAction("ErrorMessage", new { msg = "لا يمكن إعادة محاولة اختبار لم يتم إكماله بعد." });

            var now = DateTime.UtcNow;

            status.StartedAt = now;
            status.EndAt = now.AddMinutes(assignment.DurationMinutes);
            status.IsSubmitted = false;
            status.Status = ExamStatus.InProgress;
            status.SubmittedAt = null;
            status.ReviewSeconds = 0;
            status.ReviewBehaviorJson = null;
            status.AttemptCount += 1;

            var previousAttempts = _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId.Value && a.ExamAssignmentId == examAssignmentId);
            _context.QuestionAttemptNew.RemoveRange(previousAttempts);

            await _context.SaveChangesAsync();

            HttpContext.Session.Remove($"ExamReviewMarked_{examAssignmentId}_{studentId.Value}");

            return RedirectToAction("StartExam", new { examAssignmentId });
        }


        [HttpGet]
        public async Task<IActionResult> VerifyGeneralExamCode(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            // البحث أولاً في اختبارات الدُفعات
            var batchAssignment = await _context.ExamAssignmentsToBatches
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (batchAssignment != null)
                return View("VerifyGeneralExamCode", id);


            // البحث في الاختبارات الفردية
            var individualAssignment = await _context.ExamAssignmentsToStudents
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (individualAssignment != null)
                return View("VerifyGeneralExamCode", id);

            return RedirectToAction("ErrorMessage", new { msg = "لم يتم العثور على الاختبار." });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyGeneralExamCode(int id, string code)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (string.IsNullOrWhiteSpace(code))
                return RedirectToAction("ErrorMessage", new { msg = "لم يتم إدخال الكود." });

            code = code.Trim();

            // جلب الامتحان بالرقم المرجعي
            var exam = await _context.Exams
                .FirstOrDefaultAsync(e => e.ReferenceCode == code);

            if (exam == null)
                return RedirectToAction("ErrorMessage", new { msg = "❌ الكود غير صحيح." });

            // جلب الطالب
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // === محاولة إيجاد تكليف دُفعة
            var batchAssignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a => a.Id == id && a.ExamId == exam.Id);

            if (batchAssignment != null)
            {
                HttpContext.Session.SetString($"VerifiedGeneralExam_{batchAssignment.Id}", "true");
                return RedirectToAction("StartExam", new { examAssignmentId = batchAssignment.Id });
            }

            // === محاولة إيجاد تكليف فردي للطالب
            var individualAssignment = await _context.ExamAssignmentsToStudents
                .FirstOrDefaultAsync(a => a.Id == id
                                          && a.ExamId == exam.Id
                                          && a.StudentId == studentId);

            if (individualAssignment != null)
            {
                HttpContext.Session.SetString($"VerifiedGeneralExam_{individualAssignment.Id}", "true");
                return RedirectToAction("StartIndividualExam", new { examAssignmentId = individualAssignment.Id });
            }

            // لا يوجد أي تكليف مرتبط بهذا الكود
            return RedirectToAction("ErrorMessage", new { msg = "❌ هذا الكود لا يخصك أو لا يخص هذا الاختبار." });
        }


        // =======================================================
        // 🟦  StartIndividualExam  —  الاختبارات الفردية فقط
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> StartIndividualExam(int examAssignmentId, Guid? q = null)
        {
            using var _context = _contextFactory.CreateDbContext();



            // 🔐 التحقق من الكود المرجعي
            var verifiedKey = $"VerifiedGeneralExam_{examAssignmentId}";
            if (HttpContext.Session.GetString(verifiedKey) != "true")
            {
                return RedirectToAction("VerifyGeneralExamCode", new { id = examAssignmentId });
            }


            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // -------------------------------------------------------------
            // تحميل بيانات التكليف الفردي
            // -------------------------------------------------------------
            var assignment = await _context.ExamAssignmentsToStudents
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == examAssignmentId && a.StudentId == studentId.Value);

            if (assignment == null)
                return RedirectToAction("ErrorMessage", new { msg = "لا يمكن بدء هذا الاختبار." });

            var exam = assignment.Exam;

            // -------------------------------------------------------------
            // تحميل حالة الطالب في هذا الامتحان
            // ExamAssignmentId == null للامتحان الفردي
            // -------------------------------------------------------------
            var status = await _context.ExamStudentStatuses
                         .FirstOrDefaultAsync(s =>
                             s.StudentId == studentId &&
                             s.ExamAssignmentToStudentId == examAssignmentId &&
                             s.ExamId == exam.Id);

            // الطالب أنهى الامتحان مسبقًا → توجيه لصفحة النتيجة (يمكنه إعادة المحاولة من هناك)
            if (status != null && status.IsSubmitted && status.Status == ExamStatus.Completed)
            {
                return RedirectToAction("ExamResult", "StudentExamResults",
                    new { area = "Students", id = examAssignmentId });
            }

            // أول مرة يدخل الامتحان
            if (status == null)
            {
                status = new ExamStudentStatus
                {
                    StudentId = studentId.Value,
                    ExamId = exam.Id,
                    ExamAssignmentId = null,
                    ExamAssignmentToStudentId = examAssignmentId,
                    StartedAt = DateTime.UtcNow,
                    Status = ExamStatus.InProgress,
                    IsSubmitted = false
                };
                _context.ExamStudentStatuses.Add(status);
                await _context.SaveChangesAsync();
            }

            // -------------------------------------------------------------
            // حساب الوقت المتبقي (لا يوجد قيد على موعد النهاية بعد الآن)
            // -------------------------------------------------------------
            var elapsed = DateTime.UtcNow - (status.StartedAt ?? DateTime.UtcNow);
            var remainingSeconds = Math.Max(0, (assignment.DurationMinutes * 60) - (int)elapsed.TotalSeconds);

            // -------------------------------------------------------------
            // تحميل أسئلة الامتحان
            // -------------------------------------------------------------
            var questionIds = await _context.ExamQuestions
                .Where(eq => eq.ExamAssignmentToStudentId == examAssignmentId)
                .OrderBy(eq => eq.Order)
                .Select(eq => eq.QuestionId)
                .ToListAsync();

            var sessionKey = $"IndividualExam_{examAssignmentId}_{studentId}";
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(questionIds));

            if (!questionIds.Any())
            {
                return RedirectToAction("ErrorMessage", new { msg = "⚠️ لا توجد أسئلة لهذا الاختبار." });
            }


            Guid currentQ = q ?? questionIds.First();

            var vm = await GetExamSolveViewModel(examAssignmentId, currentQ, studentId.Value);

            vm.ExamId = exam.Id;
            vm.ExamTitle = exam.Title;
            vm.RemainingSeconds = remainingSeconds;
            vm.IsIndividual = true;



            Response.Headers["Cache-Control"] = "no-cache,no-store,must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return View("StartExam", vm);
        }

        // =====================================================
        // إعادة محاولة اختبار فردي — يحتفظ بأفضل درجة سابقة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> RetakeIndividualExam(int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var assignment = await _context.ExamAssignmentsToStudents
                .FirstOrDefaultAsync(a => a.Id == examAssignmentId && a.StudentId == studentId.Value);

            if (assignment == null)
                return RedirectToAction("ErrorMessage", new { msg = "لا يمكن إعادة محاولة هذا الاختبار." });

            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId.Value &&
                    s.ExamAssignmentToStudentId == examAssignmentId);

            if (status == null || !status.IsSubmitted || status.Status != ExamStatus.Completed)
                return RedirectToAction("ErrorMessage", new { msg = "لا يمكن إعادة محاولة اختبار لم يتم إكماله بعد." });

            status.StartedAt = DateTime.UtcNow;
            status.IsSubmitted = false;
            status.Status = ExamStatus.InProgress;
            status.SubmittedAt = null;
            status.ReviewSeconds = 0;
            status.ReviewBehaviorJson = null;
            status.AttemptCount += 1;

            var previousAttempts = _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId.Value && a.ExamAssignmentToStudentId == examAssignmentId);
            _context.QuestionAttemptNew.RemoveRange(previousAttempts);

            await _context.SaveChangesAsync();

            HttpContext.Session.Remove($"ExamReviewMarked_Individual_{examAssignmentId}_{studentId.Value}");

            return RedirectToAction("StartIndividualExam", new { examAssignmentId });
        }


        [HttpGet]
        public async Task<IActionResult> StartQuestion(int id, Guid? q = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ✅ جلب حالة الاختبار
            var status = await _context.ExamStudentStatuses
                .Include(s => s.Exam)
                .FirstOrDefaultAsync(s => s.ExamId == id && s.StudentId == studentId);

            if (status == null || status.IsSubmitted)
            {
                TempData["ErrorMessage"] = "🚫 لا يمكنك الدخول إلى هذا الاختبار.";
                return RedirectToAction("Index");
            }

            // ✅ جلب الأسئلة بالترتيب
            var examQuestions = await _context.ExamQuestions
                .Where(eq => eq.ExamId == id)
                .OrderBy(eq => eq.Order)
                .ToListAsync();

            var allIds = examQuestions.Select(eq => eq.QuestionId).ToList();

            if (!allIds.Any())
            {
                return RedirectToAction("ErrorMessage", new { msg = "لا توجد أسئلة مرتبطة بهذا الاختبار." });

            }

            // ✅ تحديد السؤال الحالي
            int currentIndex = 0;
            if (q.HasValue)
            {
                var idx = allIds.IndexOf(q.Value);
                if (idx >= 0) currentIndex = idx;
            }

            var currentQuestionId = allIds[currentIndex];

            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == currentQuestionId);

            var attempt = await _context.QuestionAttemptNew
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.QuestionId == currentQuestionId && a.ExamId == id);

            // ✅ ViewModel
            var vm = new ExamSolveViewModel
            {
                ExamId = id,
                CurrentQuestionId = currentQuestionId,
                AllQuestionIds = allIds,
                Question = question?.ToDisplayModel(),
                SelectedAnswer = attempt?.SelectedAnswer,
                DurationMinutes = status.Exam.DurationMinutes,
                StartTime = status.AssignedAt,
                IsSubmitted = status.IsSubmitted
            };

            return View("StartQuestion", vm);
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
        public async Task<IActionResult> ReviewLessonQuestions(int lessonId, int assignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🔹 جلب بيانات التعيين
            var assignment = await _context.ExamAssignments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null)
                return NotFound("⚠️ لم يتم العثور على بيانات التعيين.");

            // 🔹 جلب الأسئلة بناءً على المحاولات الفعلية للطالب
            var questions = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions.Include(q => q.Options).Include(q => q.VerbalPassage)
                    on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId
                      && a.ExamAssignmentId == assignmentId
                      && l.Id == lessonId
                select new QdratNew.ViewModels.Exam.ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,
                    IsCorrect = a.IsCorrect,
                    StudentAnswer = a.SelectedAnswer,
                    CorrectAnswer = q.CorrectAnswer ?? "",
                    TimeTakenSeconds = a.TimeTakenSeconds,
                    ImageUrl = q.ImageUrl,
                    DisplayType = (QdratNew.Enums.QuestionDisplayType)
                        ((int)q.Template == (int)QdratNew.Enums.QuestionTemplate.CompareValues ? 1 :
                         (int)q.Template == (int)QdratNew.Enums.QuestionTemplate.CompareWithImage ? 2 : 0),
                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,
                    VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                    Options = q.Options.Select(opt => new QdratNew.ViewModels.Homework.QuestionOptionVm
                    {
                        Text = opt.Text,
                        ImageUrl = opt.ImageUrl
                    }).ToList(),
                    IsQuantitative = q.IsQuantitative
                }
            ).ToListAsync();

            if (!questions.Any())
                return NotFound("❌ لا توجد أسئلة مرتبطة بهذا المؤشر في اختبار تحديد المستوى.");

            // 🔹 تجهيز العناوين
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

            // 🔹 بناء الـ ViewModel النهائي
            var vm = new QdratNew.ViewModels.Exam.ExamReviewViewModel
            {
                ExamTitle = "اختبار تحديد المستوى",
                LessonTitle = lessonTitle,
                SectionTitle = sectionTitle,
                LessonId = lessonId,
                ExamAssignmentId = assignmentId,
                Questions = questions,
                TotalQuestions = questions.Count,
                TimeSpentMinutes = Math.Round(questions.Sum(q => q.TimeTakenSeconds) / 60.0, 1)
            };

            return View("ReviewLessonQuestions", vm);
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

        [HttpPost]
        public async Task<IActionResult> SubmitFinalExam(int id, bool forceSubmit = false, int reviewSeconds = 0)
        {
            using var _context = _contextFactory.CreateDbContext();

            try
            {
                // ======================================================
                // 0) جلب الطالب الحالي (الطريقة الصحيحة في هذا الكنترولر)
                // ======================================================
                var studentId = await GetCurrentStudentIdAsync();
                if (studentId == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "⚠️ لم يتم التعرف على الطالب."
                    });
                }

                int sid = studentId.Value;

                // ======================================================
                // 1) تحديد نوع الاختبار (فردي / دفعة)
                // ======================================================
                var individualAssign = await _context.ExamAssignmentsToStudents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id && a.StudentId == sid);

                bool isIndividual = individualAssign != null;

                // ======================================================
                // 2) جلب / إنشاء ExamStudentStatus
                //    (بنفس منطق الكود القديم العامل)
                // ======================================================
                var status = await _context.ExamStudentStatuses
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == sid &&
                        (
                            (!isIndividual && s.ExamAssignmentId == id) ||
                            (isIndividual && s.ExamAssignmentToStudentId == id)
                        ));

                if (status == null)
                {
                    status = new ExamStudentStatus
                    {
                        StudentId = sid,
                        ExamAssignmentId = isIndividual ? null : id,
                        ExamAssignmentToStudentId = isIndividual ? id : null,
                        StartedAt = DateTime.UtcNow,
                        Status = ExamStatus.InProgress,
                        IsSubmitted = false
                    };

                    _context.ExamStudentStatuses.Add(status);
                    await _context.SaveChangesAsync();
                }

                // ======================================================
                // 3) منع إعادة التسليم
                // ======================================================
                if (status.IsSubmitted && status.Status == ExamStatus.Completed)
                {
                    return Json(new
                    {
                        success = true,
                        redirectUrl = Url.Action(
                            "ExamResult",
                            "Exams",
                            new { area = "Students", id = id })
                    });
                }

                // ======================================================
                // 4) التنفيذ النهائي (Engine Authority)
                //    ⚠️ نفس الـ Contract القديم
                // ======================================================
                var resultContext = new ExamResultContext
                {
                    StudentId = sid,
                    ExamAssignmentId = isIndividual ? null : id,
                    ExamAssignmentToStudentId = isIndividual ? id : null,
                    Kind = ExamKind.General
                };

                await _examResultEngine.FinalizeAsync(resultContext, reviewSeconds);

                // ======================================================
                // 5) إغلاق الحالة يدويًا (حاسم)
                // ======================================================
                status.IsSubmitted = true;
                status.Status = ExamStatus.Completed;
                status.SubmittedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // ======================================================
                // 6) تسجيل التقدم (غير حرج)
                // ======================================================
                try
                {
                    await _studentProgressService.RecordExamProgress(sid, id);
                }
                catch
                {
                    // لا نكسر التسليم
                }

                // ======================================================
                // 7) منع الكاش + الترحيل
                // ======================================================
                Response.Headers["Cache-Control"] = "no-cache,no-store,must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                return Json(new
                {
                    success = true,
                    redirectUrl = Url.Action(
                        "ExamResult",
                        "Exams",
                        new { area = "Students", id = id })
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"⚠️ خطأ أثناء إتمام الاختبار: {ex.Message}"
                });
            }
        }



        [HttpGet]
        public async Task<IActionResult> ExamResult(int id)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // قراءة Snapshot الجاهز
            var resultDto = await _examResultReader.GetResultAsync(id, studentId.Value);
            if (resultDto == null)
                return RedirectToAction("ErrorMessage", new { msg = "لم يتم العثور على نتيجة الاختبار." });

            // ------------------------------------------------
            // تحميل بيانات أساسية للاختبار (العنوان – الزمن)
            // ------------------------------------------------
            using var db = _contextFactory.CreateDbContext();

            var examInfo = await db.ExamAssignmentsToBatches
                .AsNoTracking()
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == id);

            string examTitle = examInfo?.Exam?.Title ?? "الاختبار";
            int durationMinutes = examInfo?.DurationMinutes ?? 0;

            // ------------------------------------------------
            // 🧠 التحويل DTO → ViewModel (بدون حساب جديد)
            // ------------------------------------------------
            var vm = new ExamResultViewModel
            {
                ExamAssignmentId = id,
                ExamTitle = examTitle,

                TotalQuestions = resultDto.TotalQuestions,
                CorrectAnswers = resultDto.Correct,

                // ❗ المتخطاة تُحسب ضمن الخطأ
                WrongAnswers = resultDto.Wrong + resultDto.Skipped,
                SkippedQuestions = resultDto.Skipped,

                // النسبة محفوظة من Engine
                ScorePercentage = resultDto.ScorePercent,
                Percent = resultDto.ScorePercent,

                // قيم افتراضية (كما كان يحدث سابقًا)
                Rank = 0,
                TotalStudents = 0,
                SuccessRate = (int)Math.Round(resultDto.ScorePercent),

                TimeSpentMinutes = durationMinutes,
                TimeSpentFormatted = durationMinutes > 0
                    ? $"{durationMinutes} دقيقة"
                    : "غير محدد",

                LastUpdated = DateTime.UtcNow,

                // رسائل تحفيزية بسيطة (كما هو معتاد)
                EncouragementMessage = resultDto.ScorePercent >= 70
                    ? "أداء ممتاز 👏 استمر!"
                    : "يمكنك التحسن أكثر، لا تستسلم 💪",

                MoodIcon = resultDto.ScorePercent >= 70 ? "😊" : "😐"
            };

            // ------------------------------------------------
            // ملاحظة مهمة:
            // القوائم التالية كانت تُملأ سابقًا من Attempts
            // نتركها فارغة الآن (بدون كسر View)
            // وسيتم معالجتها لاحقًا إن أردت
            // ------------------------------------------------
            vm.AnsweredQuestions = new();
            vm.WrongQuestions = new();
            vm.Questions = new();

            return View(vm);
        }


        [HttpPost]
        public async Task<IActionResult> RefreshRank(int batchId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { success = false, message = "لم يتم العثور على الطالب" });

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
        public async Task<IActionResult> SubmitExamAnswerFetch(
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
                    return Json(new { success = false, message = "❌ لم يتم التعرف على الطالب." });

                // ======================================================
                // 🔐 تثبيت وقت السؤال (إجباري)
                // ======================================================
                int safeTimeTaken = (timeTakenSeconds.HasValue && timeTakenSeconds.Value > 0)
                    ? timeTakenSeconds.Value
                    : 1;

                // كشف التلاعب الزمني
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
                            ExamType = "General",
                            DetectedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                // ======================================================
                // 🔍 تحديد نوع الامتحان
                // ======================================================
                bool isIndividual = await _context.ExamAssignmentsToStudents
                    .AnyAsync(x => x.Id == id && x.StudentId == studentId);

                // ======================================================
                // 🟦 تحميل الأسئلة (IDs فقط)
                // ======================================================
                List<Guid> allQuestions = isIndividual
                    ? await _context.ExamQuestions
                        .Where(eq => eq.ExamAssignmentToStudentId == id)
                        .OrderBy(eq => eq.Order)
                        .Select(eq => eq.QuestionId)
                        .ToListAsync()
                    : await _context.ExamQuestions
                        .Where(eq => eq.ExamAssignmentId == id)
                        .OrderBy(eq => eq.Order)
                        .Select(eq => eq.QuestionId)
                        .ToListAsync();

                if (!allQuestions.Any())
                    return Json(new { success = false, message = "⚠️ لا توجد أسئلة للامتحان." });

                // ✅ رفض أي سؤال ليس ضمن أسئلة هذا التكليف تحديدًا
                if (!allQuestions.Contains(q))
                {
                    _logger.LogWarning(
                        "SubmitExamAnswerFetch: محاولة تسجيل سؤال {QuestionId} غير منتمٍ لتكليف {AssignmentId} " +
                        "(طالب {StudentId}) — تم الرفض.", q, id, studentId);

                    return Json(new { success = false, message = "⚠️ هذا السؤال لا ينتمي لهذا الاختبار." });
                }

                // ======================================================
                // 🟩 حفظ الإجابة + الوقت
                // ======================================================
                if (!string.IsNullOrWhiteSpace(SelectedOption))
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

                        var attempt = await _context.QuestionAttemptNew
                            .FirstOrDefaultAsync(a =>
                                a.StudentId == studentId &&
                                a.QuestionId == q &&
                                (
                                    (!isIndividual && a.ExamAssignmentId == id) ||
                                    (isIndividual && a.ExamAssignmentToStudentId == id)
                                ));

                        if (attempt == null)
                        {
                            attempt = new QuestionAttemptNew
                            {
                                StudentId = studentId.Value,
                                QuestionId = q,
                                SelectedAnswer = SelectedOption,
                                IsCorrect = isCorrect,
                                AttemptedAt = DateTime.UtcNow,
                                TimeTakenSeconds = safeTimeTaken, // ✅ حاسم
                                LessonId = question.LessonId,
                                SectionId = question.SectionId,
                                ExamAssignmentId = isIndividual ? null : id,
                                ExamAssignmentToStudentId = isIndividual ? id : null
                            };

                            _context.QuestionAttemptNew.Add(attempt);
                        }
                        else
                        {
                            attempt.SelectedAnswer = SelectedOption;
                            attempt.IsCorrect = isCorrect;
                            attempt.AttemptedAt = DateTime.UtcNow;
                            attempt.TimeTakenSeconds = safeTimeTaken; // ✅ حاسم
                            attempt.ExamAssignmentId = isIndividual ? null : id;
                            attempt.ExamAssignmentToStudentId = isIndividual ? id : null;
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                // ======================================================
                // 🟧 علامة المراجعة
                // ======================================================
                if (nav == "review")
                {
                    string key = isIndividual
                        ? $"ExamReviewMarked_Individual_{id}_{studentId}"
                        : $"ExamReviewMarked_{id}_{studentId}";

                    var existing = HttpContext.Session.GetString(key);
                    var list = string.IsNullOrWhiteSpace(existing)
                        ? new List<Guid>()
                        : JsonSerializer.Deserialize<List<Guid>>(existing)!;

                    if (!list.Contains(q))
                        list.Add(q);

                    HttpContext.Session.SetString(key, JsonSerializer.Serialize(list));
                }

                // ======================================================
                // 🟥 عرض المراجعة
                // ======================================================
                if (nav == "showReview")
                {
                    var vm = await GetExamSolveViewModel(id, q, studentId.Value);
                    vm.IsReviewMode = true;
                    vm.ForceReviewVisible = true;
                    vm.SubmitAnswerUrl = "/Students/Exams/SubmitExamAnswerFetch";
                    vm.FinalSubmitUrl  = "/Students/Exams/SubmitFinalExam";
                    vm.ExamType        = "General";
                    HttpContext.Session.SetString($"QStart_{id}_{studentId}_{q}", DateTime.UtcNow.ToString("O"));
                    return PartialView("_SolveUnifiedExamPartial", vm);
                }

                if (nav == "goto")
                {
                    Guid targetQ = Guid.Parse(Request.Form["target"]);
                    var gotoVm = await GetExamSolveViewModel(id, targetQ, studentId.Value);
                    gotoVm.IsReviewMode = true;
                    gotoVm.ForceReviewVisible = true;
                    gotoVm.SubmitAnswerUrl = "/Students/Exams/SubmitExamAnswerFetch";
                    gotoVm.FinalSubmitUrl  = "/Students/Exams/SubmitFinalExam";
                    gotoVm.ExamType        = "General";
                    HttpContext.Session.SetString($"QStart_{id}_{studentId}_{targetQ}", DateTime.UtcNow.ToString("O"));
                    return PartialView("_SolveUnifiedExamPartial", gotoVm);
                }

                // ======================================================
                // 🟦 التنقل
                // ======================================================
                int currentIndex = allQuestions.IndexOf(q);
                int nextIndex = currentIndex;

                if (nav == "next" && currentIndex < allQuestions.Count - 1) nextIndex++;
                if (nav == "prev" && currentIndex > 0) nextIndex--;

                Guid nextQ = allQuestions[nextIndex];

                var nextVm = await GetExamSolveViewModel(id, nextQ, studentId.Value);
                nextVm.IsReviewMode = false;
                nextVm.ForceReviewVisible = false;
                nextVm.SubmitAnswerUrl = "/Students/Exams/SubmitExamAnswerFetch";
                nextVm.FinalSubmitUrl  = "/Students/Exams/SubmitFinalExam";
                nextVm.ExamType        = "General";
                HttpContext.Session.SetString($"QStart_{id}_{studentId}_{nextQ}", DateTime.UtcNow.ToString("O"));
                return PartialView("_SolveUnifiedExamPartial", nextVm);
            }
            catch
            {
                return Json(new { success = false, message = "⚠️ حدث خطأ أثناء معالجة الإجابة." });
            }
        }


        private async Task<ExamSolveViewModel> GetExamSolveViewModel(
         int examAssignmentId,
         Guid questionId,
         int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            bool isIndividual = await _context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId);

            string questionsKey = isIndividual
                ? $"IndividualExam_{examAssignmentId}_{studentId}"
                : $"ExamQuestions_{examAssignmentId}_{studentId}";

            // =========================
            // 1) تحميل IDs فقط (من Session)
            // =========================
            if (!HttpContext.Session.TryGetValue(questionsKey, out var raw))
                throw new Exception("Session expired.");

            var allQuestionIds = JsonSerializer.Deserialize<List<Guid>>(raw)!;

            int index = allQuestionIds.IndexOf(questionId);
            if (index < 0) index = 0;
            questionId = allQuestionIds[index];

            // =========================
            // 2) تحميل السؤال الحالي فقط
            // =========================
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                throw new Exception("Question not found.");

            var vmQuestion = question.ToDisplayModel();

            // =========================
            // 3) محاولة الطالب (السؤال الحالي)
            // =========================
            var attempt = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    a.QuestionId == questionId &&
                    (
                        (!isIndividual && a.ExamAssignmentId == examAssignmentId) ||
                        (isIndividual && a.ExamAssignmentToStudentId == examAssignmentId)
                    ))
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefaultAsync();

            if (attempt != null)
            {
                foreach (var opt in vmQuestion.Options)
                    opt.IsSelected = opt.Text == attempt.SelectedAnswer;
            }

            // =========================
            // 4) الأسئلة المجابة (جميعها مرة واحدة)
            // =========================
            var answeredIds = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    (
                        (!isIndividual && a.ExamAssignmentId == examAssignmentId) ||
                        (isIndividual && a.ExamAssignmentToStudentId == examAssignmentId)
                    ))
                .Select(a => a.QuestionId)
                .Distinct()
                .ToListAsync();

            var answeredMap = allQuestionIds.ToDictionary(
                q => q,
                q => answeredIds.Contains(q)
            );

            // =========================
            // 5) Review Flags (Session فقط)
            // =========================
            string reviewKey = isIndividual
                ? $"ExamReviewMarked_Individual_{examAssignmentId}_{studentId}"
                : $"ExamReviewMarked_{examAssignmentId}_{studentId}";

            var reviewList = HttpContext.Session.TryGetValue(reviewKey, out var rv)
                ? JsonSerializer.Deserialize<List<Guid>>(rv)!
                : new List<Guid>();

            // =========================
            // 6) بناء الـ ViewModel
            // =========================
            return new ExamSolveViewModel
            {
                ExamAssignmentId = examAssignmentId,
                CurrentQuestionId = questionId,
                AllQuestionIds = allQuestionIds,
                Question = vmQuestion,
                SelectedAnswer = attempt?.SelectedAnswer ?? "",
                IsIndividual = isIndividual,

                // ⭐⭐ المطلوب لتلوين الأزرار
                AnsweredQuestions = answeredMap,
                ReviewMarkedIds = reviewList,

                // ⚠️ لا نلمس المؤقت هنا
                QuestionStartTime = DateTime.UtcNow
            };

        }



        [HttpGet]
        public async Task<IActionResult> ExamDashboard()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ============================================================
            // 🟢 1) ملخص حالات الاختبارات (كما هو)
            // ============================================================
            var statusSummary = await _examStatusService.GetStatusSummaryAsync(studentId.Value);

            // ============================================================
            // 🟢 2) الإحصاءات العامة (كما هي)
            // ============================================================
            var statsVm = await _studentExamDashboardService.GetDashboardAsync(studentId.Value);

            // ============================================================
            // 🟢 3) حساب عدد الأخطاء (بدون Attempts – بدون Runtime)
            //    ❗ نعتمد فقط على Result Store
            // ============================================================
            var examResults = await _context.ExamStudentStatuses
                .AsNoTracking()
                .Where(s =>
                    s.StudentId == studentId.Value &&
                    s.IsSubmitted &&
                    s.Status == ExamStatus.Completed &&
                    s.Note != null)
                .Select(s => s.Note)
                .ToListAsync();

            int mistakesCount = 0;

            foreach (var note in examResults)
            {
                try
                {
                    var snapshot = System.Text.Json.JsonSerializer
                        .Deserialize<ExamFinalResultDto>(note);

                    if (snapshot != null)
                    {
                        // ❗ المتخطاة تُحسب ضمن الخطأ
                        mistakesCount += snapshot.Wrong + snapshot.Skipped;
                    }
                }
                catch
                {
                    // نتجاهل أي Snapshot قديم أو تالف
                }
            }

            statsVm.MistakesCount = mistakesCount;

            // ============================================================
            // 🟢 4) مناهج الطالب (كما هي)
            // ============================================================
            var curriculums = await _studentExamDashboardService
                .GetStudentCurriculumsAsync(studentId.Value);

            statsVm.Curriculums = curriculums.Select(c => new CurriculumVm
            {
                Id = int.Parse(c.Value),
                Title = c.Text
            }).ToList();

            // ============================================================
            // 🟢 5) ترتيب الطالب (كما هو)
            // ============================================================
            var rankVm = await _studentRankingService.GetCurrentRankAsync(studentId.Value);

            if (rankVm != null)
            {
                statsVm.StudentRank = new ViewModels.Exam.StudentRankViewModel
                {
                    Rank = rankVm.Rank,
                    TotalStudents = rankVm.TotalStudents,
                    LastUpdated = rankVm.LastUpdated,
                    MedalImageUrl = rankVm.MedalImageUrl,
                    MotivationalMessage = rankVm.MotivationalMessage
                };
            }

            // ============================================================
            // 🟢 6) دمج ملخص الحالات (كما هو)
            // ============================================================
            statsVm.TotalAssigned = statusSummary.Total;
            statsVm.Completed = statusSummary.Solved;
            statsVm.Pending = statusSummary.Required;
            statsVm.Late = statusSummary.Late;
            statsVm.ExpiringSoon = statusSummary.ExpiringSoon;

            // ============================================================
            return View(statsVm);
        }


        [HttpGet]
        public async Task<IActionResult> GetDashboardDataByCurriculum(int curriculumId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Unauthorized();

            var data = await _studentExamDashboardService.GetDashboardDataByCurriculumAsync(studentId.Value, curriculumId);
            return Json(data);
        }


        [HttpGet]
        public async Task<IActionResult> RequiredExams()
        {
            var studentId = await GetCurrentStudentIdAsync();
            var vm = await _examStatusService.GetRequiredExamsAsync(studentId.Value);

            ViewBag.TotalRequired = vm.Count;
            return View(vm);
        }



        [HttpGet]
        public async Task<IActionResult> CompletedExams()
        {
            var studentId = await GetCurrentStudentIdAsync();
            var vm = await _examStatusService.GetSolvedExamsAsync(studentId.Value);
            return View(vm);
        }



        [HttpGet]
        public async Task<IActionResult> LateExams()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var vm = await _examStatusService.GetLateExamsPageAsync(studentId.Value);
            return View(vm);
        }


        [HttpGet]
        public async Task<IActionResult> GetExamCurriculumData(int curriculumId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { error = "Student not found" });

            // =========================================
            // 1️⃣ جلب دفعة الطالب
            // =========================================
            var batchId = await _context.StudentBatchEnrollments
                .Where(x => x.StudentID == studentId)
                .Select(x => x.BatchId)
                .FirstOrDefaultAsync();

            if (batchId == 0)
                return Json(new { error = "Batch not found" });

            // =========================================
            // 2️⃣ المحاور المطلوبة
            // =========================================
            var sections = await _context.Sections
                .Where(s => curriculumId == 0 || s.CurriculumId == curriculumId)
                .AsNoTracking()
                .Select(s => new { s.Id, s.Title })
                .ToListAsync();

            // =========================================
            // 3️⃣ كل محاولات الأسئلة (Query واحد)
            // =========================================
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join sbe in _context.StudentBatchEnrollments on a.StudentId equals sbe.StudentID
                where l.SectionId != null
                      && sbe.BatchId == batchId
                select new
                {
                    l.SectionId,
                    a.StudentId,
                    a.IsCorrect
                }
            ).ToListAsync();

            // =========================================
            // 4️⃣ تحليل الأداء حسب المحاور (In-Memory)
            // =========================================
            var sectionLabels = new List<string>();
            var studentScores = new List<double>();
            var batchAverages = new List<double>();

            foreach (var sec in sections)
            {
                sectionLabels.Add(sec.Title ?? $"محور {sec.Id}");

                var secAttempts = attempts.Where(x => x.SectionId == sec.Id).ToList();

                var studentAttempts = secAttempts.Where(x => x.StudentId == studentId).ToList();
                var batchAttempts = secAttempts;

                double studentAcc =
                    studentAttempts.Count > 0
                        ? (double)studentAttempts.Count(x => x.IsCorrect) / studentAttempts.Count * 100
                        : 0;

                double batchAcc =
                    batchAttempts.Count > 0
                        ? (double)batchAttempts.Count(x => x.IsCorrect) / batchAttempts.Count * 100
                        : 0;

                studentScores.Add(Math.Round(studentAcc, 1));
                batchAverages.Add(Math.Round(batchAcc, 1));
            }

            // =========================================
            // 5️⃣ نتائج اختبارات الطالب
            // =========================================
            var examResults = await (
                from s in _context.ExamStudentStatuses
                join ea in _context.ExamAssignmentsToBatches on s.ExamAssignmentId equals ea.Id
                join e in _context.Exams on ea.ExamId equals e.Id
                where s.StudentId == studentId && s.IsSubmitted
                orderby ea.AssignedAt
                select new
                {
                    ea.Id,
                    Title = ea.Title ?? e.Title,
                    Score = s.Score ?? 0
                }
            ).ToListAsync();

            // =========================================
            // 6️⃣ تفاصيل الإجابات لكل اختبار (Query واحد)
            // =========================================
            var examAttempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.ExamAssignmentId != null)
                .Select(a => new
                {
                    a.ExamAssignmentId,
                    a.QuestionId,
                    a.IsCorrect
                })
                .ToListAsync();

            var correctAnswers = new List<int>();
            var remainingQuestions = new List<int>();

            foreach (var exam in examResults)
            {
                var attemptsForExam = examAttempts
                    .Where(x => x.ExamAssignmentId == exam.Id)
                    .ToList();

                int total = attemptsForExam.Select(x => x.QuestionId).Distinct().Count();
                int correct = attemptsForExam.Count(x => x.IsCorrect);

                correctAnswers.Add(correct);
                remainingQuestions.Add(Math.Max(0, total - correct));
            }

            // =========================================
            // 7️⃣ متوسط الدفعة لنفس الاختبارات
            // =========================================
            var batchExamAverages = await (
                from s in _context.ExamStudentStatuses
                join sb in _context.StudentBatchEnrollments on s.StudentId equals sb.StudentID
                where sb.BatchId == batchId && s.IsSubmitted
                group s by s.ExamAssignmentId into g
                select new
                {
                    ExamAssignmentId = g.Key,
                    Avg = g.Average(x => x.Score ?? 0)
                }
            ).ToListAsync();

            var batchScores = examResults
                .Select(e =>
                    batchExamAverages.FirstOrDefault(x => x.ExamAssignmentId == e.Id)?.Avg ?? 0
                )
                .ToList();

            // =========================================
            // 8️⃣ الإخراج النهائي
            // =========================================
            return Json(new
            {
                sectionLabels,
                studentScores,
                batchAverages,
                examTitles = examResults.Select(x => x.Title).ToList(),
                studentExamScores = examResults.Select(x => (double)x.Score).ToList(),
                batchExamScores = batchScores,
                correctAnswers,
                remainingQuestions
            });
        }


        [HttpGet]
        public async Task<IActionResult> WeakSections()
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // ✅ محاولات الطالب من الاختبارات العامة فقط
            var rawData = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                join ex in _context.Exams on a.ExamId equals ex.Id into exj
                from ex in exj.DefaultIfEmpty()
                where a.StudentId == studentId
                      && a.ExamAssignmentId != null
                      && a.HomeworkSetId == null
                      && a.PerformanceIndicatorExamId == null
                      && (ex == null ||
                          (ex.Type != ExamType.LevelAssessment &&
                           ex.Type != ExamType.PerformanceScale))
                select new
                {
                    SectionId = s.Id,
                    SectionName = s.Title,
                    IsCorrect = a.IsCorrect
                }
            ).ToListAsync();

            // ✅ حساب متوسط الدقة لكل محور
            var sectionAccuracies = rawData
                .GroupBy(x => new { x.SectionId, x.SectionName })
                .Select(g => new WeakSectionVm
                {
                    SectionId = g.Key.SectionId,
                    SectionName = g.Key.SectionName,
                    Accuracy = g.Count() > 0 ? (double)g.Count(x => x.IsCorrect) / g.Count() * 100 : 0,
                    Trained = _context.StudentWeaknessTrainings
                                .Any(t => t.StudentId == studentId && t.SectionId == g.Key.SectionId)
                })
                .Where(x => x.Accuracy < 50)   // 🔹 أقل من 50% يُعتبر ضعيفًا
                .OrderBy(x => x.Accuracy)
                .ToList();

            return View(sectionAccuracies);
        }







        [HttpGet]
        public async Task<IActionResult> ExamRecommendations(int curriculumId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { recommendation = "❌ لم يتم العثور على الطالب" });

            // 🟢 مثال بسيط لتوصية
            return Json(new
            {
                recommendation = "راجع محاور الاختبار السابقة جيداً",
                successProbability = 0.75
            });
        }


    }
}
