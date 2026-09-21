using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.HomeworkAnalytics;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Instructor.Homework;
using QdratNew.ViewModels.Partner.HomeworkDraft;
using QdratNew.ViewModels.Reports;
using System.Security.Claims;

namespace QdratNew.Areas.Instructors.Controllers
{
    [Area("Instructors")]
    public class InstructorHomeworksController : BaseInstructorController
    {
        private readonly ApplicationDbContext _context;
        private readonly IHomeworkAnalyticsService _analyticsService;

        public InstructorHomeworksController(
            ApplicationDbContext context,
            IHomeworkAnalyticsService analyticsService,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _context = context;
            _analyticsService = analyticsService;
        }

        private async Task<bool> IsArchivedHomeworkSetAsync(int homeworkSetId)
        {
            return await _context.HomeworkSets
                .AsNoTracking()
                .AnyAsync(x => x.Id == homeworkSetId && x.IsArchived);
        }

        // =====================================================
        // قائمة الواجبات
        // =====================================================

        public async Task<IActionResult> Index()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0) return Unauthorized();

            var allAccessibleBatchIds = await GetInstructorAllowedBatchIdsAsync(instructorId);

            // ======================================
            // 1️⃣ جلب الواجبات
            // ======================================
            var homeworks = await _context.HomeworkSets
                .AsNoTracking()
                .Where(x => !x.IsArchived && allAccessibleBatchIds.Contains(x.BatchId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            if (!homeworks.Any())
                return View(new List<HomeworkListVM>());

            // ======================================
            // 2️⃣ جلب كل HomeworkSetStudents مرة واحدة
            // ======================================
            var homeworkStudentsAll = await _context.HomeworkSetStudents
                .AsNoTracking()
                .ToListAsync();

            // ======================================
            // 3️⃣ جلب كل الدفعات مرة واحدة
            // ======================================
            var batchesAll = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted)
                .Select(b => new
                {
                    b.Id,
                    b.Name
                })
                .ToListAsync();

            // ======================================
            // 4️⃣ تحويل الدفعات إلى Dictionary
            // ======================================
            var batchDict = new Dictionary<int, string>();

            foreach (var b in batchesAll)
            {
                if (!batchDict.ContainsKey(b.Id))
                    batchDict.Add(b.Id, b.Name);
            }

            // ======================================
            // 5️⃣ بناء النتيجة
            // ======================================
            var result = new List<HomeworkListVM>();

            foreach (var h in homeworks)
            {
                // فلترة الطلاب في الذاكرة (بدون Contains)
                var students = new List<HomeworkSetStudent>();

                foreach (var s in homeworkStudentsAll)
                {
                    if (s.HomeworkSetId == h.Id)
                        students.Add(s);
                }

                // اسم الدفعة من العلاقة الصحيحة
                string batchName = "-";

                if (batchDict.ContainsKey(h.BatchId))
                    batchName = batchDict[h.BatchId];

                result.Add(new HomeworkListVM
                {
                    HomeworkSetId = h.Id,
                    Title = h.Title,
                    CreatedAt = h.CreatedAt,
                    StartAt = h.StartAt,
                    TotalStudents = students.Count,
                    SubmittedStudents = students.Count(x => x.IsSubmitted),
                    IsClosed = h.IsClosed,
                    Batches = new List<string> { batchName }
                });
            }

            return View(result);
        }
        
        
        
        // =====================================================
        // الطلاب الذين حلوا الواجب
        // =====================================================

        public async Task<IActionResult> Students(int homeworkSetId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var students = await _context.HomeworkSetStudents
                .AsNoTracking()
                .Include(x => x.Student)
                .Where(x => x.HomeworkSetId == homeworkSetId)
                .Select(x => new HomeworkStudentVM
                {
                    StudentId = x.StudentId,
                    StudentName = x.Student.FullName,
                    IsSolved = x.IsSubmitted,
                    Score = (decimal?)x.Score,
                    SubmittedAt = x.SubmittedAt
                })
                .ToListAsync();
            ViewBag.HomeworkSetId = homeworkSetId;
            return View(students);
        }

        // =====================================================
        // تقرير الطالب
        // =====================================================



