
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using QdratNew.Data;
using QdratNew.DTOs;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Admin;
using QdratNew.ViewModels.EnhancementSkills;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Homework;
using System.Security.Claims;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminLessonCompletionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILessonCompletionService _lessonCompletionService;
        private readonly IStudentSectionExamService _sectionExamService;
        private readonly IAdminActivityLogger _logger;
        private readonly IAdvancedNotificationService _notificationService;
        private readonly IAutoExamGenerationService _autoExamGenerationService;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ICurriculumExamGeneratorService _curriculumExamGeneratorService;

        public AdminLessonCompletionsController(ApplicationDbContext context, ILessonCompletionService lessonCompletionService, IStudentSectionExamService sectionExamService, IAdminActivityLogger logger,
    IAdvancedNotificationService notificationService // ✅ أضف هذا
, IAutoExamGenerationService autoExamGenerationService, ITimeZoneService timeZoneService, ICurriculumExamGeneratorService curriculumExamGeneratorService)
        {
            _context = context;
            _lessonCompletionService = lessonCompletionService;
            _sectionExamService = sectionExamService;
            _logger = logger;
            _notificationService = notificationService; // ✅ وربطها هنا
            _autoExamGenerationService = autoExamGenerationService;
            _timeZoneService = timeZoneService;
            _curriculumExamGeneratorService = curriculumExamGeneratorService;
        }

        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> SelectLessons(int? batchId)
        {
            var batches = await _context.Batches
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = $"الدفعة - {b.Name}"
                }).ToListAsync();

            ViewBag.Batches = batches;

            return View();
        }


        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> SelectQuestionsForLesson(int lessonId)
        {
            var lesson = await _context.Lessons.Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return NotFound();

            var questions = await _context.Questions
                .Where(q => q.LessonId == lessonId && q.IsReviewed)
                .Select(q => new QuestionSummaryViewModel
                {
                    QuestionId = q.Id,
                    Title = q.Title,
                    Difficulty = q.Difficulty.ToString(), // ✅ تحويل enum إلى نص
                    IsReviewed = q.IsReviewed
                })
                .ToListAsync();


            var vm = new LessonQuestionsSelectionViewModel
            {
                LessonId = lesson.Id,
                LessonTitle = lesson.Title,
                BatchId = _context.Lecture
                    .Where(le => le.SectionId == lesson.SectionId)
                    .Select(le => le.BatchId)
                    .FirstOrDefault(),
                Questions = questions
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "GenerateHomework")]
        public async Task<IActionResult> SaveSelectedQuestions(LessonQuestionsSelectionViewModel vm)
        {
            if (vm.SelectedQuestionIds == null || !vm.SelectedQuestionIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم اختيار أي أسئلة.";
                return RedirectToAction(nameof(SelectQuestionsForLesson), new { lessonId = vm.LessonId });
            }

            // يمكنك حفظ الاختيارات مؤقتًا في جدول مخصص أو Cache، أو تمريرها إلى ConfirmHomework عبر TempData
            TempData["SelectedLesson_" + vm.LessonId] = string.Join(",", vm.SelectedQuestionIds);

            TempData["Success"] = $"✅ تم اختيار {vm.SelectedQuestionIds.Count} سؤال للمؤشر بنجاح.";
            return RedirectToAction(nameof(ConfirmHomework), new { batchId = vm.BatchId });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions","GenerateHomework")]
        public async Task<IActionResult> SaveAndGenerate(int batchId, int sectionId, int lectureId, string completionTitle, string lessonIds)
        {
            if (string.IsNullOrWhiteSpace(lessonIds))
            {
                TempData["Error"] = "❌ لم يتم اختيار أي مؤشرات.";
                return RedirectToAction("SelectLessons", new { batchId });
            }

            var parsedLessonIds = lessonIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id =>
                {
                    if (int.TryParse(id, out int parsed))
                        return parsed;
                    return -1;
                })
                .Where(x => x > 0)
                .ToList();

            if (!parsedLessonIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم تحويل المؤشرات بنجاح.";
                return RedirectToAction("ConfirmHomework", new { batchId, lectureId });
            }

            var dto = new LessonCompletionInputDto
            {
                BatchId = batchId,
                SectionId = sectionId,
                LectureId = lectureId,
                CompletionTitle = completionTitle,
                LessonIds = parsedLessonIds
            };

            var success = await _lessonCompletionService.RegisterCompletedLessonsAsync(dto);

            if (success)
            {
                // ✅ إنشاء جلسة تعزيزية مرتبطة بالمحاضرة (Draft فقط)
                var skillSet = new EnhancementSkillSet
                {
                    BatchId = batchId,
                    LectureId = lectureId,
                    Title = $"مهارات تعزيزية - محاضرة {lectureId}",
                    CreatedAt = _timeZoneService.GetNowUtc()
                    // مفيش إرسال هنا 👇
                };

                _context.EnhancementSkillSets.Add(skillSet);
                await _context.SaveChangesAsync();

                TempData["Success"] = "✅ تم تسجيل المؤشرات وإنشاء جلسة تعزيزية كمسودة.";
                return RedirectToAction("ConfirmHomework", new { batchId, lectureId });
            }
            else
            {
                TempData["Error"] = "⚠️ لم يتم تسجيل المؤشرات. حاول مجددًا.";
                return RedirectToAction("SelectLessons", new { batchId });
            }
        }

        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "ManageModelHomework")]
        public async Task<IActionResult> EditProfessionalHomework(int id)
        {
            var homeworkSet = await _context.HomeworkSets
                .Include(h => h.Batch)
                .FirstOrDefaultAsync(h => h.Id == id);

            if (homeworkSet == null)
                return NotFound("❌ لم يتم العثور على هذا الواجب.");

            var vm = new EditProfessionalHomeworkVm
            {
                Id            = homeworkSet.Id,
                Title         = homeworkSet.Title,
                BatchId       = homeworkSet.BatchId,
                BatchName     = homeworkSet.Batch?.Name ?? "-",
                StartAt       = homeworkSet.StartAt,
                EndAt         = homeworkSet.EndAt,
                DurationMinutes = 30,
                Curriculums   = await GetCurriculumsSelectListAsync(),
                ProfessionalModels = await GetModelsSelectListAsync(ProfessionalModelType.Homework, null)
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelHomework")]

        public async Task<IActionResult> EditProfessionalHomework(EditProfessionalHomeworkVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var homeworkSet = await _context.HomeworkSets.FindAsync(vm.Id);
            if (homeworkSet == null)
                return NotFound("⚠️ الواجب غير موجود.");

            // 🟢 التحديثات الفعلية
            homeworkSet.Title = vm.Title;
            homeworkSet.StartAt = vm.StartAt;
            homeworkSet.EndAt = vm.EndAt;

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تعديل بيانات الواجب بنجاح.";
            return RedirectToAction("ModelAssignments", "AdminLessonCompletions");
        }


        // ============================================================
        // GET: Edit Professional Exam
        // ============================================================
        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "ManageModelExam")]
        public async Task<IActionResult> EditProfessionalExam(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .Include(a => a.Batch)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار.");

            var vm = new EditProfessionalExamVm
            {
                Id              = assignment.Id,
                Title           = assignment.Title,
                BatchId         = assignment.BatchId,
                BatchName       = assignment.Batch?.Name ?? "-",
                TotalQuestions  = assignment.TotalQuestions,
                DurationMinutes = assignment.DurationMinutes,
                StartAt         = assignment.ScheduledDate,
                EndAt           = assignment.EndAt,
                IsOnline        = assignment.IsOnline,
                IsInLab         = assignment.IsInLab,
                ReferenceCode   = assignment.Exam?.ReferenceCode,
                RandomizeQuestions = assignment.Exam?.RandomizeQuestions ?? true,
                Curriculums        = await GetCurriculumsSelectListAsync(),
                ProfessionalModels = await GetModelsSelectListAsync(ProfessionalModelType.Exam, null)
            };

            return View(vm);
        }



        // ============================================================
        // POST: Edit Professional Exam
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelExam")]
        public async Task<IActionResult> EditProfessionalExam(EditProfessionalExamVm vm)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "⚠️ لم يتم حفظ التعديلات. تأكد من صحة البيانات.";
                return View(vm);
            }

            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == vm.Id);

            if (assignment == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على هذا الاختبار.";
                return RedirectToAction("ModelAssignments", "AdminLessonCompletions", new { area = "Admin" });
            }

            try
            {
                // ============================================================
                // 1) تحديث بيانات التكليف الأساسي
                // ============================================================
                assignment.Title = vm.Title?.Trim();
                assignment.TotalQuestions = vm.TotalQuestions;
                assignment.DurationMinutes = vm.DurationMinutes;

                if (vm.StartAt.HasValue)
                    assignment.ScheduledDate = vm.StartAt.Value;

                assignment.EndAt = vm.EndAt ?? assignment.ScheduledDate?.AddMinutes(vm.DurationMinutes);

                // ============================================================
                // 2) نوع الاختبار: Online or حضوري
                // ============================================================
                assignment.IsOnline = vm.IsOnline;        // true = Online
                assignment.IsInLab = !vm.IsOnline;        // false = Online → true حضوري

                // ============================================================
                // 3) تعامل مع الرقم المرجعي
                // ============================================================
                if (assignment.Exam != null)
                {
                    if (!vm.IsOnline)
                    {
                        // حضوري → الرقم المرجعي مطلوب
                        if (string.IsNullOrWhiteSpace(vm.ReferenceCode))
                        {
                            TempData["Error"] = "⚠️ يجب إدخال رقم مرجعي للاختبار الحضوري.";
                            return View(vm);
                        }

                        assignment.Exam.ReferenceCode = vm.ReferenceCode.Trim();
                    }
                    else
                    {
                        // Online → لا يستخدم رقم مرجعي
                        assignment.Exam.ReferenceCode = null;
                    }

                    assignment.Exam.RandomizeQuestions = vm.RandomizeQuestions;
                }

                // ============================================================
                // 4) حفظ التعديلات
                // ============================================================
                await _context.SaveChangesAsync();

                TempData["Success"] = "✔ تم حفظ تعديلات الاختبار بنجاح.";

                return RedirectToAction("ModelAssignments", "AdminLessonCompletions", new { area = "Admin" });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ حدث خطأ أثناء حفظ التعديلات: {ex.Message}";
                return View(vm);
            }
        }






        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> ModelAssignments()
        {
            var canViewArchive = User.IsInRole("Owner") || User.IsInRole("Developer");

            var batches = await _context.Batches
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();

            ViewBag.Batches = batches;
            ViewBag.CanViewArchive = canViewArchive;

            var allHomeworks = await (
                from hs in _context.HomeworkSets
                join b in _context.Batches on hs.BatchId equals b.Id
                where hs.IsFromProfessionalModel == true
                orderby hs.CreatedAt descending
                select new ModelAssignmentViewModel
                {
                    Id = hs.Id,
                    BatchId = hs.BatchId,
                    Title = hs.Title,
                    BatchName = b.Name,
                    CreatedAt = hs.CreatedAt,
                    StudentCount = _context.Homeworks
                        .Where(h => h.HomeworkSetId == hs.Id)
                        .Select(h => h.StudentId)
                        .Distinct()
                        .Count(),
                    QuestionCount = _context.Homeworks
                        .Where(h => h.HomeworkSetId == hs.Id)
                        .Count(),
                    Type = "واجب",
                    IsArchived = hs.IsArchived
                }).ToListAsync();

            var allExams = await (
                from ea in _context.ExamAssignmentsToBatches
                join b in _context.Batches on ea.BatchId equals b.Id
                join e in _context.Exams on ea.ExamId equals e.Id
                where e.IsFromProfessionalModel == true
                orderby ea.CreatedAt descending
                select new ModelAssignmentViewModel
                {
                    Id = ea.Id,
                    BatchId = ea.BatchId,
                    Title = ea.Title,
                    BatchName = b.Name,
                    CreatedAt = ea.CreatedAt,
                    StudentCount = _context.StudentBatchEnrollments
                        .Count(s => s.BatchId == ea.BatchId),
                    QuestionCount = ea.TotalQuestions,
                    Type = "اختبار",
                    IsArchived = ea.IsArchived
                }).ToListAsync();

            allExams = allExams
                .Where(x =>
                    !(x.Title.StartsWith("اختبار رقم")
                      && x.CreatedAt.ToString("yyyy-MM-dd HH:mm") == "2025-12-01 11:48")
                )
                .ToList();

            var vm = new ModelAssignmentsPageViewModel
            {
                Homeworks = allHomeworks.Where(x => !x.IsArchived).OrderByDescending(x => x.CreatedAt).ToList(),
                Exams = allExams.Where(x => !x.IsArchived).OrderByDescending(x => x.CreatedAt).ToList(),
                ArchivedHomeworks = canViewArchive
                    ? allHomeworks.Where(x => x.IsArchived).OrderByDescending(x => x.CreatedAt).ToList()
                    : new List<ModelAssignmentViewModel>(),
                ArchivedExams = canViewArchive
                    ? allExams.Where(x => x.IsArchived).OrderByDescending(x => x.CreatedAt).ToList()
                    : new List<ModelAssignmentViewModel>()
            };

            return View(vm);
        }

        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> BatchModelHomeworks(int batchId)
        {
            var batch = await _context.Batches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound("لم يتم العثور على الدفعة.");

            var homeworks = await (
                from hs in _context.HomeworkSets.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on hs.BatchId equals b.Id
                where hs.IsFromProfessionalModel
                      && !hs.IsArchived
                      && hs.BatchId == batchId
                orderby hs.CreatedAt descending
                select new ModelAssignmentViewModel
                {
                    Id = hs.Id,
                    BatchId = hs.BatchId,
                    Title = hs.Title,
                    BatchName = b.Name,
                    CreatedAt = hs.CreatedAt,
                    StudentCount = _context.Homeworks
                        .Where(h => h.HomeworkSetId == hs.Id)
                        .Select(h => h.StudentId)
                        .Distinct()
                        .Count(),
                    QuestionCount = _context.Homeworks
                        .Where(h => h.HomeworkSetId == hs.Id)
                        .Count(),
                    Type = "واجب",
                    IsArchived = hs.IsArchived,
                    BlockedStudentsCount = _context.IntegrityViolationLogs
                        .Count(v => v.AttemptType == IntegrityAttemptType.Homework
                                 && v.AttemptEntityId == hs.Id
                                 && !v.IsResolved)
                }).ToListAsync();

            ViewBag.BatchId = batch.Id;
            ViewBag.BatchName = batch.Name;
            return View(homeworks);
        }

        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> BatchModelExams(int batchId)
        {
            var batch = await _context.Batches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound("لم يتم العثور على الدفعة.");

            var exams = await (
                from ea in _context.ExamAssignmentsToBatches.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on ea.BatchId equals b.Id
                join e in _context.Exams.AsNoTracking() on ea.ExamId equals e.Id
                where e.IsFromProfessionalModel == true
                      && !ea.IsArchived
                      && ea.BatchId == batchId
                orderby ea.CreatedAt descending
                select new ModelAssignmentViewModel
                {
                    Id = ea.Id,
                    BatchId = ea.BatchId,
                    Title = ea.Title,
                    BatchName = b.Name,
                    CreatedAt = ea.CreatedAt,
                    StudentCount = _context.StudentBatchEnrollments
                        .Count(s => s.BatchId == ea.BatchId),
                    QuestionCount = ea.TotalQuestions,
                    Type = "اختبار",
                    IsArchived = ea.IsArchived,
                    BlockedStudentsCount = _context.IntegrityViolationLogs
                        .Count(v => v.AttemptType == IntegrityAttemptType.Exam
                                 && v.AttemptEntityId == ea.Id
                                 && !v.IsResolved)
                }).ToListAsync();

            exams = exams
                .Where(x =>
                    !(x.Title.StartsWith("اختبار رقم")
                      && x.CreatedAt.ToString("yyyy-MM-dd HH:mm") == "2025-12-01 11:48")
                )
                .ToList();

            ViewBag.BatchId = batch.Id;
            ViewBag.BatchName = batch.Name;
            return View(exams);
        }

        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> BatchModelArchive(int batchId)
        {
            if (!User.IsInRole("Owner") && !User.IsInRole("Developer"))
                return Forbid();

            var batch = await _context.Batches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound("لم يتم العثور على الدفعة.");

            var archivedHomeworks = await (
                from hs in _context.HomeworkSets.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on hs.BatchId equals b.Id
                where hs.IsFromProfessionalModel
                      && hs.IsArchived
                      && hs.BatchId == batchId
                orderby hs.CreatedAt descending
                select new ModelAssignmentViewModel
                {
                    Id = hs.Id,
                    BatchId = hs.BatchId,
                    Title = hs.Title,
                    BatchName = b.Name,
                    CreatedAt = hs.CreatedAt,
                    StudentCount = _context.Homeworks
                        .Where(h => h.HomeworkSetId == hs.Id)
                        .Select(h => h.StudentId)
                        .Distinct()
                        .Count(),
                    QuestionCount = _context.Homeworks
                        .Where(h => h.HomeworkSetId == hs.Id)
                        .Count(),
                    Type = "واجب",
                    IsArchived = hs.IsArchived
                }).ToListAsync();

            var archivedExams = await (
                from ea in _context.ExamAssignmentsToBatches.AsNoTracking()
                join b in _context.Batches.AsNoTracking() on ea.BatchId equals b.Id
                join e in _context.Exams.AsNoTracking() on ea.ExamId equals e.Id
                where e.IsFromProfessionalModel == true
                      && ea.IsArchived
                      && ea.BatchId == batchId
                orderby ea.CreatedAt descending
                select new ModelAssignmentViewModel
                {
                    Id = ea.Id,
                    BatchId = ea.BatchId,
                    Title = ea.Title,
                    BatchName = b.Name,
                    CreatedAt = ea.CreatedAt,
                    StudentCount = _context.StudentBatchEnrollments
                        .Count(s => s.BatchId == ea.BatchId),
                    QuestionCount = ea.TotalQuestions,
                    Type = "اختبار",
                    IsArchived = ea.IsArchived
                }).ToListAsync();

            var vm = new ModelAssignmentsPageViewModel
            {
                ArchivedHomeworks = archivedHomeworks,
                ArchivedExams = archivedExams
            };

            ViewBag.BatchId = batch.Id;
            ViewBag.BatchName = batch.Name;
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelHomework")]
        public async Task<IActionResult> ArchiveModelHomework(int id)
        {
            var set = await _context.HomeworkSets
                .FirstOrDefaultAsync(hs => hs.Id == id && hs.IsFromProfessionalModel);

            if (set == null)
            {
                TempData["Error"] = "❌ الواجب غير موجود أو ليس من النماذج.";
                return RedirectToAction(nameof(ModelAssignments));
            }

            set.IsArchived = true;
            set.ArchivedAt = _timeZoneService.GetNowUtc();
            set.ArchivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم نقل الواجب إلى الأرشيف.";
            return RedirectToAction(nameof(ModelAssignments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelHomework")]
        public async Task<IActionResult> RestoreModelHomework(int id)
        {
            var set = await _context.HomeworkSets
                .FirstOrDefaultAsync(hs => hs.Id == id && hs.IsFromProfessionalModel);

            if (set == null)
            {
                TempData["Error"] = "❌ الواجب غير موجود أو ليس من النماذج.";
                return RedirectToAction(nameof(ModelAssignments));
            }

            set.IsArchived = false;
            set.ArchivedAt = null;
            set.ArchivedByUserId = null;

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إخراج الواجب من الأرشيف.";
            return RedirectToAction(nameof(ModelAssignments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelExam")]
        public async Task<IActionResult> ArchiveModelExam(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(ea => ea.Exam)
                .FirstOrDefaultAsync(ea => ea.Id == id && ea.Exam != null && ea.Exam.IsFromProfessionalModel);

            if (assignment == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على الاختبار أو ليس من النماذج.";
                return RedirectToAction(nameof(ModelAssignments));
            }

            assignment.IsArchived = true;
            assignment.ArchivedAt = _timeZoneService.GetNowUtc();
            assignment.ArchivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم نقل الاختبار إلى الأرشيف.";
            return RedirectToAction(nameof(ModelAssignments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelExam")]
        public async Task<IActionResult> RestoreModelExam(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(ea => ea.Exam)
                .FirstOrDefaultAsync(ea => ea.Id == id && ea.Exam != null && ea.Exam.IsFromProfessionalModel);

            if (assignment == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على الاختبار أو ليس من النماذج.";
                return RedirectToAction(nameof(ModelAssignments));
            }

            assignment.IsArchived = false;
            assignment.ArchivedAt = null;
            assignment.ArchivedByUserId = null;

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إخراج الاختبار من الأرشيف.";
            return RedirectToAction(nameof(ModelAssignments));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelHomework")]
        public async Task<IActionResult> ArchiveModelItemsByBatches(List<int> selectedBatchIds, bool includeHomeworks = true, bool includeExams = true)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "⚠️ اختر دفعة واحدة على الأقل للأرشفة.";
                return RedirectToAction(nameof(ModelAssignments));
            }

            var batchIds = selectedBatchIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (!batchIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم العثور على دفعات صالحة للأرشفة.";
                return RedirectToAction(nameof(ModelAssignments));
            }

            var archivedAt = _timeZoneService.GetNowUtc();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var homeworksCount = 0;
            var examsCount = 0;

            if (includeHomeworks)
            {
                // ✅ SQL Server 2014 Safe:
                // لا نستخدم Contains داخل الاستعلام.
                // نجلب واجبات النماذج غير المؤرشفة ثم نفلتر داخل الذاكرة.
                var allProfessionalHomeworks = await _context.HomeworkSets
                    .Where(hs => hs.IsFromProfessionalModel && !hs.IsArchived)
                    .ToListAsync();

                var homeworks = allProfessionalHomeworks
                    .Where(hs => batchIds.Any(batchId => batchId == hs.BatchId))
                    .ToList();

                foreach (var homework in homeworks)
                {
                    homework.IsArchived = true;
                    homework.ArchivedAt = archivedAt;
                    homework.ArchivedByUserId = userId;
                }

                homeworksCount = homeworks.Count;
            }

            if (includeExams)
            {
                // ✅ SQL Server 2014 Safe:
                // لا نستخدم Contains داخل الاستعلام.
                // نجلب اختبارات النماذج غير المؤرشفة ثم نفلتر داخل الذاكرة.
                var allProfessionalExams = await _context.ExamAssignmentsToBatches
                    .Include(ea => ea.Exam)
                    .Where(ea =>
                        ea.Exam != null &&
                        ea.Exam.IsFromProfessionalModel &&
                        !ea.IsArchived)
                    .ToListAsync();

                var exams = allProfessionalExams
                    .Where(ea => batchIds.Any(batchId => batchId == ea.BatchId))
                    .ToList();

                foreach (var exam in exams)
                {
                    exam.IsArchived = true;
                    exam.ArchivedAt = archivedAt;
                    exam.ArchivedByUserId = userId;
                }

                examsCount = exams.Count;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم أرشفة {homeworksCount} واجب و {examsCount} اختبار.";
            return RedirectToAction(nameof(ModelAssignments));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelHomework")]
        public async Task<IActionResult> RestoreModelItemsByBatches(List<int> selectedBatchIds, bool includeHomeworks = true, bool includeExams = true)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "⚠️ اختر دفعة واحدة على الأقل للاسترجاع.";
                return RedirectToAction(nameof(ModelAssignments));
            }

            var batchIds = selectedBatchIds
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            if (!batchIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم العثور على دفعات صالحة للاسترجاع.";
                return RedirectToAction(nameof(ModelAssignments));
            }

            var homeworksCount = 0;
            var examsCount = 0;

            if (includeHomeworks)
            {
                // ✅ SQL Server 2014 Safe:
                // لا نستخدم Contains داخل الاستعلام.
                var allArchivedProfessionalHomeworks = await _context.HomeworkSets
                    .Where(hs => hs.IsFromProfessionalModel && hs.IsArchived)
                    .ToListAsync();

                var homeworks = allArchivedProfessionalHomeworks
                    .Where(hs => batchIds.Any(batchId => batchId == hs.BatchId))
                    .ToList();

                foreach (var homework in homeworks)
                {
                    homework.IsArchived = false;
                    homework.ArchivedAt = null;
                    homework.ArchivedByUserId = null;
                }

                homeworksCount = homeworks.Count;
            }

            if (includeExams)
            {
                // ✅ SQL Server 2014 Safe:
                // لا نستخدم Contains داخل الاستعلام.
                var allArchivedProfessionalExams = await _context.ExamAssignmentsToBatches
                    .Include(ea => ea.Exam)
                    .Where(ea =>
                        ea.Exam != null &&
                        ea.Exam.IsFromProfessionalModel &&
                        ea.IsArchived)
                    .ToListAsync();

                var exams = allArchivedProfessionalExams
                    .Where(ea => batchIds.Any(batchId => batchId == ea.BatchId))
                    .ToList();

                foreach (var exam in exams)
                {
                    exam.IsArchived = false;
                    exam.ArchivedAt = null;
                    exam.ArchivedByUserId = null;
                }

                examsCount = exams.Count;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم إخراج {homeworksCount} واجب و {examsCount} اختبار من الأرشيف.";
            return RedirectToAction(nameof(ModelAssignments));
        }

        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<JsonResult> LoadStudentsByBatches(string batchIds)
        {
            if (string.IsNullOrWhiteSpace(batchIds))
                return Json(new { success = false, message = "لم يتم اختيار أي دفعات." });

            var ids = batchIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                              .Select(x => int.TryParse(x, out int id) ? id : 0)
                              .Where(x => x > 0)
                              .ToList();

            var result = new List<object>();

            foreach (var batchId in ids)
            {
                var batchName = await _context.Batches
                    .Where(b => b.Id == batchId && !b.IsDeleted && !b.IsArchived)
                    .Select(b => b.Name)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(batchName))
                {
                    continue;
                }

                var students = await _context.StudentBatchEnrollments
                    .Where(e => e.BatchId == batchId)
                    .Join(_context.Students,
                          e => e.StudentID,
                          s => s.StudentID,
                          (e, s) => new
                          {
                              id = s.StudentID,           // ✅ كلها lowercase
                              name = s.FullName,
                              batch = batchName
                          }).ToListAsync();

                result.AddRange(students);
            }

            return Json(new { success = true, students = result });
        }





        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "ManageModelExam")]
        public async Task<IActionResult> CreateModelExamPage(string? selectedBatches)
        {
            // 🧩 جلب جميع النماذج الاحترافية
            var models = await _context.ProfessionalModels
                .Where(m => !m.IsArchived)
                .OrderBy(m => m.Title)
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = m.Title
                }).ToListAsync();

            // 🧩 جلب جميع الدفعات
            var batches = await _context.Batches
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToListAsync();

            var students = new List<SelectListItem>();

            // ✅ لو تم تمرير دفعات مختارة (عبر Query String selectedBatches=1,2,3)
            if (!string.IsNullOrWhiteSpace(selectedBatches))
            {
                var batchIds = selectedBatches
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => int.TryParse(id, out int i) ? i : 0)
                    .Where(x => x > 0)
                    .ToList();

                foreach (var batchId in batchIds)
                {
                    var batchName = await _context.Batches
                        .Where(b => b.Id == batchId && !b.IsDeleted && !b.IsArchived)
                        .Select(b => b.Name)
                        .FirstOrDefaultAsync();

                    if (string.IsNullOrWhiteSpace(batchName))
                    {
                        continue;
                    }

                    // 🧩 جلب طلاب الدفعة الحالية
                    var batchStudents = await _context.StudentBatchEnrollments
                        .Where(e => e.BatchId == batchId)
                        .Join(_context.Students,
                              e => e.StudentID,
                              s => s.StudentID,
                              (e, s) => new SelectListItem
                              {
                                  Value = s.StudentID.ToString(),
                                  Text = $"{s.FullName} - ({batchName})"
                              }).ToListAsync();

                    students.AddRange(batchStudents);
                }
            }

            var vm = new CreateModelExamViewModel
            {
                Models = models,
                Batches = batches,
                Students = students,
                StartAt = DateTime.Now,
                EndAt = DateTime.Now.AddDays(1),
                DurationMinutes = 30
            };

            return View(vm);
        }








        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "ManageModelHomework")]
        public async Task<IActionResult> CreateModelHomework(int? batchId)
        {
            // 🧩 جلب الطلاب لو تم تمرير دفعة
            var students = new List<SelectListItem>();
            if (batchId.HasValue && batchId > 0)
            {
                students = await (
                    from e in _context.StudentBatchEnrollments
                    join b in _context.Batches on e.BatchId equals b.Id
                    join s in _context.Students on e.StudentID equals s.StudentID
                    where e.BatchId == batchId
                          && !b.IsDeleted
                          && !b.IsArchived
                    select new SelectListItem
                    {
                        Value = s.StudentID.ToString(),
                        Text = s.FullName
                    }).ToListAsync();
            }

            var vm = new CreateModelHomeworkViewModel
            {
                BatchId = batchId ?? 0,
                Students = students,
                StartAt = DateTime.Now,
                EndAt = DateTime.Now.AddDays(1)
                // ⛔ DurationMinutes محذوف لأنها غير موجودة في هذا الـ ViewModel
            };
            if (batchId.HasValue && batchId > 0)
            {
                vm.SelectedBatchIds = new List<int> { batchId.Value };
            }

            await PopulateCreateModelHomeworkListsAsync(vm);

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelHomework")]
        public async Task<IActionResult> CreateModelHomework(CreateModelHomeworkViewModel vm)
        {
            if (!ModelState.IsValid || vm.SelectedBatchIds == null || !vm.SelectedBatchIds.Any())
            {
                TempData["Error"] = "⚠️ تأكد من اختيار نموذج ودفعة واحدة على الأقل.";
                await PopulateCreateModelHomeworkListsAsync(vm);
                return View(vm);
            }

            var model = await _context.ProfessionalModels
                .Include(m => m.Questions)
                .ThenInclude(mq => mq.Question)
                .FirstOrDefaultAsync(m => m.Id == vm.ModelId && !m.IsArchived);

            if (model == null)
            {
                TempData["Error"] = "❌ النموذج غير موجود.";
                return RedirectToAction(nameof(CreateModelHomework));
            }

            var availableBatchIds = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .Select(b => b.Id)
                .ToListAsync();

            vm.SelectedBatchIds = vm.SelectedBatchIds
                .Where(batchId => availableBatchIds.Any(id => id == batchId))
                .Distinct()
                .ToList();

            if (!vm.SelectedBatchIds.Any())
            {
                TempData["Error"] = "⚠️ الدفعات المحددة غير متاحة أو مؤرشفة.";
                return RedirectToAction(nameof(CreateModelHomework));
            }

            int? linkedLectureId = null;
            if (vm.IsLinkedToLecture)
            {
                if (!vm.LectureId.HasValue || vm.LectureId.Value <= 0)
                {
                    TempData["Error"] = "⚠️ اختر المحاضرة المرتبطة بالواجب.";
                    await PopulateCreateModelHomeworkListsAsync(vm);
                    return View(vm);
                }

                if (vm.SelectedBatchIds.Count != 1)
                {
                    TempData["Error"] = "⚠️ ربط الواجب بمحاضرة يتطلب اختيار دفعة واحدة فقط.";
                    await PopulateCreateModelHomeworkListsAsync(vm);
                    return View(vm);
                }

                var selectedBatchId = vm.SelectedBatchIds[0];
                var lectureExists = await _context.Lecture
                    .AsNoTracking()
                    .AnyAsync(l => l.Id == vm.LectureId.Value && l.BatchId == selectedBatchId);

                if (!lectureExists)
                {
                    TempData["Error"] = "⚠️ المحاضرة المختارة لا تنتمي للدفعة المحددة.";
                    await PopulateCreateModelHomeworkListsAsync(vm);
                    return View(vm);
                }

                linkedLectureId = vm.LectureId.Value;
            }

            // 🕐 تحويل التواريخ إلى UTC
            if (vm.StartAt.HasValue)
                vm.StartAt = _timeZoneService.ConvertToUtc(vm.StartAt.Value);
            if (vm.EndAt.HasValue)
                vm.EndAt = _timeZoneService.ConvertToUtc(vm.EndAt.Value);

            // 🔁 إرسال النموذج إلى كل دفعة
            foreach (var batchId in vm.SelectedBatchIds)
            {
                // جلب الطلاب داخل الدفعة
                var studentIds = await _context.StudentBatchEnrollments
                    .Where(e => e.BatchId == batchId)
                    .Select(e => e.StudentID)
                    .ToListAsync();

                if (!studentIds.Any())
                    continue;

                // إنشاء HomeworkSet جديد لكل دفعة
                var set = new HomeworkSet
                {
                    BatchId = batchId,
                    Title = string.IsNullOrEmpty(vm.Title)
                        ? $"واجب من النموذج {model.Title}"
                        : vm.Title.Trim(),
                    CompletionTitle = model.Title,
                    CreatedAt = _timeZoneService.GetNowUtc(),
                    StartAt = vm.StartAt,
                    EndAt = vm.EndAt,
                    IsExtra = !linkedLectureId.HasValue,
                    LectureId = linkedLectureId,
                    IsFromProfessionalModel = true,
                    AssignedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                    IsSent = true
                };
                _context.HomeworkSets.Add(set);
                await _context.SaveChangesAsync();

                var assignedAt = _timeZoneService.GetNowUtc();
                var homeworkSetStudents = studentIds
                    .Distinct()
                    .Select(studentId => new HomeworkSetStudent
                    {
                        HomeworkSetId = set.Id,
                        StudentId = studentId,
                        AssignedAt = assignedAt,
                        LastUpdated = assignedAt,
                        NotificationSent = true
                    })
                    .ToList();

                _context.HomeworkSetStudents.AddRange(homeworkSetStudents);

                // ربط الأسئلة بالطلاب
                var homeworks = new List<Homework>();
                foreach (var q in model.Questions.OrderBy(x => x.OrderNumber))
                {
                    foreach (var studentId in studentIds)
                    {
                        homeworks.Add(new Homework
                        {
                            StudentId = studentId,
                            QuestionId = q.QuestionId.Value,
                            LessonId = q.Question.LessonId,
                            LectureId = linkedLectureId,
                            HomeworkSetId = set.Id,
                            AssignedAt = assignedAt,
                            Status = HomeworkStatus.Pending,
                            IsSent = true
                        });
                    }
                }

                _context.Homeworks.AddRange(homeworks);
                await _context.SaveChangesAsync();

                // إشعار الطلاب
                await _notificationService.SendToStudentsAsync(
                    studentIds,
                    $"📘 تم إرسال واجب جديد من النموذج {model.Title}. آخر موعد: {vm.EndAt:yyyy-MM-dd HH:mm}",
                    NotificationCategory.Homework,
                    "/Students/Homeworks"
                );

                // إشعار المدرب المرتبط بالدفعة
                var instructorId = await _context.InstructorCurriculumBatches
                    .Where(x => x.BatchId == batchId)
                    .Select(x => x.InstructorId)
                    .FirstOrDefaultAsync();
                if (instructorId > 0)
                {
                    await _notificationService.SendToInstructorAsync(
                        instructorId,
                        $"📢 تم إرسال واجب من النموذج {model.Title} للدفعة {batchId}.",
                        NotificationCategory.Homework,
                        "/Instructors/HomeworkDashboard"
                    );
                }
            }

            TempData["Success"] = "✅ تم إرسال واجب النموذج لجميع الدفعات المحددة بنجاح.";
            return RedirectToAction("ModelAssignments");
        }

        private async Task PopulateCreateModelHomeworkListsAsync(CreateModelHomeworkViewModel vm)
        {
            vm.Curriculums = await GetCurriculumsSelectListAsync();

            vm.Models = await GetModelsSelectListAsync(ProfessionalModelType.Homework, vm.CurriculumId);

            vm.Batches = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();

            var selectedBatchIds = vm.SelectedBatchIds?
                .Where(batchId => batchId > 0)
                .Distinct()
                .ToList() ?? new List<int>();

            if (!selectedBatchIds.Any() && vm.BatchId > 0)
            {
                selectedBatchIds.Add(vm.BatchId);
            }

            vm.Lectures = new List<SelectListItem>();
            if (selectedBatchIds.Count == 1)
            {
                var batchId = selectedBatchIds[0];
                vm.Lectures = await _context.Lecture
                    .AsNoTracking()
                    .Where(l => l.BatchId == batchId)
                    .OrderByDescending(l => l.Date)
                    .Select(l => new SelectListItem
                    {
                        Value = l.Id.ToString(),
                        Text = l.Title + " - " + l.Date.ToString("yyyy-MM-dd")
                    })
                    .ToListAsync();
            }
        }



      
        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "ManageModelExam")]
        public async Task<IActionResult> CreateModelExam(int? batchId)
        {
            var students = new List<SelectListItem>();
            if (batchId.HasValue && batchId > 0)
            {
                students = await (
                    from e in _context.StudentBatchEnrollments
                    join b in _context.Batches on e.BatchId equals b.Id
                    join s in _context.Students on e.StudentID equals s.StudentID
                    where e.BatchId == batchId.Value
                          && !b.IsDeleted
                          && !b.IsArchived
                    select new SelectListItem
                    {
                        Value = s.StudentID.ToString(),
                        Text = s.FullName
                    }).ToListAsync();
            }

            var vm = new CreateModelExamViewModel
            {
                BatchId = batchId ?? 0,
                Models  = await GetModelsSelectListAsync(ProfessionalModelType.Exam, null),
                Batches = await _context.Batches
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .OrderBy(b => b.Name)
                    .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                    .ToListAsync(),
                Curriculums = await GetCurriculumsSelectListAsync(),
                Students    = students,
                StartAt     = DateTime.Now,
                EndAt       = DateTime.Now.AddDays(1),
                DurationMinutes = 30
            };

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "ManageModelExam")]

        public async Task<IActionResult> CreateModelExam(CreateModelExamViewModel vm)
        {
            try
            {
                if (vm.SelectedBatchIds == null || !vm.SelectedBatchIds.Any())
                {
                    TempData["Error"] = "⚠️ لم يتم اختيار أي دفعات.";
                    return RedirectToAction(nameof(CreateModelExam));
                }

                var availableBatchIds = await _context.Batches
                    .AsNoTracking()
                    .Where(b => !b.IsDeleted && !b.IsArchived)
                    .Select(b => b.Id)
                    .ToListAsync();

                vm.SelectedBatchIds = vm.SelectedBatchIds
                    .Where(batchId => availableBatchIds.Any(id => id == batchId))
                    .Distinct()
                    .ToList();

                if (!vm.SelectedBatchIds.Any())
                {
                    TempData["Error"] = "⚠️ الدفعات المحددة غير متاحة أو مؤرشفة.";
                    return RedirectToAction(nameof(CreateModelExam));
                }

                if (vm.ModelId <= 0)
                {
                    TempData["Error"] = "⚠️ لم يتم اختيار النموذج.";
                    return RedirectToAction(nameof(CreateModelExam));
                }

                // 🔹 اجلب النموذج
                var model = await _context.ProfessionalModels
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == vm.ModelId && !m.IsArchived);

                if (model == null)
                {
                    TempData["Error"] = "❌ النموذج المحدد غير موجود.";
                    return RedirectToAction(nameof(CreateModelExam));
                }

                // 🔹 اجلب الأسئلة المرتبطة بالنموذج
                var modelQuestions = await _context.ProfessionalModelQuestions
                    .Where(mq => mq.ModelId == model.Id)
                    .Select(mq => new { mq.QuestionId, mq.OrderNumber })
                    .AsNoTracking()
                    .ToListAsync();

                if (!modelQuestions.Any())
                {
                    TempData["Error"] = "❌ النموذج لا يحتوي على أسئلة مرتبطة.";
                    return RedirectToAction(nameof(CreateModelExam));
                }

                // ✅ تحديد وقت البدء والانتهاء بشكل مضمون
                DateTime utcStart = vm.StartAt.HasValue
                    ? _timeZoneService.ConvertToUtc(vm.StartAt.Value)
                    : _timeZoneService.GetNowUtc();

                DateTime utcEnd = vm.EndAt.HasValue
                    ? _timeZoneService.ConvertToUtc(vm.EndAt.Value)
                    : utcStart.AddMinutes(vm.DurationMinutes);

                // 🔹 إنشاء الاختبار الرئيسي
                var exam = new Exam
                {
                    Title = string.IsNullOrEmpty(vm.Title)
             ? $"اختبار من النموذج {model.Title}"
             : vm.Title.Trim(),
                    Type = ExamType.Manual,
                    TotalQuestions = modelQuestions.Count,
                    DurationMinutes = vm.DurationMinutes,
                    CreatedAt = _timeZoneService.GetNowUtc(),
                    IsActive = true,
                    IsFromProfessionalModel = true,
                    ReferenceCode = vm.IsInLab ? GenerateNumericReferenceCode() : null,
                    RandomizeQuestions = vm.RandomizeQuestions
                };



                _context.Exams.Add(exam);
                await _context.SaveChangesAsync();

                // 🔹 إنشاء الربط مع الدفعات
                foreach (var batchId in vm.SelectedBatchIds)
                {
                    var assignment = new ExamAssignmentToBatch
                    {
                        ExamId = exam.Id,
                        BatchId = batchId,
                        Title = exam.Title,
                        CreatedAt = _timeZoneService.GetNowUtc(),
                        ScheduledDate = utcStart,
                        EndAt = utcEnd,
                        DurationMinutes = vm.DurationMinutes,
                        TotalQuestions = exam.TotalQuestions,
                        IsSentToStudents = true,
                        IsOnline = !vm.IsInLab,
                        IsInLab = vm.IsInLab
                    };


                    _context.ExamAssignmentsToBatches.Add(assignment);
                    await _context.SaveChangesAsync();

                    // 🔹 إضافة الأسئلة
                    int order = 1;
                    foreach (var q in modelQuestions.OrderBy(x => x.OrderNumber))
                    {
                        _context.ExamQuestions.Add(new ExamQuestion
                        {
                            ExamId = exam.Id,
                            ExamAssignmentId = assignment.Id,
                            QuestionId = q.QuestionId ?? Guid.Empty,
                            Order = order++,
                            IsManuallySelected = true
                        });
                    }
                    await _context.SaveChangesAsync();

                    // ✅ جلب الطلاب بناءً على طريقة الإرسال
                    List<int> studentIds;
                    if (vm.SendToBatch)
                    {
                        studentIds = await _context.StudentBatchEnrollments
                            .Where(e => e.BatchId == batchId)
                            .Select(e => e.StudentID)
                            .ToListAsync();
                    }
                    else
                    {
                        studentIds = await (
                            from e in _context.StudentBatchEnrollments
                            join s in vm.SelectedStudentIds on e.StudentID equals s
                            where e.BatchId == batchId
                            select e.StudentID
                        ).ToListAsync();
                    }

                    // ✅ ربط الاختبار بالطلاب المحددين
                    await _curriculumExamGeneratorService.AssignExamToSpecificStudentsAsync(assignment.Id, studentIds);

                    // ✅ إرسال إشعار للطلاب
                    if (studentIds.Any())
                    {
                        await _notificationService.SendToStudentsAsync(
                            studentIds,
                            $"📘 تم إرسال اختبار جديد من النموذج {model.Title}. ينتهي في {utcEnd:yyyy-MM-dd HH:mm}.",
                            NotificationCategory.Exam,
                            "/Students/Exams");
                    }

                    // ✅ إشعار المدرب
                    var instructorId = await _context.InstructorCurriculumBatches
                        .Where(x => x.BatchId == batchId)
                        .Select(x => x.InstructorId)
                        .FirstOrDefaultAsync();

                    if (instructorId > 0)
                    {
                        await _notificationService.SendToInstructorAsync(
                            instructorId,
                            $"📢 تم إرسال اختبار من النموذج {model.Title} للدفعة {batchId}.",
                            NotificationCategory.Exam,
                            "/Instructors/ExamDashboard");
                    }
                }

                TempData["Success"] = "✅ تم إنشاء وإرسال اختبار النموذج بنجاح.";
                return RedirectToAction("ModelAssignments");
            }
            catch (Exception ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = "❌ حدث خطأ أثناء توليد الاختبار: " + message;
                return RedirectToAction(nameof(CreateModelExam));
            }
        }






        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> ConfirmHomework(int batchId, int? lectureId = null)
        {
            var vm = await _lessonCompletionService.PrepareConfirmHomeworkViewModelAsync(batchId, lectureId);

            if (vm == null || !vm.Lessons.Any())
            {
                TempData["Error"] = "❌ لا توجد مؤشرات مكتملة لهذه المحاضرة تحتوي على أسئلة مراجعة صالحة.";
                return RedirectToAction("SelectLessons", new { batchId });
            }

            // ✅ تعبئة الأسئلة لكل مؤشر
            foreach (var lesson in vm.Lessons)
            {
                lesson.SelectedQuestions = await _context.Questions
                    .Where(q => q.LessonId == lesson.LessonId && q.IsReviewed)
                    .OrderBy(q => q.Id)
                    .Take(lesson.QuestionsToUse > 0 ? lesson.QuestionsToUse : 5) // الافتراضي 5 أسئلة لو العدد 0
                    .Select(q => new QuestionSummaryViewModel
                    {
                        QuestionId = q.Id,
                        Title = q.Title,
                        Difficulty = q.Difficulty.ToString(), // 🔁 التحويل إلى string
                        IsReviewed = q.IsReviewed
                    })
                    .ToListAsync();
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "GenerateHomework")]
        public async Task<IActionResult> ConfirmHomework(ConfirmHomeworkViewModel vm, int? lectureId = null)
        {
            try
            {
                if (vm == null || vm.Lessons == null || !vm.Lessons.Any())
                {
                    TempData["Error"] = "❌ لم يتم تمرير أي مؤشرات لتوليد الواجب.";
                    return RedirectToAction("SelectLessons", new { batchId = vm?.BatchId });
                }

                // ✅ التحقق من وجود أسئلة فعلية
                var totalQuestionsToUse = vm.Lessons.Sum(l => l.QuestionsToUse);
                if (totalQuestionsToUse == 0 && !vm.ForceGenerateAnyway)
                {
                    TempData["Error"] = "⚠️ لا توجد أسئلة مراد إرسالها. الرجاء التأكد من تحديد عدد الأسئلة.";
                    TempData["AllowForceGenerate"] = true;
                    return View(vm);
                }

                // ✅ ضبط التوقيتات إن لم تُحدّد
                if (vm.StartAt == null)
                    vm.StartAt = _timeZoneService.GetNowUtc();
                if (vm.EndAt == null)
                    vm.EndAt = _timeZoneService.GetNowUtc().AddDays(3);

                // ✅ تنفيذ المنطق الأساسي لتوليد الواجب
                var result = await _lessonCompletionService.GenerateHomeworksAsync(vm, vm.ForceGenerateAnyway);

                if (result)
                {
                    // ✅ سجل نشاط المشرف
                    await _logger.LogAsync(
                        actionType: "إرسال واجب",
                        description: $"تم إرسال واجب المؤشرات للدفعة {vm.BatchId}",
                        adminId: User.FindFirstValue(ClaimTypes.NameIdentifier),
                        adminName: User.Identity?.Name ?? "غير معروف"
                    );

                    // ✅ إشعار الطلاب
                    var studentIds = await _context.StudentBatchEnrollments
                        .Where(s => s.BatchId == vm.BatchId)
                        .Select(s => s.StudentID)
                        .ToListAsync();

                    await _notificationService.SendToStudentsAsync(
                        studentIds,
                        "📘 تم إرسال واجب جديد إلى دفعتك، الرجاء البدء في حله.",
                        NotificationCategory.Homework,
                        "/Students/Homeworks"
                    );

                    // ✅ إشعار المدرب
                    var instructorId = await _context.InstructorCurriculumBatches
                        .Where(icb => icb.BatchId == vm.BatchId)
                        .Select(icb => icb.InstructorId)
                        .FirstOrDefaultAsync();

                    if (instructorId > 0)
                    {
                        await _notificationService.SendToInstructorAsync(
                            instructorId,
                            $"📢 تم إرسال واجب جديد للدفعة {vm.BatchId}.",
                            NotificationCategory.Homework,
                            "/Instructors/HomeworkDashboard"
                        );
                    }

                    // ✅ توليد اختبارات تلقائية للمحاور المكتملة
                    var distinctSections = vm.Lessons.Select(l => l.SectionId).Distinct().ToList();
                    foreach (var secId in distinctSections)
                    {
                        if (secId > 0)
                        {
                            var examAssignmentId = await _autoExamGenerationService
                                .TryGenerateExamIfSectionCompletedAsync(vm.BatchId, secId);

                            if (examAssignmentId > 0)
                                TempData["Info"] = $"📘 تم توليد اختبار تلقائي للمحور رقم {secId}.";
                        }
                    }

                    // ✅ إنشاء جلسة تعزيزية مرتبطة بالمحاضرة (إن لم تكن موجودة)
                    if (lectureId.HasValue)
                    {
                        var existingSet = await _context.EnhancementSkillSets
                            .FirstOrDefaultAsync(s => s.BatchId == vm.BatchId && s.LectureId == lectureId.Value);

                        if (existingSet == null)
                        {
                            var skillSet = new EnhancementSkillSet
                            {
                                BatchId = vm.BatchId,
                                LectureId = lectureId.Value,
                                Title = $"مهارات تعزيزية - محاضرة {lectureId}",
                                CreatedAt = _timeZoneService.GetNowUtc()
                            };
                            _context.EnhancementSkillSets.Add(skillSet);
                            await _context.SaveChangesAsync();
                        }
                    }

                    TempData["Success"] = "✅ تم توليد الواجبات بنجاح وإرسالها للطلاب.";
                    return RedirectToAction("SelectLessons", new { batchId = vm.BatchId });
                }
                else
                {
                    TempData["Error"] = "⚠️ فشل في توليد الواجب. تأكد من وجود أسئلة مراجعة كافية.";
                    return View(vm);
                }
            }
            catch (Exception ex)
            {
                var fullMessage = ex.InnerException?.Message ?? ex.Message;
                TempData["Error"] = "❌ خطأ داخلي: " + fullMessage;
                return RedirectToAction("SelectLessons", new { batchId = vm?.BatchId ?? 0 });
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions","Enhancement")]
        public async Task<IActionResult> SendEnhancementSkills(int setId)
        {
            var set = await _context.EnhancementSkillSets
                .Include(s => s.Batch)
                .FirstOrDefaultAsync(s => s.Id == setId);

            if (set == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على الجلسة.";
                return RedirectToAction("EnhancementIndex");
            }

            // 🔹 جلب الطلاب في الدفعة
            var studentIds = await _context.StudentBatchEnrollments
                .Where(e => e.BatchId == set.BatchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            if (!studentIds.Any())
            {
                TempData["Error"] = "⚠️ لا يوجد طلاب في هذه الدفعة.";
                return RedirectToAction("EnhancementIndex");
            }

            // 🔹 جلب الأسئلة (افتراضيًا 25 سؤال أو عدد مخصص)
            // 1️⃣ هات الـ lessonIds من قاعدة البيانات
            var lessonIds = await _context.Lessons
                .Where(l => l.SectionId == _context.Lecture
                    .Where(le => le.Id == set.LectureId)
                    .Select(le => le.SectionId)
                    .FirstOrDefault())
                .Select(l => l.Id)
                .ToListAsync();

            // 2️⃣ هات الأسئلة كلها المرتبطة بالـ UsageType (من قاعدة البيانات فقط)
            var allQuestions = await _context.Questions
                .Where(q => (q.UsageTypes & QuestionUsageType.Enhancement) == QuestionUsageType.Enhancement)
                .ToListAsync();

            // 3️⃣ فلترة الأسئلة في الذاكرة بالـ lessonIds
            var questions = allQuestions
                .Where(q => lessonIds.Contains(q.LessonId))
                .OrderBy(r => Guid.NewGuid()) // عشوائي
                .Take(25) // 👈 العدد الافتراضي
                .ToList();

            foreach (var studentId in studentIds)
            {
                foreach (var q in questions)
                {
                    _context.EnhancementSkillAssignments.Add(new EnhancementSkillAssignment
                    {
                        EnhancementSkillSetId = set.Id,
                        StudentId = studentId,
                        QuestionId = q.Id
                    });
                }
            }

            await _context.SaveChangesAsync();

            // ✅ إشعار الطلاب
            await _notificationService.SendToStudentsAsync(
                studentIds,
                $"📘 تم إرسال تدريب تعزيز مهاري جديد مرتبط بالمحاضرة {set.LectureId}",
                NotificationCategory.Homework,
                "/Students/EnhancementSkills");

            TempData["Success"] = "✅ تم إرسال الجلسة التعزيزية للطلاب.";
            return RedirectToAction("EnhancementIndex");
        }


        [HttpPost]
        [AdminPermission("AdminLessonCompletions", "Enhancement")]
        public async Task<IActionResult> ResendToStudent(int setId, int studentId, int questionsCount)
        {
            var set = await _context.EnhancementSkillSets.FindAsync(setId);
            if (set == null) return NotFound();

            var lessonIds = await _context.Lessons
                .Where(l => l.Section.Curriculum.CourseCurriculums.Any(cc => cc.CourseId == set.Batch.CourseId))
                .Select(l => l.Id)
                .ToListAsync();

            var questions = await _context.Questions
                .Where(q => lessonIds.Contains(q.LessonId) &&
                            (q.UsageTypes & QuestionUsageType.Enhancement) == QuestionUsageType.Enhancement)
                .OrderBy(x => Guid.NewGuid())
                .Take(questionsCount)
                .ToListAsync();

            foreach (var q in questions)
            {
                _context.EnhancementSkillAssignments.Add(new EnhancementSkillAssignment
                {
                    EnhancementSkillSetId = setId,
                    StudentId = studentId,
                    QuestionId = q.Id
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إعادة إرسال التدريب للطالب.";
            return RedirectToAction("EnhancementDetails", new { id = setId });
        }

        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> EnhancementIndex()
        {
            var sets = await _context.EnhancementSkillSets
                .Include(s => s.Batch)
                .Include(s => s.Lecture)
                .Select(s => new QdratNew.ViewModels.EnhancementSkills.EnhancementSkillSetViewModel
                {
                    Id = s.Id,
                    BatchName = s.Batch.Name,
                    LectureTitle = s.Lecture.Title,
                    Title = s.Title,
                    CreatedAt = s.CreatedAt,
                    StudentsCount = s.Assignments.Count(),
                    IsSent = s.Assignments.Any()
                })
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            return View(sets);
        }


        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> EnhancementDetails(int id)
        {
            var skillSet = await _context.EnhancementSkillSets
                .Include(s => s.Lecture)
                .Include(s => s.Batch)
                .Include(s => s.Assignments)
                .ThenInclude(a => a.Student)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (skillSet == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على جلسة المهارات التعزيزية.";
                return RedirectToAction("EnhancementIndex");
            }

            // لو مفيش Assignments يبقى لسه في Draft
            ViewBag.CanSend = !(skillSet.Assignments?.Any() ?? false);

            return View(skillSet);
        }



        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "Enhancement")]
        public async Task<IActionResult> PickEnhancementQuestions(int setId)
        {
            var set = await _context.EnhancementSkillSets
                .Include(s => s.Batch)
                .Include(s => s.Lecture)
                .FirstOrDefaultAsync(s => s.Id == setId);

            if (set == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على جلسة المهارات التعزيزية.";
                return RedirectToAction("EnhancementIndex");
            }


            // 1️⃣ هات الـ Ids للمؤشرات المكتملة
            var completedLessonIds = await _context.BatchLessonCompletions
                .Where(lc => lc.LectureId == set.LectureId && lc.BatchId == set.BatchId)
                .Select(lc => lc.LessonId)
                .Distinct()
                .ToListAsync();

            // 2️⃣ جلب الأسئلة باستخدام join على المؤشرات المكتملة (من غير Contains)
            var questionIdsQuery = from q in _context.Questions
                                   join lId in completedLessonIds on q.LessonId equals lId
                                   where (q.UsageTypes & QuestionUsageType.Enhancement) == QuestionUsageType.Enhancement
                                   select q.Id;

            //var questionIds = await questionIdsQuery.ToListAsync();

            // 3️⃣ استعلام منفصل لجلب التفاصيل + Include
            // ✅ جلب الأسئلة مباشرة بالانضمام على BatchLessonCompletions
            var availableQuestions = await (
                from q in _context.Questions
                join lc in _context.BatchLessonCompletions
                    on q.LessonId equals lc.LessonId
                where lc.LectureId == set.LectureId
                      && lc.BatchId == set.BatchId
                      && (q.UsageTypes & QuestionUsageType.Enhancement) == QuestionUsageType.Enhancement
                select q
            )
            .Include(q => q.Section)
            .Include(q => q.Lesson)
            .ToListAsync();


            // 3️⃣ تجهيز الـ ViewModel
            var vm = new PickEnhancementQuestionsViewModel
            {
                SetId = set.Id,
                BatchId = set.BatchId,
                BatchName = set.Batch?.Name ?? "",
                LectureTitle = set.Lecture?.Title ?? "",
                AvailableQuestions = availableQuestions.Select(q => new PickEnhancementQuestionsViewModel.QuestionItem
                {
                    QuestionId = q.Id,
                    ReferenceNumber = q.ReferenceNumber ?? "",
                    Title = q.Title ?? "",
                    LessonTitle = q.Lesson?.Title ?? "",
                    SectionTitle = q.Section?.Title ?? "",
                    IsSelected = false
                }).ToList()
            };

            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "Enhancement")]
        public async Task<IActionResult> PickEnhancementQuestions(PickEnhancementQuestionsViewModel vm)
        {
            if (vm.SelectedQuestionIds == null || !vm.SelectedQuestionIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم اختيار أي أسئلة.";
                return RedirectToAction("PickEnhancementQuestions", new { setId = vm.SetId });
            }

            // جلب الطلاب
            var studentIds = await _context.StudentBatchEnrollments
                .Where(e => e.BatchId == vm.BatchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            foreach (var studentId in studentIds)
            {
                foreach (var qId in vm.SelectedQuestionIds)
                {
                    _context.EnhancementSkillAssignments.Add(new EnhancementSkillAssignment
                    {
                        EnhancementSkillSetId = vm.SetId,
                        StudentId = studentId,
                        QuestionId = qId
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم اختيار الأسئلة يدويًا وإرسالها للطلاب.";
            return RedirectToAction("EnhancementDetails", new { id = vm.SetId });
        }


        // تم نقل إنشاء المهارات التعزيزية للكنترولار المستقل EnhancementSkillsController
        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "Enhancement")]
        public IActionResult CreateEnhancementSet(int? batchId)
        {
            return RedirectToAction("Create", "EnhancementSkills", new { area = "Admin" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "Enhancement")]
        public IActionResult CreateEnhancementSetPost()
        {
            return RedirectToAction("Create", "EnhancementSkills", new { area = "Admin" });
        }





        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<JsonResult> GetSectionsByBatch(int batchId)
        {
            var courseId = await _context.Batches
                .Where(b => b.Id == batchId && !b.IsDeleted && !b.IsArchived)
                .Select(b => b.CourseId)
                .FirstOrDefaultAsync();

            var sections = await _context.Sections
                             .Where(s => s.Curriculum.CourseCurriculums
                                 .Any(cc => cc.CourseId == courseId))
                             .Select(s => new
                             {
                                 id = s.Id,
                                 title = s.Title
                             })
                             .ToListAsync();


            return Json(sections);
        }

        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<JsonResult> GetLecturesByBatchAndSection(int batchId, int sectionId)
        {
            var lectures = await _context.Lecture
                .Where(l => l.BatchId == batchId && l.SectionId == sectionId)
                .OrderByDescending(l => l.Date)
                .Select(l => new
                {
                    id = l.Id,
                    title = l.Title,
                    date = l.Date.ToString("yyyy-MM-dd")
                }).ToListAsync();

            return Json(lectures);
        }

        [HttpGet]
        [AdminPermission("AdminLessonCompletions","Read")]
        public async Task<JsonResult> GetLessonsBySection(int sectionId)
        {
            var lessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .Select(l => new
                {
                    id = l.Id,
                    title = l.Title
                }).ToListAsync();

            return Json(lessons);
        }

        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<JsonResult> GetLecturesByBatch(int batchId)
        {
            var lectures = await _context.Lecture
                .Where(l => l.BatchId == batchId)
                .OrderByDescending(l => l.Date)
                .Select(l => new
                {
                    id = l.Id,
                    title = l.Title,
                    date = l.Date.ToString("yyyy-MM-dd")
                }).ToListAsync();

            return Json(lectures);
        }


     
        // الدورات المتاحة (يمكنك فلترتها حسب صلاحياتك)
        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]

        public async Task<JsonResult> GetCourses()
        {
            var courses = await _context.Courses
                .Select(c => new { id = c.Id, title = c.Name })
                .OrderBy(c => c.title)
                .ToListAsync();
            return Json(courses);
        }

        // مناهج دورة
        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<JsonResult> GetCurriculumsByCourse(int courseId)
        {
            var curriculums = await _context.CourseCurriculums
      .Where(cc => cc.CourseId == courseId)
      .Select(cc => new
      {
          id = cc.Curriculum.Id,
          title = cc.Curriculum.Title
      })
      .OrderBy(c => c.title)
      .ToListAsync();

            return Json(curriculums);

        }

        // محاور المنهج
        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<JsonResult> GetSectionsByCurriculum(int curriculumId)
        {
            var sections = await _context.Sections
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => new { id = s.Id, title = s.Title })
                .OrderBy(s => s.title)
                .ToListAsync();
            return Json(sections);
        }

        // دُفعات الدورة
        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<JsonResult> GetBatchesByCourse(int courseId)
        {
            var batches = await _context.Batches
                .Where(b => b.CourseId == courseId && !b.IsDeleted && !b.IsArchived)
                .Select(b => new { id = b.Id, title = b.Name })
                .OrderBy(b => b.title)
                .ToListAsync();
            return Json(batches);
        }

        // طلاب الدفعة (باستخدام StudentBatchEnrollments أو Student.BatchId)
        // نستخدم StudentID الصحيح حسب تصميم Student
    


        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> CreateExtraHomework()
        {
            var batches = await _context.Batches
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();

            var vm = new CreateExtraHomeworkViewModel
            {
                Title = $"واجب إضافي - {DateTime.Now:yyyyMMdd}",
                StartAt = DateTime.Now,
                EndAt = DateTime.Now.AddDays(3),
                AllowRetake = true,
                MaxRetakes = 2,
                KeepSameQuestionsOnRetake = true,
                Batches = batches
            };

            return View(vm);
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "GenerateHomework")]
        public async Task<IActionResult> CreateExtraHomework(CreateExtraHomeworkViewModel vm)
        {
            if (vm == null || vm.SelectedBatchIds == null || !vm.SelectedBatchIds.Any())
            {
                ModelState.AddModelError("", "الرجاء اختيار دفعة واحدة على الأقل.");
                await PopulateCreateExtraHomeworkListsAsync(vm);
                return View(vm);
            }

            var selectedSectionIds = vm.Sections
                .Where(s => s.Selected)
                .Select(s => s.SectionId)
                .ToList();

            if (!selectedSectionIds.Any())
            {
                ModelState.AddModelError("", "اختر محورًا واحدًا على الأقل.");
                await PopulateCreateExtraHomeworkListsAsync(vm);
                return View(vm);
            }

            var availableBatchIds = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .Select(b => b.Id)
                .ToListAsync();

            vm.SelectedBatchIds = vm.SelectedBatchIds
                .Where(batchId => availableBatchIds.Any(id => id == batchId))
                .Distinct()
                .ToList();

            if (!vm.SelectedBatchIds.Any())
            {
                ModelState.AddModelError("", "الدفعات المحددة غير متاحة أو مؤرشفة.");
                await PopulateCreateExtraHomeworkListsAsync(vm);
                return View(vm);
            }

            // 🕐 تحويل التوقيت إلى UTC
            if (vm.StartAt.HasValue)
                vm.StartAt = _timeZoneService.ConvertToUtc(vm.StartAt.Value);
            if (vm.EndAt.HasValue)
                vm.EndAt = _timeZoneService.ConvertToUtc(vm.EndAt.Value);

            // ✅ التحضير للحفظ
            var allHomeworkSets = new List<HomeworkSet>();
            var allSections = new List<HomeworkSetSection>();
            var allStudents = new List<HomeworkSetStudent>();

            foreach (var batchId in vm.SelectedBatchIds)
            {
                // 🔹 جلب الطلاب المختارين يدويًا في الواجهة (من الـ checkboxes)
                var manuallySelectedStudents = vm.SelectedStudentIds ?? new List<int>();

                // 🔹 استخراج طلاب الدفعة من قاعدة البيانات
                var batchStudents = await _context.StudentBatchEnrollments
                    .Where(e => e.BatchId == batchId)
                    .Select(e => e.StudentID)
                    .ToListAsync();

                // 🧩 حدد فقط الطلاب الموجودين في الدفعة والمختارين فعليًا
                var finalStudentIds = manuallySelectedStudents
                    .Where(id => batchStudents.Contains(id))
                    .ToList();

                // ⚠️ لو لم يتم تحديد أي طلاب يدويًا → استخدم جميع طلاب الدفعة كافتراض
                if (!finalStudentIds.Any())
                    finalStudentIds = batchStudents;

                if (!finalStudentIds.Any())
                    continue;

                var set = new HomeworkSet
                {
                    BatchId = batchId,
                    Title = vm.Title.Trim(),
                    CompletionTitle = vm.Title.Trim(),
                    IsExtra = true,
                    StartAt = vm.StartAt,
                    EndAt = vm.EndAt,
                    AllowRetake = vm.AllowRetake,
                    MaxRetakes = vm.MaxRetakes,
                    KeepSameQuestionsOnRetake = vm.KeepSameQuestionsOnRetake,
                    AssignedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                };

                allHomeworkSets.Add(set);

                // ربط المحاور
                foreach (var sectionId in selectedSectionIds)
                    allSections.Add(new HomeworkSetSection { HomeworkSet = set, SectionId = sectionId });

                // ربط الطلاب الفعليين
                foreach (var sid in finalStudentIds)
                    allStudents.Add(new HomeworkSetStudent { HomeworkSet = set, StudentId = sid });
            }

            // ✅ الحفظ
            _context.HomeworkSets.AddRange(allHomeworkSets);
            await _context.SaveChangesAsync();

            _context.HomeworkSetSections.AddRange(allSections);
            _context.HomeworkSetStudents.AddRange(allStudents);
            await _context.SaveChangesAsync();

            // 🧠 توليد الواجب فعليًا
            foreach (var set in allHomeworkSets)
            {
                var selectedSectionIdsForSet = allSections
                    .Where(s => s.HomeworkSetId == set.Id)
                    .Select(s => s.SectionId)
                    .ToList();

                var studentIds = allStudents
                    .Where(s => s.HomeworkSetId == set.Id)
                    .Select(s => s.StudentId)
                    .ToList();

                var generated = await _lessonCompletionService.GenerateHomeworksForExtraSetAsync(
                    set.Id, selectedSectionIdsForSet, studentIds, questionsPerStudent: vm.QuestionsPerStudent);

                if (!generated)
                    continue;

                // إشعار الطلاب
                await _notificationService.SendToStudentsAsync(
                    studentIds,
                    $"📘 تم إرسال '{set.Title}'. آخر موعد: {(set.EndAt.HasValue ? set.EndAt.Value.ToString("yyyy-MM-dd HH:mm") : "غير محدد")}.",
                    NotificationCategory.Homework,
                    "/Students/Homeworks"
                );

                // إشعار المدرب
                var instructorId = await _context.InstructorCurriculumBatches
                    .Where(x => x.BatchId == set.BatchId)
                    .Select(x => x.InstructorId)
                    .FirstOrDefaultAsync();

                if (instructorId > 0)
                {
                    await _notificationService.SendToInstructorAsync(
                        instructorId,
                        $"📢 تم إرسال واجب إضافي '{set.Title}' للدفعة {set.BatchId}.",
                        NotificationCategory.Homework,
                        "/Instructors/HomeworkDashboard"
                    );
                }
            }

            TempData["SuccessMessage"] = "✅ تم إنشاء الواجب الإضافي وإرساله للطلاب المحددين بنجاح.";
            return RedirectToAction("Index", "HomeworkManagement", new { area = "Admin" });
        }

        private async Task PopulateCreateExtraHomeworkListsAsync(CreateExtraHomeworkViewModel? vm)
        {
            if (vm == null)
            {
                return;
            }

            vm.Batches = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted && !b.IsArchived)
                .OrderBy(b => b.Name)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                })
                .ToListAsync();
        }



        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> GetStudentsByBatch(int batchId)
        {
            var students = await _context.StudentBatchEnrollments
                .Join(_context.Batches,
                      e => e.BatchId,
                      b => b.Id,
                      (e, b) => new { e, b })
                .Where(x => x.e.BatchId == batchId && !x.b.IsDeleted && !x.b.IsArchived)
                .Join(_context.Students,
                      x => x.e.StudentID,
                      s => s.StudentID,
                      (x, s) => new
                      {
                          studentID = s.StudentID,
                          fullName = s.FullName
                      })
                .OrderBy(s => s.fullName)
                .ToListAsync();

            return Json(students);
        }




        [HttpPost]
[AdminPermission("AdminLessonCompletions", "GenerateHomework")]
        public async Task<IActionResult> GenerateEnhancementSkills(int batchId, int lectureId, List<int> lessonIds)
        {
            // 1. إنشاء SkillSet
            var set = new EnhancementSkillSet
            {
                BatchId = batchId,
                LectureId = lectureId,
                Title = $"مهارات تعزيزية - محاضرة {lectureId}"
            };
            _context.EnhancementSkillSets.Add(set);
            await _context.SaveChangesAsync();

            // 2. اختيار الطلاب في الدفعة
            var studentIds = await _context.StudentBatchEnrollments
                .Where(e => e.BatchId == batchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            // 3. اختيار أسئلة من الدروس
            var questions = await _context.Questions
                .Where(q => lessonIds.Contains(q.LessonId))
                .OrderBy(x => Guid.NewGuid()) // عشوائي
                .Take(5) // مثال: 5 أسئلة
                .ToListAsync();

            // 4. توزيع الأسئلة على الطلاب
            foreach (var studentId in studentIds)
            {
                foreach (var q in questions)
                {
                    _context.EnhancementSkillAssignments.Add(new EnhancementSkillAssignment
                    {
                        EnhancementSkillSetId = set.Id,
                        StudentId = studentId,
                        QuestionId = q.Id
                    });
                }
            }

            await _context.SaveChangesAsync();

            // إشعار الطلاب
            await _notificationService.SendToStudentsAsync(
                studentIds,
                $"📘 تم إرسال تدريب تعزيز مهاري جديد مرتبط بالمحاضرة {lectureId}",
                NotificationCategory.Homework,
                "/Students/EnhancementSkills");

            TempData["Success"] = "✅ تم توليد التدريب التعزيزي للطلاب.";
            return RedirectToAction("SelectLessons", new { batchId });
        }




        [HttpGet]
[AdminPermission("AdminLessonCompletions", "Read")]
        public async Task<IActionResult> CompletedSections(int batchId)
        {
            // 1️⃣ جلب البيانات من الخدمة القديمة
            var summaries = await _lessonCompletionService.GetCompletedSectionsForBatchAsync(batchId);

            // 2️⃣ تحويل البيانات إلى ViewModel الجديد الخاص بالاختبارات
            var sectionData = summaries.Select(s => new CompletedSectionItemViewModel
            {
                SectionId = s.SectionId ?? 0, // ✅ معالجة Nullable إلى int
                SectionTitle = s.SectionTitle,
                CompletedLessonsCount = s.CompletedLessonsCount,
                LastLectureTitle = s.LastLectureTitle,
                LastLectureDate = s.LastLectureDate
            }).ToList();

            // 3️⃣ جلب اسم الدفعة
            var batchName = await _context.Batches
                .Where(b => b.Id == batchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            // 4️⃣ تجهيز الـ ViewModel النهائي للعرض
            var vm = new CompletedSectionsViewModel
            {
                BatchId = batchId,
                BatchName = batchName,
                CompletedSections = sectionData
            };

            return View(vm);
        }

        // ===================== حذف واجب نموذج =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "Delete")]
        public async Task<IActionResult> DeleteModelHomework(int id)
        {
            try
            {
                var set = await _context.HomeworkSets
                    .Include(hs => hs.Homeworks)
                    .FirstOrDefaultAsync(hs => hs.Id == id && hs.IsFromProfessionalModel);

                if (set == null)
                {
                    TempData["Error"] = "❌ الواجب غير موجود أو ليس من النماذج.";
                    return RedirectToAction(nameof(ModelAssignments));
                }

                // 🟡 تحقق هل هناك طلاب سلّموا الواجب فعلاً
                bool hasSubmitted = await _context.Homeworks
                    .AnyAsync(h => h.HomeworkSetId == set.Id && h.Status == HomeworkStatus.Submitted);

                if (hasSubmitted)
                {
                    TempData["Error"] = "⚠️ لا يمكن حذف هذا الواجب لأن بعض الطلاب قد سلّموه بالفعل.";
                    return RedirectToAction(nameof(ModelAssignments));
                }

                // ✅ حذف محاولات الأسئلة (HomeworkSetAttempts)
                var attempts = await _context.HomeworkSetAttempts
                    .Where(a => a.HomeworkSetId == set.Id)
                    .ToListAsync();
                if (attempts.Any())
                    _context.HomeworkSetAttempts.RemoveRange(attempts);

                // ✅ حذف ربط الطلاب بالواجب (HomeworkSetStudents)
                var setStudents = await _context.HomeworkSetStudents
                    .Where(s => s.HomeworkSetId == set.Id)
                    .ToListAsync();
                if (setStudents.Any())
                    _context.HomeworkSetStudents.RemoveRange(setStudents);

                // ✅ حذف ربط الواجب بالمؤشرات أو المحاور (HomeworkSetSections)
                var setSections = await _context.HomeworkSetSections
                    .Where(s => s.HomeworkSetId == set.Id)
                    .ToListAsync();
                if (setSections.Any())
                    _context.HomeworkSetSections.RemoveRange(setSections);

                // ✅ حذف الواجبات الفعلية للطلاب (Homeworks)
                if (set.Homeworks.Any())
                    _context.Homeworks.RemoveRange(set.Homeworks);

                await _context.SaveChangesAsync(); // نحفظ أولاً بعد حذف العلاقات

                // ✅ حذف الـ HomeworkSet نفسه الآن بأمان
                _context.HomeworkSets.Remove(set);
                await _context.SaveChangesAsync();

                TempData["Success"] = "✅ تم حذف واجب النموذج وكل البيانات المرتبطة به بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "⚠️ فشل في حذف واجب النموذج: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(ModelAssignments));
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "GenerateHomework")]
        public async Task<IActionResult> GenerateHomeworkForNewStudents(int homeworkSetId)
        {
            var set = await _context.HomeworkSets
                .Include(hs => hs.Homeworks)
                .FirstOrDefaultAsync(hs => hs.Id == homeworkSetId);

            if (set == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على الواجب المحدد.";
                return RedirectToAction("ModelAssignments");
            }

            // 1️⃣ جميع الطلاب الحاليين في الدفعة
            var allStudentIds = await _context.StudentBatchEnrollments
                .Where(e => e.BatchId == set.BatchId)
                .Select(e => e.StudentID)
                .ToListAsync();

            // 2️⃣ الطلاب الذين لديهم الواجب مسبقًا
            var existingStudentIds = set.Homeworks
                .Select(h => h.StudentId)
                .Distinct()
                .ToList();

            // 3️⃣ الطلاب الجدد فقط
            var newStudentIds = allStudentIds.Except(existingStudentIds).ToList();

            if (!newStudentIds.Any())
            {
                TempData["Info"] = "ℹ️ لا يوجد طلاب جدد في هذه الدفعة لم يتلقوا الواجب.";
                return RedirectToAction("ModelAssignments");
            }

            // 4️⃣ الأسئلة الأصلية في الواجب
            var questionData = set.Homeworks
                .Select(h => new { h.QuestionId, h.LessonId })
                .Distinct()
                .ToList();

            // 5️⃣ توليد نفس الأسئلة للطلاب الجدد
            foreach (var studentId in newStudentIds)
            {
                foreach (var q in questionData)
                {
                    _context.Homeworks.Add(new Homework
                    {
                        StudentId = studentId,
                        HomeworkSetId = set.Id,
                        QuestionId = q.QuestionId,
                        LessonId = q.LessonId,
                        AssignedAt = DateTime.UtcNow,
                        Status = HomeworkStatus.Pending,
                        IsSent = true
                    });
                }
            }

            await _context.SaveChangesAsync();

            // 6️⃣ إرسال إشعار للطلاب الجدد فقط
            await _notificationService.SendToStudentsAsync(
                newStudentIds,
                $"📘 تم إرسال الواجب السابق '{set.Title}' لك بعد انضمامك إلى الدفعة.",
                NotificationCategory.Homework,
                "/Students/Homeworks"
            );

            TempData["Success"] = $"✅ تم توليد الواجب ({set.Title}) بنجاح للطلاب الجدد ({newStudentIds.Count} طالب).";
            return RedirectToAction("ModelAssignments");
        }

   

        private string GenerateNumericReferenceCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }

        // ===================== حذف اختبار نموذج =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("AdminLessonCompletions", "Delete")]
        public async Task<IActionResult> DeleteModelExam(int id)
        {
            try
            {
                var assignment = await _context.ExamAssignmentsToBatches
                    .Include(ea => ea.Exam)
                    .FirstOrDefaultAsync(ea => ea.Id == id && ea.Exam.IsFromProfessionalModel == true);

                if (assignment == null)
                {
                    TempData["Error"] = "❌ لم يتم العثور على الاختبار أو ليس من النماذج.";
                    return RedirectToAction(nameof(ModelAssignments));
                }

                // 🟡 تحقق هل يوجد طلاب خاضوا الاختبار
                bool hasAttempts = await _context.ExamStudentStatuses
                    .AnyAsync(s => s.ExamAssignmentId == assignment.Id &&
                                   (s.IsSubmitted == true || s.Status == ExamStatus.Completed));

                if (hasAttempts)
                {
                    TempData["Error"] = "⚠️ لا يمكن حذف هذا الاختبار لأن بعض الطلاب قد خاضوه بالفعل.";
                    return RedirectToAction(nameof(ModelAssignments));
                }

                var examId = assignment.ExamId;

                // ✅ حذف كل الحالات الخاصة بالطلاب
                var statuses = await _context.ExamStudentStatuses
                    .Where(s => s.ExamId == examId)
                    .ToListAsync();
                _context.ExamStudentStatuses.RemoveRange(statuses);

                // ✅ حذف الأسئلة المرتبطة
                var questions = await _context.ExamQuestions
                    .Where(q => q.ExamId == examId)
                    .ToListAsync();
                _context.ExamQuestions.RemoveRange(questions);

                // ✅ حذف توزيع الأسئلة
                var counts = await _context.ExamCurriculumQuestionCounts
                    .Where(c => c.ExamAssignmentId == assignment.Id)
                    .ToListAsync();
                _context.ExamCurriculumQuestionCounts.RemoveRange(counts);

                // ✅ حذف التعيينات الثانوية
                var assignments = await _context.ExamAssignments
                    .Where(a => a.ExamId == examId)
                    .ToListAsync();
                _context.ExamAssignments.RemoveRange(assignments);

                // ✅ حذف كل الروابط مع الدفعات (جميع ExamAssignmentsToBatches لنفس الامتحان)
                var allBatchAssignments = await _context.ExamAssignmentsToBatches
                    .Where(ea => ea.ExamId == examId)
                    .ToListAsync();
                _context.ExamAssignmentsToBatches.RemoveRange(allBatchAssignments);

                await _context.SaveChangesAsync(); // نحفظ أولاً بعد حذف العلاقات

                // ✅ الآن نحذف الامتحان نفسه
                var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
                if (exam != null)
                {
                    _context.Exams.Remove(exam);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = "✅ تم حذف اختبار النموذج وكل الدفعات المرتبطة به بنجاح.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "⚠️ فشل في حذف الاختبار: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(ModelAssignments));
        }

        // ══════════════════════════════════════════════════════
        // AJAX: جلب النماذج حسب المنهج والنوع
        // ══════════════════════════════════════════════════════

        [HttpGet]
        [AdminPermission("AdminLessonCompletions", "ManageModelHomework")]
        public async Task<IActionResult> GetModelsByCurriculum(int? curriculumId, string modelType)
        {
            var type = modelType == "homework"
                ? ProfessionalModelType.Homework
                : ProfessionalModelType.Exam;

            var items = await GetModelsSelectListAsync(type, curriculumId);
            return Json(items.Select(x => new { value = x.Value, text = x.Text }));
        }

        // ── Private helpers ──────────────────────────────────

        private async Task<List<SelectListItem>> GetCurriculumsSelectListAsync()
        {
            var items = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync();

            items.Insert(0, new SelectListItem { Value = "", Text = "— كل المناهج —" });
            return items;
        }

        private async Task<List<SelectListItem>> GetModelsSelectListAsync(
            ProfessionalModelType type, int? curriculumId)
        {
            var query = _context.ProfessionalModels
                .AsNoTracking()
                .Where(m => !m.IsArchived && m.ModelType == type);

            if (curriculumId.HasValue && curriculumId.Value > 0)
                query = query.Where(m => m.CurriculumId == curriculumId.Value);

            var items = await query
                .OrderBy(m => m.Title)
                .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Title })
                .ToListAsync();

            items.Insert(0, new SelectListItem { Value = "", Text = "— اختر النموذج —" });
            return items;
        }
    }
}
