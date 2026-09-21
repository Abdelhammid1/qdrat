using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Admin;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Notifications;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Question;
using QdratNew.ViewModels.Reports;
using QdratNew.ViewModels.Section;
using QdratNew.ViewModels.Students;
using System.Security.Claims;
using static QdratNew.ViewModels.Exam.ExamAssignmentDetailsViewModel;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ExamAssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationCenterService _notificationService;
        private readonly ISystemSettingService _systemSettingService;
        private readonly IAdvancedNotificationService _advancedNotificationService;
        private readonly IExamRecommendationService _examRecommendationService;
        private readonly IStudentExamStatisticsService _studentExamStatisticsService;
        private readonly IEmployeeBatchAccessService _batchAccess;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly QdratNew.Services.Exams.Interfaces.IExamAssignmentIntegrityService _integrityService;
        private readonly IAdminActivityLogger _activityLogger;

        public ExamAssignmentsController(
            ApplicationDbContext context,
            INotificationCenterService notificationService,
            ISystemSettingService systemSettingService,
            IAdvancedNotificationService advancedNotificationService,
            IExamRecommendationService examRecommendationService,
            IStudentExamStatisticsService studentExamStatisticsService,
            IEmployeeBatchAccessService batchAccess,
            UserManager<ApplicationUser> userManager,
            QdratNew.Services.Exams.Interfaces.IExamAssignmentIntegrityService integrityService,
            IAdminActivityLogger activityLogger)
        {
            _context = context;
            _notificationService = notificationService;
            _systemSettingService = systemSettingService;
            _advancedNotificationService = advancedNotificationService;
            _examRecommendationService = examRecommendationService;
            _studentExamStatisticsService = studentExamStatisticsService;
            _batchAccess = batchAccess;
            _userManager = userManager;
            _integrityService = integrityService;
            _activityLogger = activityLogger;
        }

        // ─── Archive Access Helpers ──────────────────────────────────────────
        private bool IsExamArchiveOwner() =>
            User.IsInRole("Owner") || User.IsInRole("Developer");

        private string CurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private string CurrentUserName() =>
            User.Identity?.Name ?? "غير معروف";

        private async Task<List<ApplicationUser>> GetExamArchiveApprovalCandidatesAsync()
        {
            var roleNames = new[] { "Admin", "Employee", "SuperAdmin" };
            var usersById = new Dictionary<string, ApplicationUser>();
            foreach (var role in roleNames)
            {
                var users = await _userManager.GetUsersInRoleAsync(role);
                foreach (var user in users.Where(x => x.IsActive))
                    usersById[user.Id] = user;
            }
            return usersById.Values.ToList();
        }

        private async Task<List<ExamQuestion>> LoadExamQuestionsForAssignmentAsync(ExamAssignmentToBatch assignment)
        {
            var query = _context.ExamQuestions
                .AsNoTracking()
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Section)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Options)
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.VerbalPassage);

            var questions = await query
                .Where(eq => eq.ExamAssignmentId == assignment.Id)
                .OrderBy(eq => eq.Order)
                .ThenBy(eq => eq.Id)
                .ToListAsync();

            if (!questions.Any() && assignment.ExamId.HasValue)
            {
                var examId = assignment.ExamId.Value;
                questions = await query
                    .Where(eq => eq.ExamId == examId)
                    .OrderBy(eq => eq.Order)
                    .ThenBy(eq => eq.Id)
                    .ToListAsync();
            }

            return questions
                .Where(eq => eq.Question != null)
                .GroupBy(eq => eq.QuestionId)
                .Select(g => g.OrderBy(eq => eq.Order).ThenBy(eq => eq.Id).First())
                .ToList();
        }

        private async Task<List<QuestionAttemptNew>> LoadStudentAttemptsForAssignmentAsync(int assignmentId, int? examId, int studentId)
        {
            var attempts = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a => a.ExamAssignmentId == assignmentId && a.StudentId == studentId)
                .ToListAsync();

            if (!attempts.Any() && examId.HasValue)
            {
                var sourceExamId = examId.Value;
                attempts = await _context.QuestionAttemptNew
                    .AsNoTracking()
                    .Where(a => a.ExamId == sourceExamId && a.StudentId == studentId)
                    .ToListAsync();
            }

            return attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).ThenByDescending(a => a.Id).First())
                .ToList();
        }

        private int? GetQuestionSectionId(Question question)
        {
            return question.Lesson?.SectionId ?? question.SectionId;
        }

        private string GetQuestionSectionTitle(Question question)
        {
            return question.Lesson?.Section?.Title ?? question.Section?.Title ?? "غير محدد";
        }

        private bool IsAttemptSkipped(QuestionAttemptNew? attempt)
        {
            return attempt == null || string.IsNullOrWhiteSpace(attempt.SelectedAnswer);
        }



        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> Questions(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(e => e.Exam)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            var questions = await _context.ExamQuestions
                .Include(eq => eq.Question)
                .ThenInclude(q => q.Lesson)
                .ThenInclude(l => l.Section)
                .Where(eq => eq.ExamAssignmentId == id)
                .OrderBy(eq => eq.Order)
              .Select(eq => new ExamQuestionViewModel
              {
                  QuestionId = eq.QuestionId,
                  QuestionText = eq.Question.Title,
                  LessonTitle = eq.Question.Lesson.Title,
                  SectionTitle = eq.Question.Lesson.Section.Title,
                  Difficulty = eq.Question.Difficulty.ToString(), // ✅ حل المشكلة
                  IsManual = eq.IsManuallySelected,
                  Order = eq.Order
              }).ToListAsync();


            var vm = new ExamQuestionsListViewModel
            {
                AssignmentId = assignment.Id,
                ExamTitle = assignment.Title,
                Questions = questions
            };

            return View(vm);
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        }

        private async Task<string> GenerateBatchExamReferenceCodeAsync()
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var code = Random.Shared.Next(100000, 999999).ToString();
                var exists = await _context.Exams.AnyAsync(x => x.ReferenceCode == code)
                    || await _context.ExamAssignmentsToBatches.AnyAsync(x => x.ReferenceCode == code);

                if (!exists)
                    return code;
            }

            return DateTime.UtcNow.Ticks.ToString()[^8..];
        }


        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> SelectQuestions(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a => a.Id == id);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            var requiredCount = assignment.TotalQuestions;

            var availableQuestions = await _context.Questions
                .Include(q => q.Lesson)
                .ThenInclude(l => l.Section)
                .Where(q => q.CurriculumId == assignment.CurriculumId && q.IsReviewed && !q.IsRejected)
                .Select(q => new QuestionItemViewModel
                {
                    Id = q.Id,
                    Title = q.Title,
                    Difficulty = q.Difficulty,
                    LessonTitle = q.Lesson.Title,
                    SectionTitle = q.Lesson.Section.Title,
                    UsageTypes = q.UsageTypes
                }).ToListAsync();

            var vm = new ManualExamQuestionSelectionViewModel
            {
                ExamId = assignment.ExamId ?? 0,
                AssignmentId = assignment.Id,
                RequiredCount = requiredCount,
                AvailableQuestions = availableQuestions
            };

            return View(vm);
        }


   
        private async Task LoadDropdownLists()
        {
            ViewBag.Students = await _context.Students
                .Select(s => new SelectListItem { Value = s.StudentID.ToString(), Text = s.FullName })
                .ToListAsync();

            ViewBag.Curriculums = await _context.Curriculums
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                .ToListAsync();

            ViewBag.ProfessionalModels = await _context.ProfessionalModels
                .Where(m => !m.IsArchived && m.ModelType == QdratNew.Enums.ProfessionalModelType.Exam)
                .OrderBy(m => m.Title)
                .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Title })
                .ToListAsync();
        }

        private async Task<IActionResult> ReturnFormWithLists(CreateStudentExamPageVm page)
        {
            page.Students = await _context.Students
                .Select(s => new SelectListItem
                {
                    Value = s.StudentID.ToString(),
                    Text = s.FullName
                })
                .ToListAsync();

            page.Curriculums = await _context.Curriculums
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                })
                .ToListAsync();

            page.ProfessionalModels = await _context.ProfessionalModels
                .Where(m => !m.IsArchived && m.ModelType == QdratNew.Enums.ProfessionalModelType.Exam)
                .OrderBy(m => m.Title)
                .Select(m => new SelectListItem
                {
                    Value = m.Id.ToString(),
                    Text = m.Title
                })
                .ToListAsync();

            return View("CreateStudentExam", page);
        }


        [HttpGet]
        [AdminPermission("ExamAssignments", "Edit")]
        public async Task<IActionResult> EditStudentExam(int assignmentId)
        {

            var assignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null) return NotFound();

            var vm = new EditStudentExamVm
            {
                AssignmentId = assignment.Id,
                Title = assignment.Exam.Title,
                ScheduledDate = assignment.AssignedAt,
                EndAt = assignment.DueDate ?? assignment.AssignedAt.AddMinutes(assignment.Exam.DurationMinutes),
                DurationMinutes = assignment.Exam.DurationMinutes,
                ReferenceCode = assignment.Exam.ReferenceCode
            };

            return View(vm);
        }



        [HttpPost]
        [AdminPermission("ExamAssignments", "Edit")]
        public async Task<IActionResult> EditStudentExam(EditStudentExamVm vm)
        {

            var assignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == vm.AssignmentId);

            if (assignment == null) return NotFound();

            assignment.AssignedAt = vm.ScheduledDate;
            assignment.DueDate = vm.EndAt;

            assignment.Exam.Title = vm.Title;
            assignment.Exam.DurationMinutes = vm.DurationMinutes;
            assignment.Exam.ReferenceCode = vm.ReferenceCode;

            await _context.SaveChangesAsync();

            TempData["Success"] = "✔ تم حفظ التعديلات بنجاح";
            return RedirectToAction("Index", "ExamsAdmin");
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamAssignments", "Edit")]
        public async Task<IActionResult> SelectQuestions(ManualExamQuestionSelectionViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "⚠️ البيانات غير صحيحة.";
                return RedirectToAction("SelectQuestions", new { id = model.AssignmentId });
            }

            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == model.AssignmentId);

            if (assignment == null || assignment.ExamId == null)
            {
                TempData["Error"] = "❌ لم يتم العثور على الاختبار.";
                return RedirectToAction("Index");
            }

            // ==============================
            // حراسة: لا يجوز تعديل أسئلة تكليف دفعة له محاولات إجابة سابقة دون
            // تأكيد صريح + سبب إجباري (Sprint 2 — EB3)
            // ==============================
            var attemptSummary = await _integrityService.GetAttemptSummaryAsync(
                examAssignmentId: assignment.Id, examAssignmentToStudentId: null);

            if (attemptSummary.AttemptedQuestionsCount > 0 && !model.ConfirmResetAttempts)
            {
                TempData["Warning"] =
                    $"⚠️ هذا الاختبار له {attemptSummary.AttemptedQuestionsCount} إجابة مسجّلة عبر " +
                    $"{attemptSummary.AffectedStudentsCount} طالب/ة" +
                    (attemptSummary.IsSubmitted ? " (منها تسليمات مكتملة)" : "") +
                    ". اعتماد أسئلة جديدة الآن سيمسح محاولاتهم القديمة ويُعاد الاختبار من الصفر لكل من بدأه. " +
                    "لو متأكد، فعّل \"تأكيد إعادة التعيين\" واكتب سبب التعديل ثم اعتمد مرة أخرى.";
                return RedirectToAction("SelectQuestions", new { id = model.AssignmentId });
            }

            if (attemptSummary.AttemptedQuestionsCount > 0 && model.ConfirmResetAttempts)
            {
                if (string.IsNullOrWhiteSpace(model.EditReason))
                {
                    TempData["Error"] = "سبب التعديل إجباري عند وجود محاولات سابقة للطلاب.";
                    return RedirectToAction("SelectQuestions", new { id = model.AssignmentId });
                }

                await _integrityService.ResetStudentAttemptsAsync(
                    examAssignmentId: assignment.Id, examAssignmentToStudentId: null);
            }

            // 1️⃣ امسح أي أسئلة قديمة مرتبطة بالاختبار
            var oldQuestions = _context.ExamQuestions
                .Where(eq => eq.ExamAssignmentId == assignment.Id);
            int oldQuestionsCount = await oldQuestions.CountAsync();
            _context.ExamQuestions.RemoveRange(oldQuestions);
            await _context.SaveChangesAsync();

            // 2️⃣ أضف الأسئلة اليدوية
            int order = 1;
            foreach (var qId in model.SelectedQuestionIds.Distinct())
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamId = assignment.ExamId.Value,
                    ExamAssignmentId = assignment.Id,
                    QuestionId = qId,
                    Order = order++,
                    IsManuallySelected = true
                });
            }

            // 3️⃣ لو العدد أقل من RequiredCount → كمل تلقائي من بنك الأسئلة
            int remaining = model.RequiredCount - model.SelectedQuestionIds.Count;
            if (remaining > 0)
            {
                // 🟢 هات كل الأسئلة المتاحة في الذاكرة
                var allAvailable = await _context.Questions
                    .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                    .Where(q => q.CurriculumId == assignment.CurriculumId
                                && q.IsReviewed && !q.IsRejected)
                    .ToListAsync();

                // 🟢 فلترة يدويًا (بدون Contains في SQL Server)
                var autoQuestions = allAvailable
                    .Where(q => !model.SelectedQuestionIds.Contains(q.Id))
                    .OrderBy(q => Guid.NewGuid()) // ✅ العشوائية هنا في C# وليس SQL
                    .Take(remaining)
                    .ToList();

                foreach (var q in autoQuestions)
                {
                    _context.ExamQuestions.Add(new ExamQuestion
                    {
                        ExamId = assignment.ExamId.Value,
                        ExamAssignmentId = assignment.Id,
                        QuestionId = q.Id,
                        Order = order++,
                        IsManuallySelected = false
                    });
                }
            }

            // 4️⃣ احفظ التغييرات
            await _context.SaveChangesAsync();

            // Sprint 3 (EC3) — تسجيل التعديل في AdminActivityLog: من/ليه/الأثر (دفعة — بلا طالب واحد)
            await _activityLogger.LogExamQuestionsChangeAsync(
                actionType: "تعديل شامل لأسئلة اختبار دفعة",
                reason: model.EditReason ?? "بدون محاولات سابقة — لا يتطلب سببًا",
                CurrentUserId(), CurrentUserName(),
                examAssignmentId: assignment.Id, examAssignmentToStudentId: null,
                studentId: null,
                questionsBeforeCount: oldQuestionsCount, questionsAfterCount: order - 1,
                affectedAttemptsCount: attemptSummary.AttemptedQuestionsCount);

            TempData["Success"] = "✅ تم اعتماد الأسئلة للاختبار بنجاح.";
            return RedirectToAction("Details", new { id = assignment.Id });
        }


        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]

        public async Task<IActionResult> SectionQuestionsAdmin(int examAssignmentId, int sectionId)
        {
            var questions = await _context.ExamQuestions
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Options)
                .Include(eq => eq.Question.VerbalPassage)
                .Include(eq => eq.Question.Lesson.Section)
                .Where(eq => eq.ExamAssignmentId == examAssignmentId &&
                             eq.Question.Lesson.SectionId == sectionId)
                .ToListAsync();

            // 🔥 تحويل كل سؤال إلى DisplayModel ثم تعبئته في ViewModel العرض الإداري
            var vm = questions.Select(eq =>
            {
                var display = eq.Question.ToDisplayModel();

                return new AdminQuestionReviewVm
                {
                    QuestionId = eq.Question.Id,                   // Guid → Guid (تمام)
                    QuestionTitle = display.Title,
                    VerbalPassageContent = display.VerbalPassageContent,
                    ImageUrl = display.ImageUrl,
                    ComparisonValue1 = display.ComparisonValue1,
                    ComparisonValue2 = display.ComparisonValue2,
                    DisplayType = display.DisplayType,
                    IsQuantitative = display.IsQuantitative,

                    LessonTitle = display.LessonTitle,
                    SectionTitle = display.SectionTitle,

                    Options = display.Options.Select(o => new AdminQuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                };
            }).ToList();

            return View("~/Areas/Admin/Views/ExamAssignments/SectionQuestionsAdmin.cshtml", vm);
        }


        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> SectionLessonsAdmin(int examAssignmentId, int sectionId)
        {
            var lessons = await _context.ExamQuestions
                .Include(eq => eq.Question.Lesson)
                .Where(eq => eq.ExamAssignmentId == examAssignmentId &&
                             eq.Question.Lesson.SectionId == sectionId)
                .GroupBy(eq => new { eq.Question.LessonId, eq.Question.Lesson.Title })
                .Select(g => new AdminLessonVm
                {
                    LessonId = g.Key.LessonId,
                    LessonTitle = g.Key.Title,
                    TotalQuestions = g.Count()
                })
                .ToListAsync();

            ViewBag.ExamAssignmentId = examAssignmentId;

            return View("~/Areas/Admin/Views/ExamAssignments/SectionLessonsAdmin.cshtml", lessons);
        }


        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> LessonQuestionsAdmin(int examAssignmentId, int lessonId)
        {
            var examQuestions = await _context.ExamQuestions
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Options)
                .Include(eq => eq.Question.VerbalPassage)
                .Include(eq => eq.Question.Lesson.Section)
                .Where(eq => eq.ExamAssignmentId == examAssignmentId &&
                             eq.Question.LessonId == lessonId)
                .ToListAsync();

            // 🔥 تحويل كل سؤال إلى DisplayModel ثم تعبئته في ViewModel الإداري
            var questions = examQuestions.Select(eq =>
            {
                var display = eq.Question.ToDisplayModel();

                return new AdminQuestionReviewVm
                {
                    QuestionId = eq.Question.Id,
                    QuestionTitle = display.Title,
                    VerbalPassageContent = display.VerbalPassageContent,
                    ImageUrl = display.ImageUrl,
                    ComparisonValue1 = display.ComparisonValue1,
                    ComparisonValue2 = display.ComparisonValue2,
                    DisplayType = display.DisplayType,
                    IsQuantitative = display.IsQuantitative,

                    LessonTitle = display.LessonTitle,
                    SectionTitle = display.SectionTitle,

                    Options = display.Options.Select(o => new AdminQuestionOptionVm
                    {
                        Text = o.Text,
                        ImageUrl = o.ImageUrl
                    }).ToList()
                };
            }).ToList();

            return View("~/Areas/Admin/Views/ExamAssignments/LessonQuestionsAdmin.cshtml", questions);
        }



        // الوصول للاختبارات العادية يكفي فيه صلاحية البروفايل (مُحققة بـ [AdminPermission])
        // فلترة الدفعات تُطبق فقط على الأرشيف عبر ExamAssignmentBatchArchiveAccess
        private Task<List<int>?> GetPermittedExamBatchIdsAsync()
            => Task.FromResult<List<int>?>(null);

        // يُعيد قائمة الدفعات المسموح للمستخدم الحالي بالوصول لأرشيفها
        // null = وصول كامل (Owner/Developer)، قائمة فارغة = لا وصول لأي دفعة مؤرشفة
        private async Task<List<int>?> GetPermittedArchivedExamBatchIdsAsync()
        {
            if (IsExamArchiveOwner())
                return null;

            var uid = CurrentUserId();
            if (string.IsNullOrWhiteSpace(uid))
                return new List<int>();

            return await _context.ExamAssignmentBatchArchiveAccesses
                .AsNoTracking()
                .Where(x => x.UserId == uid && x.IsActive)
                .Select(x => x.BatchId)
                .Distinct()
                .ToListAsync();
        }

        private static int CalculatePercentage(int part, int total)
        {
            return total > 0 ? (int)Math.Round(part * 100.0 / total) : 0;
        }

        private async Task<List<ExamBatchCardVM>> BuildExamBatchCardsAsync(bool archived)
        {
            // للأرشيف: نستخدم صلاحيات الأرشيف المحددة من المالك
            // للاختبارات العادية: نستخدم صلاحيات الدفعات العامة للمستخدم
            var permittedBatchIds = archived
                ? await GetPermittedArchivedExamBatchIdsAsync()
                : await GetPermittedExamBatchIdsAsync();

            var assignments = await (
                from eab in _context.ExamAssignmentsToBatches.AsNoTracking()
                join ex in _context.Exams.AsNoTracking() on eab.ExamId equals ex.Id
                where ex.Type != ExamType.LevelAssessment
                      && eab.IsArchived == archived
                      && (permittedBatchIds == null || permittedBatchIds.Contains(eab.BatchId))
                select new
                {
                    eab.Id,
                    eab.BatchId,
                    eab.IsSentToStudents
                }).ToListAsync();

            if (!assignments.Any())
                return new List<ExamBatchCardVM>();

            var assignmentBatchMap = assignments.ToDictionary(x => x.Id, x => x.BatchId);
            var assignmentIds = assignmentBatchMap.Keys.ToHashSet();
            var batchIdsWithExams = assignments.Select(x => x.BatchId).Distinct().ToHashSet();

            var statusesRaw = await _context.ExamStudentStatuses
                .AsNoTracking()
                .Where(s => s.ExamAssignmentId.HasValue)
                .Select(s => new
                {
                    AssignmentId = s.ExamAssignmentId!.Value,
                    s.StudentId,
                    s.IsSubmitted,
                    s.Status,
                    s.StartedAt
                })
                .ToListAsync();

            var statusStats = statusesRaw
                .Where(s => assignmentIds.Contains(s.AssignmentId))
                .Select(s => new
                {
                    BatchId = assignmentBatchMap[s.AssignmentId],
                    s.StudentId,
                    s.IsSubmitted,
                    s.Status,
                    s.StartedAt
                })
                .ToList();

            var activeCounts = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking() on e.StudentID equals s.StudentID
                join u in _context.Users.AsNoTracking() on s.UserId equals u.Id
                where u.IsActive
                group e by e.BatchId into g
                select new { BatchId = g.Key, Count = g.Count() }
            ).ToListAsync();

            var batches = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted)
                .Include(b => b.Course)
                .OrderBy(b => b.Name)
                .ToListAsync();

            batches = batches
                .Where(b => batchIdsWithExams.Contains(b.Id))
                .ToList();

            return batches.Select(b =>
            {
                var batchAssignments = assignments.Where(x => x.BatchId == b.Id).ToList();
                var batchStatuses = statusStats.Where(x => x.BatchId == b.Id).ToList();
                var totalAssigned = batchStatuses.Count;
                var completed = batchStatuses.Count(x => x.IsSubmitted || x.Status == ExamStatus.Completed);
                var inProgress = batchStatuses.Count(x => !x.IsSubmitted && x.Status == ExamStatus.InProgress);
                var pending = Math.Max(totalAssigned - completed - inProgress, 0);

                return new ExamBatchCardVM
                {
                    BatchId = b.Id,
                    BatchName = b.Name,
                    CourseTitle = b.Course?.Name ?? "",
                    TotalStudents = activeCounts.FirstOrDefault(ac => ac.BatchId == b.Id)?.Count ?? 0,
                    TotalExams = batchAssignments.Count,
                    SentExams = batchAssignments.Count(x => x.IsSentToStudents),
                    TotalAssigned = totalAssigned,
                    CompletedCount = completed,
                    InProgressCount = inProgress,
                    PendingCount = pending,
                    CompletionPercentage = CalculatePercentage(completed, totalAssigned)
                };
            }).ToList();
        }

        [HttpGet]
        [AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> BatchExams(int batchId, bool archived = false)
        {
            // للأرشيف: تحقق من صلاحيات الأرشيف المحددة من المالك
            // لغير الأرشيف: تحقق من صلاحيات الدفعات العامة
            var permittedBatchIds = archived
                ? await GetPermittedArchivedExamBatchIdsAsync()
                : await GetPermittedExamBatchIdsAsync();
            if (permittedBatchIds != null && !permittedBatchIds.Contains(batchId))
                return Forbid();

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound();

            var activeStudentCount = await (
                from e in _context.StudentBatchEnrollments.AsNoTracking()
                join s in _context.Students.AsNoTracking() on e.StudentID equals s.StudentID
                join u in _context.Users.AsNoTracking() on s.UserId equals u.Id
                where e.BatchId == batchId && u.IsActive
                select e.StudentID
            ).CountAsync();

            var assignments = await (
                from eab in _context.ExamAssignmentsToBatches.AsNoTracking()
                join ex in _context.Exams.AsNoTracking() on eab.ExamId equals ex.Id
                where eab.BatchId == batchId
                      && eab.IsArchived == archived
                      && ex.Type != ExamType.LevelAssessment
                orderby eab.CreatedAt descending
                select new
                {
                    eab.Id,
                    eab.Title,
                    CurriculumTitle = eab.Curriculum != null ? eab.Curriculum.Title : "",
                    eab.CreatedAt,
                    eab.ScheduledDate,
                    eab.DurationMinutes,
                    eab.TotalQuestions,
                    eab.IsSentToStudents,
                    eab.IsArchived,
                    eab.IsOnline,
                    eab.IsInLab
                }).ToListAsync();

            var assignmentIds = assignments.Select(x => x.Id).ToHashSet();
            var statuses = await _context.ExamStudentStatuses
                .AsNoTracking()
                .Where(s => s.ExamAssignmentId.HasValue)
                .Select(s => new
                {
                    AssignmentId = s.ExamAssignmentId!.Value,
                    s.IsSubmitted,
                    s.Status
                })
                .ToListAsync();

            statuses = statuses
                .Where(s => assignmentIds.Contains(s.AssignmentId))
                .ToList();

            var examCards = assignments.Select(a =>
            {
                var examStatuses = statuses.Where(s => s.AssignmentId == a.Id).ToList();
                var totalAssigned = examStatuses.Count;
                var completed = examStatuses.Count(s => s.IsSubmitted || s.Status == ExamStatus.Completed);
                var inProgress = examStatuses.Count(s => !s.IsSubmitted && s.Status == ExamStatus.InProgress);
                var pending = Math.Max(totalAssigned - completed - inProgress, 0);

                return new ExamCardForBatchVM
                {
                    AssignmentId = a.Id,
                    Title = a.Title,
                    CurriculumTitle = a.CurriculumTitle,
                    CreatedAt = a.CreatedAt,
                    ScheduledDate = a.ScheduledDate,
                    DurationMinutes = a.DurationMinutes,
                    TotalQuestions = a.TotalQuestions,
                    IsSent = a.IsSentToStudents,
                    IsArchived = a.IsArchived,
                    IsOnline = a.IsOnline,
                    IsInLab = a.IsInLab,
                    TotalAssigned = totalAssigned,
                    CompletedCount = completed,
                    InProgressCount = inProgress,
                    PendingCount = pending,
                    CompletionPercentage = CalculatePercentage(completed, totalAssigned)
                };
            }).ToList();

            var vm = new ExamBatchDetailsPageVM
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CourseTitle = batch.Course?.Name ?? "",
                TotalStudents = activeStudentCount,
                IsArchived = archived,
                Exams = examCards
            };

            return View(vm);
        }


        [HttpGet]
        [AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> Index(int? batchId, int? curriculumId, DateTime? fromDate, DateTime? toDate, bool showArchived = false)
        {
            ViewBag.IsExamArchiveOwner = IsExamArchiveOwner();
            ViewBag.ArchivedExamAssignmentsCount = await _context.ExamAssignmentsToBatches
                .Include(eab => eab.Exam)
                .CountAsync(eab =>
                    eab.IsArchived &&
                    (eab.Exam == null || eab.Exam.Type != ExamType.LevelAssessment));

            // للأرشيف: صلاحيات الأرشيف المحددة من المالك — لغيره: صلاحيات الدفعات العامة
            var examPermittedBatchIds = showArchived
                ? await GetPermittedArchivedExamBatchIdsAsync()
                : await GetPermittedExamBatchIdsAsync();
            var batchCards = await BuildExamBatchCardsAsync(showArchived);

            // 🟢 الاستعلام الأساسي مع Join على جدول Exams
            var query =
                from eab in _context.ExamAssignmentsToBatches
                    .Include(e => e.Batch)
                    .Include(e => e.Curriculum)
                join ex in _context.Exams on eab.ExamId equals ex.Id
                where ex.Type != ExamType.LevelAssessment // ❌ استبعاد اختبارات تحديد المستوى
                      && eab.IsArchived == showArchived
                      && (examPermittedBatchIds == null || examPermittedBatchIds.Contains(eab.BatchId))
                select new
                {
                    eab.Id,
                    eab.Title,
                    eab.BatchId,
                    BatchName = eab.Batch.Name,
                    CurriculumId = eab.CurriculumId,
                    CurriculumTitle = eab.Curriculum.Title,
                    eab.CreatedAt,
                    eab.IsSentToStudents,
                    eab.IsArchived
                };

            // 🟡 تطبيق الفلاتر
            if (batchId.HasValue)
                query = query.Where(e => e.BatchId == batchId);

            if (curriculumId.HasValue)
                query = query.Where(e => e.CurriculumId == curriculumId);

            if (fromDate.HasValue)
                query = query.Where(e => e.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.CreatedAt <= toDate.Value);

            // 🟢 جلب البيانات بدون GroupBy (عرض كل دفعة مرتبطة بنفس الاختبار)
            var data = await query
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            // 🧩 تجهيز النتائج للعرض
            var studentCounts = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .GroupBy(s => s.BatchId)
                .Select(g => new { BatchId = g.Key, Count = g.Count() })
                .ToListAsync();

            var results = data
                .Select(e => new ExamAssignmentRowViewModel
                {
                    Id = e.Id,
                    Title = e.Title,
                    BatchName = e.BatchName,
                    CurriculumTitle = e.CurriculumTitle,
                    CreatedAt = e.CreatedAt,
                    IsSent = e.IsSentToStudents,
                    StudentCount = studentCounts.FirstOrDefault(s => s.BatchId == e.BatchId)?.Count ?? 0,
                    IsArchived = e.IsArchived
                })
                .ToList();

            // 🧩 تجهيز ViewModel
            var model = new ExamAssignmentFilterViewModel
            {
                BatchId = batchId,
                CurriculumId = curriculumId,
                FromDate = fromDate,
                ToDate = toDate,
                ShowArchived = showArchived,
                Results = results,
                BatchCards = batchCards,
                TotalBatches = batchCards.Count,
                TotalExamsCount = batchCards.Sum(x => x.TotalExams),
                TotalSentExams = batchCards.Sum(x => x.SentExams),
                TotalAssignedStudents = batchCards.Sum(x => x.TotalAssigned),
                TotalCompletedStudents = batchCards.Sum(x => x.CompletedCount),
                Batches = batchCards
                    .Select(bc => new SelectListItem { Value = bc.BatchId.ToString(), Text = bc.BatchName })
                    .ToList(),
                Curriculums = await _context.Curriculums
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Title })
                    .ToListAsync()
            };

            return View(model);
        }



        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> GetSectionsByCurriculum(int curriculumId)
        {
            var sections = await _context.Sections
                .Where(s => s.CurriculumId == curriculumId)
                .Select(s => new {
                    id = s.Id,
                    title = s.Title
                })
                .ToListAsync();

            return Json(sections);
        }







        // ✅ API لعرض البيانات (للـ DataTables)
        [HttpPost]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> LoadExamAssignmentsData(int? batchId, int? curriculumId, DateTime? fromDate, DateTime? toDate, bool showArchived = false)
        {
            var query = _context.ExamAssignmentsToBatches
                .Include(x => x.Batch)
                .Include(x => x.Curriculum)
                .Include(x => x.Exam)
                .AsQueryable();

            query = query.Where(x =>
                x.IsArchived == showArchived &&
                (x.Exam == null || x.Exam.Type != ExamType.LevelAssessment));

            if (batchId.HasValue)
                query = query.Where(x => x.BatchId == batchId.Value);

            if (curriculumId.HasValue)
                query = query.Where(x => x.CurriculumId == curriculumId.Value);

            if (fromDate.HasValue)
                query = query.Where(x => x.CreatedAt >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(x => x.CreatedAt <= toDate.Value);

            var result = await query
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    id = x.Id,                                  // ✅ الحروف الصغيرة متطابقة مع السكربت
                    title = x.Title,                            // ✅ نفس الاسم في الجدول
                    batchName = x.Batch != null ? x.Batch.Name : "-",
                    curriculumTitle = x.Curriculum != null ? x.Curriculum.Title : "-",
                    createdAt = x.CreatedAt.ToString("yyyy-MM-dd"),
                    isSent = x.IsSentToStudents ? "✅ نعم" : "❌ لا",
                    studentCount = _context.StudentBatchEnrollments.Count(s => s.BatchId == x.BatchId),
                    isArchived = x.IsArchived
                })
                .ToListAsync();

            return Json(new { data = result });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamAssignments", "Archive")]
        public async Task<IActionResult> ArchiveExamAssignmentsByBatches(List<int> selectedBatchIds)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "⚠️ اختر دفعة واحدة على الأقل للأرشفة.";
                return RedirectToAction(nameof(Index));
            }

            var batchIds = selectedBatchIds.Where(x => x > 0).Distinct().ToList();
            if (!batchIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم العثور على دفعات صالحة للأرشفة.";
                return RedirectToAction(nameof(Index));
            }

            var allExams = await _context.ExamAssignmentsToBatches
                .Include(ea => ea.Exam)
                .Where(ea =>
                    !ea.IsArchived &&
                    (ea.Exam == null || ea.Exam.Type != ExamType.LevelAssessment))
                .ToListAsync();

            var exams = allExams
                .Where(ea => batchIds.Any(batchId => batchId == ea.BatchId))
                .ToList();

            var archivedAt = DateTime.UtcNow;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            foreach (var exam in exams)
            {
                exam.IsArchived = true;
                exam.ArchivedAt = archivedAt;
                exam.ArchivedByUserId = userId;
            }

            await _context.SaveChangesAsync();

            if (exams.Count == 0)
                TempData["Error"] = "⚠️ لم يتم العثور على اختبارات مطابقة في الدفعات المحددة.";
            else
                TempData["Success"] = $"✅ تم أرشفة {exams.Count} اختبار بنجاح.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamAssignments", "Archive")]
        public async Task<IActionResult> RestoreExamAssignmentsByBatches(List<int> selectedBatchIds)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "⚠️ اختر دفعة واحدة على الأقل للاسترجاع.";
                return RedirectToAction(nameof(Index));
            }

            var batchIds = selectedBatchIds.Where(x => x > 0).Distinct().ToList();
            if (!batchIds.Any())
            {
                TempData["Error"] = "⚠️ لم يتم العثور على دفعات صالحة للاسترجاع.";
                return RedirectToAction(nameof(Index));
            }

            var allExams = await _context.ExamAssignmentsToBatches
                .Include(ea => ea.Exam)
                .Where(ea =>
                    ea.IsArchived &&
                    (ea.Exam == null || ea.Exam.Type != ExamType.LevelAssessment))
                .ToListAsync();

            var exams = allExams
                .Where(ea => batchIds.Any(batchId => batchId == ea.BatchId))
                .ToList();

            foreach (var exam in exams)
            {
                exam.IsArchived = false;
                exam.ArchivedAt = null;
                exam.ArchivedByUserId = null;
            }

            await _context.SaveChangesAsync();

            if (exams.Count == 0)
                TempData["Error"] = "⚠️ لم يتم العثور على اختبارات مطابقة في الدفعات المحددة.";
            else
                TempData["Success"] = $"✅ تم إخراج {exams.Count} اختبار من الأرشيف بنجاح.";

            return RedirectToAction(nameof(Index), new { showArchived = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamAssignments", "Archive")]
        public async Task<IActionResult> ArchiveExamAssignment(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches.FirstOrDefaultAsync(x => x.Id == id);

            if (assignment == null)
                return Json(new { success = false, message = "لم يتم العثور على الاختبار." });

            assignment.IsArchived = true;
            assignment.ArchivedAt = DateTime.UtcNow;
            assignment.ArchivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم نقل الاختبار إلى الأرشيف." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamAssignments", "Archive")]
        public async Task<IActionResult> RestoreExamAssignment(int id)
        {
            var assignment = await _context.ExamAssignmentsToBatches.FirstOrDefaultAsync(x => x.Id == id);

            if (assignment == null)
                return Json(new { success = false, message = "لم يتم العثور على الاختبار." });

            assignment.IsArchived = false;
            assignment.ArchivedAt = null;
            assignment.ArchivedByUserId = null;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "تم إخراج الاختبار من الأرشيف." });
        }

        [HttpGet]
        [AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> SectionQuestions(int examAssignmentId, int sectionId, int studentId)
        {
            var sectionTitle = await _context.Sections
                .Where(s => s.Id == sectionId)
                .Select(s => s.Title)
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(sectionTitle))
                return NotFound("❌ لم يتم العثور على المحور.");

            var assignment = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == examAssignmentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            var examQuestions = await LoadExamQuestionsForAssignmentAsync(assignment);
            var sectionQuestions = examQuestions
                .Where(eq => GetQuestionSectionId(eq.Question) == sectionId)
                .OrderBy(eq => eq.Order)
                .ThenBy(eq => eq.Id)
                .ToList();

            if (!sectionQuestions.Any())
                return NotFound("⚠️ لا توجد أسئلة مرتبطة بهذا المحور داخل الاختبار.");

            var attempts = studentId > 0
                ? await LoadStudentAttemptsForAssignmentAsync(examAssignmentId, assignment.ExamId, studentId)
                : new List<QuestionAttemptNew>();

            var attemptsByQuestionId = attempts.ToDictionary(a => a.QuestionId, a => a);

            var questionsData = sectionQuestions
                .Select(eq =>
                {
                    attemptsByQuestionId.TryGetValue(eq.QuestionId, out var attempt);
                    var isSkipped = IsAttemptSkipped(attempt);

                    return new
                    {
                        LessonTitle = eq.Question.Lesson?.Title ?? "بدون مؤشر",
                        Entry = new ExamQuestionEntry
                        {
                            QuestionTitle = eq.Question.Title ?? "بدون نص",
                            StudentAnswer = isSkipped ? "متخطى / لم يجب" : attempt!.SelectedAnswer,
                            CorrectAnswer = eq.Question.CorrectAnswer ?? "",
                            IsCorrect = !isSkipped && attempt!.IsCorrect,
                            IsSkipped = isSkipped,
                            StatusText = isSkipped ? "متخطى" : (attempt!.IsCorrect ? "صحيح" : "خطأ")
                        }
                    };
                })
                .GroupBy(x => x.LessonTitle)
                .Select(g => new ExamLessonQuestionsVm
                {
                    LessonTitle = g.Key,
                    Questions = g.Select(x => x.Entry).ToList()
                }).ToList();

            var vm = new ExamSectionQuestionsViewModel
            {
                ExamAssignmentId = examAssignmentId,
                StudentId = studentId,
                SectionId = sectionId,
                SectionTitle = sectionTitle,
                Lessons = questionsData
            };

            ViewBag.ExamAssignmentId = examAssignmentId;
            ViewBag.StudentId = studentId;

            return View(vm);
        }

        [HttpGet]
        [AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> LessonQuestionsDetail(int assignmentId, int lessonId)
        {
            var lesson = await _context.Lessons
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return NotFound("❌ لم يتم العثور على المؤشر.");

            // 🧩 جلب الأسئلة التي تخص هذا المؤشر ومرتبطة بالاختبار المحدد فقط
            var questions = await (
                from eq in _context.ExamQuestions
                join q in _context.Questions on eq.QuestionId equals q.Id
                where eq.ExamAssignmentId == assignmentId && q.LessonId == lessonId
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
                return Content("<div class='alert alert-warning text-center'>⚠️ لا توجد أسئلة مرتبطة بهذا المؤشر في هذا الاختبار.</div>", "text/html");
            }

            // 🧩 محاولات الأسئلة (آخر محاولة لكل سؤال)
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                where a.ExamAssignmentId == assignmentId && q.LessonId == lessonId
                select new
                {
                    a.QuestionId,
                    a.SelectedAnswer,
                    a.IsCorrect,
                    a.TimeTakenSeconds,
                    a.AttemptedAt
                }
            ).ToListAsync();

            var latestAttempts = attempts
                .GroupBy(a => a.QuestionId)
                .Select(g => g.OrderByDescending(a => a.AttemptedAt).First())
                .ToList();

            // 🕒 الوقت الكلي
            var totalTimeSeconds = latestAttempts.Sum(a => a.TimeTakenSeconds);
            var totalMinutes = totalTimeSeconds / 60;
            var remainingSeconds = totalTimeSeconds % 60;
            var formattedTime = $"{totalMinutes} دقيقة {remainingSeconds} ثانية";

            // 🧩 جلب الخيارات
            var optionsRaw = await (
                from o in _context.QuestionOptions
                join q in _context.Questions on o.QuestionId equals q.Id
                where q.LessonId == lessonId
                select new
                {
                    o.QuestionId,
                    o.Text,
                    o.ImageUrl
                }
            ).ToListAsync();

            // 🧠 بناء الأسئلة
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

                return new ExamReviewQuestionVm
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

            // 🧾 الموديل النهائي
            var vm = new ExamReviewViewModel
            {
                ExamAssignmentId = assignmentId,
                ExamTitle = $"مراجعة أسئلة المؤشر: {lesson.Title}",
                TotalQuestions = questionVms.Count,
                TimeSpentFormatted = formattedTime,
                TimeSpentMinutes = totalMinutes,
                Questions = questionVms
            };

            return View("~/Areas/Admin/Views/ExamAssignments/LessonQuestionsDetail.cshtml", vm);
        }


        // ✅ تفاصيل اختبار دفعة معينة


        [HttpGet]
        [AdminPermission("ExamAssignments", "Details")]
        public async Task<IActionResult> Details(int id)
        {
            // ============================================================
            // 1) جلب بيانات التكليف الرئيسي
            // ============================================================
            var mainAssignment = await _context.ExamAssignmentsToBatches
                .Include(e => e.Batch)
                .Include(e => e.Curriculum)
                .Include(e => e.Exam)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (mainAssignment == null)
                return NotFound($"❌ لم يتم العثور على Assignment برقم: {id}");

            var isInLab = mainAssignment.IsInLab || !mainAssignment.IsOnline;
            var isOnline = !isInLab;
            var referenceCode = isInLab
                ? FirstNonEmpty(mainAssignment.ReferenceCode, mainAssignment.Exam?.ReferenceCode)
                : null;

            var shouldSaveReferenceState = false;

            if (isOnline)
            {
                if (!string.IsNullOrWhiteSpace(mainAssignment.ReferenceCode))
                {
                    mainAssignment.ReferenceCode = null;
                    shouldSaveReferenceState = true;
                }

                if (mainAssignment.Exam != null && !string.IsNullOrWhiteSpace(mainAssignment.Exam.ReferenceCode))
                {
                    mainAssignment.Exam.ReferenceCode = null;
                    shouldSaveReferenceState = true;
                }
            }
            else if (string.IsNullOrWhiteSpace(referenceCode))
            {
                referenceCode = await GenerateBatchExamReferenceCodeAsync();
                mainAssignment.ReferenceCode = referenceCode;

                if (mainAssignment.Exam != null)
                    mainAssignment.Exam.ReferenceCode = referenceCode;

                shouldSaveReferenceState = true;
            }
            else
            {
                referenceCode = referenceCode.Trim();

                if (string.IsNullOrWhiteSpace(mainAssignment.ReferenceCode))
                {
                    mainAssignment.ReferenceCode = referenceCode;
                    shouldSaveReferenceState = true;
                }

                if (mainAssignment.Exam != null && string.IsNullOrWhiteSpace(mainAssignment.Exam.ReferenceCode))
                {
                    mainAssignment.Exam.ReferenceCode = referenceCode;
                    shouldSaveReferenceState = true;
                }
            }

            if (shouldSaveReferenceState)
                await _context.SaveChangesAsync();


            // ============================================================
            // 2) جلب تفاصيل المناهج (لو الاختبار مدمج)
            // ============================================================
            var curriculums = await _context.ExamCurriculumQuestionCounts
                .Include(x => x.Curriculum)
                .Where(x => x.ExamAssignmentId == mainAssignment.Id)
                .Select(x => new ExamAssignmentDetailsViewModel.CurriculumDetailVm
                {
                    CurriculumTitle = x.Curriculum.Title,
                    QuestionCount = x.QuestionCount
                })
                .ToListAsync();

            int totalQuestions = curriculums.Sum(c => c.QuestionCount);


            // ============================================================
            // 3) بناء الـ ViewModel الأساسي
            // ============================================================
            var vm = new ExamAssignmentDetailsViewModel
            {
                AssignmentId = mainAssignment.Id,
                Title = mainAssignment.Title,
                BatchName = mainAssignment.Batch?.Name ?? "-",
                CurriculumTitle = mainAssignment.Curriculum?.Title ?? "-",
                SectionTitle = mainAssignment.Section?.Title ?? "-",
                ScheduledDate = mainAssignment.ScheduledDate ?? mainAssignment.AssignedAt,
                TotalQuestions = totalQuestions,
                DurationMinutes = mainAssignment.DurationMinutes,
                IsSentToStudents = mainAssignment.IsSentToStudents,
                IsOnline = isOnline,
                IsInLab = isInLab,
                RequireAttendanceBeforeExam = mainAssignment.RequireAttendanceBeforeExam,
                ReferenceCode = referenceCode,

                StudentCount = await _context.StudentBatchEnrollments
                    .CountAsync(s => s.BatchId == mainAssignment.BatchId),
                CreatedAt = mainAssignment.CreatedAt,
                Curriculums = curriculums,

                ManualCount = await _context.ExamQuestions
                    .CountAsync(eq => eq.ExamAssignmentId == mainAssignment.Id && eq.IsManuallySelected),

                AutoCount = await _context.ExamQuestions
                    .CountAsync(eq => eq.ExamAssignmentId == mainAssignment.Id && !eq.IsManuallySelected),

                HasQuestionsSelected = await _context.ExamQuestions
                    .AnyAsync(eq => eq.ExamAssignmentId == mainAssignment.Id)
            };


            // ============================================================
            // 4) جلب الأسئلة الكاملة + ربطها بالدرس والمحور
            // ============================================================
            var examQuestions = await _context.ExamQuestions
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Where(eq => eq.ExamAssignmentId == mainAssignment.Id)
                .OrderBy(eq => eq.Order)
                .ToListAsync();


            // ============================================================
            // 5) تجميع الأسئلة حسب المحور Section → vm.Sections
            // ============================================================
            vm.Sections = examQuestions
                .GroupBy(eq => eq.Question.Lesson.Section)
                .Select(g => new SectionGroupVm
                {
                    SectionId = g.Key.Id,
                    SectionTitle = g.Key.Title,
                    Questions = g.Select(q => new SectionQuestionVm
                    {
                        QuestionId = q.QuestionId,
                        Title = q.Question.Title,
                        LessonTitle = q.Question.Lesson.Title,
                        DifficultyLevel = (int)q.Question.Difficulty,
                        CorrectAnswer = q.Question.CorrectAnswer,
                        Order = q.Order
                    }).ToList()
                })
                .ToList();


            // ============================================================
            // 6) إرجاع الفيو
            // ============================================================
            return View(vm);
        }


        [HttpGet]
        [AdminPermission("ExamAssignments", "Edit")]
        public async Task<IActionResult> ReplaceQuestionPage(Guid oldId, int examAssignmentId)
        {
            // 1) ابحث عن السؤال داخل نفس Assignment أولاً
            var oldLink = await _context.ExamQuestions
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(eq =>
                    eq.ExamAssignmentId == examAssignmentId &&
                    eq.QuestionId == oldId);

            // 2) لو لم نجده → ابحث داخل الاختبار نفسه ExamId
            if (oldLink == null)
            {
                var examId = await _context.ExamAssignmentsToBatches
                    .Where(x => x.Id == examAssignmentId)
                    .Select(x => x.ExamId)
                    .FirstOrDefaultAsync();

                if (examId == 0)
                    return NotFound("المهمة غير مرتبطة بأي اختبار.");

                oldLink = await _context.ExamQuestions
                    .Include(eq => eq.Question)
                        .ThenInclude(q => q.Lesson)
                            .ThenInclude(l => l.Section)
                    .FirstOrDefaultAsync(eq =>
                        eq.ExamId == examId &&
                        eq.QuestionId == oldId);
            }

            // 3) لو مازال null → هذا يعني فعلاً أنه غير موجود
            if (oldLink == null)
                return Content("لم يتم العثور على السؤال داخل هذا الاختبار.");

            var oldQuestion = oldLink.Question;

            // 4) البدائل من نفس المحور والدرس
            var alternatives = await _context.Questions
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                .Where(q =>
                    q.Id != oldId &&
                    q.Lesson.SectionId == oldQuestion.Lesson.SectionId &&
                    q.CurriculumId == oldQuestion.CurriculumId)
                .Take(200)
                .ToListAsync();

            var vm = new ExamReplaceQuestionVm
            {
                AssignmentId = examAssignmentId,
                OldQuestionId = oldId,
                OldTitle = oldQuestion.Title,
                OldOrder = oldLink.Order,
                OldLessonTitle = oldQuestion.Lesson?.Title ?? "—",
                OldSectionTitle = oldQuestion.Lesson?.Section?.Title ?? "—",

                Alternatives = alternatives.Select(q => new AlternativeQuestionVm
                {
                    QuestionId = q.Id,
                    Title = q.Title,
                    CorrectAnswer = q.CorrectAnswer,
                    DifficultyLevel = (int)q.Difficulty,
                    LessonTitle = q.Lesson?.Title ?? "—",
                    SectionTitle = q.Lesson?.Section?.Title ?? "—"
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [AdminPermission("ExamAssignments", "Edit")]
        public async Task<IActionResult> ReplaceQuestionConfirm(
            int assignmentId, Guid oldQuestionId, Guid newQuestionId,
            bool confirmReset = false, string? reason = null)
        {
            var oldLink = await _context.ExamQuestions
                .FirstOrDefaultAsync(eq =>
                    eq.ExamAssignmentId == assignmentId &&
                    eq.QuestionId == oldQuestionId);

            if (oldLink == null)
                return Json(new { success = false, message = "لم يتم العثور على السؤال القديم." });

            // ==============================
            // حراسة: لا يجوز استبدال سؤال له محاولة إجابة سابقة دون تأكيد + سبب (Sprint 2 — EB4)
            // ==============================
            var attemptsOnQuestion = await _context.QuestionAttemptNew.CountAsync(a =>
                a.QuestionId == oldQuestionId && a.ExamAssignmentId == assignmentId);

            if (attemptsOnQuestion > 0 && !confirmReset)
            {
                return Json(new
                {
                    success = false,
                    requiresConfirm = true,
                    message = $"هذا السؤال له {attemptsOnQuestion} محاولة إجابة مسجّلة. الاستبدال سيمسح هذه المحاولات ويصفّر النتائج المحفوظة المتأثرة لإعادة حسابها."
                });
            }

            if (attemptsOnQuestion > 0 && confirmReset)
            {
                if (string.IsNullOrWhiteSpace(reason))
                    return Json(new { success = false, message = "سبب الاستبدال إجباري عند وجود محاولات إجابة سابقة على هذا السؤال." });

                await _integrityService.ResetSingleQuestionAttemptAsync(
                    oldQuestionId, examAssignmentId: assignmentId, examAssignmentToStudentId: null);
            }

            int order = oldLink.Order;

            _context.ExamQuestions.Remove(oldLink);
            await _context.SaveChangesAsync();

            _context.ExamQuestions.Add(new ExamQuestion
            {
                ExamAssignmentId = assignmentId,
                ExamId = oldLink.ExamId,
                QuestionId = newQuestionId,
                Order = order,
                IsManuallySelected = true
            });

            await _context.SaveChangesAsync();

            // Sprint 3 (EC3) — تسجيل التعديل في AdminActivityLog: من/ليه/الأثر (دفعة — بلا طالب واحد)
            await _activityLogger.LogExamQuestionsChangeAsync(
                actionType: "استبدال سؤال في اختبار دفعة",
                reason: reason ?? "بدون محاولات سابقة — لا يتطلب سببًا",
                CurrentUserId(), CurrentUserName(),
                examAssignmentId: assignmentId, examAssignmentToStudentId: null,
                studentId: null,
                questionsBeforeCount: 1, questionsAfterCount: 1,
                affectedAttemptsCount: attemptsOnQuestion);

            return Json(new { success = true });
        }

        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> PreviewQuestion(Guid id)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (question == null)
                return Content("<div class='text-danger text-center p-3'>❌ لم يتم العثور على السؤال.</div>");

            // تحويل السؤال إلى نموذج العرض الرسمي
            var vm = question.ToDisplayModel();

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", vm);
        }


        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]

        public async Task<IActionResult> ExamStudents(int assignmentId)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(x => x.Batch)
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == assignmentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            // ✅ الأسئلة الفعلية في الاختبار
            var examQuestionIds = await _context.ExamQuestions
                .Where(eq => eq.ExamAssignmentId == assignment.Id)
                .Select(eq => eq.QuestionId)
                .ToListAsync();

            int totalQuestionsInExam = examQuestionIds.Count;

            // ✅ الطلاب المسجلين في الدفعة
            var enrollments = await _context.StudentBatchEnrollments
                .Include(e => e.Student)
                .Where(e => e.BatchId == assignment.BatchId)
                .ToListAsync();

            var studentsVm = new List<ExamStudentViewModel>();

            foreach (var e in enrollments)
            {
                // ✅ اجلب ملخص النتيجة من الخدمة الموحدة
                var summary = await _studentExamStatisticsService.GetExamResultAsync(assignment.Id, e.StudentID);

                // ✅ الحالة الزمنية من ExamStudentStatus
                var status = await _context.ExamStudentStatuses
                    .FirstOrDefaultAsync(es => es.StudentId == e.StudentID && es.ExamAssignmentId == assignment.Id);

                // 🕒 الوقت المستغرق
                double timeSpent = 0;
                if (status?.StartedAt != null)
                {
                    DateTime end = status.SubmittedAt ?? status.EndAt ?? DateTime.UtcNow;
                    timeSpent = Math.Round((end - status.StartedAt.Value).TotalMinutes, 1);
                }

                // ✅ تحديد حالة الحضور بدقة
                // ✅ تحديد حالة الحضور بدقة
                // ✅ تحديد حالة الحضور بدقة ومنطقية حتى لو لم تُحدَّث EndAt
                bool isCompleted = false;
                bool isInProgress = false;

                if (status != null)
                {
                    // 🟢 الحالة مكتملة فعلاً (تسليم أو إغلاق تلقائي)
                    if (status.IsSubmitted || status.Status == ExamStatus.Completed)
                    {
                        isCompleted = true;
                    }
                    else
                    {
                        // 🕓 حالة زمنية: لو بدأ الاختبار وانتهى الوقت المخصص، نعتبره مكتمل تلقائيًا
                        if (status.StartedAt.HasValue)
                        {
                            var elapsed = (DateTime.UtcNow - status.StartedAt.Value).TotalMinutes;
                            if (elapsed >= assignment.DurationMinutes)
                                isCompleted = true;
                            else
                                isInProgress = true;
                        }
                    }
                }

                bool isPresent = status != null && (isCompleted || isInProgress);

                // ✅ صياغة الحالة النصية النهائية
                string displayStatus;
                if (isCompleted)
                    displayStatus = "تم الإنهاء";
                else if (isInProgress)
                    displayStatus = "قيد التنفيذ";
                else if (status == null)
                    displayStatus = "لم يبدأ";
                else
                    displayStatus = "غير مكتمل";

                // ✅ أضف النتيجة إلى القائمة
                studentsVm.Add(new ExamStudentViewModel
                {
                    StudentId = e.StudentID,
                    FullName = e.Student.FullName,
                    Status = displayStatus,
                    Score = summary.ScorePercentage,
                    CorrectAnswers = summary.CorrectAnswers,
                    WrongAnswers = summary.WrongAnswers,
                    SkippedQuestions = summary.SkippedQuestions,
                    AnsweredQuestions = summary.AnsweredQuestions,
                    ExamDate = status?.SubmittedAt ?? status?.EndAt ?? status?.StartedAt,
                    TimeSpentMinutes = timeSpent,
                    IsPresent = isPresent
                });
            }

            var vm = new ExamStudentsListViewModel
            {
                AssignmentId = assignment.Id,
                ExamTitle = assignment.Title ?? assignment.Exam.Title,
                BatchName = assignment.Batch.Name,
                ExamDuration = assignment.DurationMinutes,
                Students = studentsVm
            };

            return View(vm);
        }



        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> CheckBeforeResend(int assignmentId, int studentId)
        {
            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(x => x.StudentId == studentId &&
                                          x.ExamAssignmentId == assignmentId);

            // 1️⃣ الطالب لم يبدأ الاختبار → لا مانع
            if (status == null || status.StartedAt == null)
            {
                return Json(new { canResend = true });
            }

            // 2️⃣ الطالب في حالة "قيد التنفيذ"
            if (!status.IsSubmitted && status.StartedAt != null && status.SubmittedAt == null)
            {
                return Json(new
                {
                    canResend = false,
                    reason = "الطالب بدأ الاختبار ولم يُنهِه بعد. لا يمكن إعادة الإرسال أثناء التنفيذ."
                });
            }

            // 3️⃣ الطالب أنهى الاختبار → سيتم فقد النتائج
            if (status.IsSubmitted || status.SubmittedAt != null)
            {
                return Json(new
                {
                    canResend = true,
                    reason = "تنبيه: الطالب أنهى الاختبار سابقًا. سيتم حذف جميع النتائج والإحصائيات."
                });
            }

            // 🔄 حالات أخرى
            return Json(new { canResend = true });
        }





        [HttpPost]
        [AdminPermission("ExamAssignments", "Publish")]
        public async Task<IActionResult> SendReminder(int studentId, int assignmentId)
        {
            var assignment = await _context.ExamAssignmentsToBatches.FindAsync(assignmentId);
            if (assignment == null) return NotFound();

            await _advancedNotificationService.SendToStudentAsync(
                studentId,
                $"⚠️ لم تقم بعد بحل الاختبار: {assignment.Title}. يرجى الدخول وحله قبل انتهاء الموعد.",
                NotificationCategory.Exam,
                $"/Students/Exams/StartExam/{assignment.ExamId}"
            );

            TempData["Success"] = "✅ تم إرسال التذكير للطالب.";
            return RedirectToAction("ExamStudents", new { assignmentId });
        }

        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> ExamBatchReport(int assignmentId)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(x => x.Batch)
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == assignmentId);

            if (assignment == null) return NotFound();

            var totalStudents = await _context.StudentBatchEnrollments
                .CountAsync(s => s.BatchId == assignment.BatchId);

            var statuses = await _context.ExamStudentStatuses
                .Where(es => es.ExamAssignmentId == assignment.Id)
                .Include(es => es.Student)
                .ToListAsync();

            var completedCount = statuses.Count(s => s.Status == ExamStatus.Completed);
            var notCompletedCount = totalStudents - completedCount;

            // 📌 لسه مفيش درجات، هنسيب Score null أو نكملها بعدين لما نربط بنتائج الأسئلة
            var model = new ExamBatchReportViewModel
            {
                AssignmentId = assignment.Id,
                ExamTitle = assignment.Title,
                BatchName = assignment.Batch.Name,
                TotalStudents = totalStudents,
                CompletedCount = completedCount,
                NotCompletedCount = notCompletedCount,
                AverageScore = 0, // Placeholder
                TopStudentName = "-",
                WeakStudentName = "-",
                Students = statuses.Select(s => new ExamStudentViewModel
                {
                    StudentId = s.StudentId,
                    FullName = s.Student.FullName,
                    Status = s.Status.ToString(),
                    Score = null
                }).ToList()
            };

            return View(model);
        }


        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]

        public async Task<IActionResult> ExamBatchAnalytics(int assignmentId, bool print = false)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(x => x.Batch)
                .Include(x => x.Exam)
                .Include(x => x.Curriculum)
                .FirstOrDefaultAsync(x => x.Id == assignmentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار.");

            // 🧩 الطلاب في الدفعة
            var students = await (
                from e in _context.StudentBatchEnrollments
                join s in _context.Students on e.StudentID equals s.StudentID
                where e.BatchId == assignment.BatchId
                select new { s.StudentID, s.FullName }
            ).ToListAsync();

            // 🕒 حالات الطلاب في الاختبار
            var statuses = await _context.ExamStudentStatuses
                .Where(es => es.ExamAssignmentId == assignment.Id)
                .ToListAsync();

            // 🧮 محاولات الأسئلة
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                .ThenInclude(q => q.Lesson)
                .ThenInclude(l => l.Section)
                .Where(a => a.ExamAssignmentId == assignment.Id)
                .ToListAsync();

            // 🧾 بناء إحصائيات الطلاب
            var studentStats = new List<StudentExamStatVm>();

            foreach (var st in students)
            {
                var status = statuses.FirstOrDefault(x => x.StudentId == st.StudentID);
                var studentAttempts = attempts.Where(a => a.StudentId == st.StudentID).ToList();

                int totalQuestions = studentAttempts.Count;
                int correct = studentAttempts.Count(a => a.IsCorrect);
                int wrong = studentAttempts.Count(a => !a.IsCorrect && !string.IsNullOrEmpty(a.SelectedAnswer));
                int skipped = studentAttempts.Count(a => string.IsNullOrEmpty(a.SelectedAnswer));

                double score = totalQuestions > 0 ? (double)correct / totalQuestions * 100.0 : 0;
                double duration = 0;

                if (status != null && status.StartedAt.HasValue && status.EndAt.HasValue)
                {
                    var diff = status.EndAt.Value - status.StartedAt.Value;
                    duration = diff.TotalMinutes;
                }

                studentStats.Add(new StudentExamStatVm
                {
                    FullName = st.FullName,
                    Attended = status != null,
                    Score = totalQuestions > 0 ? Math.Round(score, 2) : (double?)null,
                    DurationMinutes = Math.Round(duration, 1),
                    TotalQuestions = totalQuestions,
                    CorrectAnswers = correct,
                    WrongAnswers = wrong,
                    SkippedQuestions = skipped
                });
            }

            // 🧮 التحليل العام
            var total = students.Count;
            var completed = studentStats.Count(x => x.Attended && x.Score.HasValue);
            var absent = total - completed;

            var avgScore = studentStats.Where(s => s.Score.HasValue).DefaultIfEmpty()
                .Average(s => s == null ? 0 : s.Score.Value);

            var avgDuration = studentStats.Where(s => s.DurationMinutes > 0).DefaultIfEmpty()
                .Average(s => s == null ? 0 : s.DurationMinutes);

            var passed = studentStats.Count(s => s.Score >= 60);
            var failed = studentStats.Count(s => s.Score < 60);

            // 🧭 تحليل المحاور
            var sectionStats = attempts
                .GroupBy(a => a.Question.Lesson.Section)
                .Select(g => new ExamSectionPerformanceVm
                {
                    SectionId = g.Key.Id,
                    SectionTitle = g.Key.Title,
                    QuestionCount = g.Select(a => a.QuestionId).Distinct().Count(),
                    LessonCount = g.Select(a => a.Question.LessonId).Distinct().Count(),
                    AvgSuccessRate = Math.Round((double)g.Count(a => a.IsCorrect) / g.Count() * 100.0, 1)
                })
                .OrderByDescending(x => x.AvgSuccessRate)
                .ToList();

            // 🧩 بناء النموذج النهائي
            var vm = new ExamBatchAnalyticsViewModel
            {
                AssignmentId = assignment.Id,
                ExamTitle = assignment.Title,
                BatchName = assignment.Batch.Name,
                TotalStudents = total,
                CompletedCount = completed,
                AbsentCount = absent,
                AvgScore = Math.Round(avgScore, 2),
                AvgDurationMinutes = Math.Round(avgDuration, 1),
                PassedCount = passed,
                FailedCount = failed,
                Students = studentStats.OrderByDescending(s => s.Score ?? 0).ToList(),
                Sections = sectionStats
            };
            // ✅ لو الطلب للطباعة، نعرض View بدون Layout
            if (print)
                return View("~/Areas/Admin/Views/ExamAssignments/ExamBatchAnalytics_Print.cshtml", vm);

           
            return View("~/Areas/Admin/Views/ExamAssignments/ExamBatchAnalytics.cshtml", vm);
        }

        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> StudentExamReport(int examAssignmentId, int studentId)
        {

            // 🟢 جلب بيانات الاختبار
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == examAssignmentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            // 🟢 جلب بيانات الطالب
            var student = await _context.Students
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("❌ لم يتم العثور على بيانات الطالب.");

            // 🧩 احسب النتائج من الخدمة الموحدة نفسها المستخدمة في الطالب
            var summary = await _studentExamStatisticsService.GetExamResultAsync(examAssignmentId, studentId);

            // 🟢 جلب المحاولات
            var attempts = await _context.QuestionAttemptNew
                .Include(a => a.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                .Where(a => a.StudentId == studentId && a.ExamAssignmentId == examAssignmentId)
                .ToListAsync();

            // 🕒 الوقت الفعلي
            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.ExamAssignmentId == examAssignmentId);

            double solveMinutes = summary.SolveMinutes;
            double totalMinutes = assignment.DurationMinutes;
            double percentTime = totalMinutes > 0 ? Math.Round((solveMinutes / totalMinutes) * 100, 1) : 0;

            // 🧠 التوصيات الذكية (نفس منطق الطالب)
            var rec = _examRecommendationService.GetRecommendation(
                summary.ScorePercentage,
                (int)solveMinutes,
                totalMinutes,
                assignment.Exam?.Type
            );

            // 🧩 بناء ViewModel
            var vm = new StudentExamReportViewModel
            {
                StudentName = student.FullName,
                ParentName = student.Parent?.FullName,
                ParentPhone = student.Parent?.WhatsAppNumber,
                Level = student.Level,
                StudentId = student.StudentID,
                ExamTitle = assignment.Title ?? assignment.Exam.Title,
                ExamDate = assignment.AssignedAt,

                TotalQuestions = summary.TotalQuestions,
                CorrectAnswers = summary.CorrectAnswers,
                WrongAnswers = summary.WrongAnswers,
                Skipped = summary.SkippedQuestions,
                Answered = summary.AnsweredQuestions,
                OverallPercent = summary.ScorePercentage,
                ElapsedTimeFormatted = $"{Math.Round(solveMinutes, 1)} دقيقة",
                PercentTimeUsed = percentTime,

                SpeedAccuracyFeedback = rec.PerformanceSummary,
                MotivationalMessage = rec.SpeedNote,
                TrackCard1 = rec.TrackCard1,
                TrackCard1Desc = rec.TrackCard1Desc,
                TrackCard2 = rec.TrackCard2,
                TrackCard2Desc = rec.TrackCard2Desc
            };

            // 🟢 تحليل الأداء حسب المحاور (Sections)
            vm.Sections = attempts
     .Where(a => a.Question?.Lesson?.Section != null)
     .GroupBy(a => new
     {
         a.Question.Lesson.Section.Id,
         a.Question.Lesson.Section.Title
     })
     .Select(g => new QdratNew.ViewModels.Reports.SectionReportItem
     {
         SectionTitle = g.Key.Title,
         Total = g.Count(),
         Correct = g.Count(x => x.IsCorrect),
         Wrong = g.Count(x => !x.IsCorrect),
         Skipped = 0 // يمكنك لاحقًا تعديلها إذا أردت إظهار المتخطاة داخل كل محور
     })
     .OrderByDescending(x => x.Correct)
     .ToList();


            ViewBag.ExamAssignmentId = examAssignmentId;

            return View("~/Areas/Admin/Views/ExamAssignments/StudentExamReport.cshtml", vm);
        }




        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]

        public async Task<IActionResult> StudentExamPerformanceReport(int examAssignmentId, int studentId)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == examAssignmentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            var student = await _context.Students
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("❌ لم يتم العثور على بيانات الطالب.");

            var summary = await _studentExamStatisticsService.GetExamResultAsync(examAssignmentId, studentId);

            double totalMinutes = assignment.DurationMinutes;
            double solveMinutes = summary.SolveMinutes;

            var examQuestions = await LoadExamQuestionsForAssignmentAsync(assignment);
            var attempts = await LoadStudentAttemptsForAssignmentAsync(examAssignmentId, assignment.ExamId, studentId);
            var attemptsByQuestionId = attempts.ToDictionary(a => a.QuestionId, a => a);

            int totalQuestions = examQuestions.Count;
            int correctAnswers = 0;
            int wrongAnswers = 0;
            int skippedQuestions = 0;

            foreach (var examQuestion in examQuestions)
            {
                attemptsByQuestionId.TryGetValue(examQuestion.QuestionId, out var attempt);

                if (IsAttemptSkipped(attempt))
                {
                    skippedQuestions++;
                }
                else if (attempt!.IsCorrect)
                {
                    correctAnswers++;
                }
                else
                {
                    wrongAnswers++;
                }
            }

            double overallPercent = totalQuestions > 0
                ? Math.Round((double)correctAnswers / totalQuestions * 100, 1)
                : 0;

            var rec = _examRecommendationService.GetRecommendation(
                overallPercent,
                (int)solveMinutes,
                totalMinutes,
                assignment.Exam?.Type
            );

            var sectionStats = examQuestions
                .Where(eq => GetQuestionSectionId(eq.Question).HasValue)
                .GroupBy(eq => new
                {
                    SectionId = GetQuestionSectionId(eq.Question)!.Value,
                    SectionTitle = GetQuestionSectionTitle(eq.Question)
                })
                .Select(g =>
                {
                    int sectionCorrect = 0;
                    int sectionWrong = 0;
                    int sectionSkipped = 0;

                    foreach (var examQuestion in g)
                    {
                        attemptsByQuestionId.TryGetValue(examQuestion.QuestionId, out var attempt);

                        if (IsAttemptSkipped(attempt))
                        {
                            sectionSkipped++;
                        }
                        else if (attempt!.IsCorrect)
                        {
                            sectionCorrect++;
                        }
                        else
                        {
                            sectionWrong++;
                        }
                    }

                    return new QdratNew.ViewModels.Reports.SectionPerformancesVm
                    {
                        SectionId = g.Key.SectionId,
                        SectionTitle = g.Key.SectionTitle,
                        Total = g.Count(),
                        Correct = sectionCorrect,
                        Wrong = sectionWrong,
                        Skipped = sectionSkipped
                    };
                })
                .OrderByDescending(x => x.Correct)
                .ToList();

            var vm = new QdratNew.ViewModels.Reports.StudentExamPerformanceReportVm
            {
                StudentName = student.FullName,
                ExamTitle = assignment.Title ?? assignment.Exam?.Title ?? "اختبار بدون عنوان",
                ExamDate = assignment.AssignedAt,

                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                WrongAnswers = wrongAnswers,
                SkippedQuestions = skippedQuestions,
                OverallPercent = overallPercent,
                SolveMinutes = summary.SolveMinutes,
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

            return View("~/Areas/Admin/Views/ExamAssignments/StudentExamPerformanceReport.cshtml", vm);
        }






        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]

        public async Task<IActionResult> SectionLessons(int assignmentId, int examAssignmentId, int sectionId, int? studentId)
        {
            if (assignmentId == 0 && examAssignmentId > 0)
                assignmentId = examAssignmentId;

            var section = await _context.Sections
                .Include(s => s.Curriculum)
                .FirstOrDefaultAsync(s => s.Id == sectionId);

            if (section == null)
                return NotFound("❌ لم يتم العثور على المحور.");

            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على الاختبار.");

            var examQuestions = await LoadExamQuestionsForAssignmentAsync(assignment);
            var sectionQuestions = examQuestions
                .Where(eq => GetQuestionSectionId(eq.Question) == sectionId)
                .Where(eq => eq.Question.LessonId > 0)
                .ToList();

            if (!sectionQuestions.Any())
                return NotFound("⚠️ لا توجد مؤشرات مرتبطة بأسئلة هذا المحور داخل الاختبار.");

            List<QuestionAttemptNew> attempts;
            if (studentId.HasValue && studentId.Value > 0)
            {
                attempts = await LoadStudentAttemptsForAssignmentAsync(assignmentId, assignment.ExamId, studentId.Value);
            }
            else
            {
                attempts = await _context.QuestionAttemptNew
                    .AsNoTracking()
                    .Where(a => a.ExamAssignmentId == assignmentId)
                    .ToListAsync();

                if (!attempts.Any() && assignment.ExamId.HasValue)
                {
                    var sourceExamId = assignment.ExamId.Value;
                    attempts = await _context.QuestionAttemptNew
                        .AsNoTracking()
                        .Where(a => a.ExamId == sourceExamId)
                        .ToListAsync();
                }
            }

            var isStudentSpecificReport = studentId.HasValue && studentId.Value > 0;
            var latestStudentAttempts = isStudentSpecificReport
                ? attempts
                    .GroupBy(a => a.QuestionId)
                    .Select(g => g.OrderByDescending(a => a.AttemptedAt).ThenByDescending(a => a.Id).First())
                    .ToList()
                : new List<QuestionAttemptNew>();

            var attemptsByQuestionId = latestStudentAttempts.ToDictionary(a => a.QuestionId, a => a);

            var lessons = sectionQuestions
                .GroupBy(eq => new
                {
                    LessonId = eq.Question.LessonId,
                    LessonTitle = eq.Question.Lesson?.Title ?? "بدون مؤشر"
                })
                .Select(g =>
                {
                    int correct;
                    int wrong;
                    int skipped;
                    var answeredTimes = new List<double>();

                    if (isStudentSpecificReport)
                    {
                        correct = 0;
                        wrong = 0;
                        skipped = 0;

                        foreach (var examQuestion in g)
                        {
                            attemptsByQuestionId.TryGetValue(examQuestion.QuestionId, out var attempt);

                            if (IsAttemptSkipped(attempt))
                            {
                                skipped++;
                            }
                            else if (attempt!.IsCorrect)
                            {
                                correct++;
                                answeredTimes.Add(attempt.TimeTakenSeconds);
                            }
                            else
                            {
                                wrong++;
                                answeredTimes.Add(attempt.TimeTakenSeconds);
                            }
                        }
                    }
                    else
                    {
                        var questionIds = g.Select(eq => eq.QuestionId).ToHashSet();
                        var lessonAttempts = attempts
                            .Where(a => questionIds.Contains(a.QuestionId))
                            .ToList();

                        correct = lessonAttempts.Count(a => a.IsCorrect);
                        wrong = lessonAttempts.Count(a => !a.IsCorrect && !string.IsNullOrWhiteSpace(a.SelectedAnswer));
                        skipped = Math.Max(0, g.Count() - lessonAttempts.Select(a => a.QuestionId).Distinct().Count());
                        answeredTimes = lessonAttempts.Select(a => a.TimeTakenSeconds).ToList();
                    }

                    var total = g.Count();
                    var rateDenominator = isStudentSpecificReport ? total : Math.Max(1, correct + wrong);
                    var successRate = rateDenominator > 0 ? Math.Round((double)correct / rateDenominator * 100, 1) : 0;

                    return new QdratNew.ViewModels.Exam.LessonPerformanceVm
                    {
                        LessonId = g.Key.LessonId,
                        LessonTitle = g.Key.LessonTitle,
                        LessonName = g.Key.LessonTitle,
                        QuestionCount = total,
                        TotalQuestions = total,
                        CorrectCount = correct,
                        WrongCount = wrong,
                        SkippedCount = skipped,
                        AvgSuccessRate = successRate,
                        SuccessRate = successRate,
                        ExamSuccessRate = successRate,
                        Percent = successRate,
                        AvgTimeSeconds = answeredTimes.Any() ? Math.Round(answeredTimes.Average(), 1) : 0,
                        HardnessIndex = Math.Round(100 - successRate, 1),
                        LastActivityDate = isStudentSpecificReport
                            ? (latestStudentAttempts.Any() ? latestStudentAttempts.Max(a => a.AttemptedAt) : DateTime.MinValue)
                            : (attempts.Any() ? attempts.Max(a => a.AttemptedAt) : DateTime.MinValue)
                    };
                })
                .OrderBy(l => l.LessonTitle)
                .ToList();


            var model = new SectionLessonsViewModel
            {
                AssignmentId = assignment.Id,
                SectionId = section.Id,
                SectionTitle = section.Title,
                StudentId = studentId,
                Lessons = lessons
            };

            return View(model);
        }


        [HttpPost]
        [AdminPermission("ExamAssignments", "Publish")]
        public async Task<IActionResult> ResendExamToStudent(int assignmentId, int studentId)
        {

            // ============================================================
            // 1) فحص وجود تكليف الدفعة
            // ============================================================
            var batchAssignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (batchAssignment == null)
            {
                return Json(new { success = false, message = "❌ لم يتم العثور على بيانات الاختبار." });
            }

            int examId = batchAssignment.ExamId ?? 0;

            // ============================================================
            // 2) تحميل حالة الطالب
            // ============================================================
            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s => s.StudentId == studentId &&
                                          s.ExamAssignmentId == assignmentId);

            // الطالب داخل الاختبار → ممنوع إعادة الإرسال
            if (status != null && !status.IsSubmitted && status.StartedAt != null && status.SubmittedAt == null)
            {
                return Json(new
                {
                    success = false,
                    message = "❌ لا يمكن إعادة الإرسال: الطالب ما زال داخل الاختبار."
                });
            }

            // ============================================================
            // 3) حذف Attempts الخاصة بالطالب
            // ============================================================
            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId &&
                            a.ExamAssignmentId == assignmentId)
                .ToListAsync();

            if (attempts.Any())
                _context.QuestionAttemptNew.RemoveRange(attempts);

            // ============================================================
            // 4) حذف ExamStudentStatus
            // ============================================================
            if (status != null)
                _context.ExamStudentStatuses.Remove(status);

            // ============================================================
            // 5) حذف StudentPerformance
            // ============================================================
            var perf = await _context.StudentPerformances
                .Where(p => p.StudentID == studentId && p.ExamId == examId)
                .ToListAsync();

            if (perf.Any())
                _context.StudentPerformances.RemoveRange(perf);

            await _context.SaveChangesAsync();

            // ============================================================
            // ⚠️ بدون إنشاء ExamAssignment جديد
            // ⚠️ بدون نسخ ExamQuestions
            // لأن الأسئلة موجودة فعلاً داخل ExamAssignmentsToBatches
            // والطالب سيبدأ نفس الاختبار كما هو
            // ============================================================

            return Json(new
            {
                success = true,
                message = "✔ تم حذف بيانات الطالب وتمت إعادة تهيئة الاختبار له بنجاح."
            });
        }


        [HttpGet]
        [AdminPermission("ExamAssignments", "Publish")]

        public async Task<IActionResult> SendToStudents(int assignmentId)
        {
            return await SendToStudentsPost(assignmentId);
        }

        [NonAction]
        [AdminPermission("ExamAssignments", "Publish")]
        public async Task<IActionResult> SendToStudentsPost(int assignmentId)
        {
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(x => x.Exam)
                .Include(x => x.Batch)
                .FirstOrDefaultAsync(x => x.Id == assignmentId);

            if (assignment == null || assignment.IsSentToStudents || assignment.ExamId == null)
                return NotFound();

            // 🕐 احصل على الوقت الحالي UTC وليس المحلي
            var nowUtc = DateTime.UtcNow;

            var students = await _context.StudentBatchEnrollments
                .Where(s => s.BatchId == assignment.BatchId)
                .ToListAsync();

            foreach (var student in students)
            {
                _context.ExamAssignments.Add(new ExamAssignment
                {
                    ExamId = assignment.ExamId.Value,
                    StudentId = student.StudentID,
                    AssignedAt = nowUtc, // ✅ تخزين UTC
                    DueDate = assignment.ScheduledDate // هذا التاريخ مخزن أصلًا كـ UTC من ExamGenerator
                });

                _context.ExamStudentStatuses.Add(new ExamStudentStatus
                {
                    StudentId = student.StudentID,
                    ExamId = assignment.ExamId.Value,
                    ExamAssignmentId = assignment.Id,
                    AssignedAt = nowUtc, // ✅ تخزين UTC
                    Status = ExamStatus.Pending
                });

                // 🟢 إشعار للطالب
                await _advancedNotificationService.SendToStudentAsync(
                    student.StudentID,
                    $"📘 تم إرسال اختبار جديد بعنوان {assignment.Title}، الرجاء الدخول وحله.",
                    NotificationCategory.Exam,
                    $"/Students/Exams/StartExam/{assignment.ExamId}"
                );
            }

            assignment.IsSentToStudents = true;
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إرسال الاختبار لجميع الطلاب بنجاح.";
            return RedirectToAction("Details", new { id = assignment.Id });
        }


        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]

        public async Task<IActionResult> StudentAnalyticsReport(int studentId)
        {

            // 🧩 بيانات الطالب الأساسية
            var student = await _context.Students
                .Include(s => s.BatchEnrollments)
                    .ThenInclude(b => b.Batch)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("❌ لم يتم العثور على الطالب.");

            // 🧠 جلب أداء الطالب العام من الاختبارات والخدمات الموحدة
            var performances = await _context.StudentPerformances
                .Include(p => p.Section)
                .Include(p => p.Curriculum)
                .Where(p => p.StudentID == studentId)
                .ToListAsync();

            // 🧩 بيانات الواجبات
            var homeworks = await _context.HomeworkSetStudents
                .Include(h => h.HomeworkSet)
                .Where(h => h.StudentId == studentId)
                .ToListAsync();

            // 🧩 بيانات الحضور
            var totalLectures = await _context.AttendanceRecords.CountAsync(a => a.StudentId == studentId);
            var presentLectures = await _context.AttendanceRecords.CountAsync(a => a.StudentId == studentId && a.IsPresent);

            double attendancePercent = totalLectures > 0
                ? Math.Round((double)presentLectures / totalLectures * 100, 1)
                : 0;

            // 🔹 الحساب العام
            int totalExams = performances.Count;
            int completedExams = performances.Count(p => p.Score > 0);
            int totalHomeworks = homeworks.Count;
            int completedHomeworks = homeworks.Count(h => h.IsSubmitted);
            double avgExamScore = performances.Any() ? Math.Round(performances.Average(p => p.Score), 1) : 0;
            double overallPerformance = Math.Round(
                (avgExamScore + attendancePercent + (homeworks.Any() ? homeworks.Average(h => h.Score ?? 0) : 0)) / 3, 1);

            // 🧩 تحليل المحاور (Sections)
            var sectionPerformances = performances
                .Where(p => p.SectionId != null)
                .GroupBy(p => p.Section)
                .Select(g => new QdratNew.ViewModels.Reports.SectionPerformanceDetailedVm
                {
                    SectionTitle = g.Key.Title,
                    Total = g.Count(),
                    Correct = g.Count(x => x.Score >= 70),
                    Wrong = g.Count(x => x.Score < 70),
                    Skipped = 0
                }).ToList();

            // 🧩 خط التطور الزمني (Progress Timeline)
            var progressTimeline = performances
                .OrderBy(p => p.ExamDate)
                .Select(p => new PerformanceTimelineVm
                {
                    Label = p.ExamDate.ToString("MM/dd") ?? "—",
                    Value = Math.Round(p.Score, 1)
                })
                .ToList();

            // 🧩 التوصيات الذكية من الخدمة الرسمية
            var rec = _examRecommendationService.GetRecommendation(avgExamScore, (int)overallPerformance, 60);

            // ✅ بناء الـ ViewModel
            var vm = new QdratNew.ViewModels.Reports.StudentAnalyticsDashboard2Vm
            {
                StudentName = student.FullName,
                Level = student.Level,
                BatchName = student.BatchEnrollments.FirstOrDefault()?.Batch?.Name ?? "-",

                TotalExams = totalExams,
                CompletedExams = completedExams,
                TotalHomeworks = totalHomeworks,
                CompletedHomeworks = completedHomeworks,
                AttendancePercent = attendancePercent,
                AverageExamScore = avgExamScore,
                OverallPerformance = overallPerformance,

                SpeedLabel = rec.SpeedLabel,
                SpeedNote = rec.SpeedNote,
                TrackCard1 = rec.TrackCard1,
                TrackCard1Desc = rec.TrackCard1Desc,
                TrackCard2 = rec.TrackCard2,
                TrackCard2Desc = rec.TrackCard2Desc,
                IndividualTips = rec.IndividualTips,

                SectionPerformances = sectionPerformances,
                ProgressTimeline = progressTimeline
            };

            return View("~/Areas/Admin/Views/ExamAssignments/StudentAnalyticsReport.cshtml", vm);
        }


        [HttpGet]