        [HttpGet]
        public async Task<IActionResult> StudentResult(int studentId, int homeworkSetId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .AsNoTracking()
                .ToListAsync();

            if (!attempts.Any())
                return Content("⚠️ لا توجد محاولات لهذا الواجب.");

            var grouped = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToList();

            var correct = grouped.Count(x => x.IsCorrect);
            var total = grouped.Count;

            var vm = new HomeworkResultViewModel
            {
                HomeworkSetId = homeworkSetId,
                TotalQuestions = total,
                CorrectAnswers = correct,
                ScorePercentage = total == 0 ? 0 : Math.Round((double)correct * 100 / total, 2),
                SubmittedAt = grouped.Max(x => x.AttemptedAt),
                IsQuantitative = grouped.Any(x => x.Question.IsQuantitative),

                Questions = grouped.Select(x => new HomeworkResultItemViewModel
                {
                    QuestionId = x.QuestionId,
                    QuestionTitle = x.Question.Title,
                    StudentAnswer = x.SelectedAnswer,
                    CorrectAnswer = x.Question.CorrectAnswer,
                    IsCorrect = x.IsCorrect
                }).ToList()
            };

            ViewBag.StudentId = studentId;

            return View(vm);
        }


        [HttpGet]
        public async Task<IActionResult> ReviewStudent(int studentId, int homeworkSetId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();


            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Options)
                .Include(a => a.Question.VerbalPassage)
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .AsNoTracking()
                .ToListAsync();

            if (!attempts.Any())
                return Content("⚠️ لا توجد محاولات لهذا الواجب.");

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
                    TimeTakenSeconds = x.TimeTakenSeconds,
                    ImageUrl = x.Question.ImageUrl,
                    IsQuantitative = x.Question.IsQuantitative,
                    ComparisonValue1 = x.Question.ValueA,
                    ComparisonValue2 = x.Question.ValueB,
                    VerbalPassageTitle = x.Question.VerbalPassage?.Title,
                    VerbalPassageContent = x.Question.VerbalPassage?.Content,

                    DisplayType = x.Question.Template switch
                    {
                        QuestionTemplate.CompareValues => QuestionDisplayType.ComparisonText,
                        QuestionTemplate.CompareWithImage => QuestionDisplayType.ComparisonWithImage,
                        _ => QuestionDisplayType.WithImage
                    },

