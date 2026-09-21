using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PuppeteerSharp;
using PuppeteerSharp.Media;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Reports;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Areas.Admin.Controllers
{
    // اختبار محاكاة اختبار الوزارة — Sprint 2 (MSE-B): واجهة بناء الاختبار (5 كروت مراحل + Dropdown محور لكل منهج)
    // Sprint 3 (MSE-B): Endpoint AJAX لجلب مؤشرات المحور مع عدد الأسئلة المتاحة لكل صعوبة
    // Sprint 5 (MSE-C): حفظ الاختبار كمسودة (C4) + بوابة النشر الصارمة (C3)
    // Sprint 6 (MSE-D): شاشة مراجعة أسئلة المرحلة (D1) + استبدال سؤال (D2)
    // Sprint 7 (MSE-D): استبعاد سؤال + إعادة توليد مؤشر (D3) + إضافة سؤال يدويًا (D4)
    // Sprint 8 (MSE-E): إسناد لدفعة/دفعات (E1) + إسناد لطالب/طلاب محددين (E2) + فرض محاولة واحدة عند الإسناد (E4)
    // Sprint 18 (MSE-K): تقرير ولي الأمر — ViewModel + الشاشة الأساسية (K1: GetBatchAverageStatsAsync، K2: ParentReport)
    [Area("Admin")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,Employee,DataEntry")]
    public class MinistrySimExamController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMinistrySimExamGeneratorService _generatorService;
        private readonly IMinistrySimExamAssignmentService _assignmentService;
        private readonly IMinistrySimExamResultService _resultService;
        private readonly IMinistrySimExamBatchAnalyticsService _batchAnalyticsService;
        private readonly ISystemSettingService _settingService;
        private readonly ILogger<MinistrySimExamController> _logger;
        private readonly ReportBrowserProvider _reportBrowserProvider;

        public MinistrySimExamController(
            ApplicationDbContext context,
            IMinistrySimExamGeneratorService generatorService,
            IMinistrySimExamAssignmentService assignmentService,
            IMinistrySimExamResultService resultService,
            IMinistrySimExamBatchAnalyticsService batchAnalyticsService,
            ISystemSettingService settingService,
            ILogger<MinistrySimExamController> logger,
            ReportBrowserProvider reportBrowserProvider)
        {
            _context = context;
            _generatorService = generatorService;
            _assignmentService = assignmentService;
            _resultService = resultService;
            _batchAnalyticsService = batchAnalyticsService;
            _settingService = settingService;
            _logger = logger;
            _reportBrowserProvider = reportBrowserProvider;
        }

        // نقطة الدخول من قائمة الأدمن (لم تكن موجودة في أي Sprint سابق — الميزة لم يكن لها أي رابط دخول قبل هذا):
        // تعرض كل اختبارات محاكاة الوزارة الموجودة (لكل الدورات) + نموذج بسيط لاختيار دورة وبدء اختبار جديد عبر Create.
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var vm = new MinistrySimExamIndexVm
            {
                Exams = await _context.MinistrySimExams
                    .AsNoTracking()
                    .OrderByDescending(e => e.CreatedAt)
                    .Select(e => new MinistrySimExamListItemVm
                    {
                        Id = e.Id,
                        Title = e.Title,
                        CourseName = e.Course.Name,
                        IsPublished = e.IsPublished,
                        CreatedAt = e.CreatedAt
                    })
                    .ToListAsync(),

                Courses = await _context.Courses
                    .AsNoTracking()
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Name })
                    .ToListAsync()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int courseId)
        {
            var course = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == courseId);

            if (course == null)
                return NotFound("❌ لم يتم العثور على الدورة.");

            var vm = new MinistrySimExamCreateVm
            {
                CourseId = courseId,
                CourseName = course.Name,
                Title = $"اختبار معمل القياس - {course.Name}",
                Stages = Enumerable.Range(1, 5)
                    .Select(stageNumber => new MinistrySimExamStageCreateVm { StageNumber = stageNumber })
                    .ToList()
            };

            await PopulateSectionsAsync(vm);

            return View(vm);
        }

        // Sprint 5 (MSE-C / C4): يحفظ الاختبار كمسودة (IsPublished = false) — 5 مراحل + توليد أسئلة كل مرحلة
        // فورًا من اختيارات المؤشرات المُرسَلة، دون أي علاقة بشرط النشر الصارم (ADR-MSE-3). النواقص هنا مسموح بها
        // وتُعرض للأدمن في شاشة Details ليكملها لاحقًا عبر شاشات المراجعة/الاستبدال (Sprint 6/7).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(MinistrySimExamCreateVm vm)
        {
            // تشخيص مؤقت (سيُزال بعد تحديد سبب فشل ربط Stages) — يسجّل كل مفاتيح النموذج المُرسَلة فعليًا من المتصفح
            _logger.LogWarning("MSE Create POST diagnostic — raw form keys: {Keys}",
                string.Join(" | ", Request.Form.Select(kv => kv.Key + "=" + kv.Value)));
            _logger.LogWarning("MSE Create POST diagnostic — bound vm.Stages.Count = {Count}", vm.Stages?.Count ?? -1);

            if (vm.Stages == null || vm.Stages.Count != 5)
                ModelState.AddModelError(string.Empty, "يجب تحديد كل المراحل الخمس بالكامل.");

            foreach (var stage in vm.Stages ?? new List<MinistrySimExamStageCreateVm>())
            {
                var quantSum = stage.QuantIndicators?.Sum(x => x.RequestedCount) ?? 0;
                var verbalSum = stage.VerbalIndicators?.Sum(x => x.RequestedCount) ?? 0;

                if (quantSum != stage.QuantQuestionCount)
                    ModelState.AddModelError(string.Empty,
                        $"المرحلة {stage.StageNumber}: مجموع أسئلة المؤشرات الكمية المختارة ({quantSum}) لا يطابق العدد المطلوب ({stage.QuantQuestionCount}).");

                if (verbalSum != stage.VerbalQuestionCount)
                    ModelState.AddModelError(string.Empty,
                        $"المرحلة {stage.StageNumber}: مجموع أسئلة المؤشرات اللفظية المختارة ({verbalSum}) لا يطابق العدد المطلوب ({stage.VerbalQuestionCount}).");
            }

            if (!ModelState.IsValid)
            {
                await PopulateSectionsAsync(vm);

                // كان يُستبدَل برسالة عامة واحدة تُخفي رسائل ModelState الدقيقة (رقم المرحلة/المحور والفارق
                // بالضبط) — الآن تُعرض كل الرسائل الفعلية كما هي لتوضيح السبب الحقيقي للأدمن بدل تخمينه.
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct()
                    .ToList();

                TempData["Error"] = errors.Any()
                    ? string.Join("\n", errors)
                    : "تحقق من البيانات المدخلة: بعض الحقول ناقصة أو عدد المؤشرات المختارة لا يطابق العدد المطلوب لمرحلة ما.";
                return View(vm);
            }

            var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == vm.CourseId);
            if (course == null)
                return NotFound("❌ لم يتم العثور على الدورة.");

            var curriculumIds = await _context.CourseCurriculums
                .AsNoTracking()
                .Where(cc => cc.CourseId == vm.CourseId)
                .Select(cc => cc.CurriculumId)
                .ToListAsync();

            // القرار: يُولَّد الاختبار من قبل الأدمن/الموظف مباشرة، والمدرب المرتبط فعليًا بمناهج الدورة
            // (InstructorCurriculumBatch) يُسجَّل تلقائيًا كـ CreatedByInstructorId — بلا اختيار يدوي إضافي.
            var instructorId = await _context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(icb => curriculumIds.Contains(icb.CurriculumId))
                .Select(icb => icb.InstructorId)
                .FirstOrDefaultAsync();

            if (instructorId == 0)
            {
                await PopulateSectionsAsync(vm);
                TempData["Error"] = "لا يوجد مدرب مرتبط بمناهج هذه الدورة بعد — يجب ربط مدرب بمنهج الدورة أولاً قبل بناء اختبار المحاكاة.";
                return View(vm);
            }

            var exam = new MinistrySimExam
            {
                CourseId = vm.CourseId,
                Title = vm.Title,
                CreatedByInstructorId = instructorId
            };
            _context.MinistrySimExams.Add(exam);
            await _context.SaveChangesAsync();

            var stageEntities = vm.Stages.Select(s => new MinistrySimExamStage
            {
                MinistrySimExamId = exam.Id,
                StageNumber = s.StageNumber,
                QuantSectionId = s.QuantSectionId,
                QuantQuestionCount = s.QuantQuestionCount,
                VerbalSectionId = s.VerbalSectionId,
                VerbalQuestionCount = s.VerbalQuestionCount,
                DurationMinutes = s.DurationMinutes
            }).ToList();

            _context.MinistrySimExamStages.AddRange(stageEntities);
            await _context.SaveChangesAsync();

            for (int i = 0; i < vm.Stages.Count; i++)
            {
                var stageVm = vm.Stages[i];
                var stageEntity = stageEntities[i];

                var quantSelections = (stageVm.QuantIndicators ?? new List<MinistrySimExamStageIndicatorInputVm>())
                    .Where(x => x.RequestedCount > 0)
                    .Select(x => new IndicatorSelectionInput { LessonId = x.LessonId, Difficulty = x.Difficulty, RequestedCount = x.RequestedCount })
                    .ToList();

                var verbalSelections = (stageVm.VerbalIndicators ?? new List<MinistrySimExamStageIndicatorInputVm>())
                    .Where(x => x.RequestedCount > 0)
                    .Select(x => new IndicatorSelectionInput { LessonId = x.LessonId, Difficulty = x.Difficulty, RequestedCount = x.RequestedCount })
                    .ToList();

                // النواقص هنا (إن وجدت) لا تمنع الحفظ كمسودة (ADR-MSE-3) — تُعرض لاحقًا في شاشة Details
                await _generatorService.GenerateStageQuestionsAsync(stageEntity.Id, quantSelections, verbalSelections);
            }

            return RedirectToAction(nameof(Details), new { id = exam.Id });
        }

        // Sprint 12 (تصحيح بعد مراجعة المطوّر): Curriculum.IsQuantitative لا يحدّد محور المنهج (كمي/لفظي) —
        // هذا الحقل يخص تنسيق عرض الأرقام (هندية/عربية) فقط، وليس له علاقة بموضوع الأسئلة. المحور الحقيقي لكل
        // محور (Section) يُشتق من أغلبية Question.IsQuantitative لأسئلته الفعلية — وهو نفس الحقل المستخدم فعليًا
        // في CourseExamGeneratorService لفصل الكمي عن اللفظي، وليس أي حقل على مستوى Curriculum.
        private async Task PopulateSectionsAsync(MinistrySimExamCreateVm vm)
        {
            var curriculumIds = await _context.CourseCurriculums
                .AsNoTracking()
                .Where(cc => cc.CourseId == vm.CourseId)
                .Select(cc => cc.CurriculumId)
                .ToListAsync();

            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => EF.Constant(curriculumIds).Contains(s.CurriculumId))
                .Select(s => new { s.Id, s.Title })
                .ToListAsync();

            var sectionIds = sections.Select(s => s.Id).ToList();

            var axisCounts = await _context.Questions
                .AsNoTracking()
                .Where(q => q.SectionId != null && EF.Constant(sectionIds).Contains(q.SectionId.Value))
                .GroupBy(q => q.SectionId)
                .Select(g => new
                {
                    SectionId = g.Key.Value,
                    QuantCount = g.Count(q => q.IsQuantitative),
                    TotalCount = g.Count()
                })
                .ToListAsync();

            // قسم بلا أسئلة إطلاقًا (لا يوجد ما يُبنى منه اختبار) يُستبعد من كلا القائمتين عمدًا
            var isQuantBySection = axisCounts
                .Where(x => x.TotalCount > 0)
                .ToDictionary(x => x.SectionId, x => x.QuantCount * 2 >= x.TotalCount);

            vm.QuantSections = sections
                .Where(s => isQuantBySection.TryGetValue(s.Id, out var isQuant) && isQuant)
                .OrderBy(s => s.Title)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                .ToList();

            vm.VerbalSections = sections
                .Where(s => isQuantBySection.TryGetValue(s.Id, out var isQuant) && !isQuant)
                .OrderBy(s => s.Title)
                .Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Title })
                .ToList();
        }

        // Sprint 5 (MSE-C / C4): شاشة حالة المسودة — يعود إليها الأدمن لاحقًا لمتابعة الاكتمال أو النشر
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على اختبار معمل القياس المطلوب.");

            var stages = await _context.MinistrySimExamStages
                .AsNoTracking()
                .Where(s => s.MinistrySimExamId == id)
                .OrderBy(s => s.StageNumber)
                .Select(s => new MinistrySimExamStageStatusVm
                {
                    StageId = s.Id,
                    StageNumber = s.StageNumber,
                    QuantSectionTitle = s.QuantSection.Title,
                    QuantTarget = s.QuantQuestionCount,
                    QuantActual = s.Questions.Count(q => q.IsQuant),
                    VerbalSectionTitle = s.VerbalSection.Title,
                    VerbalTarget = s.VerbalQuestionCount,
                    VerbalActual = s.Questions.Count(q => !q.IsQuant),
                    DurationMinutes = s.DurationMinutes
                })
                .ToListAsync();

            var validation = await _generatorService.ValidateExactCountsAsync(id);

            var assignedBatchesCount = await _context.MinistrySimExamAssignmentsToBatches
                .AsNoTracking()
                .CountAsync(x => x.MinistrySimExamId == id);

            var assignedStudentsCount = await _context.MinistrySimExamAssignmentsToStudents
                .AsNoTracking()
                .CountAsync(x => x.MinistrySimExamId == id);

            var assignedBatches = await (
                from a in _context.MinistrySimExamAssignmentsToBatches.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on a.BatchId equals b.Id
                where a.MinistrySimExamId == id
                orderby b.Name
                select new MinistrySimExamAssignedBatchVm { BatchId = b.Id, BatchName = b.Name }
            ).ToListAsync();

            var vm = new MinistrySimExamDetailsVm
            {
                ExamId = exam.Id,
                Title = exam.Title,
                CourseName = exam.Course.Name,
                IsPublished = exam.IsPublished,
                PublishedAt = exam.PublishedAt,
                Stages = stages,
                IsReadyToPublish = validation.IsValid,
                ShortfallMessages = validation.ShortfallMessages,
                AssignedBatchesCount = assignedBatchesCount,
                AssignedStudentsCount = assignedStudentsCount,
                AssignedBatches = assignedBatches
            };

            return View(vm);
        }

        // Sprint 5 (MSE-C / C3): بوابة النشر الصارمة — ترفض النشر بأي نقص/زيادة عن العدد المطلوب بالضبط (400 + رسائل تفصيلية)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(int id)
        {
            var exam = await _context.MinistrySimExams.FirstOrDefaultAsync(e => e.Id == id);
            if (exam == null)
                return NotFound(new { success = false, messages = new[] { "لم يتم العثور على اختبار معمل القياس المطلوب." } });

            if (exam.IsPublished)
                return BadRequest(new { success = false, messages = new[] { "الاختبار منشور بالفعل." } });

            var validation = await _generatorService.ValidateExactCountsAsync(id);
            if (!validation.IsValid)
                return BadRequest(new { success = false, messages = validation.ShortfallMessages });

            exam.IsPublished = true;
            exam.PublishedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "تم نشر اختبار معمل القياس بنجاح." });
        }

        // Sprint 3 (MSE-B / B3): يُستدعى عبر AJAX عند اختيار محور (كمي أو لفظي) داخل أي كرت مرحلة
        // يُرجع قائمة مؤشرات (Lessons) المحور مع عدد الأسئلة المتاحة فعليًا في البنك لكل صعوبة،
        // بنفس شروط الجودة المستخدمة في ExamQuestionSelectorService (IsReviewed && !IsRejected && CorrectAnswer != null)
        [HttpGet]
        public async Task<IActionResult> GetIndicatorsForSection(int sectionId)
        {
            var sectionExists = await _context.Sections
                .AsNoTracking()
                .AnyAsync(s => s.Id == sectionId);

            if (!sectionExists)
                return NotFound("❌ لم يتم العثور على المحور.");

            var vm = new MinistrySimExamIndicatorsListVm { SectionId = sectionId };

            var lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .OrderBy(l => l.Title)
                .Select(l => new { l.Id, l.Title })
                .ToListAsync();

            if (!lessons.Any())
                return PartialView("_IndicatorsListPartial", vm);

            var lessonIds = lessons.Select(l => l.Id).ToList();

            // EF.Constant() يجبر الترجمة على IN (...) حرفية بدل OPENJSON غير المدعوم على SQL Server 2014 (Compat 120)
            var counts = await _context.Questions
                .AsNoTracking()
                .Where(q => EF.Constant(lessonIds).Contains(q.LessonId)
                            && q.IsReviewed
                            && !q.IsRejected
                            && q.CorrectAnswer != null)
                .GroupBy(q => new { q.LessonId, q.Difficulty })
                .Select(g => new { g.Key.LessonId, g.Key.Difficulty, Count = g.Count() })
                .ToListAsync();

            vm.Indicators = lessons.Select(l => new MinistrySimExamIndicatorRowVm
            {
                LessonId = l.Id,
                LessonTitle = l.Title,
                EasyAvailable = counts.Where(c => c.LessonId == l.Id && c.Difficulty == DifficultyLevel.Easy).Select(c => c.Count).FirstOrDefault(),
                MediumAvailable = counts.Where(c => c.LessonId == l.Id && c.Difficulty == DifficultyLevel.Medium).Select(c => c.Count).FirstOrDefault(),
                HardAvailable = counts.Where(c => c.LessonId == l.Id && c.Difficulty == DifficultyLevel.Hard).Select(c => c.Count).FirstOrDefault(),
                VeryHardAvailable = counts.Where(c => c.LessonId == l.Id && c.Difficulty == DifficultyLevel.VeryHard).Select(c => c.Count).FirstOrDefault(),
            }).ToList();

            return PartialView("_IndicatorsListPartial", vm);
        }

        // Sprint 6 (MSE-D / D1): يعرض كل أسئلة المرحلة (24 سؤال) بترتيب StageOrder، مع زر استبدال بجوار كل سؤال
        [HttpGet]
        public async Task<IActionResult> ReviewStage(int stageId)
        {
            var stage = await _context.MinistrySimExamStages
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == stageId);

            if (stage == null)
                return NotFound("❌ لم يتم العثور على مرحلة اختبار معمل القياس المطلوبة.");

            var quantSectionTitle = await _context.Sections
                .AsNoTracking()
                .Where(s => s.Id == stage.QuantSectionId)
                .Select(s => s.Title)
                .FirstOrDefaultAsync() ?? "—";

            var verbalSectionTitle = await _context.Sections
                .AsNoTracking()
                .Where(s => s.Id == stage.VerbalSectionId)
                .Select(s => s.Title)
                .FirstOrDefaultAsync() ?? "—";

            var questions = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(q => q.MinistrySimExamStageId == stageId)
                .OrderBy(q => q.StageOrder)
                .Select(q => new MinistrySimExamReviewQuestionVm
                {
                    StageQuestionId = q.Id,
                    QuestionId = q.QuestionId,
                    StageOrder = q.StageOrder,
                    GlobalOrder = q.GlobalOrder,
                    IsQuant = q.IsQuant,
                    IsManuallySelected = q.IsManuallySelected,
                    Title = q.Question.Title,
                    CorrectAnswer = q.Question.CorrectAnswer,
                    DifficultyLevel = (int)q.Question.Difficulty,
                    LessonTitle = q.Question.Lesson.Title,
                    SectionTitle = q.Question.Lesson.Section.Title
                })
                .ToListAsync();

            // Sprint 7 (MSE-D / D3-D4): حالة كل مؤشر (مطلوب مقابل فعلي) لعرض أزرار "إعادة توليد"/"إضافة يدوية" عند النقص
            var indicatorSelections = await _context.MinistrySimExamStageIndicatorSelections
                .AsNoTracking()
                .Where(x => x.MinistrySimExamStageId == stageId)
                .Select(x => new { x.SectionId, x.LessonId, x.Difficulty, x.RequestedCount, LessonTitle = x.Lesson.Title })
                .ToListAsync();

            var indicatorActualCounts = await (
                from sq in _context.MinistrySimExamStageQuestions.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on sq.QuestionId equals q.Id
                where sq.MinistrySimExamStageId == stageId
                group q by new { q.LessonId, q.Difficulty } into g
                select new { g.Key.LessonId, g.Key.Difficulty, Count = g.Count() }
            ).ToListAsync();

            var indicatorStatuses = indicatorSelections
                .Select(sel => new MinistrySimExamReviewIndicatorStatusVm
                {
                    LessonId = sel.LessonId,
                    LessonTitle = sel.LessonTitle,
                    IsQuant = sel.SectionId == stage.QuantSectionId,
                    DifficultyLevel = (int)sel.Difficulty,
                    RequestedCount = sel.RequestedCount,
                    ActualCount = indicatorActualCounts
                        .Where(a => a.LessonId == sel.LessonId && a.Difficulty == sel.Difficulty)
                        .Select(a => a.Count)
                        .FirstOrDefault()
                })
                .OrderBy(x => !x.IsQuant)
                .ThenBy(x => x.LessonTitle)
                .ThenBy(x => x.DifficultyLevel)
                .ToList();

            var vm = new MinistrySimExamReviewStageVm
            {
                StageId = stage.Id,
                MinistrySimExamId = stage.MinistrySimExamId,
                StageNumber = stage.StageNumber,
                QuantSectionTitle = quantSectionTitle,
                VerbalSectionTitle = verbalSectionTitle,
                DurationMinutes = stage.DurationMinutes,
                Questions = questions,
                IndicatorStatuses = indicatorStatuses
            };

            return View(vm);
        }

        // Sprint 6 (MSE-D / D2): استنساخ نمط ExamAssignmentsController.ReplaceQuestionPage — البدائل من نفس
        // المؤشر (Lesson) ونفس مستوى الصعوبة بالضبط (وليس فقط نفس المحور/المنهج)، لأن ValidateExactCountsAsync
        // تتحقق من العدد الفعلي لكل (مرحلة، مؤشر، صعوبة) — أي بديل بصعوبة مختلفة يكسر اكتمال العدد المطلوب.
        [HttpGet]
        public async Task<IActionResult> ReplaceQuestionPage(int stageQuestionId)
        {
            var link = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == stageQuestionId);

            if (link == null)
                return NotFound("❌ لم يتم العثور على السؤال المطلوب داخل هذه المرحلة.");

            var ministrySimExamId = await _context.MinistrySimExamStages
                .AsNoTracking()
                .Where(s => s.Id == link.MinistrySimExamStageId)
                .Select(s => s.MinistrySimExamId)
                .FirstOrDefaultAsync();

            var oldQuestion = await _context.Questions
                .AsNoTracking()
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(q => q.Id == link.QuestionId);

            if (oldQuestion == null)
                return NotFound("❌ لم يتم العثور على السؤال.");

            // الأسئلة المستخدمة فعليًا داخل نفس المرحلة تُستبعد من البدائل لمنع تكرار نفس السؤال مرتين بالمرحلة
            var usedQuestionIds = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(q => q.MinistrySimExamStageId == link.MinistrySimExamStageId)
                .Select(q => q.QuestionId)
                .ToListAsync();

            // EF.Constant() يجبر الترجمة على IN (...) حرفية بدل OPENJSON غير المدعوم على SQL Server 2014
            var alternatives = await _context.Questions
                .AsNoTracking()
                .Where(q => q.LessonId == oldQuestion.LessonId
                            && q.Difficulty == oldQuestion.Difficulty
                            && q.IsReviewed && !q.IsRejected && q.CorrectAnswer != null
                            && !EF.Constant(usedQuestionIds).Contains(q.Id))
                .OrderBy(q => q.Title)
                .Take(200)
                .Select(q => new MinistrySimExamAlternativeQuestionVm
                {
                    QuestionId = q.Id,
                    Title = q.Title,
                    CorrectAnswer = q.CorrectAnswer,
                    DifficultyLevel = (int)q.Difficulty
                })
                .ToListAsync();

            var vm = new MinistrySimExamReplaceQuestionVm
            {
                StageQuestionId = link.Id,
                MinistrySimExamId = ministrySimExamId,
                StageId = link.MinistrySimExamStageId,
                OldQuestionId = oldQuestion.Id,
                OldTitle = oldQuestion.Title,
                StageOrder = link.StageOrder,
                LessonTitle = oldQuestion.Lesson?.Title ?? "—",
                SectionTitle = oldQuestion.Lesson?.Section?.Title ?? "—",
                DifficultyLevel = (int)oldQuestion.Difficulty,
                Alternatives = alternatives
            };

            return View(vm);
        }

        // Sprint 6 (MSE-D / D2): يستبدل السؤال بتحديث QuestionId على نفس رابط MinistrySimExamStageQuestion —
        // يحافظ تلقائيًا على نفس StageOrder/GlobalOrder دون أي إعادة ترقيم.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplaceQuestionConfirm(int stageQuestionId, Guid newQuestionId)
        {
            var link = await _context.MinistrySimExamStageQuestions
                .FirstOrDefaultAsync(q => q.Id == stageQuestionId);

            if (link == null)
                return Json(new { success = false, message = "لم يتم العثور على السؤال المطلوب استبداله." });

            var oldQuestion = await _context.Questions
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == link.QuestionId);

            var newQuestion = await _context.Questions
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == newQuestionId);

            if (newQuestion == null)
                return Json(new { success = false, message = "السؤال البديل غير موجود." });

            // تحقق خادمي صارم: البديل يجب أن يكون من نفس المؤشر ونفس مستوى الصعوبة بالضبط، وإلا يُفسد
            // اكتمال العدد المطلوب لكل (مرحلة، مؤشر، صعوبة) الذي تتحقق منه ValidateExactCountsAsync
            if (oldQuestion == null || newQuestion.LessonId != oldQuestion.LessonId || newQuestion.Difficulty != oldQuestion.Difficulty)
                return Json(new { success = false, message = "السؤال البديل يجب أن يكون من نفس المؤشر ونفس مستوى الصعوبة." });

            var alreadyUsedInStage = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .AnyAsync(q => q.MinistrySimExamStageId == link.MinistrySimExamStageId && q.QuestionId == newQuestionId);

            if (alreadyUsedInStage)
                return Json(new { success = false, message = "هذا السؤال مستخدم بالفعل داخل نفس المرحلة." });

            link.QuestionId = newQuestionId;
            link.IsManuallySelected = true;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Sprint 7 (MSE-D / D3): يستبعد سؤالاً من المرحلة — حذف الرابط فقط بلا أي سحب تعويضي تلقائي (القرار #2)،
        // يترك المؤشر ناقصًا حتى يقرر الأدمن إعادة التوليد أو الإضافة اليدوية.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcludeQuestion(int stageQuestionId)
        {
            try
            {
                await _generatorService.RemoveStageQuestionAsync(stageQuestionId);
                return Json(new { success = true });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Sprint 7 (MSE-D / D3): زر "إعادة توليد لهذا المؤشر" — يستبدل كل أسئلة مؤشر واحد (Lesson+Difficulty) ضمن
        // المرحلة باختيار عشوائي جديد بنفس العدد المطلوب أصلاً، بلا أي تأثير على بقية مؤشرات المرحلة.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateIndicator(int stageId, int lessonId, DifficultyLevel difficulty)
        {
            try
            {
                var result = await _generatorService.RegenerateIndicatorAsync(stageId, lessonId, difficulty);
                return Json(new { success = result.IsSuccess, messages = result.ShortfallMessages });
            }
            catch (InvalidOperationException ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Sprint 7 (MSE-D / D4): يعرض أسئلة نفس المؤشر/الصعوبة غير المستخدمة بعد في المرحلة، لإضافة واحد منها يدويًا
        [HttpGet]
        public async Task<IActionResult> AddQuestionPage(int stageId, int lessonId, DifficultyLevel difficulty)
        {
            var stage = await _context.MinistrySimExamStages
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == stageId);

            if (stage == null)
                return NotFound("❌ لم يتم العثور على مرحلة اختبار معمل القياس المطلوبة.");

            var lesson = await _context.Lessons
                .AsNoTracking()
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return NotFound("❌ لم يتم العثور على المؤشر المطلوب.");

            var usedQuestionIds = await _context.MinistrySimExamStageQuestions
                .AsNoTracking()
                .Where(q => q.MinistrySimExamStageId == stageId)
                .Select(q => q.QuestionId)
                .ToListAsync();

            // EF.Constant() يجبر الترجمة على IN (...) حرفية بدل OPENJSON غير المدعوم على SQL Server 2014
            var candidates = await _context.Questions
                .AsNoTracking()
                .Where(q => q.LessonId == lessonId
                            && q.Difficulty == difficulty
                            && q.IsReviewed && !q.IsRejected && q.CorrectAnswer != null
                            && !EF.Constant(usedQuestionIds).Contains(q.Id))
                .OrderBy(q => q.Title)
                .Take(200)
                .Select(q => new MinistrySimExamAlternativeQuestionVm
                {
                    QuestionId = q.Id,
                    Title = q.Title,
                    CorrectAnswer = q.CorrectAnswer,
                    DifficultyLevel = (int)q.Difficulty
                })
                .ToListAsync();

            var vm = new MinistrySimExamAddQuestionVm
            {
                StageId = stageId,
                MinistrySimExamId = stage.MinistrySimExamId,
                LessonId = lessonId,
                LessonTitle = lesson.Title,
                SectionTitle = lesson.Section?.Title ?? "—",
                DifficultyLevel = (int)difficulty,
                Candidates = candidates
            };

            return View(vm);
        }

        // Sprint 7 (MSE-D / D4): يضيف سؤالاً واحدًا محددًا يدويًا إلى المرحلة، بعد التحقق أنه من نفس المؤشر/الصعوبة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddQuestionManuallyConfirm(int stageId, int lessonId, DifficultyLevel difficulty, Guid questionId)
        {
            var result = await _generatorService.AddQuestionManuallyAsync(stageId, lessonId, difficulty, questionId);
            return Json(new { success = result.Success, message = result.Message });
        }

        // Sprint 8 (MSE-E / E1): شاشة اختيار دفعة/دفعات لإسناد الاختبار — يتطلب IsPublished == true
        [HttpGet]
        public async Task<IActionResult> AssignToBatch(int id)
        {
            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على اختبار معمل القياس المطلوب.");

            if (!exam.IsPublished)
            {
                TempData["Error"] = "لا يمكن إسناد الاختبار لدفعة إلا بعد نشره أولاً.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var courseName = await _context.Courses
                .AsNoTracking()
                .Where(c => c.Id == exam.CourseId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();

            var assignedBatchIds = await _context.MinistrySimExamAssignmentsToBatches
                .AsNoTracking()
                .Where(x => x.MinistrySimExamId == id)
                .Select(x => x.BatchId)
                .ToListAsync();

            var batches = await _context.Batches
                .AsNoTracking()
                .Where(b => b.CourseId == exam.CourseId && !b.IsDeleted && !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new MinistrySimExamBatchOptionVm
                {
                    BatchId = b.Id,
                    Name = b.Name,
                    EnrolledStudentsCount = b.StudentBatchEnrollments.Count(e => e.Status == "Active")
                })
                .ToListAsync();

            foreach (var batch in batches)
                batch.AlreadyAssigned = assignedBatchIds.Contains(batch.BatchId);

            var vm = new MinistrySimExamAssignBatchPageVm
            {
                ExamId = exam.Id,
                Title = exam.Title,
                CourseName = courseName,
                Batches = batches
            };

            return View(vm);
        }

        // Sprint 8 (MSE-E / E1): نفس اشتقاق CreatedByInstructorId المستخدم في Create POST — أول مدرب مرتبط بمناهج دورة الاختبار
        // Sprint 17 (MSE-J / J2): isOnline من Radio أونلاين/حضوري في الشاشة — حضوري يولّد رمزًا مرجعيًا يُعرض في Details بعد التحويل
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignToBatch(int id, List<int> batchIds, bool isOnline = true)
        {
            var courseId = await _context.MinistrySimExams
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(e => e.CourseId)
                .FirstOrDefaultAsync();

            var curriculumIds = await _context.CourseCurriculums
                .AsNoTracking()
                .Where(cc => cc.CourseId == courseId)
                .Select(cc => cc.CurriculumId)
                .ToListAsync();

            var instructorId = await _context.InstructorCurriculumBatches
                .AsNoTracking()
                .Where(icb => EF.Constant(curriculumIds).Contains(icb.CurriculumId))
                .Select(icb => (int?)icb.InstructorId)
                .FirstOrDefaultAsync();

            var result = await _assignmentService.AssignToBatchesAsync(id, batchIds, instructorId, isOnline);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            if (result.SkippedMessages.Any())
                TempData["Warning"] = string.Join(" | ", result.SkippedMessages);
            if (!string.IsNullOrWhiteSpace(result.GeneratedReferenceCode))
                TempData["GeneratedReferenceCode"] = result.GeneratedReferenceCode;

            return RedirectToAction(nameof(Details), new { id });
        }

        // Sprint 8 (MSE-E / E2): شاشة اختيار طالب/طلاب محددين لإسناد الاختبار — يتطلب IsPublished == true
        [HttpGet]
        public async Task<IActionResult> AssignToStudent(int id)
        {
            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على اختبار معمل القياس المطلوب.");

            if (!exam.IsPublished)
            {
                TempData["Error"] = "لا يمكن إسناد الاختبار لطالب إلا بعد نشره أولاً.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var courseName = await _context.Courses
                .AsNoTracking()
                .Where(c => c.Id == exam.CourseId)
                .Select(c => c.Name)
                .FirstOrDefaultAsync();

            var batches = await _context.Batches
                .AsNoTracking()
                .Where(b => b.CourseId == exam.CourseId && !b.IsDeleted && !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new MinistrySimExamBatchDropdownVm { BatchId = b.Id, Name = b.Name })
                .ToListAsync();

            var vm = new MinistrySimExamAssignStudentPageVm
            {
                ExamId = exam.Id,
                Title = exam.Title,
                CourseName = courseName,
                Batches = batches
            };

            return View(vm);
        }

        // Sprint 8 (MSE-E / E2): يُستدعى عبر AJAX عند اختيار دفعة داخل شاشة إسناد الطلاب — يُرجع طلاب الدفعة كـ Checkboxes
        // مع تعليم من مُسنَد له الاختبار بالفعل، أو من لديه محاولة مسجَّلة أصلاً (القرار #7 / E4)
        [HttpGet]
        public async Task<IActionResult> GetStudentsForBatch(int examId, int batchId)
        {
            var batchExists = await _context.Batches.AsNoTracking().AnyAsync(b => b.Id == batchId);
            if (!batchExists)
                return NotFound("❌ لم يتم العثور على الدفعة.");

            var students = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.BatchId == batchId && e.Status == "Active")
                .OrderBy(e => e.Student.FullName)
                .Select(e => new { e.StudentID, e.Student.FullName })
                .ToListAsync();

            var vm = new MinistrySimExamStudentPickerVm { BatchId = batchId };

            if (!students.Any())
                return PartialView("_StudentPickerPartial", vm);

            var studentIds = students.Select(s => s.StudentID).ToList();

            var assignedStudentIds = await _context.MinistrySimExamAssignmentsToStudents
                .AsNoTracking()
                .Where(x => x.MinistrySimExamId == examId && EF.Constant(studentIds).Contains(x.StudentId))
                .Select(x => x.StudentId)
                .ToListAsync();

            var studentsWithAttempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .Where(x => x.MinistrySimExamId == examId && EF.Constant(studentIds).Contains(x.StudentId))
                .Select(x => x.StudentId)
                .ToListAsync();

            vm.Students = students.Select(s => new MinistrySimExamStudentOptionVm
            {
                StudentId = s.StudentID,
                FullName = s.FullName,
                AlreadyAssigned = assignedStudentIds.Contains(s.StudentID),
                HasAttempt = studentsWithAttempt.Contains(s.StudentID)
            }).ToList();

            return PartialView("_StudentPickerPartial", vm);
        }

        // Sprint 8 (MSE-E / E2): يُسنِد الاختبار للطلاب المختارين (تحقق الانتماء لدورة الاختبار + استبعاد المُكرَّر يتم داخل الخدمة)
        // Sprint 17 (MSE-J / J2): isOnline من Radio أونلاين/حضوري في الشاشة — حضوري يولّد رمزًا مرجعيًا يُعرض في Details بعد التحويل
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignToStudent(int id, List<int> studentIds, bool isOnline = true)
        {
            var result = await _assignmentService.AssignToStudentsAsync(id, studentIds, isOnline);

            TempData[result.Success ? "Success" : "Error"] = result.Message;
            if (result.SkippedMessages.Any())
                TempData["Warning"] = string.Join(" | ", result.SkippedMessages);
            if (!string.IsNullOrWhiteSpace(result.GeneratedReferenceCode))
                TempData["GeneratedReferenceCode"] = result.GeneratedReferenceCode;

            return RedirectToAction(nameof(Details), new { id });
        }

        // Sprint 14 (MSE-H / H2): شاشة اختيار الطالب لعرض نتيجته (Drill-down) — كل طالب مُسنَد له الاختبار
        // فعليًا (مباشرة أو عبر دفعة)، بحالته الحالية. مرتبطة من قسم "الإسناد" في Details.cshtml.
        [HttpGet]
        public async Task<IActionResult> SelectStudentForResult(int id)
        {
            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على اختبار معمل القياس المطلوب.");

            var directStudentIds = await _context.MinistrySimExamAssignmentsToStudents
                .AsNoTracking()
                .Where(x => x.MinistrySimExamId == id)
                .Select(x => x.StudentId)
                .ToListAsync();

            var assignedBatchIds = await _context.MinistrySimExamAssignmentsToBatches
                .AsNoTracking()
                .Where(x => x.MinistrySimExamId == id)
                .Select(x => x.BatchId)
                .ToListAsync();

            var batchStudentIds = assignedBatchIds.Any()
                ? await _context.StudentBatchEnrollments
                    .AsNoTracking()
                    .Where(e => e.Status == "Active" && EF.Constant(assignedBatchIds).Contains(e.BatchId))
                    .Select(e => e.StudentID)
                    .ToListAsync()
                : new List<int>();

            var studentIds = directStudentIds.Union(batchStudentIds).Distinct().ToList();

            var students = await _context.Students
                .AsNoTracking()
                .Where(s => EF.Constant(studentIds).Contains(s.StudentID))
                .OrderBy(s => s.FullName)
                .Select(s => new { s.StudentID, s.FullName })
                .ToListAsync();

            var attempts = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .Where(a => a.MinistrySimExamId == id && EF.Constant(studentIds).Contains(a.StudentId))
                .Select(a => new { a.StudentId, a.IsCompleted, a.TotalScorePercent })
                .ToListAsync();

            var vm = new MinistrySimExamSelectStudentVm
            {
                ExamId = id,
                Title = exam.Title,
                CourseName = exam.Course.Name,
                Students = students.Select(s =>
                {
                    var attempt = attempts.FirstOrDefault(a => a.StudentId == s.StudentID);
                    return new MinistrySimExamStudentResultRowVm
                    {
                        StudentId = s.StudentID,
                        FullName = s.FullName,
                        HasAttempt = attempt != null,
                        IsCompleted = attempt?.IsCompleted ?? false,
                        TotalScorePercent = attempt?.TotalScorePercent
                    };
                }).ToList()
            };

            return View(vm);
        }

        // Sprint 14 (MSE-H / H2): نتيجة طالب واحد محدد — قراءة فقط، عبر IMinistrySimExamResultService (H1)
        [HttpGet]
        public async Task<IActionResult> StudentResult(int id, int studentId)
        {
            var lookup = await _resultService.GetResultAsync(id, studentId);

            switch (lookup.Status)
            {
                case MinistrySimExamResultStatus.ExamNotFound:
                    return NotFound("❌ لم يتم العثور على هذا الاختبار.");

                case MinistrySimExamResultStatus.AttemptNotFound:
                    TempData["Error"] = "لا توجد محاولة مسجَّلة لهذا الطالب في هذا الاختبار.";
                    return RedirectToAction(nameof(SelectStudentForResult), new { id });

                case MinistrySimExamResultStatus.NotCompleted:
                    TempData["Error"] = "لم يُتم هذا الطالب مراحل الاختبار بعد.";
                    return RedirectToAction(nameof(SelectStudentForResult), new { id });

                default:
                    return View(lookup.Vm);
            }
        }

        // Sprint 14 (MSE-H / H2): مراجعة أسئلة/إجابات طالب محدد لمرحلة واحدة — قراءة فقط
        [HttpGet]
        public async Task<IActionResult> StudentQuestionReview(int id, int studentId, int stageNumber)
        {
            var lookup = await _resultService.GetQuestionReviewAsync(id, studentId, stageNumber);

            switch (lookup.Status)
            {
                case MinistrySimExamQuestionReviewStatus.StageNotFound:
                    return NotFound("❌ لم يتم العثور على مرحلة اختبار معمل القياس المطلوبة.");

                case MinistrySimExamQuestionReviewStatus.AttemptNotFound:
                    TempData["Error"] = "لا توجد محاولة مسجَّلة لهذا الطالب في هذا الاختبار.";
                    return RedirectToAction(nameof(SelectStudentForResult), new { id });

                case MinistrySimExamQuestionReviewStatus.NotCompleted:
                    TempData["Error"] = "لم يُتم هذا الطالب مراحل الاختبار بعد.";
                    return RedirectToAction(nameof(SelectStudentForResult), new { id });

                default:
                    return View(lookup.Vm);
            }
        }

        // Sprint 15 (MSE-I / I1+I2): لوحة تحليلات مجمّعة لدفعة كاملة — متوسطات عامة + قائمة طلاب بلا تفصيل مراحل بعد
        // (تفصيل المراحل الخمس مؤجَّل لـSprint 16 / I3-I4). مرتبطة من قسم "نتائج الطلاب" في Details.cshtml.
        // Sprint 18 (MSE-K / K1): استُخرج منطق الحساب بالكامل إلى IMinistrySimExamBatchAnalyticsService
        // بلا أي تغيير في السلوك، ليُعاد استخدامه من ParentReport عبر GetBatchAverageStatsAsync.
        [HttpGet]
        public async Task<IActionResult> BatchAnalytics(int examId, int batchId)
        {
            var lookup = await _batchAnalyticsService.GetBatchAnalyticsAsync(examId, batchId);

            switch (lookup.Status)
            {
                case MinistrySimExamBatchAnalyticsStatus.ExamNotFound:
                    return NotFound("❌ لم يتم العثور على اختبار معمل القياس المطلوب.");

                case MinistrySimExamBatchAnalyticsStatus.AssignmentNotFound:
                    return NotFound("❌ هذه الدفعة غير مُسنَد لها هذا الاختبار.");

                default:
                    return View(lookup.Vm);
            }
        }

        // Sprint 18 (MSE-K / K2): تقرير شامل موجَّه لولي الأمر — ملخص إحصائي فقط (بلا تفصيل سؤال بسؤال، قرار مُقفَل)
        // + مقارنة بمتوسط دفعة الطالب عبر GetBatchAverageStatsAsync (K1) بلا تكرار حساب BatchAnalytics.
        // Sprint 19 (MSE-K / K3+K4): + اتجاه الأداء عبر محاولات سابقة + ملاحظات المدرّس + رأس خطاب حقيقي (شعار/اسم/عنوان المعهد).
        [HttpGet]
        public async Task<IActionResult> ParentReport(int examId, int studentId, bool print = false)
        {
            var lookup = await _resultService.GetResultAsync(examId, studentId);

            switch (lookup.Status)
            {
                case MinistrySimExamResultStatus.ExamNotFound:
                    return NotFound("❌ لم يتم العثور على هذا الاختبار.");

                case MinistrySimExamResultStatus.AttemptNotFound:
                    TempData["Error"] = "لا توجد محاولة مسجَّلة لهذا الطالب في هذا الاختبار.";
                    return RedirectToAction(nameof(SelectStudentForResult), new { id = examId });

                case MinistrySimExamResultStatus.NotCompleted:
                    TempData["Error"] = "لم يُتم هذا الطالب مراحل الاختبار بعد.";
                    return RedirectToAction(nameof(SelectStudentForResult), new { id = examId });
            }

            var resultVm = lookup.Vm;

            var student = await _context.Students
                .AsNoTracking()
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("❌ لم يتم العثور على الطالب المطلوب.");

            var exam = await _context.MinistrySimExams
                .AsNoTracking()
                .Include(e => e.Course)
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (exam == null)
                return NotFound("❌ لم يتم العثور على اختبار معمل القياس المطلوب.");

            // بطاقة المقارنة: دفعة الطالب الفعلية ضمن دورة هذا الاختبار (بصرف النظر عن مسار الإسناد — دفعة أو فردي)
            var studentBatch = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == studentId && e.Status == "Active" && e.Batch.CourseId == exam.CourseId)
                .Select(e => new { e.BatchId, BatchName = e.Batch.Name })
                .FirstOrDefaultAsync();

            var batchStats = studentBatch != null
                ? await _batchAnalyticsService.GetBatchAverageStatsAsync(examId, studentBatch.BatchId)
                : new MinistrySimExamBatchAverageStatsVm();

            // المحاولة الحالية — نحتاج Id لحفظ/عرض RecommendationsNote (Sprint 19 / K3)
            var attempt = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .Where(a => a.MinistrySimExamId == examId && a.StudentId == studentId)
                .Select(a => new { a.Id, a.RecommendationsNote })
                .FirstOrDefaultAsync();

            // اتجاه الأداء عبر كل محاولات هذا الطالب المكتملة في اختبارات محاكاة سابقة (Sprint 19 / K3)
            var trend = await _context.MinistrySimExamStudentAttempts
                .AsNoTracking()
                .Where(a => a.StudentId == studentId && a.IsCompleted && a.CompletedAt != null)
                .OrderBy(a => a.CompletedAt)
                .Select(a => new MinistrySimExamParentReportTrendPointVm
                {
                    ExamTitle = a.MinistrySimExam.Title,
                    CompletedAt = a.CompletedAt.Value,
                    TotalScorePercent = a.TotalScorePercent ?? 0
                })
                .ToListAsync();

            // رأس الخطاب — Sprint 19 (K4): شعار/اسم/عنوان المعهد الفعليين بدل Placeholder ثابت
            var siteLogoPath = await _settingService.GetAsync("SiteLogoPath");
            var instituteName = await _settingService.GetAsync("SiteName");
            var instituteAddress = await _settingService.GetAsync("InstituteAddress");

            var vm = new MinistrySimExamParentReportVm
            {
                InstituteName = string.IsNullOrWhiteSpace(instituteName) ? "معهد القدرات للتدريب" : instituteName,
                InstituteAddress = instituteAddress,
                SiteLogoUrl = string.IsNullOrWhiteSpace(siteLogoPath) ? Url.Content("~/assets/logo.png") : Url.Content(siteLogoPath),

                MinistrySimExamId = exam.Id,
                ExamTitle = exam.Title,
                CourseName = exam.Course.Name,

                StudentId = student.StudentID,
                StudentFullName = student.FullName,
                BatchName = studentBatch?.BatchName,

                ParentFullName = student.Parent?.FullName,
                ParentPhoneNumber = student.Parent?.PhoneNumber,
                ParentRelationToStudent = student.Parent?.RelationToStudent.ToString(),

                AttemptId = attempt?.Id ?? 0,
                AttemptCompletedAt = resultVm.CompletedAt,
                TotalScorePercent = resultVm.TotalScorePercent,
                QuantScorePercent = resultVm.QuantPercent,
                VerbalScorePercent = resultVm.VerbalPercent,
                Stages = resultVm.Stages.Select(s => new MinistrySimExamParentReportStageVm
                {
                    StageNumber = s.StageNumber,
                    StagePercentScore = s.StagePercentScore,
                    TimeExpired = s.TimeExpired,
                    DurationMinutes = s.DurationMinutes,
                    MotivationMessage = ResultToneHelper.Build(s.StagePercentScore, ResultContext.Exam).MotivationMsg
                }).ToList(),

                BatchTotalStudents = batchStats.TotalStudents,
                BatchAvgScorePercent = batchStats.AvgScorePercent,
                BatchAvgQuantPercent = batchStats.AvgQuantPercent,
                BatchAvgVerbalPercent = batchStats.AvgVerbalPercent,

                PreviousAttemptsTrend = trend,
                RecommendationsNote = attempt?.RecommendationsNote,
                IsPrintMode = print
            };

            // توصية محفِّزة عامة على مستوى الاختبار كامل — نفس نبرة "بلا لغة رسوب" المعتمدة بكل المنصة
            var overallTone = ResultToneHelper.Build(vm.TotalScorePercent, ResultContext.Exam);
            vm.OverallMotivationHeading = overallTone.MotivationHeading;
            vm.OverallMotivationMessage = overallTone.MotivationMsg;

            return View(vm);
        }

        // Sprint 19 (MSE-K / K3): حفظ ملاحظات/توصيات المدرّس على المحاولة قبل الطباعة (Ajax من شاشة ParentReport)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveParentReportNote(int attemptId, string note)
        {
            var attempt = await _context.MinistrySimExamStudentAttempts.FindAsync(attemptId);
            if (attempt == null)
                return NotFound();

            attempt.RecommendationsNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Sprint 19 (MSE-K / K5): تصدير تقرير ولي الأمر PDF — استنساخ حرفي لنمط
        // PlacementExamsController.DownloadReportChrome (ADR-MSE-5): يفتح Chrome بلا واجهة، يزور نفس شاشة
        // ParentReport (print=true) داخليًا، وينتظر اكتمال الشبكة قبل التصوير لضمان رسم شارتس Chart.js كاملة.
        [HttpGet]
        public async Task<IActionResult> ParentReportPdf(int examId, int studentId)
        {
            var url = Url.Action("ParentReport", "MinistrySimExam",
                new { area = "Admin", examId, studentId, print = true }, Request.Scheme);

            var executablePath = await _reportBrowserProvider.GetExecutablePathAsync(HttpContext.RequestAborted);

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                ExecutablePath = executablePath,
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            });

            var page = await browser.NewPageAsync();

            // تمرير كوكيز الجلسة الحالية للأدمن حتى لا يُعاد التوجيه لصفحة تسجيل الدخول
            // (الفرق الوحيد عن DownloadReportChrome: هذا الـController محمي بـ[Authorize] فعليًا)
            var cookies = Request.Cookies
                .Select(c => new CookieParam
                {
                    Name = c.Key,
                    Value = c.Value,
                    Url = url
                })
                .ToArray();
            if (cookies.Length > 0)
                await page.SetCookieAsync(cookies);

            // Puppeteer يفرض وسيط "print" افتراضيًا عند PdfDataAsync — هذا يُعيد رصّ الصفحة بقواعد
            // @media print (بعد أن يكون Chart.js قد رسم كل Canvas بالفعل على مقاسات "screen")، فتنكمش/تختفي
            // الشارتس بصريًا في الملف الناتج رغم ظهورها طبيعيًا في المتصفح. تثبيت "screen" من البداية يمنع
            // هذا التغيّر في التخطيط تمامًا؛ عناصر التحكم (أزرار الحفظ/التنزيل) مُخفاة أصلاً على مستوى الـView
            // نفسها في وضع print=true (Model.IsPrintMode) وليس عبر CSS، فلا داعي لقواعد الطباعة هنا.
            await page.EmulateMediaTypeAsync(MediaType.Screen);

            await page.GoToAsync(url, WaitUntilNavigation.Networkidle2);

            // انتظار إشارة صريحة من الصفحة أن كل شارتس Chart.js انتهت من الرسم فعليًا (الـAnimation مُعطَّل
            // في الصفحة نفسها، لكن الرسم الأول لا يزال يمر عبر requestAnimationFrame) — بدون هذا الانتظار
            // قد يلتقط Puppeteer لقطة الصفحة قبل اكتمال رسم أي Canvas فيظهر التقرير بلا شارتس في الـPDF.
            try
            {
                await page.WaitForExpressionAsync("window.__mseReportChartsReady === true", new WaitForFunctionOptions { Timeout = 5000 });
            }
            catch (WaitTaskTimeoutException)
            {
                _logger.LogWarning("ParentReportPdf: timed out waiting for charts to render (examId={ExamId}, studentId={StudentId})", examId, studentId);
            }

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new MarginOptions { Top = "10mm", Bottom = "10mm", Left = "10mm", Right = "10mm" }
            });

            var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentID == studentId);
            var fileName = $"تقرير_ولي_الأمر_{student?.FullName ?? studentId.ToString()}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
    }
}