[AdminPermission("ExamAssignments", "Read")]
        public async Task<IActionResult> ModelAssignments()
        {
            // 📘 الواجبات من النماذج الاحترافية
            var homeworks = await (
                from hs in _context.HomeworkSets
                join b in _context.Batches on hs.BatchId equals b.Id
                where hs.IsFromProfessionalModel == true
                orderby hs.CreatedAt descending
                select new ModelAssignmentViewModel
                {
                    Id = hs.Id,
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
                    Type = "واجب"
                }).ToListAsync();

            // 📝 الاختبارات من النماذج الاحترافية
            var exams = await (
                from ea in _context.ExamAssignmentsToBatches
                join b in _context.Batches on ea.BatchId equals b.Id
                join e in _context.Exams on ea.ExamId equals e.Id
                where e.IsFromProfessionalModel == true
                orderby ea.CreatedAt descending
                select new ModelAssignmentViewModel
                {
                    Id = ea.Id,
                    Title = ea.Title,
                    BatchName = b.Name,
                    CreatedAt = ea.CreatedAt,
                    StudentCount = _context.StudentBatchEnrollments
                        .Count(s => s.BatchId == ea.BatchId),
                    QuestionCount = ea.TotalQuestions,
                    Type = "اختبار"
                }).ToListAsync();

            var vm = new ModelAssignmentsPageViewModel
            {
                Homeworks = homeworks ?? new List<ModelAssignmentViewModel>(),
                Exams = exams ?? new List<ModelAssignmentViewModel>()
            };

            return View(vm);
        }


        // ✅ حذف اختبار لطالب
        [AdminPermission("ExamAssignments", "Delete")]
        public async Task<IActionResult> Remove(int examId, int studentId, int assignmentId)
        {
            var status = await _context.ExamStudentStatuses.FirstOrDefaultAsync(x =>
                x.StudentId == studentId && x.ExamAssignmentId == assignmentId);

            var assignment = await _context.ExamAssignments.FirstOrDefaultAsync(x =>
                x.StudentId == studentId && x.ExamId == examId);

            if (status != null)
                _context.ExamStudentStatuses.Remove(status);

            if (assignment != null)
                _context.ExamAssignments.Remove(assignment);

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = assignmentId });
        }

        // =====================================================
        // إدارة صلاحيات الوصول لأرشيف الاختبارات (Owner/Developer فقط)
        // =====================================================
        [HttpGet]
        [AdminPermission("ExamAssignments", "Archive")]
        public async Task<IActionResult> ManageArchiveAccess(int batchId)
        {
            if (!IsExamArchiveOwner())
                return Forbid();

            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null) return NotFound();

            var hasArchivedExams = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Include(e => e.Exam)
                .AnyAsync(e => e.BatchId == batchId &&
                               e.IsArchived &&
                               (e.Exam == null || e.Exam.Type != ExamType.LevelAssessment));

            if (!hasArchivedExams)
            {
                TempData["Error"] = "إدارة الصلاحيات متاحة للدفعات التي تحتوي على اختبارات مؤرشفة فقط.";
                return RedirectToAction(nameof(Index), new { showArchived = true });
            }

            var users = await GetExamArchiveApprovalCandidatesAsync();
            var activeUserIds = (await _context.ExamAssignmentBatchArchiveAccesses
                .AsNoTracking()
                .Where(x => x.BatchId == batchId && x.IsActive)
                .Select(x => x.UserId)
                .ToListAsync()).ToHashSet();

            var items = new List<ExamArchiveUserAccessItem>();
            foreach (var user in users.OrderBy(x => x.FullName ?? x.UserName))
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new ExamArchiveUserAccessItem
                {
                    UserId      = user.Id,
                    DisplayName = user.FullName ?? user.UserName ?? user.Email ?? user.Id,
                    Email       = user.Email ?? string.Empty,
                    Roles       = string.Join("، ", roles),
                    IsAllowed   = activeUserIds.Contains(user.Id)
                });
            }

            var model = new ExamArchiveBatchAccessVM
            {
                BatchId     = batch.Id,
                BatchName   = batch.Name,
                CourseTitle = batch.Course?.Name ?? string.Empty,
                Users       = items
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ExamAssignments", "Archive")]
        public async Task<IActionResult> UpdateArchiveAccess(int batchId, List<string> allowedUserIds)
        {
            if (!IsExamArchiveOwner())
                return Forbid();

            var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Id == batchId);
            if (batch == null) return NotFound();

            allowedUserIds ??= new List<string>();
            var allowedSet = allowedUserIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct().ToHashSet();

            var candidateUsers   = await GetExamArchiveApprovalCandidatesAsync();
            var candidateUserIds = candidateUsers.Select(x => x.Id).ToHashSet();
            allowedSet.RemoveWhere(x => !candidateUserIds.Contains(x));

            var existing        = await _context.ExamAssignmentBatchArchiveAccesses
                .Where(x => x.BatchId == batchId).ToListAsync();
            var existingUserIds = existing.Select(x => x.UserId).ToHashSet();
            var now             = DateTime.UtcNow;
            var currentUserId   = CurrentUserId();

            foreach (var access in existing)
                access.IsActive = allowedSet.Contains(access.UserId);

            foreach (var userId in allowedSet.Where(x => !existingUserIds.Contains(x)))
            {
                _context.ExamAssignmentBatchArchiveAccesses.Add(new ExamAssignmentBatchArchiveAccess
                {
                    BatchId         = batchId,
                    UserId          = userId,
                    GrantedByUserId = currentUserId,
                    GrantedAt       = now,
                    IsActive        = true
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ تم تحديث صلاحيات الوصول للأرشيف. عدد المصرح لهم: {allowedSet.Count}.";
            return RedirectToAction(nameof(ManageArchiveAccess), new { batchId });
        }
    }
}
