using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.EnhancementSkills;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Question;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class StudentEnhancementSkillsController : StudentBaseController
    {
        private readonly new IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ILogger<StudentEnhancementSkillsController> _logger;
        private readonly IIntegrityGuardService _integrityGuard;

        public StudentEnhancementSkillsController(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            UserManager<ApplicationUser> userManager,
            ILogger<StudentEnhancementSkillsController> logger,
            IIntegrityGuardService integrityGuard
        ) : base(contextFactory, userManager)
        {
            _contextFactory = contextFactory;
            _logger = logger;
            _integrityGuard = integrityGuard;
        }

        // ────────────────────────────────────────────────────
        // Helper: جلب كل مجموعات المهارات للطالب في الدفعة الحالية
        // ────────────────────────────────────────────────────
        private async Task<List<StudentEnhancementCardViewModel>> GetEnhancementCardsAsync(
            ApplicationDbContext db, bool? onlySubmitted = null)
        {
            var studentId = StudentId;
            var batchId = ActiveBatchId;
            var now = DateTime.Now;

            var raw = await (
                from a in db.EnhancementSkillAssignments.AsNoTracking()
                join s in db.EnhancementSkillSets.AsNoTracking() on a.EnhancementSkillSetId equals s.Id
                join lec in db.Lecture.AsNoTracking() on s.LectureId equals lec.Id into lecJoin
                from lec in lecJoin.DefaultIfEmpty()
                where a.StudentId == studentId && s.BatchId == batchId && s.IsSent
                select new
                {
                    a.EnhancementSkillSetId,
                    a.IsCorrect,
                    a.AnsweredAt,
                    SetTitle = s.Title,
                    s.CreatedAt,
                    s.EndAt,
                    LectureTitle = lec != null ? lec.Title : null
                }
            ).ToListAsync();

            if (!raw.Any()) return new List<StudentEnhancementCardViewModel>();

            var cards = raw
                .GroupBy(x => x.EnhancementSkillSetId)
                .Select(g =>
                {
                    var first = g.First();
                    var total = g.Count();
                    var submitted = g.All(x => x.AnsweredAt != null);
                    var correct = g.Count(x => x.IsCorrect == true);

                    string delay = submitted ? "محلول"
                        : (first.EndAt.HasValue && first.EndAt.Value < now) ? "متأخر"
                        : "مطلوب";

                    return new StudentEnhancementCardViewModel
                    {
                        SetId = g.Key,
                        Title = first.SetTitle,
                        LectureTitle = first.LectureTitle,
                        CreatedAt = first.CreatedAt,
                        EndAt = first.EndAt,
                        QuestionsCount = total,
                        IsSubmitted = submitted,
                        Score = submitted && total > 0 ? correct * 100.0 / total : 0,
                        BatchAverageScore = 0, // computed separately when needed
                        DelayLevel = delay
                    };
                })
                .ToList();

            if (onlySubmitted == true)
                return cards.Where(c => c.IsSubmitted).ToList();
            if (onlySubmitted == false)
                return cards.Where(c => !c.IsSubmitted).ToList();

            return cards;
        }

        // ────────────────────────────────────────────────────
        // Dashboard
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            using var db = _contextFactory.CreateDbContext();

            var cards = await GetEnhancementCardsAsync(db);

            var total = cards.Count;
            var completed = cards.Count(c => c.IsSubmitted);
            var pending = cards.Count(c => !c.IsSubmitted && c.DelayLevel != "متأخر");
            var late = cards.Count(c => c.DelayLevel == "متأخر");

            // مناهج الدورة الحالية
            var curriculums = await (
                from cc in db.CourseCurriculums.AsNoTracking()
                join c in db.Curriculums.AsNoTracking() on cc.CurriculumId equals c.Id
                where cc.CourseId == ActiveCourseId
                select new QdratNew.ViewModels.Curriculum.CurriculumViewModel { Id = c.Id, Title = c.Title }
            ).Distinct().ToListAsync();

            var vm = new StudentEnhancementDashboardViewModel
            {
                TotalAssigned = total,
                Completed = completed,
                Pending = pending,
                Late = late,
                Curriculums = curriculums
            };

            return View(vm);
        }

        // ────────────────────────────────────────────────────
        // GetCurriculumData (AJAX للرسوم البيانية)
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetCurriculumData(int curriculumId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentId = StudentId;
            var batchId = ActiveBatchId;

            // ─ جلب محاولات الطالب ─
            var attempts = await (
                from a in db.EnhancementSkillAssignments.AsNoTracking()
                join s in db.EnhancementSkillSets.AsNoTracking() on a.EnhancementSkillSetId equals s.Id
                join q in db.Questions.AsNoTracking() on a.QuestionId equals q.Id
                join sec in db.Sections.AsNoTracking() on q.SectionId equals sec.Id into secJoin
                from sec in secJoin.DefaultIfEmpty()
                join cur in db.Curriculums.AsNoTracking() on sec.CurriculumId equals cur.Id into curJoin
                from cur in curJoin.DefaultIfEmpty()
                where a.StudentId == studentId && s.BatchId == batchId && s.IsSent
                select new
                {
                    a.EnhancementSkillSetId,
                    SetTitle = s.Title,
                    s.CreatedAt,
                    a.IsCorrect,
                    SectionId = sec != null ? sec.Id : (int?)null,
                    SectionTitle = sec != null ? sec.Title : null,
                    CurriculumId = cur != null ? cur.Id : (int?)null
                }
            ).ToListAsync();

            if (!attempts.Any()) return Json(EmptyCharts());

            if (curriculumId > 0)
                attempts = attempts.Where(a => a.CurriculumId == curriculumId).ToList();

            if (!attempts.Any()) return Json(EmptyCharts());

            // ─ Radar: أداء حسب المحاور ─
            var radar = attempts
                .Where(a => a.SectionId != null)
                .GroupBy(a => new { a.SectionId, a.SectionTitle })
                .OrderBy(g => g.Key.SectionTitle)
                .Select(g =>
                {
                    var total = g.Count();
                    var correct = g.Count(x => x.IsCorrect == true);
                    return new
                    {
                        g.Key.SectionTitle,
                        Score = total > 0 ? Math.Round(correct * 100.0 / total, 1) : 0.0
                    };
                })
                .ToList();

            var sectionLabels = radar.Select(r => r.SectionTitle ?? "").ToList();
            var studentScores = radar.Select(r => r.Score).ToList();
            var batchAverages = radar.Select(_ => 0.0).ToList(); // placeholder

            // ─ Progress: تطور الأداء عبر الجلسات ─
            var progressGroups = attempts
                .GroupBy(a => new { a.EnhancementSkillSetId, a.SetTitle, a.CreatedAt })
                .OrderBy(g => g.Key.CreatedAt)
                .ToList();

            var progressLabels = new List<string>();
            var correctAnswers = new List<int>();
            var remainingQuestions = new List<int>();
            var studentSetScores = new List<double>();
            var batchSetAverages = new List<double>();

            int idx = 1;
            foreach (var g in progressGroups)
            {
                var total = g.Count();
                var correct = g.Count(x => x.IsCorrect == true);

                progressLabels.Add(g.Key.SetTitle ?? $"جلسة {idx}");
                correctAnswers.Add(correct);
                remainingQuestions.Add(Math.Max(0, total - correct));
                studentSetScores.Add(total > 0 ? Math.Round(correct * 100.0 / total, 1) : 0);
                batchSetAverages.Add(0); // placeholder للمقارنة مع الدفعة
                idx++;
            }

            return Json(new
            {
                sectionLabels,
                studentScores,
                batchAverages,
                homeworkTitles = progressLabels,
                correctAnswers,
                remainingQuestions,
                studentHomeworkScores = studentSetScores,
                batchHomeworkAverages = batchSetAverages
            });
        }

        private static object EmptyCharts() => new
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

        // ────────────────────────────────────────────────────
        // كل المهارات
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> AllEnhancements()
        {
            using var db = _contextFactory.CreateDbContext();
            var cards = await GetEnhancementCardsAsync(db);
            return View(cards);
        }

        // للتوافق مع الروابط القديمة
        [HttpGet]
        public IActionResult AvailableEnhancements()
            => RedirectToAction(nameof(AllEnhancements));

        // ────────────────────────────────────────────────────
        // المحلولة
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> SolvedEnhancements()
        {
            using var db = _contextFactory.CreateDbContext();
            var cards = await GetEnhancementCardsAsync(db, onlySubmitted: true);
            return View(cards);
        }

        // ────────────────────────────────────────────────────
        // المطلوبة
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> RequiredEnhancements()
        {
            using var db = _contextFactory.CreateDbContext();
            var cards = (await GetEnhancementCardsAsync(db, onlySubmitted: false))
                .Where(c => c.DelayLevel == "مطلوب")
                .ToList();
            return View(cards);
        }

        // ────────────────────────────────────────────────────
        // المتأخرة
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> LateEnhancements()
        {
            using var db = _contextFactory.CreateDbContext();
            var cards = (await GetEnhancementCardsAsync(db, onlySubmitted: false))
                .Where(c => c.DelayLevel == "متأخر")
                .ToList();
            return View(cards);
        }

        // ────────────────────────────────────────────────────
        // بدء حل المهارة
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Start(int setId, Guid? q = null)
        {
            using var db = _contextFactory.CreateDbContext();
            var studentId = StudentId;

            if (await _integrityGuard.IsBlockedAsync(studentId, IntegrityAttemptType.EnhancementSkill, setId))
                return RedirectToAction("Blocked", "IntegrityGuard", new { area = "Students", attemptType = (int)IntegrityAttemptType.EnhancementSkill, attemptEntityId = setId, returnUrl = $"{Request.Path}{Request.QueryString}" });

            var assignments = await db.EnhancementSkillAssignments
                .Include(a => a.Question)
                    .ThenInclude(qs => qs.Options)
                .Include(a => a.Question)
                    .ThenInclude(qs => qs.Lesson)
                        .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                .Where(a => a.EnhancementSkillSetId == setId && a.StudentId == studentId)
                .OrderBy(a => a.Id)
                .ToListAsync();

            if (!assignments.Any())
                return NotFound("لا توجد أسئلة مرتبطة بهذه الجلسة.");

            if (assignments.All(a => a.AnsweredAt != null))
                return RedirectToAction(nameof(Result), new { id = setId });

            var allQuestions = assignments.Select(a => a.QuestionId).ToList();

            Guid currentId = q.HasValue && allQuestions.Contains(q.Value)
                ? q.Value
                : allQuestions.First();

            var current = assignments.First(a => a.QuestionId == currentId);

            var vm = BuildSolveVm(setId, currentId, allQuestions, current, assignments);
            return View("Start", vm);
        }

        // ────────────────────────────────────────────────────
        // AJAX: تسليم إجابة والانتقال للسؤال التالي
        // id و q من الـ query string (مثل الواجبات)
        // ────────────────────────────────────────────────────
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SubmitAnswerFetch(
            [FromQuery] int id,
            [FromQuery] Guid q,
            string? SelectedOption,
            string nav,
            int timeTakenSeconds = 0)
        {
            using var db = _contextFactory.CreateDbContext();

            // جلب studentId مباشرة بدون الاعتماد على الـ base filter
            var userId = _userManager.GetUserId(User);
            var student = await db.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);
            if (student == null) return Unauthorized();

            var studentId = student.StudentID;

            var assignments = await db.EnhancementSkillAssignments
                .Include(a => a.Question)
                    .ThenInclude(qs => qs.Options)
                .Include(a => a.Question)
                    .ThenInclude(qs => qs.Lesson)
                        .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                .Where(a => a.EnhancementSkillSetId == id && a.StudentId == studentId)
                .OrderBy(a => a.Id)
                .ToListAsync();

            if (!assignments.Any())
                return NotFound("لا توجد أسئلة لهذه الجلسة.");

            // حفظ الإجابة إن وجدت
            var current = assignments.FirstOrDefault(a => a.QuestionId == q);
            if (current != null && !string.IsNullOrWhiteSpace(SelectedOption) && current.AnsweredAt == null)
            {
                current.StudentAnswer = SelectedOption;
                current.AnsweredAt    = DateTime.Now;
                current.IsCorrect     = current.StudentAnswer == current.Question.CorrectAnswer;
                await db.SaveChangesAsync();
            }

            // إدارة المراجعة عبر Session
            var reviewKey  = $"EnhReview_{id}_{studentId}";
            var reviewJson = HttpContext.Session.GetString(reviewKey);
            var reviewIds  = string.IsNullOrEmpty(reviewJson)
                ? new List<Guid>()
                : System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(reviewJson)!;

            if (nav == "review" && !reviewIds.Contains(q))
                reviewIds.Add(q);

            HttpContext.Session.SetString(reviewKey,
                System.Text.Json.JsonSerializer.Serialize(reviewIds));

            // تحديد السؤال التالي
            var allIds = assignments.Select(a => a.QuestionId).ToList();
            int index  = allIds.IndexOf(q);
            Guid nextId = q;

            if (nav == "next" && index < allIds.Count - 1) nextId = allIds[index + 1];
            else if (nav == "prev" && index > 0)           nextId = allIds[index - 1];

            var next = assignments.FirstOrDefault(a => a.QuestionId == nextId)
                       ?? assignments.First();

            var vm = BuildSolveVm(id, nextId, allIds, next, assignments);
            vm.ReviewMarkedIds = reviewIds;
            vm.AnswersMap      = assignments.ToDictionary(x => x.QuestionId, x => x.StudentAnswer);

            return PartialView("_SolveEnhancementQuestionPartial", vm);
        }

        // ────────────────────────────────────────────────────
        // تسليم نهائي
        // ────────────────────────────────────────────────────
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SubmitFinal([FromQuery] int id)
        {
            using var db = _contextFactory.CreateDbContext();

            var userId = _userManager.GetUserId(User);
            var student = await db.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId);
            if (student == null) return Unauthorized();

            var studentId = student.StudentID;

            var assignments = await db.EnhancementSkillAssignments
                .Include(a => a.Question)
                .Where(a => a.EnhancementSkillSetId == id && a.StudentId == studentId)
                .ToListAsync();

            if (!assignments.Any())
                return RedirectToAction(nameof(Start), new { setId = id });

            if (assignments.All(a => a.AnsweredAt != null))
                return RedirectToAction(nameof(Result), new { id });

            foreach (var a in assignments.Where(a => a.AnsweredAt == null))
            {
                a.AnsweredAt = DateTime.Now;
                a.IsCorrect  = !string.IsNullOrEmpty(a.StudentAnswer)
                               && a.StudentAnswer == a.Question.CorrectAnswer;
            }

            await db.SaveChangesAsync();
            return RedirectToAction(nameof(Result), new { id });
        }

        // ────────────────────────────────────────────────────
        // النتيجة
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Result(int id)
        {
            using var db = _contextFactory.CreateDbContext();
            var studentId = StudentId;

            var assignments = await db.EnhancementSkillAssignments
                .Include(a => a.EnhancementSkillSet)
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                .Where(a => a.EnhancementSkillSetId == id && a.StudentId == studentId)
                .ToListAsync();

            if (!assignments.Any())
                return NotFound("لم يتم العثور على بيانات هذه الجلسة.");

            var total          = assignments.Count;
            var correct        = assignments.Count(a => a.IsCorrect == true);
            var curriculum0    = assignments.First().Question?.Lesson?.Section?.Curriculum;
            var isQuantitative = curriculum0?.IsQuantitative ?? assignments.First().Question?.IsQuantitative ?? false;
            var isRTL0         = curriculum0?.IsRTL ?? true;

            var vm = new EnhancementResultViewModel
            {
                SetId           = id,
                Title           = assignments.First().EnhancementSkillSet?.Title ?? "",
                TotalQuestions  = total,
                CorrectAnswers  = correct,
                ScorePercentage = total > 0 ? correct * 100.0 / total : 0,
                IsQuantitative  = isQuantitative,
                IsRTL           = isRTL0,
                Questions = assignments.Select(a => new EnhancementResultItemViewModel
                {
                    QuestionTitle = a.Question.Title,
                    StudentAnswer = a.StudentAnswer,
                    CorrectAnswer = a.Question.CorrectAnswer,
                    IsCorrect     = a.IsCorrect ?? false
                }).ToList()
            };

            // تسجيل الأداء في StudentPerformance
            var grouped = assignments
                .Where(a => a.Question?.SectionId != null)
                .GroupBy(a => a.Question.SectionId!.Value);

            foreach (var g in grouped)
            {
                var secCorrect = g.Count(a => a.IsCorrect == true);
                var secTotal = g.Count();
                var score = secTotal > 0 ? secCorrect * 100.0 / secTotal : 0;
                var curriculumId = g.First().Question.Section?.CurriculumId ?? 0;

                db.StudentPerformances.Add(new StudentPerformance
                {
                    StudentID = studentId,
                    SectionId = g.Key,
                    CurriculumId = curriculumId,
                    Score = score,
                    ExamDate = DateTime.Now
                });
            }

            await db.SaveChangesAsync();
            return View("Result", vm);
        }

        // ────────────────────────────────────────────────────
        // مراجعة الإجابات
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Review(int id)
        {
            using var db = _contextFactory.CreateDbContext();
            var studentId = StudentId;

            var assignments = await db.EnhancementSkillAssignments
                .Include(a => a.EnhancementSkillSet)
                .Include(a => a.Question)
                    .ThenInclude(q => q.Options)
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                .Where(a => a.EnhancementSkillSetId == id && a.StudentId == studentId)
                .ToListAsync();

            if (!assignments.Any())
                return NotFound("لم يتم العثور على أي إجابات.");

            var curricR    = assignments.First().Question?.Lesson?.Section?.Curriculum;
            var isQuantR   = curricR?.IsQuantitative ?? assignments.First().Question?.IsQuantitative ?? false;
            var isRTLR     = curricR?.IsRTL ?? true;

            var vm = new EnhancementReviewViewModel
            {
                SetId          = id,
                Title          = assignments.First().EnhancementSkillSet?.Title ?? "",
                TotalQuestions = assignments.Count,
                CorrectAnswers = assignments.Count(a => a.IsCorrect == true),
                WrongAnswers   = assignments.Count(a => a.IsCorrect == false),
                TimeSpentMinutes = 0,
                IsQuantitative = isQuantR,
                IsRTL          = isRTLR,
                Questions = assignments.Select(a =>
                {
                    var qCurric  = a.Question?.Lesson?.Section?.Curriculum;
                    return new EnhancementReviewQuestionVm
                    {
                        QuestionId    = a.QuestionId,
                        QuestionText  = a.Question.Title ?? "",
                        StudentAnswer = a.StudentAnswer,
                        CorrectAnswer = a.Question.CorrectAnswer,
                        IsCorrect     = a.IsCorrect ?? false,
                        IsQuantitative = qCurric?.IsQuantitative ?? a.Question.IsQuantitative,
                        IsRTL          = qCurric?.IsRTL ?? true,
                        Options = a.Question.Options.Select(o => new QuestionOptionVm
                        {
                            Text     = o.Text,
                            ImageUrl = o.ImageUrl
                        }).ToList(),
                        ImageUrl             = a.Question.ImageUrl,
                        VideoUrl             = a.Question.VideoUrl,
                        DisplayType = a.Question.Template switch
                        {
                            QuestionTemplate.CompareValues    => QuestionDisplayType.ComparisonText,
                            QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                            _                                 => QuestionDisplayType.WithImage
                        },
                        ComparisonValue1     = a.Question.ValueA,
                        ComparisonValue2     = a.Question.ValueB,
                        VerbalPassageContent = a.Question.VerbalPassage?.Content
                    };
                }).ToList()
            };

            return View("Review", vm);
        }

        // ────────────────────────────────────────────────────
        // التقرير التفصيلي
        // ────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Report(int setId, int? studentId = null)
        {
            using var db = _contextFactory.CreateDbContext();

            if (!studentId.HasValue)
                studentId = StudentId;

            var assignments = await db.EnhancementSkillAssignments
                .Include(a => a.EnhancementSkillSet)
                    .ThenInclude(s => s.Lecture)
                .Include(a => a.Question)
                    .ThenInclude(q => q.Section)
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Where(a => a.EnhancementSkillSetId == setId && a.StudentId == studentId)
                .ToListAsync();

            if (!assignments.Any())
                return NotFound("لا توجد بيانات لهذه الجلسة.");

            // اسم الطالب والدفعة
            var studentInfo = await db.Students.AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => new { s.FullName })
                .FirstOrDefaultAsync();

            var batchInfo = await (
                from e in db.StudentBatchEnrollments.AsNoTracking()
                join b in db.Batches.AsNoTracking() on e.BatchId equals b.Id
                where e.StudentID == studentId
                select b.Name
            ).FirstOrDefaultAsync();

            var first = assignments.First();
            var vm = new EnhancementReportViewModel
            {
                SetId         = setId,
                StudentId     = studentId.Value,
                Title         = first.EnhancementSkillSet.Title,
                LectureTitle  = first.EnhancementSkillSet.Lecture?.Title,
                CreatedAt     = first.EnhancementSkillSet.CreatedAt,
                StudentName   = studentInfo?.FullName ?? "",
                BatchName     = batchInfo ?? "",
                TotalQuestions = assignments.Count,
                CorrectAnswers = assignments.Count(a => a.IsCorrect == true),
                Questions = assignments.Select(a => new EnhancementReportItemViewModel
                {
                    QuestionTitle = a.Question.Title,
                    SectionTitle  = a.Question.Section?.Title ?? a.Question.Lesson?.Section?.Title,
                    StudentAnswer = a.StudentAnswer,
                    CorrectAnswer = a.Question.CorrectAnswer,
                    IsCorrect = a.IsCorrect ?? false
                }).ToList()
            };

            return View("Report", vm);
        }

        // ────────────────────────────────────────────────────
        // Helper: بناء ViewModel لشاشة الحل
        // ────────────────────────────────────────────────────
        private static EnhancementSolveViewModel BuildSolveVm(
            int setId, Guid currentId, List<Guid> allIds,
            EnhancementSkillAssignment current,
            List<EnhancementSkillAssignment> all)
        {
            var q        = current.Question;
            var index    = allIds.IndexOf(currentId);
            var curriculum = q.Lesson?.Section?.Curriculum;

            bool isQuantitative = curriculum?.IsQuantitative ?? q.IsQuantitative;
            bool isRTL          = curriculum?.IsRTL          ?? true;

            return new EnhancementSolveViewModel
            {
                SetId             = setId,
                CurrentQuestionId = currentId,
                AllQuestionIds    = allIds,
                CurrentIndex      = index,
                Total             = allIds.Count,
                IsQuantitative    = isQuantitative,
                IsRTL             = isRTL,
                Question = new QuestionDisplayViewModel
                {
                    Id                   = q.Id,
                    Title                = q.Title,
                    IsQuantitative       = isQuantitative,
                    IsRTL                = isRTL,
                    ImageUrl             = q.ImageUrl,
                    VerbalPassageContent = q.VerbalPassage?.Content,
                    DisplayType = q.Template switch
                    {
                        QuestionTemplate.CompareValues    => QuestionDisplayType.ComparisonText,
                        QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                        _                                 => QuestionDisplayType.WithImage
                    },
                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,
                    Options = q.Options.Select(o => new QuestionOptionDisplayViewModel
                    {
                        Text     = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                },
                SelectedAnswer  = current.StudentAnswer,
                AnswersMap      = all.ToDictionary(x => x.QuestionId, x => x.StudentAnswer),
                ReviewMarkedIds = new List<Guid>(),
                IsSubmitted     = false
            };
        }
    }
}
