using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.PerformanceIndicator;
using QdratNew.ViewModels.Question;
using System.Security.Claims;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PerformanceIndicatorExamsController : Controller
    {
        // 🟢 الحقول الخاصة بالخدمات المعتمدة على الـ Dependency Injection
        private readonly IPerformanceIndicatorExamService _examService;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IMemoryCache _cache;
        private readonly UserManager<ApplicationUser> _userManager;

        // 🟢 الـ Constructor الرسمي الوحيد
        // هنا يتم تمرير الخدمات من الـ DI Container تلقائيًا (Startup / Program.cs)
        public PerformanceIndicatorExamsController(
            IPerformanceIndicatorExamService examService,
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IMemoryCache cache,
            UserManager<ApplicationUser> userManager)
        {
            _examService = examService ?? throw new ArgumentNullException(nameof(examService));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        // 🟢 عرض جميع اختبارات مؤشر الأداء
        [AdminPermission("PerformanceIndicatorExams", "Read")]
        public async Task<IActionResult> Index(bool showArchived = false)
        {
            using var _context = _contextFactory.CreateDbContext();

            ViewBag.ArchivedCount = await _context.PerformanceIndicatorExams
                .CountAsync(e => e.IsArchived);

            ViewBag.ShowArchived = showArchived;

            var query = _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Where(e => e.IsArchived == showArchived)
                .Include(e => e.Curriculum)
                .Include(e => e.ExamToBatches)
                    .ThenInclude(b => b.Batch)
                .OrderByDescending(e => e.CreatedAt);

            var exams = await query.ToListAsync();

            // للأرشيف: فلترة حسب صلاحيات الأرشيف المحددة من المالك
            if (showArchived && !IsArchiveOwner())
            {
                var uid = CurrentUserId();
                if (string.IsNullOrWhiteSpace(uid))
                    return View(new List<PerformanceIndicatorExam>());

                var allowedExamIds = await _context.PerformanceIndicatorExamArchiveAccesses
                    .AsNoTracking()
                    .Where(x => x.UserId == uid && x.IsActive)
                    .Select(x => x.ExamId)
                    .Distinct()
                    .ToListAsync();

                exams = exams.Where(e => allowedExamIds.Contains(e.Id)).ToList();
            }

            return View(exams);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PerformanceIndicatorExams", "Archive")]
        public async Task<IActionResult> ArchivePerformanceIndicatorExams(List<int> selectedExamIds)
        {
            if (selectedExamIds == null || !selectedExamIds.Any())
            {
                TempData["Error"] = "⚠️ اختر اختباراً واحداً على الأقل للأرشفة.";
                return RedirectToAction(nameof(Index));
            }

            using var _context = _contextFactory.CreateDbContext();

            var exams = await _context.PerformanceIndicatorExams
                .Where(e => selectedExamIds.Contains(e.Id) && !e.IsArchived)
                .ToListAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var archivedAt = DateTime.UtcNow;

            foreach (var exam in exams)
            {
                exam.IsArchived = true;
                exam.ArchivedAt = archivedAt;
                exam.ArchivedByUserId = userId;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم أرشفة {exams.Count} اختبار.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PerformanceIndicatorExams", "Archive")]
        public async Task<IActionResult> RestorePerformanceIndicatorExams(List<int> selectedExamIds)
        {
            if (selectedExamIds == null || !selectedExamIds.Any())
            {
                TempData["Error"] = "⚠️ اختر اختباراً واحداً على الأقل للاسترجاع.";
                return RedirectToAction(nameof(Index));
            }

            using var _context = _contextFactory.CreateDbContext();

            var exams = await _context.PerformanceIndicatorExams
                .Where(e => selectedExamIds.Contains(e.Id) && e.IsArchived)
                .ToListAsync();

            foreach (var exam in exams)
            {
                exam.IsArchived = false;
                exam.ArchivedAt = null;
                exam.ArchivedByUserId = null;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم إخراج {exams.Count} اختبار من الأرشيف.";
            return RedirectToAction(nameof(Index), new { showArchived = true });
        }

        // 🟢 عرض صفحة إنشاء اختبار جديد
        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "Add")]
        public async Task<IActionResult> Create()
        {
            using var _context = _contextFactory.CreateDbContext();

            var vm = new CreatePerformanceExamViewModel
            {
                // 🟢 جلب المناهج
                AvailableCurriculums = await _context.Curriculums
                    .AsNoTracking()
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id.ToString(),
                        Text = c.Title
                    })
                    .ToListAsync(),

                // 🟢 جلب الدفعات
                AvailableBatches = await _context.Batches
                    .AsNoTracking()
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name
                    })
                    .ToListAsync(),

                // 🟢 جلب الطلاب
                AvailableStudents = await _context.Students
                    .AsNoTracking()
                    .Select(s => new SelectListItem
                    {
                        Value = s.StudentID.ToString(),
                        Text = s.FullName
                    })
                    .ToListAsync(),

                // 🟢 جلب النماذج الاحترافية للاختبارات فقط
                AvailableProfessionalModels = await _context.ProfessionalModels
                    .AsNoTracking()
                    .Where(m => !m.IsArchived && m.ModelType == QdratNew.Enums.ProfessionalModelType.Exam)
                    .OrderBy(m => m.Title)
                    .Select(m => new SelectListItem
                    {
                        Value = m.Id.ToString(),
                        Text = m.Title
                    })
                    .ToListAsync()
            };

            return View(vm);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PerformanceIndicatorExams", "Add")]
        public async Task<IActionResult> Create(CreatePerformanceExamViewModel vm)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (!ModelState.IsValid)
            {
                vm.ErrorMessage = "⚠️ تأكد من إدخال جميع البيانات.";
                await FillViewModelListsAsync(_context, vm);
                return View(vm);
            }

            try
            {
                if (vm.ProfessionalModelId.HasValue)
                {
                    var modelIsAvailable = await _context.ProfessionalModels
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == vm.ProfessionalModelId.Value && !m.IsArchived);

                    if (!modelIsAvailable)
                    {
                        vm.ErrorMessage = "❌ النموذج الاحترافي المحدد غير متاح أو مؤرشف.";
                        await FillViewModelListsAsync(_context, vm);
                        return View(vm);
                    }
                }

                // 1️⃣ إنشاء الاختبار الرئيسي
                var exam = new PerformanceIndicatorExam
                {
                    Title = vm.ExamTitle,
                    CurriculumId = vm.CurriculumId,
                    CreatedAt = DateTime.Now,
                    IsOnline = vm.IsOnline,
                    StartAt = vm.StartAt,
                    EndAt = vm.EndAt,
                    ReferenceCode = GenerateReferenceCode(_context)
                };

                _context.PerformanceIndicatorExams.Add(exam);
                await _context.SaveChangesAsync();    // 🔥 مهم جداً (لحجز exam.Id)

                // 2️⃣ ربط الدفعات (ExamToBatch)
                if (vm.SelectedBatchIds != null && vm.SelectedBatchIds.Any())
                {
                    foreach (var batchId in vm.SelectedBatchIds)
                    {
                        _context.PerformanceIndicatorExamToBatch.Add(new PerformanceIndicatorExamToBatch
                        {
                            PerformanceIndicatorExamId = exam.Id,
                            BatchId = batchId
                        });
                    }

                    await _context.SaveChangesAsync();   // 🔥 مهم جداً لحفظ ربط الدفعات
                }

                // 3️⃣ ربط الطلاب المختارين (اختياري)
                if (vm.SelectedStudentIds != null && vm.SelectedStudentIds.Any())
                {
                    foreach (var studentId in vm.SelectedStudentIds)
                    {
                        _context.PerformanceIndicatorExamStudents.Add(new PerformanceIndicatorExamStudent
                        {
                            PerformanceIndicatorExamId = exam.Id,
                            StudentId = studentId
                        });
                    }

                    await _context.SaveChangesAsync();
                }

                // 4️⃣ توليد الأسئلة تلقائيًا/احترافيًا
                await _examService.GenerateExamAsync(
                    exam.Id,
                    vm.CurriculumId,
                    vm.TotalQuestions,
                    vm.ManualSelection,
                    vm.ProfessionalModelId
                );

                TempData["Success"] = "تم إنشاء الاختبار بنجاح.";
                return RedirectToAction("ConfirmSend", new { id = exam.Id });
            }
            catch (Exception ex)
            {
                vm.ErrorMessage = $"❌ خطأ أثناء الحفظ: {ex.Message}";
                await FillViewModelListsAsync(_context, vm);
                return View(vm);
            }
        }



        private async Task FillViewModelListsAsync(ApplicationDbContext _context, CreatePerformanceExamViewModel vm)
        {
            vm.AvailableCurriculums = await _context.Curriculums
                .AsNoTracking()
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync();

            vm.AvailableBatches = await _context.Batches
                .AsNoTracking()
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToListAsync();

            vm.AvailableStudents = await _context.Students
                .AsNoTracking()
                .Select(s => new SelectListItem { Value = s.StudentID.ToString(), Text = s.FullName })
                .ToListAsync();

            vm.AvailableProfessionalModels = await _context.ProfessionalModels
                .AsNoTracking()
                .Where(m => !m.IsArchived)
                .OrderBy(m => m.Title)
                .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Title })
                .ToListAsync();
        }


        private static string GetPerformanceDifficultyText(int difficultyLevel)
        {
            return difficultyLevel <= 0 ? "سهل"
                : difficultyLevel == 1 ? "متوسط"
                : difficultyLevel == 2 ? "صعب"
                : "صعب جدًا";
        }



        // 🟢 عرض مراجعة اختبار مؤشر الأداء (عرض الأسئلة المولدة لكل محور)
        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "Read")]
        public async Task<IActionResult> Review(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ✅ جلب بيانات الاختبار مع المناهج والمحاور
            var exam = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Include(e => e.Curriculum)
                .Include(e => e.Sections)
                    .ThenInclude(s => s.Section)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على الاختبار المطلوب.");

            // ✅ جلب جميع الأسئلة التابعة للاختبار مع تفاصيلها
            var questions = await (
                from eq in _context.PerformanceIndicatorExamQuestions.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on eq.QuestionId equals q.Id
                where eq.PerformanceIndicatorExamId == exam.Id
                select new
                {
                    eq.SectionId,
                    q.Id,
                    q.Title,
                    q.Difficulty,
                    q.CorrectAnswer,
                    q.LessonId
                }
            ).ToListAsync();

            // ✅ تجميع الأسئلة تحت كل محور
            var sections = questions
                .GroupBy(q => q.SectionId)
                .Select(g => new SectionReviewItem
                {
                    SectionId = g.Key ?? 0,
                    SectionTitle = _context.Sections
                        .Where(s => s.Id == (g.Key ?? 0))
                        .Select(s => s.Title)
                        .FirstOrDefault() ?? "—",

                    Questions = g.Select((x, i) => new QuestionReviewItem
                    {
                        QuestionId = x.Id,
                        Order = i + 1,
                        Title = x.Title ?? "—",
                        DifficultyLevel = (double)x.Difficulty,
                        CorrectAnswer = x.CorrectAnswer ?? "—",
                        LessonTitle = _context.Lessons
                            .Where(l => l.Id == x.LessonId && l.IsActive)
                            .Select(l => l.Title)
                            .FirstOrDefault() ?? "—"
                    }).ToList()
                })
                .ToList();

            // ✅ تعبئة ViewModel العرضي
            var vm = new PerformanceExamReviewViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                CurriculumTitle = exam.Curriculum?.Title ?? "—",
                StartAt = exam.StartAt,
                EndAt = exam.EndAt,
                ReferenceCode = exam.ReferenceCode,
                IsOnline = exam.IsOnline,
                Sections = sections
            };

            return View(vm);
        }



        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "Read")]
        public async Task<IActionResult> Details(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Include(e => e.Curriculum)
                .Include(e => e.Batch)
                .Include(e => e.ExamToBatches)
                    .ThenInclude(b => b.Batch)
                .Include(e => e.Sections)
                    .ThenInclude(s => s.Section)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            var examQuestions = await _context.PerformanceIndicatorExamQuestions
                .AsNoTracking()
                .Include(eq => eq.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Curriculum)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Where(eq => eq.PerformanceIndicatorExamId == id)
                .OrderBy(eq => eq.OrderNumber)
                .ThenBy(eq => eq.Id)
                .ToListAsync();

            var questionSections = examQuestions
                .Where(eq => eq.Question != null)
                .GroupBy(eq => new
                {
                    SectionId = eq.SectionId
                        ?? eq.Question.Lesson?.SectionId
                        ?? eq.Question.SectionId
                        ?? 0,
                    SectionTitle = eq.Section?.Title
                        ?? eq.Question.Lesson?.Section?.Title
                        ?? eq.Question.Section?.Title
                        ?? "بدون محور"
                })
                .Select(g => new PerformanceIndicatorExamSectionViewModel
                {
                    SectionId = g.Key.SectionId,
                    SectionTitle = g.Key.SectionTitle,
                    QuestionCount = g.Count(),
                    Questions = g.Select((eq, index) => new PerformanceIndicatorExamQuestionDetailsVm
                    {
                        QuestionId = eq.QuestionId,
                        Order = eq.OrderNumber > 0 ? eq.OrderNumber : index + 1,
                        ReferenceNumber = eq.Question.ReferenceNumber ?? $"Q-{eq.Question.AutoNumber}",
                        Title = eq.Question.Title ?? "بدون نص",
                        CurriculumTitle = eq.Question.Curriculum?.Title ?? exam.Curriculum?.Title ?? "—",
                        SectionTitle = eq.Section?.Title
                            ?? eq.Question.Lesson?.Section?.Title
                            ?? eq.Question.Section?.Title
                            ?? "بدون محور",
                        LessonTitle = eq.Question.Lesson?.Title ?? "بدون مؤشر",
                        DifficultyLevel = (int)eq.Question.Difficulty,
                        DifficultyText = GetPerformanceDifficultyText((int)eq.Question.Difficulty),
                        CorrectAnswer = eq.Question.CorrectAnswer ?? "—"
                    }).OrderBy(q => q.Order).ToList()
                })
                .OrderBy(s => s.SectionTitle)
                .ToList();

            foreach (var selectedSection in exam.Sections.OrderBy(s => s.Section?.Title))
            {
                if (!questionSections.Any(s => s.SectionId == selectedSection.SectionId))
                {
                    questionSections.Add(new PerformanceIndicatorExamSectionViewModel
                    {
                        SectionId = selectedSection.SectionId,
                        SectionTitle = selectedSection.Section?.Title ?? "بدون محور",
                        QuestionCount = 0,
                        Questions = new List<PerformanceIndicatorExamQuestionDetailsVm>()
                    });
                }
            }

            var vm = new PerformanceIndicatorExamViewModel
            {
                Id = exam.Id,
                Title = exam.Title,
                CurriculumTitle = exam.Curriculum?.Title ?? "—",
                BatchName = exam.ExamToBatches != null && exam.ExamToBatches.Any()
                    ? string.Join("، ", exam.ExamToBatches.Select(b => b.Batch?.Name).Where(name => !string.IsNullOrWhiteSpace(name)))
                    : exam.Batch?.Name ?? "—",
                CreatedAt = exam.CreatedAt,
                StartAt = exam.StartAt,
                EndAt = exam.EndAt,
                DurationMinutes = exam.DurationMinutes,
                PassPercent = exam.PassPercent,
                ReferenceCode = exam.ReferenceCode ?? "—",
                IsOnline = exam.IsOnline,
                IsSent = exam.IsSent,
                TotalQuestions = examQuestions.Count,
                TotalSections = questionSections.Count(s => s.QuestionCount > 0),
                Sections = questionSections
                    .OrderByDescending(s => s.QuestionCount)
                    .ThenBy(s => s.SectionTitle)
                    .ToList()
            };

            return View(vm);
        }


        // =============================
        // 🟢 عرض صفحة استبدال السؤال
        // =============================
        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "Edit")]
        public async Task<IActionResult> ReplaceQuestionPage(int examId, int sectionId, Guid oldId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ✅ التحقق من وجود الاختبار
            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return NotFound("⚠️ لم يتم العثور على الاختبار.");

            // ✅ السؤال القديم لعرضه في الواجهة
            var oldQuestion = await _context.Questions
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == oldId);

            // ✅ الأسئلة المتاحة من نفس المحور (section)
            var available = await (
                from q in _context.Questions.AsNoTracking()
                where q.SectionId == sectionId && q.Id != oldId
                select new QuestionSimpleViewModel
                {
                    Id = q.Id,
                    Title = q.Title ?? "—",
                    Difficulty = (int)q.Difficulty,
                    InternalNote = q.InternalNote ?? "—",   // ✅ الجديد

                    CorrectAnswer = q.CorrectAnswer ?? "—"
                }
            ).ToListAsync();

            var vm = new ReplaceQuestionViewModel
            {
                ExamId = examId,
                SectionId = sectionId,
                OldQuestionId = oldId,
                OldQuestionTitle = oldQuestion?.Title ?? "—",
                AvailableQuestions = available
            };

            return View(vm);
        }



        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "Read")]
        public async Task<IActionResult> PreviewQuestion(Guid id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return Content("<div class='alert alert-danger'>⚠️ لم يتم العثور على السؤال.</div>", "text/html");

            // ✅ استخدم نفس ViewModel العرضي العام للأسئلة
            var vm = question.ToDisplayModel();

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", vm);
        }

        // ======================================================
        // 🟢 عرض صفحة إنشاء اختبار من نموذج احترافي (GET)
        // ======================================================
        [HttpGet]
        // ======================================================
        // 🟢 عرض صفحة إنشاء اختبار من نموذج احترافي
        // ======================================================
        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "CreateFromProfessionalModel")]
        public async Task<IActionResult> CreateFromProfessionalModel()
        {
            using var _context = _contextFactory.CreateDbContext();

            var vm = new CreateFromProfessionalModelViewModel
            {
                AvailableCurriculums = await _context.Curriculums
                    .AsNoTracking()
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToListAsync(),

                AvailableBatches = await _context.Batches
                    .AsNoTracking()
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToListAsync(),

                AvailableModels = await _context.ProfessionalModels
                    .AsNoTracking()
                    .Where(m => !m.IsArchived)
                    .OrderBy(m => m.Title)
                    .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Title })
                    .ToListAsync()
            };

            return View(vm);
        }


        // ======================================================
        // 🟢 تنفيذ الإنشاء الفعلي + الإرسال التلقائي للدفعة
        // ======================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PerformanceIndicatorExams", "CreateFromProfessionalModel")]
        public async Task<IActionResult> CreateFromProfessionalModel(CreateFromProfessionalModelViewModel vm)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (!ModelState.IsValid)
            {
                vm.ErrorMessage = "⚠️ تأكد من إدخال جميع الحقول المطلوبة.";
                vm.AvailableCurriculums = await _context.Curriculums.AsNoTracking()
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToListAsync();

                vm.AvailableBatches = await _context.Batches.AsNoTracking()
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToListAsync();

                vm.AvailableModels = await _context.ProfessionalModels.AsNoTracking()
                    .Where(m => !m.IsArchived)
                    .OrderBy(m => m.Title)
                    .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Title })
                    .ToListAsync();

                return View(vm);
            }

            try
            {
                // 🔹 جلب النموذج
                var model = await _context.ProfessionalModels
                    .Include(m => m.Questions)
                    .FirstOrDefaultAsync(m => m.Id == vm.SelectedModelId && !m.IsArchived);

                if (model == null)
                {
                    vm.ErrorMessage = "❌ لم يتم العثور على النموذج الاحترافي.";
                    return View(vm);
                }

                // 🔹 إنشاء الاختبار
                var exam = new PerformanceIndicatorExam
                {
                    Title = $"اختبار من النموذج {model.Title}",
                    CurriculumId = vm.SelectedCurriculumId,
                    CreatedAt = DateTime.Now,
                    IsOnline = true,
                    StartAt = vm.StartAt,
                    EndAt = vm.EndAt,
                    DurationMinutes = vm.DurationMinutes,
                    ReferenceCode = GenerateReferenceCode(_context),
                    QuestionsPerIndicator = model.Questions.Count,
                    PassPercent = 50
                };

                _context.PerformanceIndicatorExams.Add(exam);
                await _context.SaveChangesAsync();   // 🔥 ضروري

                // 🔹 ربط الدفعة الصحيحة
                _context.PerformanceIndicatorExamToBatch.Add(new PerformanceIndicatorExamToBatch
                {
                    PerformanceIndicatorExamId = exam.Id,
                    BatchId = vm.SelectedBatchId
                });

                await _context.SaveChangesAsync();   // 🔥 ضروري

                // 🔹 إضافة الأسئلة
                int order = 1;
                foreach (var q in model.Questions)
                {
                    var sectionId = await _context.Questions
                        .Where(x => x.Id == q.QuestionId)
                        .Select(x => x.SectionId)
                        .FirstOrDefaultAsync();

                    _context.PerformanceIndicatorExamQuestions.Add(new PerformanceIndicatorExamQuestion
                    {
                        PerformanceIndicatorExamId = exam.Id,
                        QuestionId = q.QuestionId ?? Guid.Empty,
                        SectionId = sectionId,
                        OrderNumber = order++
                    });
                }

                await _context.SaveChangesAsync();

                // 🔹 إرسال الطلاب عبر JOIN آمن
                var studentIds = await (
                        from eb in _context.PerformanceIndicatorExamToBatch
                        join sbe in _context.StudentBatchEnrollments
                            on eb.BatchId equals sbe.BatchId
                        where eb.PerformanceIndicatorExamId == exam.Id
                        select sbe.StudentID
                    )
                    .Distinct()
                    .ToListAsync();

                foreach (var sid in studentIds)
                {
                    _context.PerformanceIndicatorExamStudents.Add(new PerformanceIndicatorExamStudent
                    {
                        PerformanceIndicatorExamId = exam.Id,
                        StudentId = sid
                    });
                }

                exam.IsSent = true;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ تم إنشاء وإرسال الاختبار من النموذج {model.Title} بنجاح.";
                return RedirectToAction("Review", new { id = exam.Id });
            }
            catch (Exception ex)
            {
                vm.ErrorMessage = $"❌ حدث خطأ أثناء الإنشاء: {ex.Message}";
                return View(vm);
            }
        }

        // =======================================================
        // 🟢 عرض صفحة تأكيد الإرسال (GET)
        // =======================================================
        // GET: عرض صفحة التأكيد
        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "ConfirmSend")]
        public async Task<IActionResult> ConfirmSend(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.Curriculum)
                .Include(e => e.ExamToBatches)
                    .ThenInclude(b => b.Batch)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("⚠️ لم يتم العثور على الاختبار.");

            // 🟢 بناء ViewModel المطلوب للصفحة
            var vm = new ConfirmSendPerformanceExamViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                CurriculumTitle = exam.Curriculum?.Title ?? "—",

                // دعم أكثر من دفعة في الاختبار الواحد
                BatchName = exam.ExamToBatches != null && exam.ExamToBatches.Any()
                    ? string.Join(" ، ", exam.ExamToBatches.Select(b => b.Batch.Name))
                    : "—",

                ReferenceCode = exam.ReferenceCode,

                // سيتم حساب عدد الطلاب لاحقاً في ConfirmSendPost
                StudentCount = 0
            };

            return View(vm);
        }


        // POST: تنفيذ الإرسال


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PerformanceIndicatorExams", "ConfirmSend")]

        public async Task<IActionResult> ConfirmSendPost(int examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 1️⃣ جلب الاختبار مع الدفعات المرتبطة
            var exam = await _context.PerformanceIndicatorExams
                .Include(e => e.ExamToBatches)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
            {
                TempData["Error"] = "⚠️ لم يتم العثور على الاختبار.";
                return RedirectToAction("Index");
            }

            // 2️⃣ جلب الطلاب عبر JOIN بدون Contains
            var studentIds = await (
                    from eb in _context.PerformanceIndicatorExamToBatch
                    join sbe in _context.StudentBatchEnrollments
                        on eb.BatchId equals sbe.BatchId
                    where eb.PerformanceIndicatorExamId == examId
                    select sbe.StudentID
                )
                .Distinct()
                .ToListAsync();

            // 🔥🔥 3️⃣ حذف أي سجلات إرسال سابقة (مهم جدًا)
            var oldRecords = _context.PerformanceIndicatorExamStudents
                .Where(x => x.PerformanceIndicatorExamId == examId);

            _context.PerformanceIndicatorExamStudents.RemoveRange(oldRecords);
            await _context.SaveChangesAsync(); // حفظ الإزالة أولًا

            // 4️⃣ إضافة الطلاب الجدد
            foreach (var sid in studentIds)
            {
                _context.PerformanceIndicatorExamStudents.Add(new PerformanceIndicatorExamStudent
                {
                    PerformanceIndicatorExamId = exam.Id,
                    StudentId = sid
                });
            }

            // 5️⃣ تحديث حالة الإرسال
            exam.IsSent = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم إرسال الاختبار بنجاح ({studentIds.Count} طالب).";
            return RedirectToAction("Details", new { id = exam.Id });
        }



        // =============================
        // 🟢 تنفيذ الاستبدال الفعلي بعد الضغط على "اختيار"
        // =============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PerformanceIndicatorExams", "Edit")]
        public async Task<IActionResult> ReplaceQuestionPage(ReplaceQuestionViewModel model, Guid NewQuestionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var target = await _context.PerformanceIndicatorExamQuestions
                .FirstOrDefaultAsync(q =>
                    q.PerformanceIndicatorExamId == model.ExamId &&
                    q.QuestionId == model.OldQuestionId);

            if (target == null)
            {
                TempData["Error"] = "⚠️ لم يتم العثور على السؤال المراد استبداله.";
                return RedirectToAction("Review", new { id = model.ExamId });
            }

            // ✅ تحديث رقم السؤال
            target.QuestionId = NewQuestionId;
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم استبدال السؤال بنجاح.";
            return RedirectToAction("Review", new { id = model.ExamId });
        }




        // ==============================================
        // 🟢 عرض نتائج الطلاب في اختبار مؤشر الأداء
        // ==============================================
        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "Results")]
        public async Task<IActionResult> ExamStudents(int id, int? examId)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (id == 0 && examId.HasValue)
                id = examId.Value;

            var exam = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Include(e => e.Curriculum)
                .Include(e => e.ExamStudents)
                    .ThenInclude(s => s.Student)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            var passPercent = exam.PassPercent > 0 && exam.PassPercent <= 100
                ? exam.PassPercent
                : 60;

            var examQuestions = await _context.PerformanceIndicatorExamQuestions
                .AsNoTracking()
                .Include(eq => eq.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Section)
                .Where(eq => eq.PerformanceIndicatorExamId == id)
                .OrderBy(eq => eq.OrderNumber)
                .ThenBy(eq => eq.Id)
                .ToListAsync();

            var attempts = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.PerformanceIndicatorExamId == id)
                .ToListAsync();

            var examQuestionIds = examQuestions
                .Select(eq => eq.QuestionId)
                .ToHashSet();

            var sectionGroups = examQuestions
                .Where(eq => eq.Question != null)
                .GroupBy(eq => new
                {
                    SectionId = eq.SectionId
                        ?? eq.Question.Lesson?.SectionId
                        ?? eq.Question.SectionId
                        ?? 0,
                    SectionTitle = eq.Section?.Title
                        ?? eq.Question.Lesson?.Section?.Title
                        ?? eq.Question.Section?.Title
                        ?? "محور غير محدد"
                })
                .ToList();

            var studentRows = new List<PerformanceIndicatorExamStudentRowViewModel>();
            var totalQuestions = examQuestions.Count;

            var blockedStudentIds = await _context.IntegrityViolationLogs
                .Where(v => v.AttemptType == IntegrityAttemptType.PerformanceIndicator
                         && v.AttemptEntityId == exam.Id
                         && !v.IsResolved)
                .Select(v => v.StudentId)
                .ToListAsync();
            var blockedStudentIdsSet = blockedStudentIds.ToHashSet();

            foreach (var examStudent in exam.ExamStudents.OrderBy(s => s.Student?.FullName))
            {
                var latestAttempts = attempts
                    .Where(a => a.StudentId == examStudent.StudentId && examQuestionIds.Contains(a.QuestionId))
                    .GroupBy(a => a.QuestionId)
                    .Select(g => g.OrderByDescending(a => a.AttemptedAt).ThenByDescending(a => a.Id).First())
                    .ToList();

                var attemptsByQuestionId = latestAttempts.ToDictionary(a => a.QuestionId, a => a);
                var correctCount = examQuestions.Count(eq =>
                    attemptsByQuestionId.TryGetValue(eq.QuestionId, out var attempt) &&
                    !string.IsNullOrWhiteSpace(attempt.SelectedAnswer) &&
                    attempt.SelectedAnswer != "—" &&
                    attempt.IsCorrect);

                var scorePercent = totalQuestions > 0
                    ? Math.Round(correctCount * 100.0 / totalQuestions, 1)
                    : 0;

                var weakSections = new List<StudentWeakSectionViewModel>();

                foreach (var sectionGroup in sectionGroups)
                {
                    var sectionTotal = sectionGroup.Count();
                    var sectionCorrect = sectionGroup.Count(eq =>
                        attemptsByQuestionId.TryGetValue(eq.QuestionId, out var attempt) &&
                        !string.IsNullOrWhiteSpace(attempt.SelectedAnswer) &&
                        attempt.SelectedAnswer != "—" &&
                        attempt.IsCorrect);

                    var sectionScore = sectionTotal > 0
                        ? Math.Round(sectionCorrect * 100.0 / sectionTotal, 1)
                        : 0;

                    if (sectionScore < passPercent)
                    {
                        weakSections.Add(new StudentWeakSectionViewModel
                        {
                            SectionId = sectionGroup.Key.SectionId,
                            SectionTitle = sectionGroup.Key.SectionTitle,
                            ScorePercent = sectionScore,
                            CorrectCount = sectionCorrect,
                            TotalQuestions = sectionTotal
                        });
                    }
                }

                var started = examStudent.StartedAt.HasValue || latestAttempts.Any();
                var statusText = examStudent.IsCompleted
                    ? "مكتمل"
                    : started ? "بدأ ولم يكمل" : "لم يبدأ";

                studentRows.Add(new PerformanceIndicatorExamStudentRowViewModel
                {
                    StudentId = examStudent.StudentId,
                    StudentName = examStudent.Student?.FullName ?? "طالب غير محدد",
                    IsCompleted = examStudent.IsCompleted,
                    StartedAt = examStudent.StartedAt,
                    CompletedAt = examStudent.CompletedAt,
                    ScorePercent = scorePercent,
                    StatusText = statusText,
                    Passed = scorePercent >= passPercent,
                    NeedsRemedialPlan = scorePercent < passPercent,
                    WeakSections = weakSections,
                    IsIntegrityBlocked = blockedStudentIdsSet.Contains(examStudent.StudentId),
                });
            }

            var vm = new PerformanceIndicatorExamStudentsReportViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                CurriculumTitle = exam.Curriculum?.Title ?? "—",
                TotalStudents = studentRows.Count,
                CompletedStudents = studentRows.Count(s => s.IsCompleted),
                PassedStudents = studentRows.Count(s => s.Passed),
                NeedRemedialPlanStudents = studentRows.Count(s => s.NeedsRemedialPlan),
                AverageScore = studentRows.Any()
                    ? Math.Round(studentRows.Average(s => s.ScorePercent ?? 0), 1)
                    : 0,
                PassPercent = passPercent,
                Students = studentRows
            };

            return View(vm);
        }

        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "Results")]
        public async Task<IActionResult> StudentExamQuestions(int examId, int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            var student = await _context.Students
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("❌ لم يتم العثور على الطالب.");

            var examQuestions = await _context.PerformanceIndicatorExamQuestions
                .AsNoTracking()
                .Include(eq => eq.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Section)
                .Where(eq => eq.PerformanceIndicatorExamId == examId)
                .OrderBy(eq => eq.OrderNumber)
                .ThenBy(eq => eq.Id)
                .ToListAsync();

            var attempts = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.PerformanceIndicatorExamId == examId && a.StudentId == studentId)
                .ToListAsync();

            var latestAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).ThenByDescending(a => a.Id).First())
                .ToDictionary(a => a.QuestionId, a => a);

            var rows = examQuestions
                .Where(eq => eq.Question != null)
                .Select((eq, index) =>
                {
                    latestAttempts.TryGetValue(eq.QuestionId, out var attempt);

                    var isSkipped = attempt == null ||
                                    string.IsNullOrWhiteSpace(attempt.SelectedAnswer) ||
                                    attempt.SelectedAnswer == "—";
                    var isCorrect = !isSkipped && attempt!.IsCorrect;

                    return new StudentPerformanceIndicatorQuestionRowViewModel
                    {
                        QuestionId = eq.QuestionId,
                        OrderNumber = eq.OrderNumber > 0 ? eq.OrderNumber : index + 1,
                        QuestionTitle = eq.Question.Title ?? "بدون نص",
                        SectionId = eq.SectionId ?? eq.Question.Lesson?.SectionId ?? eq.Question.SectionId,
                        SectionTitle = eq.Section?.Title
                            ?? eq.Question.Lesson?.Section?.Title
                            ?? eq.Question.Section?.Title
                            ?? "محور غير محدد",
                        LessonId = eq.Question.LessonId,
                        LessonTitle = eq.Question.Lesson?.Title ?? "مؤشر غير محدد",
                        StudentAnswer = isSkipped ? "لم يجب" : attempt!.SelectedAnswer,
                        CorrectAnswer = eq.Question.CorrectAnswer ?? "—",
                        IsCorrect = isCorrect,
                        IsSkipped = isSkipped,
                        StatusText = isSkipped ? "متخطى" : (isCorrect ? "صحيح" : "خطأ"),
                        TimeTakenSeconds = isSkipped ? null : attempt!.TimeTakenSeconds
                    };
                })
                .OrderBy(q => q.OrderNumber)
                .ToList();

            var correctCount = rows.Count(q => q.IsCorrect);
            var skippedCount = rows.Count(q => q.IsSkipped);
            var wrongCount = rows.Count(q => !q.IsCorrect);
            var total = rows.Count;

            var vm = new StudentPerformanceIndicatorQuestionReportViewModel
            {
                ExamId = exam.Id,
                ExamTitle = exam.Title,
                StudentId = student.StudentID,
                StudentName = student.FullName,
                TotalQuestions = total,
                CorrectCount = correctCount,
                WrongCount = wrongCount,
                SkippedCount = skippedCount,
                ScorePercent = total > 0 ? Math.Round(correctCount * 100.0 / total, 1) : 0,
                Questions = rows
            };

            return View(vm);
        }




        // 🟢 حذف اختبار بالكامل مع العلاقات
        [HttpPost]
        [AdminPermission("PerformanceIndicatorExams", "Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var success = await _examService.DeleteExamAsync(id);
            TempData[success ? "Success" : "Error"] = success
                ? "✅ تم حذف الاختبار وجميع بياناته التابعة بنجاح."
                : "⚠️ لم يتم العثور على الاختبار المحدد.";
            return RedirectToAction(nameof(Index));
        }

        // 🟢 توليد كود مرجعي فريد
        private string GenerateReferenceCode(ApplicationDbContext context)
        {
            var random = new Random();
            string code;
            do
            {
                code = random.Next(100000, 999999).ToString();
            }
            while (context.PerformanceIndicatorExams.Any(e => e.ReferenceCode == code));

            return code;
        }

        // =====================================================
        // إدارة صلاحيات الوصول لأرشيف اختبارات مؤشر الأداء (Owner/Developer فقط)
        // =====================================================
        private bool IsArchiveOwner() =>
            User.IsInRole("Owner") || User.IsInRole("Developer");

        private string CurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        [HttpGet]
        [AdminPermission("PerformanceIndicatorExams", "Archive")]
        public async Task<IActionResult> ManageArchiveAccess(int examId)
        {
            if (!IsArchiveOwner())
                return Forbid();

            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null) return NotFound();

            if (!exam.IsArchived)
            {
                TempData["Error"] = "إدارة الصلاحيات متاحة للاختبارات المؤرشفة فقط.";
                return RedirectToAction(nameof(Index), new { showArchived = true });
            }

            var users = await GetArchiveCandidateUsersAsync();
            var allowedUserIds = (await _context.PerformanceIndicatorExamArchiveAccesses
                .AsNoTracking()
                .Where(x => x.ExamId == examId && x.IsActive)
                .Select(x => x.UserId)
                .ToListAsync()).ToHashSet();

            var items = new List<PerformanceIndicatorArchiveUserAccessItem>();
            foreach (var user in users.OrderBy(x => x.FullName ?? x.UserName))
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new PerformanceIndicatorArchiveUserAccessItem
                {
                    UserId      = user.Id,
                    DisplayName = user.FullName ?? user.UserName ?? user.Email ?? "؟",
                    Email       = user.Email ?? string.Empty,
                    Roles       = string.Join("، ", roles),
                    IsAllowed   = allowedUserIds.Contains(user.Id)
                });
            }

            var model = new PerformanceIndicatorExamArchiveAccessVM
            {
                ExamId    = exam.Id,
                ExamTitle = exam.Title,
                Users     = items
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PerformanceIndicatorExams", "Archive")]
        public async Task<IActionResult> UpdateArchiveAccess(int examId, List<string> allowedUserIds)
        {
            if (!IsArchiveOwner())
                return Forbid();

            using var _context = _contextFactory.CreateDbContext();

            var exam = await _context.PerformanceIndicatorExams.FirstOrDefaultAsync(e => e.Id == examId);
            if (exam == null) return NotFound();

            allowedUserIds ??= new List<string>();
            var allowedSet = allowedUserIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct().ToHashSet();

            var candidateUsers   = await GetArchiveCandidateUsersAsync();
            var candidateUserIds = candidateUsers.Select(x => x.Id).ToHashSet();
            allowedSet.RemoveWhere(x => !candidateUserIds.Contains(x));

            var existing        = await _context.PerformanceIndicatorExamArchiveAccesses
                .Where(x => x.ExamId == examId).ToListAsync();
            var existingUserIds = existing.Select(x => x.UserId).ToHashSet();
            var now             = DateTime.UtcNow;
            var currentUserId   = CurrentUserId();

            foreach (var access in existing)
                access.IsActive = allowedSet.Contains(access.UserId);

            foreach (var userId in allowedSet.Where(x => !existingUserIds.Contains(x)))
            {
                _context.PerformanceIndicatorExamArchiveAccesses.Add(new PerformanceIndicatorExamArchiveAccess
                {
                    ExamId          = examId,
                    UserId          = userId,
                    GrantedByUserId = currentUserId,
                    GrantedAt       = now,
                    IsActive        = true
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم تحديث صلاحيات الوصول للاختبار. عدد المصرح لهم: {allowedSet.Count}.";
            return RedirectToAction(nameof(ManageArchiveAccess), new { examId });
        }

        private async Task<List<ApplicationUser>> GetArchiveCandidateUsersAsync()
        {
            var roleNames = new[] { "Admin", "Employee", "SuperAdmin" };
            var usersById = new Dictionary<string, ApplicationUser>();
            foreach (var roleName in roleNames)
            {
                var users = await _userManager.GetUsersInRoleAsync(roleName);
                foreach (var user in users.Where(x => x.IsActive))
                    usersById[user.Id] = user;
            }
            return usersById.Values.ToList();
        }
    }
}
