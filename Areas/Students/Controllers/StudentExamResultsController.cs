using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Exams.Engines;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Reports;
using QdratNew.ViewModels.Students;
using System.Text.Json;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class StudentExamResultsController : StudentBaseController
    {
        private readonly IExamResultReader _examResultReader;
        private readonly IExamRecommendationService _recommendationService;
        private readonly IExamResultEngine _examResultEngine;

        public StudentExamResultsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
             IExamRecommendationService recommendationService,
             IExamResultEngine examResultEngine,    
            UserManager<ApplicationUser> userManager,
            IExamResultReader examResultReader)
            : base(contextFactory, userManager)
        {
            _examResultReader = examResultReader;
            _recommendationService = recommendationService;
            _examResultEngine = examResultEngine;   
        }

        // =====================================================
        // ✅ نتيجة الاختبار (Snapshot فقط – بدون حساب)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ExamResult(int id)
        {
            using var db = _contextFactory.CreateDbContext();

            int studentId = StudentId;

            // ======================================================
            // 1) تحديد نوع الاختبار (فردي / دفعة)
            // ======================================================
            bool isIndividual = await db.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == id && x.StudentId == studentId);

            // ======================================================
            // 2) جلب حالة الطالب الصحيحة
            // ======================================================
            ExamStudentStatus status = isIndividual
                ? await db.ExamStudentStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == studentId &&
                        s.ExamAssignmentToStudentId == id)
                : await db.ExamStudentStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == studentId &&
                        s.ExamAssignmentId == id);

            if (status == null || !status.IsSubmitted || status.Status != ExamStatus.Completed)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على نتيجة لهذا الاختبار.";
                return RedirectToAction(
                    "AllExams",
                    "StudentExamPages",
                    new { area = "Students" }
                );
            }

            // ======================================================
            // 3) توليد Snapshot إذا لم يكن موجودًا
            // ======================================================
            if (string.IsNullOrWhiteSpace(status.Note))
            {
                await _examResultEngine.GenerateSnapshotIfMissingAsync(id, studentId);

                status = isIndividual
                    ? await db.ExamStudentStatuses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.StudentId == studentId &&
                            s.ExamAssignmentToStudentId == id)
                    : await db.ExamStudentStatuses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.StudentId == studentId &&
                            s.ExamAssignmentId == id);

                if (status == null || string.IsNullOrWhiteSpace(status.Note))
                {
                    TempData["ErrorMessage"] = "تعذر إنشاء نتيجة الاختبار.";
                    return RedirectToAction(
                        "AllExams",
                        "StudentExamPages",
                        new { area = "Students" }
                    );
                }
            }

            // ======================================================
            // 4) قراءة Snapshot
            // ======================================================
            var snapshot = JsonSerializer.Deserialize<ExamFinalResultDto>(status.Note);
            if (snapshot == null)
            {
                TempData["ErrorMessage"] = "بيانات نتيجة الاختبار غير صالحة.";
                return RedirectToAction(
                    "AllExams",
                    "StudentExamPages",
                    new { area = "Students" }
                );
            }

            // ======================================================
            // 5) بيانات الامتحان
            // ======================================================
            string examTitle;
            int durationMinutes;



            bool isRTL = true;

            // نحاول جلب أي سؤال من الاختبار لمعرفة الاتجاه
            var sampleQuestion = await db.ExamQuestions
                .Where(x => isIndividual
                    ? x.ExamAssignmentToStudentId == id
                    : x.ExamAssignmentId == id)
                .Join(db.Questions.Include(q => q.Curriculum),
                    eq => eq.QuestionId,
                    q => q.Id,
                    (eq, q) => q)
                .FirstOrDefaultAsync();

            if (sampleQuestion != null && sampleQuestion.Curriculum != null)
            {
                isRTL = sampleQuestion.Curriculum.IsRTL;
            }

            if (isIndividual)
            {
                var assign = await db.ExamAssignmentsToStudents
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == id);

                examTitle = assign?.Exam?.Title ?? "الاختبار";
                durationMinutes = assign?.DurationMinutes ?? 0;
            }
            else
            {
                var assign = await db.ExamAssignmentsToBatches
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == id);

                examTitle = assign?.Exam?.Title ?? "الاختبار";
                durationMinutes = assign?.DurationMinutes ?? 0;
            }

            // ======================================================
            // 6) ViewModel
            // ======================================================
            var vm = new ExamResultViewModel
            {
                ExamAssignmentId = id,
                IsIndividual = isIndividual,
                ExamTitle = examTitle,

                TotalQuestions = snapshot.TotalQuestions,
                CorrectAnswers = snapshot.Correct,
                WrongAnswers = snapshot.Wrong,
                SkippedQuestions = snapshot.Skipped,
                IsRTL = isRTL,
                ScorePercentage = snapshot.ScorePercent,
                Percent = snapshot.ScorePercent,

                TimeSpentMinutes = durationMinutes,
                TimeSpentFormatted = durationMinutes > 0
                    ? $"{durationMinutes} دقيقة"
                    : "غير محدد",

                LastUpdated = DateTime.UtcNow,

                EncouragementMessage = snapshot.ScorePercent >= 70
                    ? "أداء ممتاز 👏 استمر!"
                    : "يمكنك التحسن أكثر، لا تستسلم 💪",

                MoodIcon = snapshot.ScorePercent >= 70 ? "😊" : "😐"
            };

            return View(vm);
        }


        // =====================================================
        // 🧾 مراجعة الأسئلة (نجهزها لاحقًا)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> ExamReview(int examAssignmentId)
        {
            int id = examAssignmentId;

            using var context = _contextFactory.CreateDbContext();

            int studentId = StudentId;

            // ======================================================
            // 0) تحديد نوع الاختبار (فردي / دفعة)
            // ======================================================
            bool isIndividual = await context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == id && x.StudentId == studentId);

            // ======================================================
            // 1) جلب ExamId المرتبط بالاختبار
            // ======================================================
            string? examAssignmentTitle;
            int? examId0;

            if (isIndividual)
            {
                var individualAssignment = await context.ExamAssignmentsToStudents
                    .AsNoTracking()
                    .Include(e => e.Exam)
                    .Where(e => e.Id == id && e.StudentId == studentId)
                    .Select(e => new
                    {
                        e.ExamId,
                        Title = e.Exam.Title
                    })
                    .FirstOrDefaultAsync();

                if (individualAssignment == null)
                    return NotFound();

                examId0 = individualAssignment.ExamId;
                examAssignmentTitle = individualAssignment.Title;
            }
            else
            {
                var batchAssignment = await context.ExamAssignmentsToBatches
                    .AsNoTracking()
                    .Where(e => e.Id == id)
                    .Select(e => new
                    {
                        e.ExamId,
                        e.Title
                    })
                    .FirstOrDefaultAsync();

                if (batchAssignment == null)
                    return NotFound();

                examId0 = batchAssignment.ExamId;
                examAssignmentTitle = batchAssignment.Title;
            }

            var examAssignment = new { ExamId = examId0, Title = examAssignmentTitle };


            // ======================================================
            // 2) جلب آخر محاولة لكل سؤال باستخدام ExamAssignmentId / ExamAssignmentToStudentId
            // ======================================================
            var attempts = await context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    (
                        (!isIndividual && a.ExamAssignmentId == id) ||
                        (isIndividual && a.ExamAssignmentToStudentId == id)
                    ))
                .GroupBy(a => a.QuestionId)
                .Select(g => g
                    .OrderByDescending(x => x.AttemptedAt)
                    .FirstOrDefault())
                .ToListAsync();

            var attemptsDict = attempts
                .Where(a => a != null)
                .ToDictionary(a => a.QuestionId, a => a);

            // ======================================================
            // 3) جلب الأسئلة (🔥 إضافة Curriculum)
            // ======================================================
            var questionsRaw = await (
     from eq in context.ExamQuestions
     join q in context.Questions
         .Include(x => x.Options)
         .Include(x => x.VerbalPassage)
         .Include(x => x.Lesson)
             .ThenInclude(l => l.Section)
         .Include(x => x.Curriculum)
         on eq.QuestionId equals q.Id
     where
        (!isIndividual && eq.ExamAssignmentId == id) ||
        (isIndividual && eq.ExamAssignmentToStudentId == id)
     orderby eq.Order
     select q
 )
 .AsNoTracking()
 .ToListAsync();

            // 🔥 fallback
            if (!questionsRaw.Any())
            {
                var examId = examAssignment.ExamId;

                if (examId != null)
                {
                    questionsRaw = await (
                        from eq in context.ExamQuestions
                        join q in context.Questions
                            .Include(x => x.Options)
                            .Include(x => x.VerbalPassage)
                            .Include(x => x.Lesson)
                                .ThenInclude(l => l.Section)
                            .Include(x => x.Curriculum)
                            on eq.QuestionId equals q.Id
                        where eq.ExamId == examId
                        orderby eq.Order
                        select q
                    )
                    .AsNoTracking()
                    .ToListAsync();
                }
            }

            // ======================================================
            // 4) بناء ViewModel + الاتجاه
            // ======================================================
            var questions = questionsRaw.Select(q =>
            {
                attemptsDict.TryGetValue(q.Id, out var attempt);
                var hasSelectedAnswer = !string.IsNullOrWhiteSpace(attempt?.SelectedAnswer);

                return new ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,

                    StudentAnswer = attempt?.SelectedAnswer,
                    CorrectAnswer = q.CorrectAnswer,

                    IsCorrect = attempt != null && attempt.IsCorrect,
                    IsSkipped = !hasSelectedAnswer,

                    LessonTitle = q.Lesson.Title,
                    SectionTitle = q.Lesson.Section.Title,

                    IsQuantitative = q.IsQuantitative,
                    // 🔥 الشرح
                    Explanation = q.Explanation,
                    VideoUrl = q.VideoUrl,
                    // ✅ أهم سطر
                    IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true,

                    TimeTakenSeconds = attempt?.TimeTakenSeconds ?? 0,

                    // 🔥 القطعة (كاملة)
                    VerbalPassageTitle = q.VerbalPassage != null
    ? q.VerbalPassage.Title
    : null,

                    VerbalPassageContent = q.VerbalPassage != null
    ? q.VerbalPassage.Content
    : null,

                    VerbalPassageMediaUrl = q.VerbalPassage != null
    ? q.VerbalPassage.MediaUrl
    : null,

                    VerbalPassageType = q.VerbalPassage != null
    ? (QdratNew.Enums.PassageType?)q.VerbalPassage.Type
    : null,

                    ImageUrl = q.ImageUrl,

                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,

                    DisplayType =
                        q.Template == QuestionTemplate.CompareValues
                            ? QuestionDisplayType.ComparisonText
                            : q.Template == QuestionTemplate.CompareWithImage
                                ? QuestionDisplayType.ComparisonWithImage
                                : QuestionDisplayType.WithImage,

                    Options = q.Options.Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.Text == q.CorrectAnswer,
                        IsSelectedByStudent =
                            hasSelectedAnswer && o.Text == attempt.SelectedAnswer
                    }).ToList()
                };
            }).ToList();

            // ======================================================
            // 5) تحديد اتجاه الصفحة بالكامل
            // ======================================================
            bool pageIsRTL = questions.FirstOrDefault()?.IsRTL ?? true;

            // ======================================================
            // 6) الإحصائيات
            // ======================================================
            int total = questions.Count;
            int correct = questions.Count(x => x.IsCorrect);
            int skipped = questions.Count(x => x.IsSkipped);
            int wrong = questions.Count(x => !x.IsCorrect && !x.IsSkipped);

            // ======================================================
            // 7) ViewModel النهائي
            // ======================================================
            var vm = new ExamReviewViewModel
            {
                ExamAssignmentId = id,
                ExamTitle = examAssignment.Title ?? (pageIsRTL ? "اختبار" : "Exam"),

                TotalQuestions = total,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                SkippedQuestions = skipped,

                ScorePercentage = total > 0
                    ? Math.Round((double)correct / total * 100, 2)
                    : 0,

                TimeSpentFormatted = pageIsRTL ? "غير محدد" : "Not Defined",

                // ✅ أهم إضافة
                IsRTL = pageIsRTL,

                Questions = questions
            };

            return View("ExamReview", vm);
        }


        [HttpGet]
        public async Task<IActionResult> LessonQuestions(int lessonId, int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (lessonId <= 0 || examAssignmentId <= 0)
                return NotFound();

            var studentId = StudentId;

            // ======================================================
            // 1️⃣ تحديد نوع الاختبار
            // ======================================================
            bool isIndividual = await _context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId);

            // ======================================================
            // 2️⃣ OLD SYSTEM (Assignment)
            // ======================================================
            var questions = await (
                from eq in _context.ExamQuestions

                join q in _context.Questions
                    .Include(x => x.Options)
                    .Include(x => x.VerbalPassage)
                    .Include(x => x.Curriculum)
                    on eq.QuestionId equals q.Id

                join l in _context.Lessons on q.LessonId equals l.Id

                join a in _context.QuestionAttemptNew
                    .Where(x =>
                        x.StudentId == studentId &&
                        (
                            (!isIndividual && x.ExamAssignmentId == examAssignmentId) ||
                            (isIndividual && x.ExamAssignmentToStudentId == examAssignmentId)
                        )
                    )
                    on q.Id equals a.QuestionId into gj

                from attempt in gj
                    .OrderByDescending(x => x.AttemptedAt)
                    .Take(1)
                    .DefaultIfEmpty()

                where
                    l.Id == lessonId &&
                    (
                        (!isIndividual && eq.ExamAssignmentId == examAssignmentId) ||
                        (isIndividual && eq.ExamAssignmentToStudentId == examAssignmentId)
                    )

                select new QdratNew.ViewModels.Exam.ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,

                    StudentAnswer = attempt != null ? attempt.SelectedAnswer : null,
                    CorrectAnswer = q.CorrectAnswer ?? "",

                    IsCorrect = attempt != null && attempt.IsCorrect,
                    IsSkipped = attempt == null,

                    Explanation = q.Explanation,
                    VideoUrl = q.VideoUrl,
                    TimeTakenSeconds = attempt != null ? attempt.TimeTakenSeconds : 0,

                    ImageUrl = q.ImageUrl,
                    IsQuantitative = q.IsQuantitative,

                    IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true,

                    DisplayType =
                        q.Template == QdratNew.Enums.QuestionTemplate.CompareValues
                            ? QdratNew.Enums.QuestionDisplayType.ComparisonText
                        : q.Template == QdratNew.Enums.QuestionTemplate.CompareWithImage
                            ? QdratNew.Enums.QuestionDisplayType.ComparisonWithImage
                        : QdratNew.Enums.QuestionDisplayType.TextOnly,

                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,

                    VerbalPassageTitle = q.VerbalPassage != null ? q.VerbalPassage.Title : null,
                    VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                    VerbalPassageMediaUrl = q.VerbalPassage != null ? q.VerbalPassage.MediaUrl : null,

                    VerbalPassageType = q.VerbalPassage != null
                        ? (QdratNew.Enums.PassageType?)q.VerbalPassage.Type
                        : null,

                    Options = q.Options.Select(opt => new QdratNew.ViewModels.Homework.QuestionOptionVm
                    {
                        Text = opt.Text,
                        ImageUrl = opt.ImageUrl
                    }).ToList()
                }
            )
            .AsNoTracking()
            .ToListAsync();

            // ======================================================
            // 3️⃣ 🔥 FALLBACK (ExamId)
            // ======================================================
            if (!questions.Any())
            {
                int? examId = isIndividual
                    ? await _context.ExamAssignmentsToStudents
                        .Where(x => x.Id == examAssignmentId)
                        .Select(x => x.ExamId)
                        .FirstOrDefaultAsync()
                    : await _context.ExamAssignmentsToBatches
                        .Where(x => x.Id == examAssignmentId)
                        .Select(x => x.ExamId)
                        .FirstOrDefaultAsync();

                if (examId != null)
                {
                    questions = await (
                        from eq in _context.ExamQuestions

                        join q in _context.Questions
                            .Include(x => x.Options)
                            .Include(x => x.VerbalPassage)
                            .Include(x => x.Curriculum)
                            on eq.QuestionId equals q.Id

                        join l in _context.Lessons on q.LessonId equals l.Id

                        join a in _context.QuestionAttemptNew
                            .Where(x =>
                                x.StudentId == studentId &&
                                (
                                    (!isIndividual && x.ExamAssignmentId == examAssignmentId) ||
                                    (isIndividual && x.ExamAssignmentToStudentId == examAssignmentId)
                                )
                            )
                            on q.Id equals a.QuestionId into gj

                        from attempt in gj
                            .OrderByDescending(x => x.AttemptedAt)
                            .Take(1)
                            .DefaultIfEmpty()

                        where eq.ExamId == examId && l.Id == lessonId

                        select new QdratNew.ViewModels.Exam.ExamReviewQuestionVm
                        {
                            QuestionId = q.Id,
                            QuestionTitle = q.Title,

                            StudentAnswer = attempt != null ? attempt.SelectedAnswer : null,
                            CorrectAnswer = q.CorrectAnswer ?? "",

                            IsCorrect = attempt != null && attempt.IsCorrect,
                            IsSkipped = attempt == null,

                            Explanation = q.Explanation,
                            VideoUrl = q.VideoUrl,
                            TimeTakenSeconds = attempt != null ? attempt.TimeTakenSeconds : 0,

                            ImageUrl = q.ImageUrl,
                            IsQuantitative = q.IsQuantitative,

                            IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true,

                            DisplayType =
                                q.Template == QdratNew.Enums.QuestionTemplate.CompareValues
                                    ? QdratNew.Enums.QuestionDisplayType.ComparisonText
                                : q.Template == QdratNew.Enums.QuestionTemplate.CompareWithImage
                                    ? QdratNew.Enums.QuestionDisplayType.ComparisonWithImage
                                : QdratNew.Enums.QuestionDisplayType.TextOnly,

                            ComparisonValue1 = q.ValueA,
                            ComparisonValue2 = q.ValueB,

                            VerbalPassageTitle = q.VerbalPassage != null ? q.VerbalPassage.Title : null,
                            VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                            VerbalPassageMediaUrl = q.VerbalPassage != null ? q.VerbalPassage.MediaUrl : null,

                            VerbalPassageType = q.VerbalPassage != null
                                ? (QdratNew.Enums.PassageType?)q.VerbalPassage.Type
                                : null,

                            Options = q.Options.Select(opt => new QdratNew.ViewModels.Homework.QuestionOptionVm
                            {
                                Text = opt.Text,
                                ImageUrl = opt.ImageUrl
                            }).ToList()
                        }
                    )
                    .AsNoTracking()
                    .ToListAsync();
                }
            }

            // ======================================================
            // 4️⃣ عدم وجود أسئلة
            // ======================================================
            if (!questions.Any())
                return NotFound("لا توجد أسئلة لهذا المؤشر في هذا الاختبار.");

            // ======================================================
            // 5️⃣ الاتجاه
            // ======================================================
            bool pageIsRTL = questions.FirstOrDefault()?.IsRTL ?? true;

            // ======================================================
            // 6️⃣ العناوين
            // ======================================================
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

            // ======================================================
            // 7️⃣ ViewModel
            // ======================================================
            var vm = new QdratNew.ViewModels.Exam.ExamReviewViewModel
            {
                LessonTitle = lessonTitle,
                SectionTitle = sectionTitle,

                LessonId = lessonId,
                ExamAssignmentId = examAssignmentId,

                TotalQuestions = questions.Count,

                TimeSpentMinutes = Math.Round(
                    questions.Sum(q => q.TimeTakenSeconds) / 60.0,
                    1
                ),

                IsRTL = pageIsRTL,
                Questions = questions
            };

            return View("LessonQuestions", vm);
        }


        private static ExamReviewQuestionVm MapExamReviewQuestionVm(Question q, QuestionAttemptNew? attempt)
        {
            return new ExamReviewQuestionVm
            {
                QuestionId = q.Id,
                QuestionTitle = q.Title,

                StudentAnswer = attempt != null ? attempt.SelectedAnswer : null,
                CorrectAnswer = q.CorrectAnswer,

                IsCorrect = attempt != null && attempt.IsCorrect,
                IsSkipped = attempt == null,

                Explanation = q.Explanation,
                VideoUrl = q.VideoUrl,

                LessonTitle = q.Lesson.Title,
                SectionTitle = q.Lesson.Section.Title,

                IsQuantitative = q.IsQuantitative,
                ImageUrl = q.ImageUrl,

                IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true,

                DisplayType =
                    q.Template == QuestionTemplate.CompareValues
                        ? QuestionDisplayType.ComparisonText
                    : q.Template == QuestionTemplate.CompareWithImage
                        ? QuestionDisplayType.ComparisonWithImage
                    : QuestionDisplayType.TextOnly,

                ComparisonValue1 = q.ValueA,
                ComparisonValue2 = q.ValueB,

                VerbalPassageTitle = q.VerbalPassage != null ? q.VerbalPassage.Title : null,
                VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                VerbalPassageMediaUrl = q.VerbalPassage != null ? q.VerbalPassage.MediaUrl : null,

                VerbalPassageType = q.VerbalPassage != null
                    ? (QdratNew.Enums.PassageType?)q.VerbalPassage.Type
                    : null,

                Options = q.Options.Select(o => new QuestionOptionVm
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl,
                    IsCorrect = o.Text == q.CorrectAnswer,
                    IsSelectedByStudent =
                        attempt != null && o.Text == attempt.SelectedAnswer
                }).ToList(),

                TimeTakenSeconds = attempt != null ? attempt.TimeTakenSeconds : 0
            };
        }

        [HttpGet]
        public async Task<IActionResult> ExamSectionQuestions(int examAssignmentId, int sectionId)
        {
            using var context = _contextFactory.CreateDbContext();

            int studentId = StudentId;

            // ======================================================
            // 1️⃣ تحديد نوع الاختبار (فردي / دفعة)
            // ======================================================
            bool isIndividual = await context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId);

            // ======================================================
            // 2️⃣ جلب الأسئلة (OLD SYSTEM)
            // ======================================================
            var rows1 = await (
                from eq in context.ExamQuestions

                join q in context.Questions
                    .Include(x => x.Options)
                    .Include(x => x.VerbalPassage)
                    .Include(x => x.Lesson)
                        .ThenInclude(l => l.Section)
                    .Include(x => x.Curriculum)
                    on eq.QuestionId equals q.Id

                join a in context.QuestionAttemptNew
                    .Where(a =>
                        a.StudentId == studentId &&
                        (
                            (!isIndividual && a.ExamAssignmentId == examAssignmentId) ||
                            (isIndividual && a.ExamAssignmentToStudentId == examAssignmentId)
                        ))
                    on q.Id equals a.QuestionId into gj

                from attempt in gj
                    .OrderByDescending(x => x.AttemptedAt)
                    .Take(1)
                    .DefaultIfEmpty()

                where
                    (
                        (!isIndividual && eq.ExamAssignmentId == examAssignmentId) ||
                        (isIndividual && eq.ExamAssignmentToStudentId == examAssignmentId)
                    )
                    && q.Lesson.SectionId == sectionId

                orderby eq.Order

                select new { q, attempt }
            )
            .AsNoTracking()
            .ToListAsync();

            var questions = rows1.Select(x => MapExamReviewQuestionVm(x.q, x.attempt)).ToList();

            // ======================================================
            // 3️⃣ 🔥 FALLBACK للنظام الجديد (ExamId)
            // ======================================================
            if (!questions.Any())
            {
                int? examId = isIndividual
                    ? await context.ExamAssignmentsToStudents
                        .Where(x => x.Id == examAssignmentId)
                        .Select(x => x.ExamId)
                        .FirstOrDefaultAsync()
                    : await context.ExamAssignmentsToBatches
                        .Where(x => x.Id == examAssignmentId)
                        .Select(x => x.ExamId)
                        .FirstOrDefaultAsync();

                if (examId != null)
                {
                    var rows2 = await (
                        from eq in context.ExamQuestions

                        join q in context.Questions
                            .Include(x => x.Options)
                            .Include(x => x.VerbalPassage)
                            .Include(x => x.Lesson)
                                .ThenInclude(l => l.Section)
                            .Include(x => x.Curriculum)
                            on eq.QuestionId equals q.Id

                        join a in context.QuestionAttemptNew
                            .Where(a =>
                                a.StudentId == studentId &&
                                (
                                    (!isIndividual && a.ExamAssignmentId == examAssignmentId) ||
                                    (isIndividual && a.ExamAssignmentToStudentId == examAssignmentId)
                                ))
                            on q.Id equals a.QuestionId into gj

                        from attempt in gj
                            .OrderByDescending(x => x.AttemptedAt)
                            .Take(1)
                            .DefaultIfEmpty()

                        where eq.ExamId == examId
                              && q.Lesson.SectionId == sectionId

                        orderby eq.Order

                        select new { q, attempt }
                    )
                    .AsNoTracking()
                    .ToListAsync();

                    questions = rows2.Select(x => MapExamReviewQuestionVm(x.q, x.attempt)).ToList();
                }
            }

            // ======================================================
            // 4️⃣ 🔥 FALLBACK نهائي: اعتمادًا على محاولات الطالب الفعلية
            //    (اختبارات فردية عُدِّلت/استُبدلت أسئلتها من شاشات الإدمن
            //    بعد أن يكون الطالب قد حل الاختبار فعليًا، فلم يعد جدول
            //    ExamQuestions الحالي يعكس ما حلّه الطالب. نعتمد هنا على
            //    محاولاته المحفوظة فعليًا في QuestionAttemptNew)
            // ======================================================
            if (!questions.Any())
            {
                var attemptRows = await (
                    from a in context.QuestionAttemptNew
                    join q in context.Questions
                        .Include(x => x.Options)
                        .Include(x => x.VerbalPassage)
                        .Include(x => x.Lesson)
                            .ThenInclude(l => l.Section)
                        .Include(x => x.Curriculum)
                        on a.QuestionId equals q.Id
                    where
                        a.StudentId == studentId &&
                        (
                            (!isIndividual && a.ExamAssignmentId == examAssignmentId) ||
                            (isIndividual && a.ExamAssignmentToStudentId == examAssignmentId)
                        )
                        && q.Lesson.SectionId == sectionId
                    select new { q, a }
                )
                .AsNoTracking()
                .ToListAsync();

                questions = attemptRows
                    .GroupBy(x => x.q.Id)
                    .Select(g => g.OrderByDescending(x => x.a.AttemptedAt).First())
                    .Select(x => MapExamReviewQuestionVm(x.q, x.a))
                    .ToList();
            }

            // ======================================================
            // 5️⃣ في حالة عدم وجود أسئلة
            // ======================================================
            if (!questions.Any())
                return Content("لا توجد أسئلة لهذا المحور في هذا الاختبار.");

            // ======================================================
            // 5️⃣ الاتجاه
            // ======================================================
            bool pageIsRTL = questions.FirstOrDefault()?.IsRTL ?? true;

            // ======================================================
            // 6️⃣ العنوان
            // ======================================================
            var sectionTitle = await context.Sections
                .Where(x => x.Id == sectionId)
                .Select(x => x.Title)
                .FirstOrDefaultAsync();

            // ======================================================
            // 7️⃣ ViewModel
            // ======================================================
            var vm = new ExamReviewViewModel
            {
                ExamAssignmentId = examAssignmentId,
                SectionTitle = sectionTitle,

                TotalQuestions = questions.Count,

                TimeSpentMinutes = Math.Round(
                    questions.Sum(q => q.TimeTakenSeconds) / 60.0,
                    1),

                IsRTL = pageIsRTL,
                Questions = questions
            };

            return View("ExamSectionQuestions", vm);
        }

        [HttpGet]
        public async Task<IActionResult> ReviewLessonQuestions(int lessonId, int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = StudentId;

            // ======================================================
            // 1️⃣ تحديد نوع الاختبار
            // ======================================================
            bool isIndividual = await _context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId);

            // ======================================================
            // 2️⃣ OLD SYSTEM (Assignment)
            // ======================================================
            var questions = await (
                from eq in _context.ExamQuestions

                join q in _context.Questions
                    .Include(x => x.Options)
                    .Include(x => x.VerbalPassage)
                    .Include(x => x.Curriculum)
                    on eq.QuestionId equals q.Id

                join l in _context.Lessons on q.LessonId equals l.Id

                join a in _context.QuestionAttemptNew
                    .Where(x =>
                        x.StudentId == studentId &&
                        (
                            (!isIndividual && x.ExamAssignmentId == examAssignmentId) ||
                            (isIndividual && x.ExamAssignmentToStudentId == examAssignmentId)
                        )
                    )
                    on q.Id equals a.QuestionId into gj

                from attempt in gj
                    .OrderByDescending(x => x.AttemptedAt)
                    .Take(1)
                    .DefaultIfEmpty()

                where
                    l.Id == lessonId &&
                    (
                        (!isIndividual && eq.ExamAssignmentId == examAssignmentId) ||
                        (isIndividual && eq.ExamAssignmentToStudentId == examAssignmentId)
                    )

                select new ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,

                    StudentAnswer = attempt != null ? attempt.SelectedAnswer : null,
                    CorrectAnswer = q.CorrectAnswer ?? "",

                    IsCorrect = attempt != null && attempt.IsCorrect,
                    IsSkipped = attempt == null,

                    TimeTakenSeconds = attempt != null ? attempt.TimeTakenSeconds : 0,
                    Explanation = q.Explanation,
                    VideoUrl = q.VideoUrl,
                    ImageUrl = q.ImageUrl,
                    IsQuantitative = q.IsQuantitative,

                    IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true,

                    DisplayType =
                        q.Template == QuestionTemplate.CompareValues
                            ? QuestionDisplayType.ComparisonText
                        : q.Template == QuestionTemplate.CompareWithImage
                            ? QuestionDisplayType.ComparisonWithImage
                            : QuestionDisplayType.TextOnly,

                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,

                    VerbalPassageTitle = q.VerbalPassage != null ? q.VerbalPassage.Title : null,
                    VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                    VerbalPassageMediaUrl = q.VerbalPassage != null ? q.VerbalPassage.MediaUrl : null,

                    VerbalPassageType = q.VerbalPassage != null
                        ? (QdratNew.Enums.PassageType?)q.VerbalPassage.Type
                        : null,

                    Options = q.Options.Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.Text == q.CorrectAnswer,
                        IsSelectedByStudent =
                            attempt != null && o.Text == attempt.SelectedAnswer
                    }).ToList()
                }
            )
            .AsNoTracking()
            .ToListAsync();

            // ======================================================
            // 3️⃣ 🔥 FALLBACK (ExamId)
            // ======================================================
            if (!questions.Any())
            {
                int? examId = isIndividual
                    ? await _context.ExamAssignmentsToStudents
                        .Where(x => x.Id == examAssignmentId)
                        .Select(x => x.ExamId)
                        .FirstOrDefaultAsync()
                    : await _context.ExamAssignmentsToBatches
                        .Where(x => x.Id == examAssignmentId)
                        .Select(x => x.ExamId)
                        .FirstOrDefaultAsync();

                if (examId != null)
                {
                    questions = await (
                        from eq in _context.ExamQuestions

                        join q in _context.Questions
                            .Include(x => x.Options)
                            .Include(x => x.VerbalPassage)
                            .Include(x => x.Curriculum)
                            on eq.QuestionId equals q.Id

                        join l in _context.Lessons on q.LessonId equals l.Id

                        join a in _context.QuestionAttemptNew
                            .Where(x =>
                                x.StudentId == studentId &&
                                (
                                    (!isIndividual && x.ExamAssignmentId == examAssignmentId) ||
                                    (isIndividual && x.ExamAssignmentToStudentId == examAssignmentId)
                                )
                            )
                            on q.Id equals a.QuestionId into gj

                        from attempt in gj
                            .OrderByDescending(x => x.AttemptedAt)
                            .Take(1)
                            .DefaultIfEmpty()

                        where eq.ExamId == examId && l.Id == lessonId

                        select new ExamReviewQuestionVm
                        {
                            QuestionId = q.Id,
                            QuestionTitle = q.Title,

                            StudentAnswer = attempt != null ? attempt.SelectedAnswer : null,
                            CorrectAnswer = q.CorrectAnswer ?? "",

                            IsCorrect = attempt != null && attempt.IsCorrect,
                            IsSkipped = attempt == null,

                            TimeTakenSeconds = attempt != null ? attempt.TimeTakenSeconds : 0,
                            Explanation = q.Explanation,
                            VideoUrl = q.VideoUrl,
                            ImageUrl = q.ImageUrl,
                            IsQuantitative = q.IsQuantitative,

                            IsRTL = q.Curriculum != null ? q.Curriculum.IsRTL : true,

                            DisplayType =
                                q.Template == QuestionTemplate.CompareValues
                                    ? QuestionDisplayType.ComparisonText
                                : q.Template == QuestionTemplate.CompareWithImage
                                    ? QuestionDisplayType.ComparisonWithImage
                                    : QuestionDisplayType.TextOnly,

                            ComparisonValue1 = q.ValueA,
                            ComparisonValue2 = q.ValueB,

                            VerbalPassageTitle = q.VerbalPassage != null ? q.VerbalPassage.Title : null,
                            VerbalPassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null,
                            VerbalPassageMediaUrl = q.VerbalPassage != null ? q.VerbalPassage.MediaUrl : null,

                            VerbalPassageType = q.VerbalPassage != null
                                ? (QdratNew.Enums.PassageType?)q.VerbalPassage.Type
                                : null,

                            Options = q.Options.Select(o => new QuestionOptionVm
                            {
                                Text = o.Text,
                                ImageUrl = o.ImageUrl,
                                IsCorrect = o.Text == q.CorrectAnswer,
                                IsSelectedByStudent =
                                    attempt != null && o.Text == attempt.SelectedAnswer
                            }).ToList()
                        }
                    )
                    .AsNoTracking()
                    .ToListAsync();
                }
            }

            // ======================================================
            // 4️⃣ عدم وجود أسئلة
            // ======================================================
            if (!questions.Any())
                return NotFound("❌ لا توجد أسئلة لهذا المؤشر داخل هذا الاختبار.");

            // ======================================================
            // 5️⃣ الاتجاه
            // ======================================================
            bool pageIsRTL = questions.FirstOrDefault()?.IsRTL ?? true;

            // ======================================================
            // 6️⃣ العناوين
            // ======================================================
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

            // ======================================================
            // 7️⃣ ViewModel
            // ======================================================
            var vm = new ExamReviewViewModel
            {
                LessonId = lessonId,
                ExamAssignmentId = examAssignmentId,

                LessonTitle = lessonTitle,
                SectionTitle = sectionTitle,

                TotalQuestions = questions.Count,

                TimeSpentMinutes = Math.Round(
                    questions.Sum(q => q.TimeTakenSeconds) / 60.0,
                    1
                ),

                IsRTL = pageIsRTL,
                Questions = questions
            };

            return View("ReviewLessonQuestions", vm);
        }



        [HttpGet]
        public async Task<IActionResult> ExamReport(int examAssignmentId)
        {
            using var context = _contextFactory.CreateDbContext();

            var studentId = StudentId;

            // ======================================================
            // 1) تحديد هل فردي أم دفعة
            // ======================================================
            bool isIndividual = await context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId);

            Exam exam;
            string examTitle;
            int durationMinutes;
            DateTime examDate;

            if (isIndividual)
            {
                var assign = await context.ExamAssignmentsToStudents
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a =>
                        a.Id == examAssignmentId &&
                        a.StudentId == studentId);

                if (assign == null)
                    return NotFound();

                exam = assign.Exam;
                examTitle = exam.Title;
                durationMinutes = assign.DurationMinutes;
                examDate = assign.ScheduledDate == default
                    ? assign.CreatedAt
                    : assign.ScheduledDate.Value;
            }
            else
            {
                var assign = await context.ExamAssignmentsToBatches
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
            // 2) جلب ExamStudentStatus (مهم جدًا)
            // ======================================================
            ExamStudentStatus? status;

            if (isIndividual)
            {
                status = await context.ExamStudentStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == studentId &&
                        s.ExamAssignmentToStudentId == examAssignmentId &&
                        (s.Status == ExamStatus.Completed || s.IsSubmitted));
            }
            else
            {
                status = await context.ExamStudentStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == studentId &&
                        s.ExamAssignmentId == examAssignmentId &&
                        (s.Status == ExamStatus.Completed || s.IsSubmitted));
            }

            if (status == null)
            {
                TempData["ErrorMessage"] = "لم يتم العثور على نتيجة لهذا الاختبار.";
                return RedirectToAction("AllExams", "StudentExamPages", new { area = "Students" });
            }

            // ======================================================
            // 3) Snapshot
            // ======================================================
            if (string.IsNullOrEmpty(status.Note))
            {
                await _examResultEngine.GenerateSnapshotIfMissingAsync(
                    examAssignmentId,
                    studentId
                );

                if (isIndividual)
                {
                    status = await context.ExamStudentStatuses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.StudentId == studentId &&
                            s.ExamAssignmentToStudentId == examAssignmentId);
                }
                else
                {
                    status = await context.ExamStudentStatuses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.StudentId == studentId &&
                            s.ExamAssignmentId == examAssignmentId);
                }

                if (status == null || string.IsNullOrEmpty(status.Note))
                {
                    TempData["ErrorMessage"] = "تعذر إنشاء تقرير لهذا الاختبار.";
                    return RedirectToAction("AllExams", "StudentExamPages", new { area = "Students" });
                }
            }

            var snapshot = JsonSerializer.Deserialize<ExamFinalResultDto>(status.Note);
            if (snapshot == null)
            {
                TempData["ErrorMessage"] = "بيانات التقرير غير صالحة.";
                return RedirectToAction("AllExams", "StudentExamPages", new { area = "Students" });
            }

            // ======================================================
            // 4) الوقت
            // ======================================================
            double solveMinutes = snapshot.TotalTimeSeconds > 0
                ? Math.Round(snapshot.TotalTimeSeconds / 60.0, 1)
                : 0;

            double percentTime = durationMinutes > 0
                ? Math.Round((solveMinutes / durationMinutes) * 100, 1)
                : 0;

            var rec = _recommendationService.GetRecommendation(
                snapshot.ScorePercent,
                (int)solveMinutes,
                durationMinutes
            );

            // ======================================================
            // 5) أسماء المحاور
            // ======================================================
            var sectionTitles = await context.Sections
                .Where(s => snapshot.Sections.Keys.Contains(s.Id))
                .Select(s => new { s.Id, s.Title })
                .ToDictionaryAsync(x => x.Id, x => x.Title);

            var sectionStats = snapshot.Sections.Select(sec => new ExamSectionPerformancesVm
            {
                SectionId = sec.Key,
                SectionName = sectionTitles.TryGetValue(sec.Key, out var t) ? t : "محور غير معروف",
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
            // 6) بيانات الشارت (فردي / دفعة)
            // ======================================================
            var chartRows = await (
                from eq in context.ExamQuestions
                join q in context.Questions
                    .Include(x => x.Lesson)
                        .ThenInclude(l => l.Section)
                    on eq.QuestionId equals q.Id
                where isIndividual
                    ? eq.ExamAssignmentToStudentId == examAssignmentId
                    : eq.ExamAssignmentId == examAssignmentId
                select new
                {
                    q.IsQuantitative,
                    SectionId = q.Lesson.Section.Id,
                    SectionTitle = q.Lesson.Section.Title
                }
            ).ToListAsync();

            var quantGroups = chartRows.Where(x => x.IsQuantitative).GroupBy(x => new { x.SectionId, x.SectionTitle }).ToList();
            var verbalGroups = chartRows.Where(x => !x.IsQuantitative).GroupBy(x => new { x.SectionId, x.SectionTitle }).ToList();

            // ======================================================
            // 7) ViewModel
            // ======================================================
            var vm = new ExamDetailedReportViewModel
            {
                StudentId = studentId,
                StudentName = await context.Students
                    .Where(s => s.StudentID == studentId)
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

                HasQuantChart = quantGroups.Any(),
                HasVerbalChart = verbalGroups.Any(),

                QuantLabels = quantGroups.Select(g => g.Key.SectionTitle).ToList(),
                QuantCorrectCounts = quantGroups.Select(g => snapshot.Sections[g.Key.SectionId].Correct).ToList(),
                QuantWrongCounts = quantGroups.Select(g => snapshot.Sections[g.Key.SectionId].Wrong + snapshot.Sections[g.Key.SectionId].Skipped).ToList(),

                VerbalLabels = verbalGroups.Select(g => g.Key.SectionTitle).ToList(),
                VerbalCorrectCounts = verbalGroups.Select(g => snapshot.Sections[g.Key.SectionId].Correct).ToList(),
                VerbalWrongCounts = verbalGroups.Select(g => snapshot.Sections[g.Key.SectionId].Wrong + snapshot.Sections[g.Key.SectionId].Skipped).ToList(),

                TrackCard1 = rec.TrackCard1,
                TrackCard1Desc = rec.TrackCard1Desc,
                TrackCard2 = rec.TrackCard2,
                TrackCard2Desc = rec.TrackCard2Desc,
                SpeedLabel = rec.SpeedLabel,
                SpeedNote = rec.SpeedNote,
                IndividualTips = rec.IndividualTips
            };

            return View("ExamReport", vm);
        }


        [HttpGet]
        public async Task<IActionResult> ExamSectionLessonsReport(int examAssignmentId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = StudentId;

            bool isIndividual = await _context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId);

            // ======================================================
            // 1️⃣ OLD SYSTEM (Assignment)
            // ======================================================
            var raw = await (
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
                    l.Id,
                    l.Title,
                    IsCorrect = att != null && att.IsCorrect,
                    HasAttempt = att != null
                }
            ).ToListAsync();

            // ======================================================
            // 2️⃣ 🔥 FALLBACK (ExamId)
            // ======================================================
            if (!raw.Any())
            {
                int? examId = isIndividual
                    ? await _context.ExamAssignmentsToStudents
                        .Where(x => x.Id == examAssignmentId)
                        .Select(x => x.ExamId)
                        .FirstOrDefaultAsync()
                    : await _context.ExamAssignmentsToBatches
                        .Where(x => x.Id == examAssignmentId)
                        .Select(x => x.ExamId)
                        .FirstOrDefaultAsync();

                if (examId != null)
                {
                    raw = await (
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

                        where eq.ExamId == examId && l.SectionId == sectionId

                        select new
                        {
                            l.Id,
                            l.Title,
                            IsCorrect = att != null && att.IsCorrect,
                            HasAttempt = att != null
                        }
                    ).ToListAsync();
                }
            }

            // ======================================================
            // 3️⃣ 🔥 FALLBACK نهائي: اعتمادًا على محاولات الطالب الفعلية
            //    (اختبارات فردية عُدِّلت/استُبدلت أسئلتها من شاشات الإدمن
            //    بعد أن يكون الطالب قد حل الاختبار فعليًا، فلم يعد جدول
            //    ExamQuestions الحالي يعكس ما حلّه الطالب)
            // ======================================================
            if (!raw.Any())
            {
                var attemptRows = await (
                    from a in _context.QuestionAttemptNew
                    join q in _context.Questions on a.QuestionId equals q.Id
                    join l in _context.Lessons on q.LessonId equals l.Id
                    where
                        a.StudentId == studentId &&
                        (
                            (!isIndividual && a.ExamAssignmentId == examAssignmentId) ||
                            (isIndividual && a.ExamAssignmentToStudentId == examAssignmentId)
                        )
                        && l.SectionId == sectionId
                    select new
                    {
                        l.Id,
                        l.Title,
                        a.QuestionId,
                        a.IsCorrect,
                        a.AttemptedAt
                    }
                ).ToListAsync();

                raw = attemptRows
                    .GroupBy(x => x.QuestionId)
                    .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                    .Select(x => new
                    {
                        x.Id,
                        x.Title,
                        IsCorrect = x.IsCorrect,
                        HasAttempt = true
                    })
                    .ToList();
            }

            // ======================================================
            // 4️⃣ التجميع
            // ======================================================
            var grouped = raw
                .GroupBy(x => new { x.Id, x.Title })
                .Select(g => new ExamLessonSummaryVm
                {
                    LessonId = g.Key.Id,
                    LessonTitle = g.Key.Title,
                    TotalQuestions = g.Count(),
                    Correct = g.Count(x => x.IsCorrect),
                    Wrong = g.Count(x => x.HasAttempt && !x.IsCorrect),
                    Skipped = g.Count(x => !x.HasAttempt)
                })
                .OrderBy(x => x.LessonTitle)
                .ToList();

            // ======================================================
            // 4️⃣ البيانات الإضافية
            // ======================================================
            ViewBag.SectionTitle = await _context.Sections
                .Where(s => s.Id == sectionId)
                .Select(s => s.Title)
                .FirstOrDefaultAsync();

            ViewBag.ExamAssignmentId = examAssignmentId;
            ViewBag.IsIndividual = isIndividual;

            return View(grouped);
        }


    }
}
