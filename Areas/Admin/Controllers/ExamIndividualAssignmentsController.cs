using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Admin;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Reports;
using QdratNew.ViewModels.Students;
using System.Security.Claims;
using System.Text.Json;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ExamIndividualAssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IExamRecommendationService _examRecommendationService;
        private readonly IExamResultEngine _examResultEngine;
        private readonly Services.Exams.Interfaces.IExamAssignmentIntegrityService _integrityService;
        private readonly IAdminActivityLogger _activityLogger;

        public ExamIndividualAssignmentsController(
            ApplicationDbContext context,
            IExamRecommendationService examRecommendationService,
            IExamResultEngine examResultEngine,
            Services.Exams.Interfaces.IExamAssignmentIntegrityService integrityService,
            IAdminActivityLogger activityLogger)
        {
            _context = context;
            _examRecommendationService = examRecommendationService;
            _examResultEngine = examResultEngine;
            _integrityService = integrityService;
            _activityLogger = activityLogger;

        }

        // Sprint 3 (EC3) — نفس نمط الوصول لهوية الأدمن الحالي المستخدم في كونترولرز أخرى
        private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        private string CurrentUserName() => User.Identity?.Name ?? "غير معروف";

        // ============================================================
        // GET: قائمة الاختبارات الفردية
        // ============================================================
        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Read")]
        public async Task<IActionResult> Index()
        {
            var data = await (
            from a in _context.ExamAssignmentsToStudents
            join ex in _context.Exams on a.ExamId equals ex.Id
            join s in _context.Students on a.StudentId equals s.StudentID

            join st in _context.ExamStudentStatuses
                on new { A = a.Id, S = s.StudentID }
                equals new { A = st.ExamAssignmentToStudentId.Value, S = st.StudentId }
                into statusJoin

            from status in statusJoin.DefaultIfEmpty()

            orderby a.CreatedAt descending

            select new IndividualExamListVm
            {
                AssignmentId = a.Id,
                ExamId = ex.Id,
                ExamTitle = ex.Title,
                StudentId = s.StudentID,
                StudentName = s.FullName,
                AssignedAt = a.ScheduledDate.Value,
                DueDate = a.EndAt,
                DurationMinutes = a.DurationMinutes,
                TotalQuestions = a.QuestionCount,

                // ⭐ الحالة الحقيقية
                IsSubmitted = status != null && status.IsSubmitted
            }
        ).ToListAsync();


            return View(data);
        }



        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Read")]
        public async Task<IActionResult> StudentIndividualExamPerformanceReport(
          int examAssignmentId,
          int studentId)
        {
            // ======================================================
            // 1️⃣ جلب التكليف الفردي
            // ======================================================
            var assignment = await _context.ExamAssignmentsToStudents
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x =>
                    x.Id == examAssignmentId &&
                    x.StudentId == studentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار الفردي.");

            // ======================================================
            // 2️⃣ الطالب
            // ======================================================
            var student = await _context.Students
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("❌ لم يتم العثور على الطالب.");

            // ======================================================
            // 3️⃣ اللقطة النهائية للنتيجة (نفس المصدر المستخدم في منطقة الطالب)
            //    ⚠️ لا يصح إعادة حساب النتيجة من ExamQuestions/QuestionAttemptNew
            //    مباشرة هنا لأن أسئلة الاختبار الفردي قد تُستبدل/تُعدَّل من
            //    شاشات الإدمن (ReplaceQuestion / EditStudentExam) بعد أن يكون
            //    الطالب قد أنهى الاختبار فعليًا، فتصبح المقارنة الحية خاطئة أو
            //    صفرية. اللقطة المحفوظة في ExamStudentStatuses.Note تم تجميدها
            //    وقت التسليم وهي المرجع الصحيح دائمًا.
            // ======================================================
            var status = await _context.ExamStudentStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentToStudentId == examAssignmentId);

            if (status == null)
                return NotFound("❌ لم يتم العثور على نتيجة لهذا الاختبار.");

            if (string.IsNullOrEmpty(status.Note))
            {
                await _examResultEngine.GenerateSnapshotIfMissingAsync(examAssignmentId, studentId);

                status = await _context.ExamStudentStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.StudentId == studentId &&
                        s.ExamAssignmentToStudentId == examAssignmentId);
            }

            if (status == null || string.IsNullOrEmpty(status.Note))
                return NotFound("❌ تعذر إنشاء تقرير لهذا الاختبار.");

            var snapshot = JsonSerializer.Deserialize<ExamFinalResultDto>(status.Note);

            if (snapshot == null)
                return NotFound("❌ بيانات التقرير غير صالحة.");

            // ======================================================
            // 4️⃣ الزمن
            // ======================================================
            double totalMinutes = assignment.DurationMinutes;
            double solveMinutes = snapshot.TotalTimeSeconds > 0
                ? Math.Round(snapshot.TotalTimeSeconds / 60.0, 1)
                : 0;

            // ======================================================
            // 5️⃣ التوصيات
            // ======================================================
            var rec = _examRecommendationService.GetRecommendation(
                snapshot.ScorePercent,
                (int)solveMinutes,
                totalMinutes,
                assignment.Exam?.Type
            );

            // ======================================================
            // 6️⃣ تحليل المحاور (من نفس اللقطة المجمَّدة)
            // ======================================================
            var sectionIds = snapshot.Sections.Keys.ToList();

            var sectionTitles = await _context.Sections
                .Where(s => sectionIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Title })
                .ToDictionaryAsync(x => x.Id, x => x.Title);

            var sectionStats = snapshot.Sections.Select(kv => new QdratNew.ViewModels.Reports.SectionPerformancesVm
            {
                SectionId = kv.Key,
                SectionTitle = sectionTitles.TryGetValue(kv.Key, out var title) ? title : "محور غير معروف",
                Total = kv.Value.Correct + kv.Value.Wrong + kv.Value.Skipped,
                Correct = kv.Value.Correct,
                Wrong = kv.Value.Wrong,
                Skipped = kv.Value.Skipped
            }).ToList();

            // ======================================================
            // 7️⃣ ViewModel
            // ======================================================
            var vm = new QdratNew.ViewModels.Reports.StudentExamPerformanceReportVm
            {
                StudentName = student.FullName,
                ExamTitle = assignment.Exam.Title,
                ExamDate = assignment.ScheduledDate.Value,

                TotalQuestions = snapshot.TotalQuestions,
                CorrectAnswers = snapshot.Correct,
                WrongAnswers = snapshot.Wrong,
                SkippedQuestions = snapshot.Skipped,
                OverallPercent = snapshot.ScorePercent,

                SolveMinutes = solveMinutes,
                DurationMinutes = assignment.DurationMinutes,

                SpeedLabel = rec.SpeedLabel,
                SpeedNote = rec.SpeedNote,
                TrackCard1 = rec.TrackCard1,
                TrackCard1Desc = rec.TrackCard1Desc,
                TrackCard2 = rec.TrackCard2,
                TrackCard2Desc = rec.TrackCard2Desc,
                IndividualTips = rec.IndividualTips,

                ExamAssignmentId = examAssignmentId,
                StudentId = studentId,
                Sections = sectionStats
            };

            return View(
                "~/Areas/Admin/Views/ExamIndividualAssignments/StudentExamPerformanceReport.cshtml",
                vm
            );
        }


        // ============================================================
        // عرض أسئلة محور معين - اختبار فردي (مع إجابة الطالب)
        // ============================================================
        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Read")]
        public async Task<IActionResult> SectionQuestionsIndividual(
            int examAssignmentId,
            int studentId,
            int sectionId)
        {
            // 1️⃣ تأكد من وجود الطالب
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("❌ لم يتم العثور على الطالب.");

            // 2️⃣ تأكد أن الاختبار مخصص لهذا الطالب
            var assignment = await _context.ExamAssignmentsToStudents
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a =>
                    a.Id == examAssignmentId &&
                    a.StudentId == student.StudentID);

            if (assignment == null)
                return NotFound("❌ هذا الاختبار غير مخصص لهذا الطالب.");

            // 3️⃣ جلب الأسئلة الخاصة بهذا المحور داخل هذا الاختبار الفردي
            var questions = await (
                from eq in _context.ExamQuestions
                join q in _context.Questions on eq.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                where eq.ExamAssignmentToStudentId == examAssignmentId
                      && l.SectionId == sectionId
                select q
            )
            .Include(q => q.Options)
            .Include(q => q.Lesson)
                .ThenInclude(l => l.Section)
            .Include(q => q.VerbalPassage)
            .ToListAsync();

            if (!questions.Any())
                return NotFound("⚠️ لا توجد أسئلة في هذا المحور.");

            // 4️⃣ جلب محاولات الطالب الخاصة بهذا الاختبار فقط
            var attempts = await _context.QuestionAttemptNew
                .Where(a =>
                    a.StudentId == student.StudentID &&
                    a.ExamAssignmentToStudentId == examAssignmentId)
                .ToListAsync();

            // 5️⃣ استخراج آخر محاولة لكل سؤال
            var lastAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToDictionary(x => x.QuestionId, x => x);

            // 6️⃣ بناء الموديل النهائي
            var result = questions.Select(q =>
            {
                lastAttempts.TryGetValue(q.Id, out var attempt);

                var display = q.ToDisplayModel();

                return new QdratNew.ViewModels.Reports.AdminSectionQuestionVm
                {
                    SectionTitle = display.SectionTitle,
                    LessonTitle = display.LessonTitle,
                    QuestionTitle = display.Title,
                    ImageUrl = display.ImageUrl,
                    IsQuantitative = display.IsQuantitative,
                    DisplayType = display.DisplayType,
                    ComparisonValue1 = display.ComparisonValue1,
                    ComparisonValue2 = display.ComparisonValue2,
                    VerbalPassageContent = display.VerbalPassageContent,

                    CorrectAnswer = q.CorrectAnswer,
                    StudentAnswer = attempt?.SelectedAnswer,

                    Options = q.Options
                        .Select(o => new QdratNew.ViewModels.Homework.QuestionOptionVm
                        {
                            Text = o.Text,
                            ImageUrl = o.ImageUrl
                        })
                        .ToList()
                };
            }).ToList();

            ViewBag.ExamAssignmentId = examAssignmentId;
            ViewBag.StudentId = studentId;

            return View(
                "~/Areas/Admin/Views/ExamIndividualAssignments/SectionQuestions.cshtml",
                result
            );
        }

        // ============================================================
        // مؤشرات المحور - اختبار فردي
        // ============================================================
        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Read")]
        public async Task<IActionResult> SectionLessonsIndividual(
            int examAssignmentId,
            int studentId,
            int sectionId)
        {
            var section = await _context.Sections
                .Include(s => s.Curriculum)
                .FirstOrDefaultAsync(s => s.Id == sectionId);

            if (section == null)
                return NotFound("❌ لم يتم العثور على المحور.");

            var assignment = await _context.ExamAssignmentsToStudents
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a =>
                    a.Id == examAssignmentId &&
                    a.StudentId == studentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار الفردي.");

            // 🟢 جلب المحاولات الخاصة بهذا الاختبار الفردي فقط
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                .Where(a =>
                    a.StudentId == studentId &&
                    a.ExamAssignmentToStudentId == examAssignmentId &&
                    a.Question.Lesson.SectionId == sectionId)
                .ToListAsync();

            if (!attempts.Any())
                return NotFound("⚠️ لا توجد بيانات لهذا المحور.");

            // 🟢 أخذ آخر محاولة لكل سؤال
            var lastAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(x => x.AttemptedAt).First())
                .ToList();

            // 🟢 التجميع حسب الدرس
            var lessons = lastAttempts
                .GroupBy(a => a.Question.Lesson)
                .Select(g =>
                {
                    int total = g.Count();
                    int correct = g.Count(x => x.IsCorrect);
                    double successRate = total == 0 ? 0 : (double)correct / total * 100;

                    return new QdratNew.ViewModels.Exam.LessonPerformanceVm
                    {
                        LessonId = g.Key.Id,
                        LessonTitle = g.Key.Title,
                        QuestionCount = total,
                        AvgSuccessRate = Math.Round(successRate, 1),
                        AvgTimeSeconds = Math.Round(g.Average(x => x.TimeTakenSeconds), 1),
                        HardnessIndex = Math.Round(100 - successRate, 1)
                    };
                })
                .ToList();

            var model = new QdratNew.ViewModels.Exam.SectionLessonsViewModel
            {
                AssignmentId = assignment.Id,
                SectionId = section.Id,

                SectionTitle = section.Title,
                Lessons = lessons
            };

            return View(
                "~/Areas/Admin/Views/ExamIndividualAssignments/SectionLessons.cshtml",
                model
            );
        }


        // ============================================================
        // مراجعة أسئلة مؤشر - اختبار فردي
        // ============================================================
        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Read")]
        public async Task<IActionResult> LessonQuestionsDetailIndividual(
            int examAssignmentId,
            int studentId,
            int lessonId)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return NotFound("❌ لم يتم العثور على المؤشر.");

            var assignment = await _context.ExamAssignmentsToStudents
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a =>
                    a.Id == examAssignmentId &&
                    a.StudentId == studentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار الفردي.");

            // 🟢 جلب الأسئلة الخاصة بالمؤشر داخل هذا الاختبار الفردي
            var questions = await (
                from eq in _context.ExamQuestions
                join q in _context.Questions on eq.QuestionId equals q.Id
                where eq.ExamAssignmentToStudentId == examAssignmentId
                      && q.LessonId == lessonId
                select new
                {
                    q.Id,
                    q.Title,
                    q.ImageUrl,
                    q.IsQuantitative,
                    q.CorrectAnswer
                }
            ).ToListAsync();

            if (!questions.Any())
            {
                return Content(
                    "<div class='alert alert-warning text-center'>⚠️ لا توجد أسئلة مرتبطة بهذا المؤشر في هذا الاختبار.</div>",
                    "text/html");
            }

            // 🟢 محاولات الطالب الخاصة بهذا الاختبار فقط
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                where a.StudentId == studentId
                      && a.ExamAssignmentToStudentId == examAssignmentId
                      && q.LessonId == lessonId
                select new
                {
                    a.QuestionId,
                    a.SelectedAnswer,
                    a.IsCorrect,
                    a.TimeTakenSeconds,
                    a.AttemptedAt
                }
            ).ToListAsync();

            // 🟢 آخر محاولة لكل سؤال
            var latestAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).First())
                .ToList();

            // 🟢 حساب الوقت الكلي
            var totalTimeSeconds = latestAttempts.Sum(a => a.TimeTakenSeconds);
            var totalMinutes = totalTimeSeconds / 60;
            var remainingSeconds = totalTimeSeconds % 60;
            var formattedTime = $"{totalMinutes} دقيقة {remainingSeconds} ثانية";

            // 🟢 جلب الخيارات الخاصة بأسئلة هذا الاختبار فقط
            var optionsRaw = await (
                from o in _context.QuestionOptions
                join eq in _context.ExamQuestions on o.QuestionId equals eq.QuestionId
                join q in _context.Questions on o.QuestionId equals q.Id
                where eq.ExamAssignmentToStudentId == examAssignmentId
                      && q.LessonId == lessonId
                select new
                {
                    o.QuestionId,
                    o.Text,
                    o.ImageUrl
                }
            ).ToListAsync();

            // 🧠 بناء ViewModel
            var questionVms = questions.Select(q =>
            {
                var attempt = latestAttempts.FirstOrDefault(a => a.QuestionId == q.Id);

                var opts = optionsRaw
                    .Where(o => o.QuestionId == q.Id)
                    .Select(o => new QdratNew.ViewModels.Homework.QuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList();

                return new QdratNew.ViewModels.Exam.ExamReviewQuestionVm
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,
                    CorrectAnswer = q.CorrectAnswer,
                    StudentAnswer = attempt?.SelectedAnswer,
                    IsCorrect = attempt?.IsCorrect ?? false,
                    TimeTakenSeconds = attempt?.TimeTakenSeconds ?? 0,
                    IsQuantitative = q.IsQuantitative,
                    Options = opts
                };
            }).ToList();

            var vm = new QdratNew.ViewModels.Exam.ExamReviewViewModel
            {
                ExamAssignmentId = examAssignmentId,
                ExamTitle = $"مراجعة أسئلة المؤشر: {lesson.Title}",
                TotalQuestions = questionVms.Count,

                TimeSpentFormatted = formattedTime,
                TimeSpentMinutes = totalMinutes,
                Questions = questionVms
            };

            return View(
                "~/Areas/Admin/Views/ExamAssignments/LessonQuestionsDetail.cshtml",
                vm);
        }

        // ============================================================
        // GET: Create Student Exam
        // ============================================================
        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Create")]
        public async Task<IActionResult> CreateStudentExam(
         int? branchId,
         int? batchId,
         int? studentId)
        {
            var vm = new CreateStudentExamVm
            {
                BranchId = branchId,
                BatchId = batchId,
                StudentId = studentId ?? 0,

                Branches = await _context.Branches
                    .OrderBy(b => b.Name)
                    .Select(b => new BranchDropdownVm
                    {
                        Id = b.Id,
                        Name = b.Name
                    })
                    .ToListAsync(),

                Curriculums = await _context.Curriculums
                    .Select(c => new CurriculumDropdownVm
                    {
                        Id = c.Id,
                        Title = c.Title
                    })
                    .ToListAsync(),

                ProfessionalModels = await _context.ProfessionalModels
                    .Where(m => !m.IsArchived)
                    .OrderBy(m => m.Title)
                    .Select(m => new ProfessionalModelDropdownVm
                    {
                        Id = m.Id,
                        Title = m.Title
                    })
                    .ToListAsync()
            };

            // 🔴 إذا تم تمرير الفرع → حمّل الدفعات
            if (branchId.HasValue)
            {
                vm.Batches = await _context.Batches
                    .Where(b => b.BranchId == branchId.Value && !b.IsDeleted && !b.IsArchived)
                    .OrderBy(b => b.Name)
                    .Select(b => new BatchDropdownVm
                    {
                        Id = b.Id,
                        Name = b.Name
                    })
                    .ToListAsync();
            }

            // 🔴 إذا تم تمرير الدفعة → حمّل الطلاب
            if (batchId.HasValue)
            {
                vm.Students = await (
                    from s in _context.Students
                    join e in _context.StudentBatchEnrollments
                        on s.StudentID equals e.StudentID
                    where e.BatchId == batchId.Value
                    orderby s.FullName
                    select new StudentDropdownVm
                    {
                        Id = s.StudentID,
                        Name = s.FullName
                    }
                ).ToListAsync();
            }

            return View(vm);
        }


        // ============================================================
        // POST: Create Student Exam
        // ============================================================
        [HttpPost]
        [AdminPermission("ExamIndividualAssignments", "Create")]
        public async Task<IActionResult> CreateStudentExam(CreateStudentExamVm vm)
        {
            try
            {
                // ==============================
                // 0) Model Validation
                // ==============================
                if (!ModelState.IsValid)
                {
                    await PopulateCreateStudentExamDropdowns(vm);
                    return View(vm);
                }

                if (vm.StudentId <= 0)
                {
                    TempData["Error"] = "يجب اختيار الطالب.";
                    await PopulateCreateStudentExamDropdowns(vm);
                    return View(vm);
                }

                // ==============================
                // 1) تحقق أن الطالب داخل الدفعة
                // ==============================
                var isStudentInBatch = await (
                    from be in _context.StudentBatchEnrollments
                    join b in _context.Batches
                        on be.BatchId equals b.Id
                    where be.StudentID == vm.StudentId
                          && be.BatchId == vm.BatchId
                          && !b.IsDeleted
                          && !b.IsArchived
                    select be
                ).AnyAsync();

                if (!isStudentInBatch)
                {
                    TempData["Error"] = "الطالب غير مسجل في هذه الدفعة.";
                    await PopulateCreateStudentExamDropdowns(vm);
                    return View(vm);
                }

                // ==============================
                // 2) تحديد الرقم المرجعي
                // ==============================
                string? referenceCode = null;

                if (!vm.IsOnline)
                {
                    if (!string.IsNullOrWhiteSpace(vm.ReferenceCode))
                        referenceCode = vm.ReferenceCode.Trim();
                    else
                        referenceCode = new Random().Next(100000, 999999).ToString(); // 6 أرقام
                }

                // ==============================
                // 3) إنشاء Exam
                // ==============================
                var exam = new Exam
                {
                    Title = string.IsNullOrWhiteSpace(vm.Title)
                        ? $"اختبار فردي للطالب {vm.StudentId}"
                        : vm.Title,

                    DurationMinutes = vm.DurationMinutes,
                    CurriculumId = vm.CurriculumId,
                    SectionId = null,
                    TotalQuestions = 0,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    ReferenceCode = referenceCode,
                    IsFromProfessionalModel = vm.UseProfessionalModel
                };

                _context.Exams.Add(exam);
                await _context.SaveChangesAsync();

                // ==============================
                // 4) إنشاء ExamAssignmentToStudent
                // ==============================
                var assignment = new ExamAssignmentToStudent
                {
                    ExamId = exam.Id,
                    StudentId = vm.StudentId,
                    ScheduledDate = vm.ScheduledDate!.Value,
                    EndAt = vm.EndAt!.Value,
                    DurationMinutes = vm.DurationMinutes,
                    CreatedAt = DateTime.Now,
                    IsOnline = vm.IsOnline,
                    IsInLab = !vm.IsOnline
                };

                _context.ExamAssignmentsToStudents.Add(assignment);
                await _context.SaveChangesAsync();

                // ==============================
                // 5) اختيار الأسئلة
                // ==============================
                List<Question> selectedQuestions = new();

                // -------- نموذج احترافي --------
                if (vm.UseProfessionalModel && vm.ProfessionalModelId.HasValue)
                {
                    var modelIsAvailable = await _context.ProfessionalModels
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == vm.ProfessionalModelId.Value && !m.IsArchived);

                    if (!modelIsAvailable)
                    {
                        TempData["Error"] = "النموذج الاحترافي المحدد غير متاح أو مؤرشف.";
                        await PopulateCreateStudentExamDropdowns(vm);
                        return View(vm);
                    }

                    selectedQuestions = await _context.ProfessionalModelQuestions
                        .Where(x => x.ModelId == vm.ProfessionalModelId.Value)
                        .OrderBy(x => x.OrderNumber)
                        .Select(x => x.Question)
                        .ToListAsync();

                    if (!selectedQuestions.Any())
                    {
                        TempData["Error"] = "النموذج الاحترافي لا يحتوي أسئلة.";
                        await PopulateCreateStudentExamDropdowns(vm);
                        return View(vm);
                    }
                }

                // -------- توليد تلقائي حسب المؤشرات --------
                if (vm.UseAutoGeneration && vm.Sections != null)
                {
                    foreach (var sec in vm.Sections)
                    {
                        if (sec.Lessons == null) continue;

                        foreach (var lesson in sec.Lessons)
                        {
                            if (lesson.QuestionCount <= 0) continue;

                            var lessonQuestions = await _context.Questions
                                .Where(q => q.LessonId == lesson.LessonId)
                                .ToListAsync();

                            if (lessonQuestions.Count < lesson.QuestionCount)
                            {
                                TempData["Error"] =
                                    $"عدد الأسئلة المطلوبة في مؤشر '{lesson.LessonTitle}' أكبر من المتاح.";
                                await PopulateCreateStudentExamDropdowns(vm);
                                return View(vm);
                            }

                            var selectedFromLesson = lessonQuestions
                                .OrderBy(x => Guid.NewGuid())
                                .Take(lesson.QuestionCount)
                                .ToList();

                            selectedQuestions.AddRange(selectedFromLesson);
                        }
                    }
                }

                if (!selectedQuestions.Any())
                {
                    TempData["Error"] = "لم يتم العثور على أسئلة لتوليد الاختبار.";
                    await PopulateCreateStudentExamDropdowns(vm);
                    return View(vm);
                }

                // ==============================
                // منع التكرار
                // ==============================
                selectedQuestions = selectedQuestions
                    .GroupBy(q => q.Id)
                    .Select(g => g.First())
                    .ToList();

                // ==============================
                // 6) تحديث العدد
                // ==============================
                exam.TotalQuestions = selectedQuestions.Count;
                await _context.SaveChangesAsync();

                // ==============================
                // 7) ربط الأسئلة
                // ==============================
                int order = 1;

                foreach (var q in selectedQuestions)
                {
                    _context.ExamQuestions.Add(new ExamQuestion
                    {
                        ExamId = exam.Id,
                        QuestionId = q.Id,
                        Order = order++,
                        ExamAssignmentToStudentId = assignment.Id
                    });
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "تم إنشاء الاختبار الفردي بنجاح";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"حدث خطأ: {ex.Message}";
                await PopulateCreateStudentExamDropdowns(vm);
                return View(vm);
            }
        }
        // ============================================================
        // Helper: Populate Dropdowns
        // ============================================================
        private async Task PopulateCreateStudentExamDropdowns(CreateStudentExamVm vm)
        {
            vm.Branches = await _context.Branches
                .OrderBy(b => b.Name)
                .Select(b => new BranchDropdownVm
                {
                    Id = b.Id,
                    Name = b.Name
                })
                .ToListAsync();

            vm.Curriculums = await _context.Curriculums
                .Select(c => new CurriculumDropdownVm
                {
                    Id = c.Id,
                    Title = c.Title
                })
                .ToListAsync();

            vm.ProfessionalModels = await _context.ProfessionalModels
                .Where(m => !m.IsArchived)
                .OrderBy(m => m.Title)
                .Select(m => new ProfessionalModelDropdownVm
                {
                    Id = m.Id,
                    Title = m.Title
                })
                .ToListAsync();

            if (vm.BranchId.HasValue)
            {
                vm.Batches = await _context.Batches
                    .Where(b => b.BranchId == vm.BranchId.Value && !b.IsDeleted && !b.IsArchived)
                    .OrderBy(b => b.Name)
                    .Select(b => new BatchDropdownVm
                    {
                        Id = b.Id,
                        Name = b.Name
                    })
                    .ToListAsync();
            }

            if (vm.BatchId.HasValue)
            {
                vm.Students = await (
                    from s in _context.Students
                    join e in _context.StudentBatchEnrollments
                        on s.StudentID equals e.StudentID
                    join b in _context.Batches
                        on e.BatchId equals b.Id
                    where e.BatchId == vm.BatchId.Value
                          && !b.IsDeleted
                          && !b.IsArchived
                    orderby s.FullName
                    select new StudentDropdownVm
                    {
                        Id = s.StudentID,
                        Name = s.FullName
                    }
                ).ToListAsync();
            }
        }




        // ============================================================
        // AJAX: جلب الدفعات حسب الفرع
        // ============================================================
        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Read")]
        public async Task<IActionResult> GetBatchesByBranch(int branchId)
        {
            var batches = await _context.Batches
                .Where(b => b.BranchId == branchId && !b.IsDeleted && !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new
                {
                    id = b.Id,
                    name = b.Name
                })
                .ToListAsync();

            return Json(batches);
        }

        // ============================================================
        // AJAX: جلب الطلاب حسب الدفعة (عبر جدول الربط)
        // ============================================================
        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Read")]
        public async Task<IActionResult> GetStudentsByBatch(int batchId)
        {
            var students = await (
                from s in _context.Students
                join be in _context.StudentBatchEnrollments
                    on s.StudentID equals be.StudentID
                join b in _context.Batches
                    on be.BatchId equals b.Id
                where be.BatchId == batchId
                      && !b.IsDeleted
                      && !b.IsArchived
                orderby s.FullName
                select new
                {
                    id = s.StudentID,
                    name = s.FullName
                }
            ).ToListAsync();

            return Json(students);
        }

        // ============================================================
        // AJAX: جلب المؤشرات الفعالة داخل محور
        // ============================================================
        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Read")]
        public async Task<IActionResult> GetActiveLessonsBySection(int sectionId)
        {
            var lessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .OrderBy(l => l.Title)
                .Select(l => new
                {
                    id = l.Id,
                    title = l.Title
                })
                .ToListAsync();

            return Json(lessons);
        }










        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Details")]
        public async Task<IActionResult> Details(int id)
        {
            var assignment = await _context.ExamAssignmentsToStudents
                .Include(a => a.Exam)
                .Include(a => a.Student)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound();

            var examQuestions = await _context.ExamQuestions
                .Where(q => q.ExamId == assignment.ExamId)
                .OrderBy(q => q.Order)
                .Include(q => q.Question)
                .ToListAsync();

            var vm = new IndividualExamDetailsVm
            {
                AssignmentId = assignment.Id,
                ExamId = assignment.ExamId,
                ExamTitle = assignment.Exam.Title,
                ReferenceCode = assignment.Exam.ReferenceCode,
                StudentId = assignment.StudentId,
                StudentName = assignment.Student.FullName,
                ScheduledDate = assignment.ScheduledDate.Value,
                EndAt = assignment.EndAt.Value,
                DurationMinutes = assignment.DurationMinutes,
                CurriculumTitle = assignment.Exam.Curriculum?.Title,
                SectionTitle = assignment.Exam.Section?.Title,
                Questions = examQuestions.Select(x => new QuestionRowVm
                {
                    QuestionId = x.QuestionId,
                    Title = x.Question.Title,
                    Difficulty = x.Question.Difficulty.ToString(),
                    Order = x.Order,
                    SectionId = x.Question.SectionId ?? 0,

                    // 🟦 المحور
                    SectionTitle = x.Question.Section != null
         ? x.Question.Section.Title
         : "—",

                    // 🟦 Internal Note
                    InternalNote = !string.IsNullOrWhiteSpace(x.Question.InternalNote)
                                 ? x.Question.InternalNote
                                 : "—"

                }).ToList()

            };

            return View(vm);
        }

        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Edit")]
        public async Task<IActionResult> ReplaceQuestionView(int assignmentId, Guid questionId)
        {
            // 🟦 1) جلب السؤال الحالي مع كامل العلاقات
            var oldQ = await _context.Questions
                .Include(q => q.Section)
                .Include(q => q.Lesson)
                .Include(q => q.Curriculum)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (oldQ == null)
                return NotFound("❌ لم يتم العثور على السؤال المراد استبداله.");

            // 🟧 2) جلب أسئلة بديلة من نفس القسم + نفس المنهج + نفس الدرس (إن وجد)
            var alternativesQuery = _context.Questions
                .Include(q => q.Section)
                .Include(q => q.Lesson)
                .Include(q => q.Curriculum)
                .Where(q =>
                       q.Id != questionId &&                    // استبعاد السؤال الحالي
                       q.SectionId == oldQ.SectionId &&         // نفس المحور
                       q.CurriculumId == oldQ.CurriculumId      // نفس المنهج
                );

            // 🟨 لو السؤال مرتبط بدرس محدد → اجلب أسئلة من نفس الدرس فقط
            if (oldQ.LessonId != null)
                alternativesQuery = alternativesQuery.Where(q => q.LessonId == oldQ.LessonId);

            // 🟩 3) ترتيب عشوائي + تحديد العدد
            var alternatives = await alternativesQuery
                .OrderBy(r => Guid.NewGuid())
                .Take(100)
                .ToListAsync();

            // 🟦 4) تمرير السؤال القديم + AssignmentId للفيو
            ViewBag.OldQuestion = oldQ;
            ViewBag.AssignmentId = assignmentId;

            return View(alternatives);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamIndividualAssignments", "Edit")]
        public async Task<IActionResult> ReplaceQuestion(
            int assignmentId, Guid oldQuestionId, Guid newQuestionId,
            bool confirmReset = false, string? reason = null)
        {
            var examId = await _context.ExamAssignmentsToStudents
                .Where(a => a.Id == assignmentId)
                .Select(a => a.ExamId)
                .FirstOrDefaultAsync();

            if (examId == 0)
            {
                TempData["Error"] = "لم يتم العثور على الاختبار.";
                return RedirectToAction("Details", new { id = assignmentId });
            }

            // ==============================
            // حراسة: لا يجوز استبدال سؤال له محاولة إجابة سابقة دون تأكيد + سبب (Sprint 2 — EB4)
            // ==============================
            var attemptsOnQuestion = await _context.QuestionAttemptNew.CountAsync(a =>
                a.QuestionId == oldQuestionId && a.ExamAssignmentToStudentId == assignmentId);

            if (attemptsOnQuestion > 0 && !confirmReset)
            {
                TempData["Warning"] =
                    $"⚠️ هذا السؤال له {attemptsOnQuestion} محاولة إجابة مسجّلة للطالب/ة. " +
                    "الاستبدال سيمسح هذه المحاولة ويصفّر النتيجة المحفوظة لإعادة حسابها. " +
                    "لو متأكد، فعّل \"تأكيد الاستبدال\" واكتب السبب ثم أعد الاستبدال.";
                return RedirectToAction("ReplaceQuestionView", new { assignmentId, questionId = oldQuestionId });
            }

            if (attemptsOnQuestion > 0 && confirmReset)
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    TempData["Error"] = "سبب الاستبدال إجباري عند وجود محاولة إجابة سابقة على هذا السؤال.";
                    return RedirectToAction("ReplaceQuestionView", new { assignmentId, questionId = oldQuestionId });
                }

                await _integrityService.ResetSingleQuestionAttemptAsync(
                    oldQuestionId, examAssignmentId: null, examAssignmentToStudentId: assignmentId);
            }

            // حذف السؤال القديم
            var oldLink = await _context.ExamQuestions
                .FirstOrDefaultAsync(q => q.ExamId == examId && q.QuestionId == oldQuestionId);

            if (oldLink != null)
                _context.ExamQuestions.Remove(oldLink);

            // إضافة السؤال الجديد بنفس الترتيب
            _context.ExamQuestions.Add(new ExamQuestion
            {
                ExamId = examId,
                QuestionId = newQuestionId,
                Order = oldLink?.Order ?? 1,
                ExamAssignmentToStudentId = assignmentId
            });

            await _context.SaveChangesAsync();

            // Sprint 3 (EC3) — تسجيل التعديل في AdminActivityLog: من/ليه/الأثر
            var studentIdForLog = await _context.ExamAssignmentsToStudents
                .Where(a => a.Id == assignmentId)
                .Select(a => (int?)a.StudentId)
                .FirstOrDefaultAsync();

            await _activityLogger.LogExamQuestionsChangeAsync(
                actionType: "استبدال سؤال في اختبار فردي",
                reason: reason ?? "بدون محاولات سابقة — لا يتطلب سببًا",
                CurrentUserId(), CurrentUserName(),
                examAssignmentId: null, examAssignmentToStudentId: assignmentId,
                studentId: studentIdForLog,
                questionsBeforeCount: 1, questionsAfterCount: 1,
                affectedAttemptsCount: attemptsOnQuestion);

            TempData["Success"] = "✔ تم استبدال السؤال بنجاح";
            return RedirectToAction("Details", new { id = assignmentId });
        }

        [HttpGet]
        [AdminPermission("ExamIndividualAssignments", "Edit")]
        public async Task<IActionResult> EditStudentExam(int assignmentId)
        {
            var assignment = await _context.ExamAssignmentsToStudents
                .Include(a => a.Student)
                .Include(a => a.Exam)
                    .ThenInclude(e => e.Curriculum)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null)
                return NotFound();

            var vm = new EditStudentExamVm
            {
                AssignmentId = assignment.Id,
                ExamId = assignment.ExamId,
                Title = assignment.Exam.Title,
                ReferenceCode = assignment.Exam.ReferenceCode,

                StudentId = assignment.StudentId,
                StudentName = assignment.Student.FullName,

                // نوع الاختبار
                IsOnline = assignment.IsOnline,
                IsInLab = assignment.IsInLab,

                UseProfessionalModel = assignment.Exam.IsFromProfessionalModel,
                UseAutoGeneration = !assignment.Exam.IsFromProfessionalModel,

                CurriculumId = assignment.Exam.CurriculumId,

                ScheduledDate = assignment.ScheduledDate.Value,
                EndAt = assignment.EndAt.Value,
                DurationMinutes = assignment.DurationMinutes
            };

            // الأسئلة الحالية
            vm.Questions = await _context.ExamQuestions
                .Where(q => q.ExamId == assignment.ExamId && q.ExamAssignmentToStudentId == assignmentId)
                .OrderBy(q => q.Order)
                .Select(q => new EditQuestionVm
                {
                    QuestionId = q.QuestionId,
                    Title = q.Question.Title,
                    Difficulty = q.Question.Difficulty.ToString(),
                    Order = q.Order,
                    SectionId = q.Question.SectionId ?? 0
                })
                .ToListAsync();

            // المحاور الخاصة بالمنهج
            if (vm.CurriculumId.HasValue)
            {
                var sections = await _context.Sections
                    .Where(s => s.CurriculumId == vm.CurriculumId.Value)
                    .ToListAsync();

                vm.Sections = sections
                    .Select(sec => new EditSectionVm
                    {
                        SectionId = sec.Id,
                        SectionTitle = sec.Title,
                        RequestedCount = vm.Questions.Count(q => q.SectionId == sec.Id)
                    })
                    .ToList();
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamIndividualAssignments", "Edit")]
        public async Task<IActionResult> EditStudentExam(EditStudentExamVm vm)
        {
            try
            {
                var exam = await _context.Exams.FindAsync(vm.ExamId);
                var assignment = await _context.ExamAssignmentsToStudents.FindAsync(vm.AssignmentId);

                if (exam == null || assignment == null)
                {
                    TempData["Error"] = "لم يتم العثور على بيانات الاختبار.";
                    return View(vm);
                }

                // ==============================
                // 1) تحديث البيانات الأساسية
                // ==============================
                exam.Title = vm.Title;
                exam.CurriculumId = vm.CurriculumId;
                exam.IsFromProfessionalModel = vm.UseProfessionalModel;

                assignment.ScheduledDate = vm.ScheduledDate;
                assignment.EndAt = vm.EndAt;
                assignment.DurationMinutes = vm.DurationMinutes;

                // ==============================
                // 2) نوع الاختبار (Online / حضوري)
                // ==============================
                assignment.IsOnline = vm.IsOnline;
                assignment.IsInLab = !vm.IsOnline;

                // ==============================
                // 3) الرقم المرجعي
                // ==============================
                if (!vm.IsOnline)
                {
                    // حضوري → مطلوب إدخاله
                    if (string.IsNullOrWhiteSpace(vm.ReferenceCode))
                    {
                        TempData["Error"] = "يجب إدخال الرقم المرجعي للاختبار الحضوري.";
                        return View(vm);
                    }

                    exam.ReferenceCode = vm.ReferenceCode.Trim();
                }
                else
                {
                    exam.ReferenceCode = null; // أونلاين → لا يحتاج كود
                }

                await _context.SaveChangesAsync();

                // ==============================
                // 3.5) حراسة: لا يجوز تعديل أسئلة تكليف له محاولات إجابة سابقة دون
                //      تأكيد صريح + سبب إجباري (Sprint 2 — EB2)
                // ==============================
                var attemptSummary = await _integrityService.GetAttemptSummaryAsync(
                    examAssignmentId: null, examAssignmentToStudentId: vm.AssignmentId);

                if (attemptSummary.AttemptedQuestionsCount > 0 && !vm.ConfirmResetAttempts)
                {
                    TempData["Warning"] =
                        $"⚠️ هذا التكليف له {attemptSummary.AttemptedQuestionsCount} إجابة مسجّلة للطالب/ة" +
                        (attemptSummary.IsSubmitted ? " وتم تسليمه بالفعل" : "") +
                        ". تعديل الأسئلة الآن سيمسح محاولاته/ا القديمة ويُعاد الاختبار من الصفر. " +
                        "لو متأكد، فعّل \"تأكيد إعادة التعيين\" واكتب سبب التعديل ثم احفظ مرة أخرى.";
                    return View(vm);
                }

                if (attemptSummary.AttemptedQuestionsCount > 0 && vm.ConfirmResetAttempts)
                {
                    if (string.IsNullOrWhiteSpace(vm.EditReason))
                    {
                        TempData["Error"] = "سبب التعديل إجباري عند وجود محاولات سابقة للطالب/ة.";
                        return View(vm);
                    }

                    await _integrityService.ResetStudentAttemptsAsync(
                        examAssignmentId: null, examAssignmentToStudentId: vm.AssignmentId);
                }

                // ==============================
                // 4) إعادة توليد الأسئلة
                //    ⚠️ نبني القائمة الجديدة أولاً قبل حذف القديمة، حتى لا
                //    نفقد ربط الأسئلة بالكامل إذا لم يصل اختيار صالح (نموذج
                //    احترافي / توليد تلقائي) من الفورم لأي سبب.
                // ==============================
                List<Question> selectedQuestions = new();

                // نموذج احترافي
                if (vm.UseProfessionalModel && vm.ProfessionalModelId.HasValue)
                {
                    var modelIsAvailable = await _context.ProfessionalModels
                        .AsNoTracking()
                        .AnyAsync(m => m.Id == vm.ProfessionalModelId.Value && !m.IsArchived);

                    if (!modelIsAvailable)
                    {
                        TempData["Error"] = "النموذج الاحترافي المحدد غير متاح أو مؤرشف.";
                        return View(vm);
                    }

                    selectedQuestions = await _context.ProfessionalModelQuestions
                        .Where(x => x.ModelId == vm.ProfessionalModelId.Value)
                        .OrderBy(x => x.OrderNumber)
                        .Select(x => x.Question)
                        .ToListAsync();
                }

                // توليد تلقائي
                if (vm.UseAutoGeneration && vm.Sections.Any())
                {
                    foreach (var sec in vm.Sections)
                    {
                        if (sec.RequestedCount <= 0) continue;

                        var qs = await _context.Questions
                            .Where(q => q.SectionId == sec.SectionId)
                            .OrderBy(q => Guid.NewGuid())
                            .Take(sec.RequestedCount)
                            .ToListAsync();

                        selectedQuestions.AddRange(qs);
                    }
                }

                // إزالة التكرار
                selectedQuestions = selectedQuestions.Distinct().ToList();

                if (!selectedQuestions.Any())
                {
                    TempData["Error"] = "لم يتم العثور على أسئلة صالحة للتعديل. لن يتم حذف أسئلة الاختبار الحالية.";
                    return View(vm);
                }

                // ==============================
                // 5) حذف الأسئلة القديمة ثم إدراج الجديدة
                //    (بعد التأكد من وجود بديل صالح فقط)
                // ==============================
                var oldQuestions = _context.ExamQuestions
                    .Where(q => q.ExamId == vm.ExamId && q.ExamAssignmentToStudentId == vm.AssignmentId);

                int oldQuestionsCount = await oldQuestions.CountAsync();

                _context.ExamQuestions.RemoveRange(oldQuestions);

                // تحديث العدد
                exam.TotalQuestions = selectedQuestions.Count;
                assignment.QuestionCount = selectedQuestions.Count;

                int order = 1;

                foreach (var q in selectedQuestions)
                {
                    _context.ExamQuestions.Add(new ExamQuestion
                    {
                        ExamId = vm.ExamId,
                        QuestionId = q.Id,
                        Order = order++,
                        ExamAssignmentToStudentId = vm.AssignmentId
                    });
                }

                await _context.SaveChangesAsync();

                // Sprint 3 (EC3) — تسجيل التعديل في AdminActivityLog: من/ليه/الأثر
                await _activityLogger.LogExamQuestionsChangeAsync(
                    actionType: "تعديل شامل لأسئلة اختبار فردي",
                    reason: vm.EditReason ?? "بدون محاولات سابقة — لا يتطلب سببًا",
                    CurrentUserId(), CurrentUserName(),
                    examAssignmentId: null, examAssignmentToStudentId: vm.AssignmentId,
                    studentId: assignment.StudentId,
                    questionsBeforeCount: oldQuestionsCount, questionsAfterCount: selectedQuestions.Count,
                    affectedAttemptsCount: attemptSummary.AttemptedQuestionsCount);

                TempData["Success"] = "✔ تم تحديث بيانات الاختبار بنجاح";
                return RedirectToAction("Details", new { id = vm.AssignmentId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"حدث خطأ أثناء التعديل: {ex.Message}";
                return View(vm);
            }
        }

        // ============================================================
        // GET: حذف اختبار فردي
        // ============================================================
        // ============================================================
        // POST: حذف اختبار فردي
        // ============================================================
        [HttpPost]
        [AdminPermission("ExamIndividualAssignments", "Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                // 1️⃣ جلب التكليف الفردي الصحيح
                var assignment = await _context.ExamAssignmentsToStudents
                    .FirstOrDefaultAsync(a => a.Id == id);

                if (assignment == null)
                {
                    TempData["Error"] = "لم يتم العثور على الاختبار الفردي.";
                    return RedirectToAction(nameof(Index));
                }

                var examId = assignment.ExamId;

                // 2️⃣ حذف محاولات الطالب المرتبطة
                var attempts = _context.QuestionAttemptNew
                    .Where(a => a.ExamAssignmentId == id);

                _context.QuestionAttemptNew.RemoveRange(attempts);

                // 3️⃣ حذف حالة الاختبار
                var statuses = _context.ExamStudentStatuses
                    .Where(s => s.ExamAssignmentId == id);

                _context.ExamStudentStatuses.RemoveRange(statuses);

                // 4️⃣ حذف ربط الأسئلة بالاختبار الفردي
                var questions = _context.ExamQuestions
                    .Where(q => q.ExamAssignmentToStudentId == id);

                _context.ExamQuestions.RemoveRange(questions);

                // 5️⃣ حذف التكليف نفسه
                _context.ExamAssignmentsToStudents.Remove(assignment);

                await _context.SaveChangesAsync();

                // 6️⃣ التأكد هل هذا الـ Exam مستخدم في تكليف آخر
                var isUsedElsewhere = await _context.ExamAssignmentsToStudents
                    .AnyAsync(a => a.ExamId == examId);

                if (!isUsedElsewhere)
                {
                    var exam = await _context.Exams.FindAsync(examId);
                    if (exam != null)
                        _context.Exams.Remove(exam);

                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "✔ تم حذف الاختبار الفردي بالكامل بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"تعذر حذف الاختبار: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
        
    }
}