                    Options = x.Question.Options.Select(o => new HomeworkOptionReviewItem
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.Text == x.Question.CorrectAnswer,
                        IsSelectedByStudent = o.Text == x.SelectedAnswer
                    }).ToList()

                }).ToList()
            };
            ViewBag.HomeworkSetId = homeworkSetId;

            return View("ReviewStudent", vm);
        }

        public async Task<IActionResult> SectionQuestions(int homeworkSetId, int studentId, int sectionId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var sectionTitle = await _context.Sections
                .Where(s => s.Id == sectionId)
                .Select(s => s.Title)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(sectionTitle))
                return NotFound("❌ لم يتم العثور على المحور.");

            // جلب محاولات الطالب
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                join vp in _context.VerbalPassages on q.VerbalPassageId equals vp.Id into vpJoin
                from vp in vpJoin.DefaultIfEmpty()

                where a.HomeworkSetId == homeworkSetId
                      && a.StudentId == studentId
                      && l.SectionId == sectionId

                select new
                {
                    QuestionId = q.Id,
                    LessonTitle = l.Title,
                    SectionTitle = s.Title,
                    QuestionTitle = q.Title,

                    a.SelectedAnswer,
                    q.CorrectAnswer,
                    a.IsCorrect,

                    q.ImageUrl,

                    DisplayType =
                        q.Template == QuestionTemplate.CompareValues
                        ? QuestionDisplayType.ComparisonText
                        : q.Template == QuestionTemplate.CompareWithImage
                            ? QuestionDisplayType.ComparisonWithImage
                            : QuestionDisplayType.WithImage,

                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,

                    q.IsQuantitative,
                    VerbalPassageContent = vp != null ? vp.Content : null
                }
            ).ToListAsync();

            if (!attempts.Any())
                return NotFound("⚠️ لا توجد أسئلة محلولة في هذا المحور.");

            // استخراج IDs الأسئلة
            var questionIds = attempts
                .Select(x => x.QuestionId)
                .Distinct()
                .ToList();

            // جلب كل الاختيارات
            var allOptions = await _context.QuestionOptions
                .AsNoTracking()
                .ToListAsync();

            // تصفية داخل الذاكرة (بدون Contains)
            var optionsLookup = allOptions
                .Where(o => questionIds.Any(id => id == o.QuestionId))
                .GroupBy(o => o.QuestionId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                );

            // بناء الموديل النهائي
            var model = attempts.Select(a => new AdminSectionQuestionVm
            {
                SectionTitle = a.SectionTitle,
                LessonTitle = a.LessonTitle,
                QuestionTitle = a.QuestionTitle,
                StudentAnswer = a.SelectedAnswer,
                CorrectAnswer = a.CorrectAnswer,
                IsCorrect = a.IsCorrect,
                ImageUrl = a.ImageUrl,
                DisplayType = a.DisplayType,
                ComparisonValue1 = a.ComparisonValue1,
                ComparisonValue2 = a.ComparisonValue2,
                IsQuantitative = a.IsQuantitative,
                VerbalPassageContent = a.VerbalPassageContent,

                Options = optionsLookup.ContainsKey(a.QuestionId)
                    ? optionsLookup[a.QuestionId].Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.Text == a.CorrectAnswer,
                        IsSelectedByStudent = o.Text == a.SelectedAnswer
                    }).ToList()
                    : new List<QuestionOptionVm>()
            }).ToList();

            ViewBag.HomeworkSetId = homeworkSetId;
            ViewBag.StudentId = studentId;

            return View("~/Areas/Instructors/Views/InstructorHomeworks/SectionQuestions.cshtml", model);
        }




        public async Task<IActionResult> HomeworkLessonQuestionsDetail(int homeworkSetId, int lessonId, int studentId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var lesson = await _context.Lessons
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return NotFound("❌ لم يتم العثور على المؤشر.");

            // ---------------------------------------------
            // استخراج كل QuestionIds الخاصة بالواجب
            // ---------------------------------------------
            var questionIds = await _context.QuestionAttemptNew
                .Where(a => a.HomeworkSetId == homeworkSetId)
                .Select(a => a.QuestionId)
                .Distinct()
                .ToListAsync();

            if (!questionIds.Any())
                return Content("<div class='alert alert-warning text-center'>⚠️ لا توجد أسئلة مرتبطة بهذا الواجب.</div>", "text/html");

            var questionIdSet = questionIds.ToHashSet();

            // ---------------------------------------------
            // جلب الأسئلة الخاصة بالمؤشر
            // ---------------------------------------------
            var allQuestions = await _context.Questions
                .Include(q => q.VerbalPassage)
                .AsNoTracking()
                .Where(q => q.LessonId == lessonId)
                .Select(q => new
                {
                    q.Id,
                    q.Title,
                    q.ImageUrl,
                    q.IsQuantitative,
                    q.CorrectAnswer,
                    q.ValueA,
                    q.ValueB,
                    q.Template,
                    PassageContent = q.VerbalPassage != null ? q.VerbalPassage.Content : null
                })
                .ToListAsync();

            var questions = allQuestions
                .Where(q => questionIdSet.Contains(q.Id))
                .ToList();

            if (!questions.Any())
                return Content("<div class='alert alert-warning text-center'>⚠️ لا توجد أسئلة لهذا المؤشر داخل هذا الواجب.</div>", "text/html");

            // ---------------------------------------------
            // جلب محاولات الطالب
            // ---------------------------------------------
            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.HomeworkSetId == homeworkSetId && a.StudentId == studentId)
                .Select(a => new
                {
                    a.QuestionId,
                    a.SelectedAnswer,
                    a.IsCorrect,
                    a.TimeTakenSeconds,
                    a.AttemptedAt
                })
                .ToListAsync();

            var latestAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).First())
                .ToList();

            // ---------------------------------------------
            // جلب الخيارات
            // ---------------------------------------------
            var allOptions = await _context.QuestionOptions
                .AsNoTracking()
                .Select(o => new
                {
                    o.QuestionId,
                    o.Text,
                    o.ImageUrl
                })
                .ToListAsync();

            var optionsRaw = allOptions
                .Where(o => questionIdSet.Contains(o.QuestionId))
                .ToList();

            // ---------------------------------------------
            // بناء ViewModel
            // ---------------------------------------------
            var questionVms = questions.Select(q =>
            {
                var attempt = latestAttempts.FirstOrDefault(a => a.QuestionId == q.Id);

                var opts = optionsRaw
                    .Where(o => o.QuestionId == q.Id)
                    .Select(o => new QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl,
                        IsCorrect = o.Text == q.CorrectAnswer,
                        IsSelectedByStudent = o.Text == attempt?.SelectedAnswer
                    }).ToList();

                var displayType =
                    q.Template == QuestionTemplate.CompareValues
                    ? QdratNew.Enums.QuestionDisplayType.ComparisonText
                    : q.Template == QuestionTemplate.CompareWithImage
                        ? QdratNew.Enums.QuestionDisplayType.ComparisonWithImage
                        : QdratNew.Enums.QuestionDisplayType.WithImage;

                return new AdminSectionQuestionVm
                {
                    SectionTitle = lesson.Section.Title,
                    LessonTitle = lesson.Title,

                    QuestionTitle = q.Title,
                    ImageUrl = q.ImageUrl,
                    VerbalPassageContent = q.PassageContent,

                    ComparisonValue1 = q.ValueA,
                    ComparisonValue2 = q.ValueB,
                    DisplayType = displayType,

                    IsQuantitative = q.IsQuantitative,

                    CorrectAnswer = q.CorrectAnswer,
                    StudentAnswer = attempt?.SelectedAnswer,
                    IsCorrect = attempt?.IsCorrect ?? false,

                    Options = opts
                };

            }).ToList();

            return View("~/Areas/Instructors/Views/InstructorHomeworks/HomeworkLessonQuestionsDetail.cshtml", questionVms);
        }

        public async Task<IActionResult> SectionLessons(int homeworkSetId, int sectionId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var section = await _context.Sections
                .Include(s => s.Curriculum)
                .FirstOrDefaultAsync(s => s.Id == sectionId);

            if (section == null)
                return NotFound("❌ لم يتم العثور على المحور.");


            var homework = await _context.HomeworkSets
                .FirstOrDefaultAsync(x => x.Id == homeworkSetId);

            if (homework == null)
                return NotFound("❌ لم يتم العثور على الواجب.");


            var lessons = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                .ThenInclude(q => q.Lesson)
                .Where(a => a.HomeworkSetId == homeworkSetId && a.Question.Lesson.SectionId == sectionId)
                .GroupBy(a => a.Question.Lesson)
                .Select(g => new QdratNew.ViewModels.Exam.LessonPerformanceVm
                {
                    LessonId = g.Key.Id,
                    LessonTitle = g.Key.Title,
                    QuestionCount = g.Select(x => x.QuestionId).Distinct().Count(),
                    AvgSuccessRate = Math.Round((double)g.Count(x => x.IsCorrect) / g.Count() * 100, 1),
                    AvgTimeSeconds = Math.Round(g.Average(x => x.TimeTakenSeconds), 1),
                    HardnessIndex = Math.Round(100 - ((double)g.Count(x => x.IsCorrect) / g.Count() * 100), 1)
                })
                .ToListAsync();


            var model = new QdratNew.ViewModels.Exam.SectionLessonsViewModel
            {
                AssignmentId = homeworkSetId,
                SectionId = section.Id,
                HomeworkSetId = homeworkSetId,
                SectionTitle = section.Title,
                Lessons = lessons
            };

            return View("~/Areas/Instructors/Views/InstructorHomeworks/SectionLessons.cshtml", model);
        }




        public async Task<IActionResult> StudentHomeworkPerformanceReport(int homeworkSetId, int studentId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();


            // 1️⃣ جلب بيانات الواجب
            var homeworkSet = await _context.HomeworkSets
                .FirstOrDefaultAsync(x => x.Id == homeworkSetId);

            if (homeworkSet == null)
                return NotFound("❌ لم يتم العثور على الواجب.");



            // 2️⃣ جلب بيانات الطالب
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("❌ لم يتم العثور على بيانات الطالب.");



            // 3️⃣ جلب محاولات الطالب
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Where(a => a.HomeworkSetId == homeworkSetId && a.StudentId == studentId)
                .ToListAsync();



            // 🧮 الحسابات العامة
            int totalQuestions = attempts
                .Select(a => a.QuestionId)
                .Distinct()
                .Count();

            int correct = attempts.Count(a => a.IsCorrect);

            int wrong = attempts.Count(a => !a.IsCorrect && !string.IsNullOrEmpty(a.SelectedAnswer));

            int skipped = totalQuestions - (correct + wrong);

            int percent = totalQuestions > 0
                ? (int)Math.Round((correct / (double)totalQuestions) * 100)
                : 0;



            // 4️⃣ تحليل الأداء حسب المحاور
            var sections = attempts
                .GroupBy(a => new
                {
                    a.Question.Lesson.Section.Id,
                    a.Question.Lesson.Section.Title
                })
                .Select(g =>
                {
                    int secCorrect = g.Count(x => x.IsCorrect);
                    int secWrong = g.Count(x => !x.IsCorrect && !string.IsNullOrEmpty(x.SelectedAnswer));

                    int secTotal = g.Select(x => x.QuestionId).Distinct().Count();

                    int secSkipped = secTotal - (secCorrect + secWrong);

                    return new QdratNew.ViewModels.Reports.SectionPerformancesVm
                    {
                        SectionId = g.Key.Id,
                        SectionTitle = g.Key.Title,
                        Total = secTotal,
                        Correct = secCorrect,
                        Wrong = secWrong,
                        Skipped = secSkipped
                    };

                })
                .OrderByDescending(x => x.Correct)
                .ToList();



            // 5️⃣ بناء ViewModel
            var vm = new QdratNew.ViewModels.Reports.StudentExamPerformanceReportVm
            {
                StudentName = student.FullName,

                ExamTitle = homeworkSet.Title,

                ExamDate = homeworkSet.CreatedAt,

                TotalQuestions = totalQuestions,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                SkippedQuestions = skipped,
                OverallPercent = percent,

                SolveMinutes = 0,
                DurationMinutes = 0,

                SpeedLabel = "تحليل أداء الواجب",
                SpeedNote = "هذا التقرير يعرض أداء الطالب داخل الواجب.",

                TrackCard1 = "نقطة قوة",
                TrackCard1Desc = "راجع المحاور التي حصل فيها الطالب على أعلى أداء.",

                TrackCard2 = "نقطة تطوير",
                TrackCard2Desc = "ركز على المحاور التي حصل فيها الطالب على نسبة أقل.",

                IndividualTips = new List<string>(),

                ExamAssignmentId = homeworkSetId,
                StudentId = studentId,

                Sections = sections
            };



            return View("~/Areas/Instructors/Views/InstructorHomeworks/StudentExamPerformanceReport.cshtml", vm);
        }

        // =====================================================
        // التقرير العام للمدرب
        // =====================================================

        public async Task<IActionResult> GlobalReport(int homeworkSetId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var report = await _analyticsService
                .BuildGlobalReportAsync(homeworkSetId);

            return View(report);
        }




        [HttpPost]
        public async Task<IActionResult> Reopen(int homeworkSetId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var homework = await _context.HomeworkSets
                .FirstOrDefaultAsync(x => x.Id == homeworkSetId);

            if (homework == null)
                return NotFound();

            homework.IsClosed = false;

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إعادة فتح الواجب بنجاح";

            return RedirectToAction(nameof(Index));
        }
        // =====================================================
        // إعادة إرسال الواجب
        // =====================================================

        [HttpGet]
        public async Task<IActionResult> Resend(int homeworkSetId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var model = new SendHomeworkDraftVM
            {
                DraftId = homeworkSetId,

                Batches = await _context.Batches
                    .Where(x => !x.IsDeleted)
                    .Select(x => new SelectListItem
                    {
                        Value = x.Id.ToString(),
                        Text = x.Name
                    })
                    .ToListAsync(),

                StartAt = DateTime.Now,
                EndAt = DateTime.Now.AddDays(1)
            };

            return View("SendDraft", model);
        }

        // =====================================================
        // إغلاق الواجب
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> Close(int homeworkSetId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var homework = await _context.HomeworkSets
                .FirstOrDefaultAsync(x => x.Id == homeworkSetId);

            if (homework == null)
                return NotFound();

            homework.EndAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إغلاق الواجب";

            return RedirectToAction(nameof(Index));
        }

        // =====================================================
        // حذف الواجب
        // =====================================================

        [HttpPost]
        public async Task<IActionResult> Delete(int homeworkSetId)
        {
            if (await IsArchivedHomeworkSetAsync(homeworkSetId))
                return Forbid();

            var students = await _context.HomeworkSetStudents
                .Where(x => x.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            bool hasResults = students.Any(x => x.IsSubmitted);

            if (hasResults)
            {
                var attempts = await _context.QuestionAttemptNew
                    .Where(x => x.HomeworkSetId == homeworkSetId)
                    .ToListAsync();

                _context.QuestionAttemptNew.RemoveRange(attempts);
            }

            var homeworks = await _context.Homeworks
                .Where(x => x.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            _context.Homeworks.RemoveRange(homeworks);

            var set = await _context.HomeworkSets
                .FirstOrDefaultAsync(x => x.Id == homeworkSetId);

            if (set != null)
                _context.HomeworkSets.Remove(set);

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف الواجب";

            return RedirectToAction(nameof(Index));
        }
    }
}
