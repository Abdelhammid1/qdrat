using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Interfaces;
using QdratNew.Services.HomeworkTracking.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Homework;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [Authorize(Roles = "Partner,PartnerAdmin,PartnerInstructor,SuperAdmin,Owner")]

    public class SentHomeworksController : PartnerBaseController
    {
        private readonly ISentHomeworkService _service;
        private readonly ApplicationDbContext _context;
        private readonly IHomeworkRecommendationService _homeworkRecommendationService;

        public SentHomeworksController(
            ISentHomeworkService service,
            IPartnerSubscriptionService subscriptionService,
            ApplicationDbContext context,
            IHomeworkRecommendationService homeworkRecommendationService)
            : base(subscriptionService, context)
        {
            _service = service;
            _context = context;
            _homeworkRecommendationService = homeworkRecommendationService;
        }

        public IActionResult Index()
        {
            var data = _service.GetSentHomeworksForPartner(ActivePartnerId);
            return View(data);
        }

        public IActionResult Details(int id)
        {
            var isArchived = _context.HomeworkSets
                .AsNoTracking()
                .Any(x => x.Id == id && x.IsArchived);

            if (isArchived)
                return Forbid();

            ViewBag.HomeworkSetId = id;

            var students = _service.GetHomeworkStudents(id);
            return View(students);
        }

        // =====================================================
        // 📊 تقرير حل الواجب (نفس تقرير الطالب تمامًا)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> HomeworkReportUnified(
            int homeworkSetId,
            int studentId)
        {
            var isArchived = await _context.HomeworkSets
                .AsNoTracking()
                .AnyAsync(x => x.Id == homeworkSetId && x.IsArchived);

            if (isArchived)
                return Forbid();

            // =====================================================
            // 0️⃣ التحقق من صلاحية الشريك
            // =====================================================
            var isAllowed = await _context.StudentBatchEnrollments
                .Include(e => e.Batch)
                    .ThenInclude(b => b.Branch)
                .AnyAsync(e =>
                    e.StudentID == studentId &&
                    e.Batch.Branch.PartnerId == ActivePartnerId);

            if (!isAllowed)
                return Forbid();

            // =====================================================
            // 1️⃣ نتيجة الواجب
            // =====================================================
            var summary = await _context.HomeworkSetStudents
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
            var studentInfo = await _context.Students
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

            if (studentInfo == null)
                return NotFound();

            // =====================================================
            // 3️⃣ الأسئلة المرتبطة بالواجب
            // =====================================================
            var homeworkQuestions = await (
                from h in _context.Homeworks.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on h.QuestionId equals q.Id
                join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in _context.Sections.AsNoTracking() on l.SectionId equals s.Id
                where h.StudentId == studentId
                      && h.HomeworkSetId == homeworkSetId
                select new
                {
                    h.QuestionId,
                    QuestionTitle = q.Title,
                    q.CorrectAnswer,
                    LessonId = l.Id,
                    LessonTitle = l.Title,
                    SectionId = s.Id,
                    SectionTitle = s.Title
                }
            ).ToListAsync();

            // =====================================================
            // 4️⃣ آخر محاولة لكل سؤال
            // =====================================================
            var attempts = await _context.QuestionAttemptNew
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
                        CorrectAnswer = q.CorrectAnswer,
                        IsCorrect = lastAttempt?.IsCorrect ?? false,
                        TimeTakenSeconds = lastAttempt?.TimeTakenSeconds ?? 0
                    };
                })
                .ToList();

            // =====================================================
            // 5️⃣ الحسابات العامة
            // =====================================================
            int totalQuestions = questions.Count;
            int correctCount = questions.Count(x => x.IsCorrect);
            int wrongCount = questions.Count(x => !x.IsCorrect && !string.IsNullOrWhiteSpace(x.StudentAnswer));

            double totalSeconds = questions.Sum(x => x.TimeTakenSeconds);
            double timeSpentMinutes = totalSeconds > 0
                ? Math.Round(totalSeconds / 60.0, 1)
                : 0;

            // =====================================================
            // 6️⃣ ViewModel
            // =====================================================
            var vm = new HomeworkAnalyticsViewModel
            {
                HomeworkSetId = homeworkSetId,
                StudentName = studentInfo.FullName,
                BatchName = studentInfo.BatchName,
                Questions = questions,
                ScorePercentage = summary ?? 0,
                TimeSpentMinutes = timeSpentMinutes
            };

            // =====================================================
            // 7️⃣ SectionsPerformance
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
            // 8️⃣ LessonsPerformance
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

            // 🔁 نفس View الطالب تمامًا
            return View(
                "~/Areas/Partner/Views/SentHomeworks/HomeworkReportUnified.cshtml",
                vm
            );
        }


        [HttpGet]
        public async Task<IActionResult> ReviewLesson(
    int homeworkSetId,
    int lessonId,
    int studentId)
        {
            var isArchived = await _context.HomeworkSets
                .AsNoTracking()
                .AnyAsync(x => x.Id == homeworkSetId && x.IsArchived);

            if (isArchived)
                return Forbid();

            // =====================================================
            // 0️⃣ تحديد نوع المستخدم
            // =====================================================
            bool isAdmin = User.IsInRole("Admin");
            bool isPartner = User.IsInRole("Partner");

            if (!isAdmin && !isPartner)
                return Forbid();

            // =====================================================
            // 1️⃣ التحقق من الصلاحية
            // =====================================================
            bool isAllowed;

            if (isAdmin)
            {
                // Admin يُسمح له طالما الطالب مرتبط بأي دفعة
                isAllowed = await _context.StudentBatchEnrollments
                    .AsNoTracking()
                    .AnyAsync(e => e.StudentID == studentId);
            }
            else
            {
                // Partner مرتبط بسياق الشريك الحالي
                if (ActivePartnerId <= 0)
                    return RedirectToAction("Select", "PartnerContext");

                isAllowed = await _context.StudentBatchEnrollments
                    .Include(e => e.Batch)
                        .ThenInclude(b => b.Branch)
                    .AsNoTracking()
                    .AnyAsync(e =>
                        e.StudentID == studentId &&
                        e.Batch.Branch.PartnerId == ActivePartnerId);
            }

            if (!isAllowed)
                return Forbid();

            // =====================================================
            // 2️⃣ كل أسئلة الواجب داخل هذا المؤشر
            // =====================================================
            var homeworkQuestions = await (
                from h in _context.Homeworks.AsNoTracking()
                join q in _context.Questions
                    .Include(q => q.Options)
                    .Include(q => q.VerbalPassage)
                    on h.QuestionId equals q.Id
                where h.StudentId == studentId
                      && h.HomeworkSetId == homeworkSetId
                      && q.LessonId == lessonId
                select q
            ).Distinct().ToListAsync();

            if (!homeworkQuestions.Any())
                return RedirectToAction("Details", new { id = homeworkSetId });

            // =====================================================
            // 3️⃣ عنوان المؤشر
            // =====================================================
            var lessonTitle = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.Id == lessonId)
                .Select(l => l.Title)
                .FirstOrDefaultAsync();

            // =====================================================
            // 4️⃣ آخر محاولة لكل سؤال
            // =====================================================
            var attempts = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            var lastAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(x => x.AttemptedAt).First()
                );

            // =====================================================
            // 5️⃣ بناء عناصر المراجعة
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
                    IsCorrect = hasAnswer && attempt!.IsCorrect,
                    TimeTakenSeconds = attempt?.TimeTakenSeconds ?? 0,

                    ImageUrl = q.ImageUrl,
                    IsQuantitative = q.IsQuantitative,
                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,

                    VerbalPassageTitle = q.VerbalPassage?.Title,
                    VerbalPassageContent = q.VerbalPassage?.Content,

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
                TimeSpentFormatted = $"{minutes} دقيقة {seconds} ثانية",
                Questions = questionItems
            };

            // =====================================================
            // 8️⃣ View
            // =====================================================
            return View(
                "~/Areas/Partner/Views/SentHomeworks/ReviewLesson.cshtml",
                vm
            );
        }


        // =====================================================
        // 🗑 حذف الواجب المرسل بالكامل (مرحلة 1 - فحص)
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSentHomework(int homeworkSetId, bool forceDelete = false)
        {
            if (homeworkSetId <= 0)
                return BadRequest();

            var isArchived = await _context.HomeworkSets
                .AsNoTracking()
                .AnyAsync(x => x.Id == homeworkSetId && x.IsArchived);

            if (isArchived)
                return Forbid();

            // =====================================================
            // 1️⃣ التحقق من ملكية الشريك
            // =====================================================
            var isOwnedByPartner = await _context.HomeworkSets
                .AsNoTracking()
                .Include(h => h.Batch)
                    .ThenInclude(b => b.Branch)
                .AnyAsync(h =>
                    h.Id == homeworkSetId &&
                    h.Batch.Branch.PartnerId == ActivePartnerId);

            if (!isOwnedByPartner)
                return Forbid();

            // =====================================================
            // 2️⃣ حساب التفاعل
            // =====================================================
            var studentsStartedCount = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.HomeworkSetId == homeworkSetId)
                .Select(a => a.StudentId)
                .Distinct()
                .CountAsync();

            var studentsSubmittedCount = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Where(x => x.HomeworkSetId == homeworkSetId && x.IsSubmitted)
                .CountAsync();

            if (!forceDelete && (studentsStartedCount > 0 || studentsSubmittedCount > 0))
            {
                var warningVm = new DeleteHomeworkWarningVm
                {
                    HomeworkSetId = homeworkSetId,
                    StudentsStarted = studentsStartedCount,
                    StudentsSubmitted = studentsSubmittedCount
                };

                return View("DeleteWarning", warningVm);
            }

            // =====================================================
            // 3️⃣ حذف داخل ExecutionStrategy (حل Azure)
            // =====================================================
            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    // حذف Question Attempts
                    await _context.QuestionAttemptNew
                        .Where(a => a.HomeworkSetId == homeworkSetId)
                        .ExecuteDeleteAsync();

                    // حذف HomeworkSet Attempts
                    await _context.HomeworkSetAttempts
                        .Where(a => a.HomeworkSetId == homeworkSetId)
                        .ExecuteDeleteAsync();

                    // حذف Homeworks
                    await _context.Homeworks
                        .Where(h => h.HomeworkSetId == homeworkSetId)
                        .ExecuteDeleteAsync();

                    // حذف HomeworkSetStudents
                    await _context.HomeworkSetStudents
                        .Where(x => x.HomeworkSetId == homeworkSetId)
                        .ExecuteDeleteAsync();

                    // حذف HomeworkSetSections
                    await _context.HomeworkSetSections
                        .Where(x => x.HomeworkSetId == homeworkSetId)
                        .ExecuteDeleteAsync();

                    // حذف HomeworkSet
                    var deleted = await _context.HomeworkSets
                        .Where(h => h.Id == homeworkSetId)
                        .ExecuteDeleteAsync();

                    if (deleted == 0)
                        throw new Exception("HomeworkSet not found.");

                    await transaction.CommitAsync();
                });

                TempData["Success"] = "تم حذف الواجب وجميع بيانات الطلاب المرتبطة به.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "فشل الحذف: " + ex.GetBaseException().Message;
            }

            return RedirectToAction(nameof(Index));
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetHomework(int homeworkSetId, int studentId)
        {
            _service.ResetHomeworkForStudent(homeworkSetId, studentId);

            TempData["Success"] = "تم السماح للطالب بإعادة حل الواجب.";
            return RedirectToAction(nameof(Details), new { id = homeworkSetId });
        }
    }
}
