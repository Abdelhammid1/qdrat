using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using PuppeteerSharp;

using PuppeteerSharp.Input;
using PuppeteerSharp.Media;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Extensions;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Reports;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Reports;
using QdratNew.ViewModels.Students;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Serilog.Parsing;
using System.IO;
using System.Security.Claims;
using System.Security.Policy;



namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PlacementExamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPlacementExamManagementService _managementService; // PPL-S2
        private readonly ITimeZoneService _timeZoneService;
        private readonly IExamRecommendationService _recommendationService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAdminActivityLogger _activityLogger;
        private readonly ReportBrowserProvider _reportBrowserProvider;

        public PlacementExamsController(
            ApplicationDbContext context,
            IPlacementExamManagementService managementService,
            ITimeZoneService timeZoneService,
            IExamRecommendationService recommendationService,
            UserManager<ApplicationUser> userManager,
            IAdminActivityLogger activityLogger,
            ReportBrowserProvider reportBrowserProvider)
        {
            _context = context;
            _managementService = managementService;
            _timeZoneService = timeZoneService;
            _recommendationService = recommendationService;
            _userManager = userManager;
            _activityLogger = activityLogger;
            _reportBrowserProvider = reportBrowserProvider;
        }

        // PPL-S2: كل منطق الإدارة في الخدمة بنطاق الأدمن (اختبارات OwnerPartnerId == null فقط)
        private PlacementScope AdminScope => PlacementScope.Admin(CurrentUserId(), User.Identity?.Name);

        private void SetResultMessage(PlacementOperationResult result)
        {
            var key = result.Outcome switch
            {
                PlacementOperationOutcome.Success => "Success",
                PlacementOperationOutcome.Info => "Info",
                _ => "Error"
            };
            TempData[key] = result.Message;
        }

        // ─── Archive helpers ──────────────────────────────────────────
        private bool IsPExamArchiveOwner() =>
            User.IsInRole("Owner") || User.IsInRole("Developer");

        private string CurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        private string CurrentUserName() =>
            User.Identity?.Name ?? "غير معروف";

        private async Task<bool> CanAccessArchivedPExamBatchAsync(int batchId)
        {
            var hasArchived = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .AnyAsync(x => x.BatchId == batchId && x.IsArchived);

            if (!hasArchived || IsPExamArchiveOwner())
                return true;

            var userId = CurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            return await _context.PlacementExamBatchArchiveAccesses
                .AsNoTracking()
                .AnyAsync(x => x.BatchId == batchId && x.UserId == userId && x.IsActive);
        }

        private IActionResult PExamArchivedAccessDenied()
        {
            TempData["Error"] = "هذه الدفعة داخل أرشيف اختبارات تحديد المستوى ولا يمكن الوصول إليها إلا للمالك أو المبرمج أو مستخدم لديه موافقة صريحة.";
            return RedirectToAction(nameof(Index));
        }

        private async Task LogPExamArchiveActivityAsync(int batchId, string actionType, string description)
        {
            await _activityLogger.LogAsync(
                actionType,
                description,
                CurrentUserId(),
                CurrentUserName(),
                null,
                null,
                batchId);
        }

        private async Task<List<ApplicationUser>> GetArchiveApprovalCandidateUsersAsync()
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

        // ✅ عرض كل اختبارات تحديد المستوى
        [HttpGet]
        [AdminPermission("PlacementExams", "Read")]
        public async Task<IActionResult> Index()
        {
            var model = await _managementService.GetIndexAsync(AdminScope);
            return View(model);
        }

        // =====================================================
        // ArchivedPlacementExams - عرض الدفعات المؤرشفة
        // =====================================================
        [HttpGet]
        [AdminPermission("PlacementExams", "Archive")]
        public async Task<IActionResult> ArchivedPlacementExams()
        {
            var archivedAssignments = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Include(x => x.Batch)
                .Include(x => x.Exam)
                .Where(x => x.IsArchived && x.Exam != null && x.Exam.Type == ExamType.LevelAssessment && x.Exam.OwnerPartnerId == null) // PPL-S2
                .ToListAsync();

            var batchGroups = archivedAssignments
                .GroupBy(x => new { x.BatchId, x.Batch?.Name })
                .ToList();

            var userIds = archivedAssignments
                .Where(x => !string.IsNullOrEmpty(x.ArchivedByUserId))
                .Select(x => x.ArchivedByUserId!)
                .Distinct()
                .ToList();

            var usersMap = await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName ?? u.UserName ?? u.Id);

            var cards = batchGroups.Select(g =>
            {
                var lastArchived = g.OrderByDescending(x => x.ArchivedAt).FirstOrDefault();
                var archivedByName = lastArchived?.ArchivedByUserId != null && usersMap.ContainsKey(lastArchived.ArchivedByUserId)
                    ? usersMap[lastArchived.ArchivedByUserId]
                    : "غير معروف";
                return new PlacementExamArchivedBatchVm
                {
                    BatchId = g.Key.BatchId,
                    BatchName = g.Key.Name ?? string.Empty,
                    ExamsCount = g.Count(),
                    LastExamDate = g.Max(x => (DateTime?)x.CreatedAt),
                    ArchivedAt = lastArchived?.ArchivedAt,
                    ArchivedByUserName = archivedByName
                };
            })
            .OrderByDescending(x => x.ArchivedAt)
            .ToList();

            var model = new PlacementExamArchivedIndexVM
            {
                TotalArchivedBatches = cards.Count,
                TotalExams = cards.Sum(x => x.ExamsCount),
                BatchCards = cards
            };

            return View(model);
        }

        // =====================================================
        // ArchiveBatches / RestoreBatches
        // =====================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Archive")]
        public async Task<IActionResult> ArchiveBatches(List<int> selectedBatchIds)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "لم تحدد أي دفعة.";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.UtcNow;
            var currentUserId = CurrentUserId();

            var assignments = await _context.ExamAssignmentsToBatches
                .Include(x => x.Batch)
                .Where(x => selectedBatchIds.Contains(x.BatchId) && !x.IsArchived && (x.Exam == null || x.Exam.OwnerPartnerId == null)) // PPL-S2
                .ToListAsync();

            var batchNames = assignments
                .GroupBy(x => x.BatchId)
                .ToDictionary(g => g.Key, g => g.First().Batch?.Name ?? g.Key.ToString());

            foreach (var a in assignments)
            {
                a.IsArchived = true;
                a.ArchivedAt = now;
                a.ArchivedByUserId = currentUserId;
            }

            await _context.SaveChangesAsync();

            foreach (var batchId in selectedBatchIds.Where(batchNames.ContainsKey))
            {
                await LogPExamArchiveActivityAsync(batchId, "PlacementExamBatchArchived",
                    $"تم أرشفة اختبارات تحديد المستوى للدفعة '{batchNames[batchId]}'.");
            }

            var archivedCount = batchNames.Count;
            TempData["Success"] = $"تم أرشفة اختبارات {archivedCount} دفعة بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Archive")]
        public async Task<IActionResult> RestoreBatches(List<int> selectedBatchIds)
        {
            if (selectedBatchIds == null || !selectedBatchIds.Any())
            {
                TempData["Error"] = "لم تحدد أي دفعة.";
                return RedirectToAction(nameof(ArchivedPlacementExams));
            }

            var assignments = await _context.ExamAssignmentsToBatches
                .Include(x => x.Batch)
                .Where(x => selectedBatchIds.Contains(x.BatchId) && x.IsArchived && (x.Exam == null || x.Exam.OwnerPartnerId == null)) // PPL-S2
                .ToListAsync();

            var batchNames = assignments
                .GroupBy(x => x.BatchId)
                .ToDictionary(g => g.Key, g => g.First().Batch?.Name ?? g.Key.ToString());

            foreach (var a in assignments)
            {
                a.IsArchived = false;
                a.ArchivedAt = null;
                a.ArchivedByUserId = null;
            }

            await _context.SaveChangesAsync();

            foreach (var batchId in selectedBatchIds.Where(batchNames.ContainsKey))
            {
                await LogPExamArchiveActivityAsync(batchId, "PlacementExamBatchRestored",
                    $"تم إخراج اختبارات تحديد المستوى للدفعة '{batchNames[batchId]}' من الأرشيف.");
            }

            TempData["Success"] = $"تم إخراج {batchNames.Count} دفعة من الأرشيف بنجاح.";
            return RedirectToAction(nameof(ArchivedPlacementExams));
        }

        // =====================================================
        // ManageArchiveAccess (Owner/Developer only)
        // =====================================================
        [HttpGet]
        [AdminPermission("PlacementExams", "Archive")]
        public async Task<IActionResult> ManageArchiveAccess(int batchId)
        {
            if (!IsPExamArchiveOwner())
                return Forbid();

            var batchName = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            if (batchName == null) return NotFound();

            var hasArchivedExams = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .AnyAsync(x => x.BatchId == batchId && x.IsArchived);

            if (!hasArchivedExams)
            {
                TempData["Error"] = "إدارة موافقات الأرشيف متاحة للدفعات المؤرشفة فقط.";
                return RedirectToAction(nameof(Index));
            }

            var users = await GetArchiveApprovalCandidateUsersAsync();
            var activeUserIds = (await _context.PlacementExamBatchArchiveAccesses
                .AsNoTracking()
                .Where(x => x.BatchId == batchId && x.IsActive)
                .Select(x => x.UserId)
                .ToListAsync()).ToHashSet();

            var items = new List<PlacementExamArchiveUserAccessItem>();
            foreach (var user in users.OrderBy(x => x.FullName ?? x.UserName))
            {
                var roles = await _userManager.GetRolesAsync(user);
                items.Add(new PlacementExamArchiveUserAccessItem
                {
                    UserId = user.Id,
                    DisplayName = user.FullName ?? user.UserName ?? user.Email ?? user.Id,
                    Email = user.Email ?? string.Empty,
                    Roles = string.Join("، ", roles),
                    IsAllowed = activeUserIds.Contains(user.Id)
                });
            }

            var model = new PlacementExamBatchArchiveAccessVM
            {
                BatchId = batchId,
                BatchName = batchName,
                Users = items
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Archive")]
        public async Task<IActionResult> UpdateArchiveAccess(int batchId, List<string> allowedUserIds)
        {
            if (!IsPExamArchiveOwner())
                return Forbid();

            var batchName = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            if (batchName == null) return NotFound();

            allowedUserIds ??= new List<string>();
            var allowedSet = allowedUserIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct().ToHashSet();

            var candidateUsers = await GetArchiveApprovalCandidateUsersAsync();
            var candidateUserIds = candidateUsers.Select(x => x.Id).ToHashSet();
            allowedSet.RemoveWhere(x => !candidateUserIds.Contains(x));

            var existing = await _context.PlacementExamBatchArchiveAccesses
                .Where(x => x.BatchId == batchId).ToListAsync();
            var existingUserIds = existing.Select(x => x.UserId).ToHashSet();
            var now = DateTime.UtcNow;
            var currentUserId = CurrentUserId();

            foreach (var access in existing)
                access.IsActive = allowedSet.Contains(access.UserId);

            foreach (var userId in allowedSet.Where(x => !existingUserIds.Contains(x)))
            {
                _context.PlacementExamBatchArchiveAccesses.Add(new PlacementExamBatchArchiveAccess
                {
                    BatchId = batchId,
                    UserId = userId,
                    GrantedByUserId = currentUserId,
                    GrantedAt = now,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync();

            await LogPExamArchiveActivityAsync(batchId, "PlacementExamArchiveAccessUpdated",
                $"تم تعديل موافقات الوصول لأرشيف اختبارات الدفعة '{batchName}'. عدد المصرح لهم: {allowedSet.Count}.");

            TempData["Success"] = "تم تحديث موافقات الوصول للأرشيف.";
            return RedirectToAction(nameof(ManageArchiveAccess), new { batchId });
        }

        // =====================================================
        // ArchiveHistory - سجل تاريخ الأرشفة والتعديلات
        // =====================================================
        [HttpGet]
        [AdminPermission("PlacementExams", "Archive")]
        public async Task<IActionResult> ArchiveHistory(int batchId)
        {
            if (!await CanAccessArchivedPExamBatchAsync(batchId))
                return PExamArchivedAccessDenied();

            var batchName = await _context.Batches
                .AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            if (batchName == null) return NotFound();

            var items = await _context.AdminActivityLogs
                .AsNoTracking()
                .Where(x => x.BatchId == batchId &&
                             x.ActionType.StartsWith("PlacementExam"))
                .OrderByDescending(x => x.Timestamp)
                .Select(x => new PlacementExamArchiveHistoryItem
                {
                    Timestamp = x.Timestamp,
                    AdminName = x.AdminName,
                    ActionType = x.ActionType,
                    Description = x.Description
                })
                .ToListAsync();

            var model = new PlacementExamBatchArchiveHistoryVM
            {
                BatchId = batchId,
                BatchName = batchName,
                Items = items
            };

            return View(model);
        }



        [HttpGet]
        [AdminPermission("PlacementExams", "Read")]
        public async Task<IActionResult> Review(int examId)
        {
            var model = await _managementService.GetReviewAsync(AdminScope, examId);
            if (model == null)
                return NotFound();

            return View(model);
        }


        // ✅ تفعيل/تعطيل خيار "لا أعرف الإجابة" الخامس لاختبار تحديد مستوى معين
        // ملاحظة: نستخدم صلاحية "Create" (المسجّلة فعليًا في Program.cs لنفس الكنترولر لعمليات التعديل/الإرسال)
        // لأن صلاحية "Update" غير مسجّلة أصلاً في نظام الصلاحيات، وكانت ستتسبب في خطأ 500 عند التنفيذ.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> ToggleDontKnowOption(int examId, bool enabled)
        {
            var result = await _managementService.ToggleDontKnowOptionAsync(AdminScope, examId, enabled);

            if (!result.Succeeded)
                return Json(new { success = false, message = result.Message });

            return Json(new
            {
                success = true,
                allowDontKnowOption = enabled,
                message = result.Message
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> SendExamToStudents(int examId)
        {
            SetResultMessage(await _managementService.SendToStudentsAsync(AdminScope, examId));
            return RedirectToAction("Index");
        }

        // ✅ إرسال الاختبار للطلاب الذين انضموا للدفعة بعد الإرسال الأول فقط
        // (لا يُنشئ لهم SendExamToStudents تعيينًا لأنه يُمنع تكرار إرسال الاختبار للدفعة بالكامل)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> SendExamToNewStudents(int examId, int batchId)
        {
            SetResultMessage(await _managementService.SendToNewStudentsAsync(AdminScope, examId, batchId));
            return RedirectToAction("BatchStudents", new { examId, batchId });
        }

        // ✅ إصلاح بيانات الطلاب الذين أنهوا الاختبار فعليًا لكن بلا سجل تعيين/حالة
        // (بسبب النقص القديم في تهيئة ExamAssignments/ExamStudentStatuses عند إرسال اختبار الدفعة)
        // نبني السجلات الناقصة من محاولاتهم الفعلية المحفوظة في QuestionAttemptNew حتى تظهر تقاريرهم وتُطبع بشكل صحيح
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> BackfillMissingAssignments(int examId, int batchId)
        {
            SetResultMessage(await _managementService.BackfillMissingAssignmentsAsync(AdminScope, examId, batchId));
            return RedirectToAction("BatchStudents", new { examId, batchId });
        }

        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> BatchPerformanceReport(int examId, int batchId)
        {
            // 🟦 1. جلب بيانات الدفعة والاختبار
            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
            var batch = await _context.Batches.FirstOrDefaultAsync(b => b.Id == batchId);
            if (exam == null || batch == null)
                return Content("<div class='alert alert-danger text-center mt-5'>❌ لم يتم العثور على بيانات الدفعة أو الاختبار.</div>", "text/html; charset=utf-8");

            // 🟩 2. جلب الأسئلة والمحاور
            var examQuestions = await _context.ExamQuestions
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
                .Where(eq => eq.ExamId == examId)
                .ToListAsync();

            if (!examQuestions.Any())
                return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لا توجد أسئلة مرتبطة بهذا الاختبار.</div>", "text/html; charset=utf-8");

            // 🟨 3. جلب كل محاولات طلاب الدفعة فقط
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join s in _context.Students on a.StudentId equals s.StudentID
                join sb in _context.StudentBatchEnrollments on s.StudentID equals sb.StudentID
                where a.ExamId == examId && sb.BatchId == batchId
                select new
                {
                    a.QuestionId,
                    a.IsCorrect
                }
            ).ToListAsync();

            // 🧮 4. بناء تقرير الأداء لكل محور
            var grouped = examQuestions
                .GroupBy(eq => new { eq.Question.Lesson.SectionId, eq.Question.Lesson.Section.Title })
                .Select(secGroup => new
                {
                    secGroup.Key.SectionId,
                    SectionTitle = secGroup.Key.Title,
                    Lessons = secGroup.GroupBy(x => new { x.Question.LessonId, x.Question.Lesson.Title })
                        .Select(lessonGroup =>
                        {
                            var questionIds = lessonGroup.Select(x => x.Question.Id).ToList();
                            var total = questionIds.Count;
                            var correctCount = attempts.Count(a => questionIds.Contains(a.QuestionId) && a.IsCorrect);
                            var wrongCount = attempts.Count(a => questionIds.Contains(a.QuestionId) && !a.IsCorrect);
                            var totalAttempts = correctCount + wrongCount;
                            var percent = totalAttempts > 0 ? Math.Round(correctCount * 100.0 / totalAttempts, 1) : 0.0;

                            return new QdratNew.ViewModels.Reports.LessonPerformanceEntry
                            {
                                LessonId = lessonGroup.Key.LessonId,
                                LessonTitle = lessonGroup.Key.Title,
                                TotalQuestions = total,
                                Correct = correctCount,
                                Wrong = wrongCount,
                                Score = (int)percent
                            };
                        }).ToList()
                }).ToList();

            // 🧾 5. إنشاء ViewModel للعرض
            var model = new AdminExamSectionAnalysisViewModel
            {
                ExamId = examId,
                AssignmentId = 0,
                StudentId = 0,
                ExamTitle = $"{exam.Title} - {batch.Name}",
                StudentName = $"دفعة {batch.Name}",
                SectionReports = grouped.Select(s => new QdratNew.ViewModels.Reports.SectionPerformanceEntry
                {
                    SectionId = s.SectionId,
                    SectionTitle = s.SectionTitle,
                    TotalQuestions = s.Lessons.Sum(l => l.TotalQuestions),
                    Correct = s.Lessons.Sum(l => l.Correct),
                    Wrong = s.Lessons.Sum(l => l.Wrong),
                    Score = s.Lessons.Sum(l => l.Correct + l.Wrong) > 0
                        ? (int)(s.Lessons.Sum(l => l.Correct) * 100.0 / s.Lessons.Sum(l => l.Correct + l.Wrong))
                        : 0,
                    LessonBreakdown = s.Lessons
                }).ToList()
            };

            return View("~/Areas/Admin/Views/PlacementExams/BatchPerformanceReport.cshtml", model);
        }





        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> ReviewLessonQuestionsBatch(int examId, int lessonId)
        {
            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
            if (exam == null) return NotFound();

            var lesson = await _context.Lessons
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId);
            if (lesson == null) return NotFound();

            var batchId = await _context.ExamAssignmentsToBatches
                .Where(e => e.ExamId == examId)
                .Select(e => e.BatchId)
                .FirstOrDefaultAsync();

            int totalBatchStudents = await _context.StudentBatchEnrollments
                .CountAsync(sb => sb.BatchId == batchId);

            var questionIds = await (
                from eq in _context.ExamQuestions
                join q in _context.Questions on eq.QuestionId equals q.Id
                where eq.ExamId == examId && q.LessonId == lessonId
                select q.Id
            ).ToListAsync();

            var questions = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .Where(q => questionIds.Contains(q.Id))
                .ToListAsync();

            var attempts = await (
                from a in _context.QuestionAttemptNew
                join s in _context.Students on a.StudentId equals s.StudentID
                join sb in _context.StudentBatchEnrollments on s.StudentID equals sb.StudentID
                where a.ExamId == examId && sb.BatchId == batchId
                select new { a.QuestionId, a.IsCorrect, a.SelectedAnswer, a.TimeTakenSeconds }
            ).ToListAsync();

            var optionLabels = new[] { "أ", "ب", "ج", "د" };

            var entries = questions.Select(q =>
            {
                var qa = attempts.Where(a => a.QuestionId == q.Id).ToList();
                int total = qa.Count;
                int correct = qa.Count(a => a.IsCorrect);
                int wrong = total - correct;
                int skipped = Math.Max(0, totalBatchStudents - total);
                double percent = total > 0 ? Math.Round(correct * 100.0 / total, 1) : 0.0;
                double avgTime = total > 0 ? Math.Round(qa.Average(a => a.TimeTakenSeconds), 1) : 0.0;

                var optionEntries = q.Options
                    .Select((opt, idx) =>
                    {
                        int selCount = qa.Count(a =>
                            !string.IsNullOrWhiteSpace(a.SelectedAnswer) &&
                            a.SelectedAnswer.Trim() == opt.Text?.Trim());
                        return new BatchQuestionOptionEntry
                        {
                            Label = idx < optionLabels.Length ? optionLabels[idx] : (idx + 1).ToString(),
                            Text = opt.Text,
                            ImageUrl = opt.ImageUrl,
                            IsCorrect = !string.IsNullOrWhiteSpace(q.CorrectAnswer)
                                && opt.Text?.Trim() == q.CorrectAnswer.Trim(),
                            SelectedCount = selCount,
                            SelectionPercent = total > 0
                                ? Math.Round(selCount * 100.0 / total, 1)
                                : 0.0
                        };
                    }).ToList();

                return new BatchLessonQuestionEntry
                {
                    QuestionId = q.Id,
                    QuestionTitle = q.Title,
                    ImageUrl = q.ImageUrl,
                    Template = q.Template,
                    ValueA = q.ValueA,
                    ValueB = q.ValueB,
                    HasVerbalPassage = q.VerbalPassage != null,
                    VerbalPassageTitle = q.VerbalPassage?.Title,
                    VerbalPassageContent = q.VerbalPassage?.Content,
                    VerbalPassageType = q.VerbalPassage?.Type ?? PassageType.Text,
                    VerbalPassageMediaUrl = q.VerbalPassage?.MediaUrl,
                    TotalAnswers = total,
                    CorrectAnswers = correct,
                    WrongAnswers = wrong,
                    SkippedCount = skipped,
                    Percent = percent,
                    AvgTimeTakenSeconds = avgTime,
                    Options = optionEntries
                };
            }).ToList();

            var model = new BatchLessonQuestionsReportViewModel
            {
                ExamTitle = exam.Title,
                LessonTitle = lesson.Title,
                SectionTitle = lesson.Section.Title,
                TotalQuestions = entries.Count,
                TotalCorrect = entries.Sum(e => e.CorrectAnswers),
                TotalWrong = entries.Sum(e => e.WrongAnswers),
                OverallPercent = entries.Sum(e => e.CorrectAnswers + e.WrongAnswers) > 0
                    ? Math.Round(entries.Sum(e => e.CorrectAnswers) * 100.0 /
                                 entries.Sum(e => e.CorrectAnswers + e.WrongAnswers), 1)
                    : 0,
                Questions = entries
            };

            return View("~/Areas/Admin/Views/PlacementExams/ReviewLessonQuestionsBatch.cshtml", model);
        }



        [HttpGet]
        [AdminPermission("PlacementExams", "Read")]
        public async Task<IActionResult> BatchExams(int batchId)
        {
            var batchName = await _managementService.GetBatchNameAsync(AdminScope, batchId);
            var exams = await _managementService.GetBatchExamsAsync(AdminScope, batchId);

            ViewBag.BatchName = batchName;
            return View(exams);
        }

        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> ExamSectionAnalysis(int assignmentId, int studentId)
        {
            // ✅ جلب بيانات الاختبار
            var assignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار أو الطالب.");

            // ✅ جلب الأسئلة الخاصة بالاختبار
            var examQuestions = await _context.ExamQuestions
                .Include(eq => eq.Question)
                    .ThenInclude(q => q.Lesson)
                        .ThenInclude(l => l.Section)
.Where(eq => eq.ExamId == assignment.ExamId)
                .ToListAsync();

            // ✅ جلب محاولات الطالب في نفس الاختبار
            var attempts = await _context.QuestionAttemptNew
     .Where(a => a.StudentId == studentId && a.ExamId == assignment.ExamId)
     .ToListAsync();


            // ✅ تجميع بيانات الأسئلة حسب المحور والمؤشر
            var data = examQuestions.Select(eq =>
            {
                var attempt = attempts.FirstOrDefault(a => a.QuestionId == eq.QuestionId);
                return new
                {
                    SectionId = eq.Question.Lesson.SectionId,
                    SectionTitle = eq.Question.Lesson.Section.Title,
                    LessonTitle = eq.Question.Lesson.Title,
                    IsCorrect = attempt != null && attempt.IsCorrect
                };
            }).ToList();

            // 🧮 تجميع حسب المحور
            var grouped = data.GroupBy(d => new { d.SectionId, d.SectionTitle })
                .Select(g => new
                {
                    g.Key.SectionId,
                    g.Key.SectionTitle,
                    Lessons = g.GroupBy(x => x.LessonTitle)
                               .Select(lg => new
                               {
                                   LessonTitle = lg.Key,
                                   Total = lg.Count(),
                                   Correct = lg.Count(x => x.IsCorrect),
                                   Wrong = lg.Count(x => !x.IsCorrect),
                                   Score = lg.Count() > 0 ? (int)(lg.Count(x => x.IsCorrect) * 100.0 / lg.Count()) : 0
                               }).ToList()
                }).ToList();

            // ✅ بناء الـ ViewModel النهائي
            var model = new AdminExamSectionAnalysisViewModel
            {
                AssignmentId = assignmentId,
                ExamId = assignment.ExamId,
                StudentId = studentId,
                ExamTitle = assignment.Exam?.Title ?? "اختبار غير معروف",
                StudentName = await _context.Students
        .Where(s => s.StudentID == studentId)
        .Select(s => s.FullName)
        .FirstOrDefaultAsync(),
                SectionReports = grouped.Select(s => new QdratNew.ViewModels.Reports.SectionPerformanceEntry
                {
                    SectionId = s.SectionId,
                    SectionTitle = s.SectionTitle,
                    TotalQuestions = s.Lessons.Sum(l => l.Total),
                    Correct = s.Lessons.Sum(l => l.Correct),
                    Wrong = s.Lessons.Sum(l => l.Wrong),
                    Score = s.Lessons.Sum(l => l.Total) > 0
                        ? (int)(s.Lessons.Sum(l => l.Correct) * 100.0 / s.Lessons.Sum(l => l.Total))
                        : 0,
                    LessonBreakdown = s.Lessons.Select(l => new QdratNew.ViewModels.Reports.LessonPerformanceEntry
                    {
                        LessonId = _context.Lessons
                            .Where(ls => ls.Title == l.LessonTitle)
                            .Select(ls => ls.Id)
                            .FirstOrDefault(), // ✅ يجلب LessonId الصحيح
                        LessonTitle = l.LessonTitle,
                        TotalQuestions = l.Total,
                        Correct = l.Correct,
                        Wrong = l.Wrong,
                        Score = l.Score
                    }).ToList()
                }).ToList()
            };

            return View(model);
        }





        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> ReviewLessonQuestions(int assignmentId, int studentId, int lessonId)
        {
            // ✅ 1. جلب بيانات التعيين والاختبار
            var assignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == assignmentId);

            if (assignment == null)
                return NotFound("❌ لم يتم العثور على هذا الاختبار.");

            var examId = assignment.ExamId;

            // ✅ 2. جلب بيانات المؤشر (الدرس)
            var lesson = await _context.Lessons
                .Include(l => l.Section)
                .FirstOrDefaultAsync(l => l.Id == lessonId);

            if (lesson == null)
                return NotFound("❌ لم يتم العثور على هذا المؤشر.");

            // ✅ 3. جلب الأسئلة الخاصة بالمؤشر داخل هذا الاختبار
            var questions = await (
                from eq in _context.ExamQuestions
                join q in _context.Questions on eq.QuestionId equals q.Id
                where eq.ExamId == examId && q.LessonId == lessonId
                select new
                {
                    q.Id,
                    q.Title,
                    q.ImageUrl,
                    q.CorrectAnswer,
                    q.IsQuantitative
                }
            ).ToListAsync();

            if (!questions.Any())
                return Content("<div class='alert alert-warning text-center mt-4'>⚠️ لا توجد أسئلة لهذا المؤشر في هذا الاختبار.</div>", "text/html");

            // ✅ 4. جلب محاولات الطالب على هذه الأسئلة
            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.ExamId == examId)
                .ToListAsync();

            // ✅ 5. بناء نموذج العرض
            var vm = new ExamReviewViewModel
            {
                AssignmentId = assignmentId,
                ExamTitle = $"{assignment.Exam.Title} - {lesson.Title}",
                ExamAssignmentId = assignment.Id,
                TotalQuestions = questions.Count,
                TimeSpentMinutes = 0,
                Questions = questions.Select(q =>
                {
                    var attempt = attempts.FirstOrDefault(a => a.QuestionId == q.Id);

                    return new ExamReviewQuestionVm
                    {
                        QuestionId = q.Id,
                        QuestionTitle = q.Title,
                        ImageUrl = q.ImageUrl,
                        CorrectAnswer = q.CorrectAnswer,
                        StudentAnswer = attempt?.SelectedAnswer ?? "—",
                        IsCorrect = attempt?.IsCorrect ?? false,
                        TimeTakenSeconds = attempt?.TimeTakenSeconds ?? 0,
                        DisplayType = QuestionDisplayType.TextOnly, // ✅ القيمة الافتراضية الآمنة
                        IsQuantitative = q.IsQuantitative,
                        Options = _context.QuestionOptions
                            .Where(o => o.QuestionId == q.Id)
                            .Select(o => new QuestionOptionVm
                            {
                                Text = o.Text,
                                ImageUrl = o.ImageUrl
                            })
                            .ToList()
                    };
                }).ToList()
            };

            // ✅ 6. عرض الصفحة بالمسار الكامل
            return View("~/Areas/Admin/Views/PlacementExams/ReviewLessonQuestions.cshtml", vm);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Delete")]
        public async Task<IActionResult> HideExam(int id, int? batchId)
        {
            SetResultMessage(await _managementService.HideAsync(AdminScope, id));

            // ✅ بعد الحفظ ارجع إلى نفس صفحة الدفعة إن وُجد batchId
            if (batchId.HasValue)
                return RedirectToAction("BatchExams", new { batchId });

            // لو ما كان فيه batchId، ارجع إلى الصفحة الرئيسية
            return RedirectToAction("Index");
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Delete")]

        public async Task<IActionResult> RestoreExam(int id, int? batchId)
        {
            SetResultMessage(await _managementService.RestoreAsync(AdminScope, id));

            return batchId.HasValue
                ? RedirectToAction("BatchExams", new { batchId })
                : RedirectToAction("Index");
        }



        // ===================== حذف نهائي لاختبار من دفعة =====================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Delete")]
        public async Task<IActionResult> DeleteExamPermanently(int examId, int batchId)
        {
            SetResultMessage(await _managementService.DeletePermanentlyAsync(AdminScope, examId, batchId));
            return RedirectToAction("BatchExams", new { batchId });
        }




        [HttpGet]
        [AdminPermission("PlacementExams", "Read")]
        public async Task<IActionResult> BatchStudents(int examId, int batchId)
        {
            var batchName = await _context.Batches
                .Where(b => b.Id == batchId && b.IsActive)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();

            var examTitle = await _context.Exams
                .Where(e => e.Id == examId)
                .Select(e => e.Title)
                .FirstOrDefaultAsync();

            // 🧮 عدد أسئلة الاختبار — يُستخدم لاعتبار الطالب "منتهيًا" إذا أجاب على كل الأسئلة
            // حتى لو لم يُستدعَ SubmitFinal (مثل إغلاق المتصفح بعد آخر سؤال أو انقطاع الشبكة)
            var totalQuestions = await _context.ExamQuestions
                .CountAsync(eq => eq.ExamId == examId);

            var rawStudents = await (
                from s in _context.Students
                join sb in _context.StudentBatchEnrollments on s.StudentID equals sb.StudentID
                join ea in _context.ExamAssignments
                    .Where(e => e.ExamId == examId)
                    on s.StudentID equals ea.StudentId into studentAssignments
                from lastEa in studentAssignments
                    .OrderByDescending(x => x.AssignedAt)
                    .Take(1)
                    .DefaultIfEmpty()
                where sb.BatchId == batchId
                select new
                {
                    s.StudentID,
                    s.FullName,
                    // ⚠️ إن لم يوجد سجل ExamAssignments لهذا الطالب (مثلاً انضم للدفعة بعد الإرسال الأول)
                    // نحاول استخراج رقم التعيين الحقيقي من محاولاته المسجلة فعليًا على هذا الاختبار
                    AssignmentId = lastEa != null ? lastEa.Id :
                        (_context.QuestionAttemptNew
                            .Where(a => a.StudentId == s.StudentID &&
                                        a.ExamId == examId &&
                                        a.ExamAssignmentId != null)
                            .Select(a => a.ExamAssignmentId)
                            .FirstOrDefault() ?? 0),

                    IsStatusCompleted = _context.ExamStudentStatuses
                        .Any(st =>
                            st.StudentId == s.StudentID &&
                            st.ExamId == examId &&
                            (st.Status == ExamStatus.Completed || st.IsSubmitted)),

                    ExamStatus = _context.ExamStudentStatuses
                        .Where(st =>
                            st.StudentId == s.StudentID &&
                            st.ExamId == examId)
                        .Select(st => st.Status)
                        .FirstOrDefault(),

                    AnsweredCount = _context.QuestionAttemptNew
                        .Count(a => a.StudentId == s.StudentID &&
                            (a.ExamId == examId || (lastEa != null && a.ExamAssignmentId == lastEa.Id))),

                    CorrectCount = _context.QuestionAttemptNew
                        .Count(a => a.StudentId == s.StudentID &&
                            (a.ExamId == examId || (lastEa != null && a.ExamAssignmentId == lastEa.Id)) &&
                            a.IsCorrect)
                }
            ).ToListAsync();

            var fallbackAssignments = await (
                from sb in _context.StudentBatchEnrollments
                join ea in _context.ExamAssignments on sb.StudentID equals ea.StudentId
                join eab in _context.ExamAssignmentsToBatches on ea.ExamId equals eab.ExamId
                where sb.BatchId == batchId && eab.BatchId == batchId
                select new
                {
                    StudentId = sb.StudentID,
                    AssignmentId = ea.Id,
                    ea.ExamId,
                    ea.AssignedAt,
                    HasAttempts = _context.QuestionAttemptNew.Any(a =>
                        a.StudentId == sb.StudentID &&
                        (a.ExamId == ea.ExamId || a.ExamAssignmentId == ea.Id)),
                    IsCompleted = _context.ExamStudentStatuses.Any(st =>
                        st.StudentId == sb.StudentID &&
                        (st.ExamId == ea.ExamId || st.ExamAssignmentId == ea.Id) &&
                        (st.Status == ExamStatus.Completed || st.IsSubmitted)),
                    TotalQuestions = _context.ExamQuestions.Count(eq => eq.ExamId == ea.ExamId),
                    AnsweredCount = _context.QuestionAttemptNew.Count(a =>
                        a.StudentId == sb.StudentID &&
                        (a.ExamId == ea.ExamId || a.ExamAssignmentId == ea.Id)),
                    CorrectCount = _context.QuestionAttemptNew.Count(a =>
                        a.StudentId == sb.StudentID &&
                        (a.ExamId == ea.ExamId || a.ExamAssignmentId == ea.Id) &&
                        a.IsCorrect)
                }
            ).ToListAsync();

            var fallbackAssignmentByStudent = fallbackAssignments
                .GroupBy(x => x.StudentId)
                .ToDictionary(
                    g => g.Key,
                    g => g
                        .OrderByDescending(x => x.HasAttempts)
                        .ThenByDescending(x => x.IsCompleted)
                        .ThenByDescending(x => x.AssignedAt)
                        .First());

            // 🛡️ Translation Guard: طلاب موقوفون حاليًا على اختبار تحديد المستوى هذا
            var blockedPlacementKeys = (await _context.IntegrityViolationLogs
                .Where(v => v.AttemptType == IntegrityAttemptType.Placement && !v.IsResolved)
                .Select(v => new { v.StudentId, v.AttemptEntityId })
                .ToListAsync())
                .Select(v => (v.StudentId, v.AttemptEntityId))
                .ToHashSet();

            // ⚖️ نفس منطق تحديد "الحل" المستخدم في تقرير الطالب (PlacementReport):
            // إما أن يكون هناك سجل حالة مكتمل صراحةً، أو أن الطالب أجاب على كل الأسئلة فعليًا
            var students = rawStudents.Select(s =>
            {
                fallbackAssignmentByStudent.TryGetValue(s.StudentID, out var fallbackAssignment);
                var reportAssignmentId = s.AssignmentId;

                if (fallbackAssignment != null &&
                    (reportAssignmentId == 0 || (s.AnsweredCount == 0 && fallbackAssignment.HasAttempts)))
                {
                    reportAssignmentId = fallbackAssignment.AssignmentId;
                }

                var displayAnsweredCount = s.AnsweredCount > 0
                    ? s.AnsweredCount
                    : fallbackAssignment?.AnsweredCount ?? 0;

                var displayCorrectCount = s.AnsweredCount > 0
                    ? s.CorrectCount
                    : fallbackAssignment?.CorrectCount ?? 0;

                var displayTotalQuestions = s.AnsweredCount > 0
                    ? totalQuestions
                    : fallbackAssignment?.TotalQuestions ?? totalQuestions;

                bool hasFullAttempts = displayTotalQuestions > 0 && displayAnsweredCount >= displayTotalQuestions;
                bool isSubmitted = s.IsStatusCompleted || (fallbackAssignment?.IsCompleted ?? false) || hasFullAttempts;

                return new PlacementExamStudentVm
                {
                    StudentId = s.StudentID,
                    StudentName = s.FullName,
                    AssignmentId = reportAssignmentId,
                    IsSubmitted = isSubmitted,
                    ExamStatus = isSubmitted ? ExamStatus.Completed : s.ExamStatus,
                    Score = displayAnsweredCount > 0
                        ? Math.Round(displayCorrectCount * 100.0 / displayAnsweredCount, 1)
                        : 0,
                    IsIntegrityBlocked = blockedPlacementKeys.Contains((s.StudentID, reportAssignmentId))
                };
            }).ToList();

            // ✅ تمرير القيم الضرورية للـ View بدون تغيير الموديل
            ViewBag.BatchName = batchName;
            ViewBag.ExamTitle = examTitle;
            ViewBag.ExamId = examId;
            ViewBag.BatchId = batchId;

            return View(students);
        }


        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> DownloadStudentReport(int examId, int studentId)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);
            var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);

            if (student == null || exam == null)
                return NotFound();

            var answers = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                where a.StudentId == studentId && a.ExamId == examId
                select new
                {
                    q.Title,
                    q.CorrectAnswer,
                    a.SelectedAnswer,
                    a.IsCorrect
                }
            ).ToListAsync();

            var correct = answers.Count(x => x.IsCorrect);
            var wrong = answers.Count(x => !x.IsCorrect);

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontFamily("Cairo").FontSize(13));

                    // 🟦 رأس الصفحة مع اللوجو والعنوان
                    page.Header().Row(row =>
                    {
                        row.RelativeColumn()
                            .AlignCenter()
                            .Text("تقرير اختبار الطالب")
                            .Bold()
                            .FontSize(18)
                            .FontColor("#0078D7");

                        var logoPath = Path.Combine("wwwroot", "images", "160×100.png");
                        if (System.IO.File.Exists(logoPath))
                            row.ConstantColumn(80).Image(logoPath);
                    });

                    // 📋 بيانات الطالب بصندوق ملون لطيف
                    page.Content().PaddingVertical(15).Column(col =>
                    {
                        col.Item().Background("#f4f8ff").Padding(15).Column(info =>
                        {
                            info.Item().AlignRight().Text($"👨‍🎓 الطالب: {student.FullName}").Bold();
                            info.Item().AlignRight().Text($"🧠 الاختبار: {exam.Title}");
                            info.Item().AlignRight().Text($"📅 تاريخ الإنشاء: {exam.CreatedAt:yyyy-MM-dd HH:mm}");
                            info.Item().AlignRight().Text($"✅ الصحيحة: {correct}    ❌ الخاطئة: {wrong}");
                        });

                        col.Item().PaddingVertical(10);

                        // 🧾 جدول الأسئلة
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40); // م
                                columns.RelativeColumn(3);   // الإجابة الصحيحة
                                columns.RelativeColumn(3);   // إجابتك
                                columns.RelativeColumn(6);   // السؤال
                            });

                            // رأس الجدول
                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyle).AlignCenter().Text("م").Bold();
                                header.Cell().Element(CellStyle).AlignCenter().Text("الإجابة الصحيحة").Bold();
                                header.Cell().Element(CellStyle).AlignCenter().Text("إجابتك").Bold();
                                header.Cell().Element(CellStyle).AlignCenter().Text("السؤال").Bold();
                            });

                            int index = 1;
                            foreach (var a in answers)
                            {
                                table.Cell().Element(CellStyle).AlignCenter().Text(index++.ToString());
                                table.Cell().Element(CellStyle).AlignCenter()
                                    .Text(a.CorrectAnswer ?? "—").FontColor("#0B6E4F");
                                table.Cell().Element(CellStyle).AlignCenter()
                                    .Text(a.SelectedAnswer ?? "—")
                                    .FontColor(a.IsCorrect ? "#198754" : "#C00");
                                table.Cell().Element(CellStyle).AlignRight()
                                    .Text(a.Title ?? "—");
                            }

                            // 🧱 تصميم الخلايا
                            static IContainer CellStyle(IContainer container)
                            {
                                return container.Border(0.5f)
                                                .BorderColor("#D0D7E2")
                                                .Padding(5)
                                                .AlignMiddle();
                            }
                        });
                    });

                    // 🕓 التذييل
                    page.Footer().AlignCenter()
                        .Text($"تم إنشاء التقرير بتاريخ {_timeZoneService.GetNowSaudi():yyyy/MM/dd HH:mm}")
                        .FontSize(10)
                        .FontColor("#666");
                });
            });

            var pdfBytes = pdf.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"تقرير_{student.FullName}.pdf");
        }

        [HttpGet]
        [AdminPermission("PlacementExams", "Read")]

        public async Task<IActionResult> GetSectionsByCourse(int courseId)
        {
            var data = await _managementService.GetSectionsByCourseAsync(AdminScope, courseId);
            return Json(data);
        }


        // ✅ عرض صفحة إنشاء اختبار تحديد المستوى المتقدم
        [HttpGet]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> CreateAdvanced()
        {
            var model = await _managementService.GetCreateAdvancedAsync(AdminScope);
            return View(model);
        }


        // ✅ تنفيذ إنشاء اختبار تحديد المستوى (يدوي + تلقائي)
        [HttpPost]
        [ActionName("CreateAdvancedPost")]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> CreateAdvancedPost(PlacementExamCreateAdvancedViewModel model)
        {
            var result = await _managementService.CreateAdvancedAsync(AdminScope, model);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Message;
                await _managementService.ReloadCreateAdvancedLookupsAsync(AdminScope, model);
                return View("CreateAdvanced", model);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction("Review", new { examId = result.ExamId });
        }



        [HttpGet]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> CreateFromProfessionalModel()
        {
            var vm = await _managementService.GetCreateFromModelAsync(AdminScope);
            return View(vm);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> CreateFromProfessionalModel(PlacementExamFromModelVm vm)
        {
            var result = await _managementService.CreateFromProfessionalModelAsync(AdminScope, vm);

            if (!result.Succeeded)
            {
                TempData["Error"] = result.Message;
                await _managementService.ReloadCreateFromModelLookupsAsync(AdminScope, vm);
                return View("CreateFromProfessionalModel", vm);
            }

            TempData["Success"] = result.Message;
            return RedirectToAction("Index");
        }




        [HttpGet]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> ReplaceQuestion(int examId, Guid questionId)
        {
            var model = await _managementService.GetReplaceQuestionAsync(AdminScope, examId, questionId);
            if (model == null)
                return NotFound();

            return View(model);
        }

        [HttpGet]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> AddQuestionFromBank(int examId, int sectionId)
        {
            var (result, model) = await _managementService.GetAddQuestionFromBankAsync(AdminScope, examId, sectionId);

            if (result.Outcome == PlacementOperationOutcome.NotFound)
                return NotFound();

            if (result.Outcome == PlacementOperationOutcome.BadRequest)
                return BadRequest(result.Message);

            return View(model);
        }


      
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> AddQuestionFromBank(
    int examId,
    int sectionId,
    Guid questionId)
        {
            var result = await _managementService.AddQuestionFromBankAsync(AdminScope, examId, sectionId, questionId);

            if (result.Outcome == PlacementOperationOutcome.NotFound)
                return NotFound();

            if (result.Outcome == PlacementOperationOutcome.BadRequest)
                return BadRequest(result.Message);

            SetResultMessage(result);
            return RedirectToAction("Review", new { examId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> ResendExamToStudent(int examId, int studentId)
        {
            var result = await _managementService.ResendToStudentAsync(AdminScope, examId, studentId);
            SetResultMessage(result);

            if (!result.Succeeded)
                return RedirectToAction("Index");

            return RedirectToAction("BatchStudents", new { examId, batchId = result.BatchId });
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> ReplaceQuestion(
    int examId,
    Guid oldQuestionId,
    Guid newQuestionId)
        {
            var result = await _managementService.ReplaceQuestionAsync(AdminScope, examId, oldQuestionId, newQuestionId);

            if (result.Outcome == PlacementOperationOutcome.NotFound)
                return NotFound();

            SetResultMessage(result);
            return RedirectToAction("Review", new { examId });
        }


        [HttpGet]
        [AdminPermission("PlacementExams", "Read")]
        public async Task<IActionResult> StudentExamDetails(int examId, int studentId)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            var exam = await _context.Exams
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (student == null || exam == null)
                return NotFound();

            // 🔹 جلب بيانات الأسئلة وإجابات الطالب
            var answers = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                where a.StudentId == studentId && a.ExamId == examId
                select new
                {
                    q.Title,
                    q.CorrectAnswer,
                    a.SelectedAnswer,
                    a.IsCorrect
                }
            ).ToListAsync();

            var model = new QdratNew.ViewModels.Exam.StudentExamDetailsVm
            {
                StudentId = student.StudentID,   // ✅ أضف هذا السطر
                ExamId = examId, // ✅ أضف هذا السطر

                StudentName = student.FullName,
                ExamTitle = exam.Title,
                TotalQuestions = exam.TotalQuestions,
                CorrectCount = answers.Count(x => x.IsCorrect),
                WrongCount = answers.Count(x => !x.IsCorrect),
                Answers = answers.Select(x => new StudentExamAnswerVm
                {
                    QuestionTitle = x.Title,
                    SelectedAnswer = x.SelectedAnswer,
                    CorrectAnswer = x.CorrectAnswer,
                    IsCorrect = x.IsCorrect
                }).ToList()
            };
            ViewBag.ExamId = examId;

            return View(model);
        }





        // ✅ تقرير الطالب - اختبار تحديد المستوى
        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> AdminPlacementReport(int studentId, int assignmentId, int examId = 0, int batchId = 0)
        {
            // ✅ جلب بيانات الطالب
            var student = await _context.Students
                .Include(s => s.Parent)
                .FirstOrDefaultAsync(s => s.StudentID == studentId);

            if (student == null)
                return NotFound("لم يتم العثور على بيانات الطالب.");

            // ✅ جلب بيانات التعيين والاختبار
            var assignment = assignmentId > 0
                ? await _context.ExamAssignments
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a => a.Id == assignmentId && a.StudentId == studentId)
                : null;

            if (assignment == null && examId > 0 && batchId <= 0)
            {
                batchId = await (
                    from eab in _context.ExamAssignmentsToBatches
                    join sbe in _context.StudentBatchEnrollments on eab.BatchId equals sbe.BatchId
                    where eab.ExamId == examId && sbe.StudentID == studentId
                    select eab.BatchId
                ).FirstOrDefaultAsync();
            }

            // ⚠️ إن لم يصل assignmentId صحيحًا (مثلاً 0) نبحث أولًا عن التعيين الذي عليه إجابات
            // داخل نفس الدفعة، لأن تقرير الطالب يعتمد على التعيين الحقيقي لا على ExamId المعروض فقط.
            if (assignment == null && batchId > 0)
            {
                var realAssignmentId = await (
                    from ea in _context.ExamAssignments
                    join eab in _context.ExamAssignmentsToBatches on ea.ExamId equals eab.ExamId
                    where ea.StudentId == studentId &&
                          eab.BatchId == batchId &&
                          _context.QuestionAttemptNew.Any(a =>
                              a.StudentId == studentId &&
                              (a.ExamId == ea.ExamId || a.ExamAssignmentId == ea.Id))
                    orderby ea.AssignedAt descending
                    select ea.Id
                ).FirstOrDefaultAsync();

                if (realAssignmentId > 0)
                {
                    assignment = await _context.ExamAssignments
                        .Include(a => a.Exam)
                        .FirstOrDefaultAsync(a => a.Id == realAssignmentId && a.StudentId == studentId);
                }
            }

            // ⚠️ fallback قديم: لو لا توجد دفعة في الرابط، حاول نفس الاختبار مباشرة.
            if (assignment == null && examId > 0)
            {
                var realAssignmentId = await _context.QuestionAttemptNew
                    .Where(a => a.StudentId == studentId &&
                                a.ExamId == examId &&
                                a.ExamAssignmentId != null)
                    .Select(a => a.ExamAssignmentId!.Value)
                    .FirstOrDefaultAsync();

                if (realAssignmentId > 0)
                {
                    assignment = await _context.ExamAssignments
                        .Include(a => a.Exam)
                        .FirstOrDefaultAsync(a => a.Id == realAssignmentId && a.StudentId == studentId);
                }
            }

            Exam exam;
            int reportExamId;
            int reportAssignmentId;
            DateTime reportAssignedAt;

            if (assignment != null && assignment.Exam != null)
            {
                exam = assignment.Exam;
                reportExamId = assignment.ExamId;
                reportAssignmentId = assignment.Id;
                reportAssignedAt = assignment.AssignedAt;
            }
            else if (examId > 0)
            {
                // ⚠️ لا يوجد سجل ExamAssignments شخصي لهذا الطالب (مثلاً انضم للدفعة بعد إرسال الاختبار)
                // نبني التقرير مباشرة من إجابات الطالب المحفوظة لنفس الاختبار، بنفس أسلوب AdminPlacementReportPrint
                exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);
                if (exam == null)
                    return NotFound("❌ اختبار المستوى غير موجود.");

                reportExamId = examId;
                reportAssignmentId = 0;
                reportAssignedAt = await _context.ExamStudentStatuses
                    .Where(s => s.StudentId == studentId && s.ExamId == examId)
                    .Select(s => (DateTime?)s.AssignedAt)
                    .FirstOrDefaultAsync() ?? exam.CreatedAt;
            }
            else
            {
                return NotFound("❌ اختبار المستوى غير موجود.");
            }

            // 🔹 جلب الأسئلة ومحاولات الطالب بنفس بنية تقرير الطالب
            var fullData = await (
                from eq in _context.ExamQuestions.AsNoTracking()
                join q in _context.Questions.AsNoTracking() on eq.QuestionId equals q.Id
                join l in _context.Lessons.AsNoTracking() on q.LessonId equals l.Id
                join s in _context.Sections.AsNoTracking() on l.SectionId equals s.Id
                join c in _context.Curriculums.AsNoTracking() on s.CurriculumId equals c.Id
                join att in _context.QuestionAttemptNew
                    .AsNoTracking()
                    .Where(a => a.StudentId == studentId &&
                                (a.ExamId == reportExamId || a.ExamAssignmentId == reportAssignmentId))
                    on q.Id equals att.QuestionId into gj
                from attempt in gj.DefaultIfEmpty()
                where eq.ExamId == reportExamId
                select new
                {
                    QuestionId = q.Id,
                    q.Title,
                    SectionId = s.Id,
                    SectionName = s.Title,
                    LessonId = l.Id,
                    LessonName = l.Title,
                    CurriculumTitle = c.Title,
                    IsCorrect = attempt != null && attempt.IsCorrect,
                    IsDontKnowAnswer = attempt != null && attempt.IsDontKnowAnswer,
                    HasAttempt = attempt != null
                }
            ).ToListAsync();

            if (!fullData.Any())
                return Content("<div class='alert alert-warning text-center mt-5'>⚠️ لا توجد بيانات للأسئلة.</div>", "text/html");

            // 🔸 حساب النتائج العامة
            int totalQuestions = fullData.Count;
            int correctAnswers = fullData.Count(x => x.IsCorrect);
            int answeredQuestions = fullData.Count(x => x.HasAttempt);
            int skippedQuestions = totalQuestions - answeredQuestions;
            int wrongAnswers = fullData.Count(x => x.HasAttempt && !x.IsCorrect);
            // 🆕 من ضمن wrongAnswers: عدد إجابات "لا أعرف" تحديدًا
            int dontKnowAnswers = fullData.Count(x => x.IsDontKnowAnswer);

            double overall = totalQuestions > 0
                ? Math.Round(correctAnswers * 100.0 / totalQuestions, 1)
                : 0.0;

            // 🕒 حساب وقت الحل
            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s => s.StudentId == studentId &&
                                          (s.ExamId == reportExamId || s.ExamAssignmentId == reportAssignmentId));

            int solveMinutes = 0;
            if (status != null && status.StartedAt.HasValue && status.SubmittedAt.HasValue)
                solveMinutes = (int)Math.Round((status.SubmittedAt.Value - status.StartedAt.Value).TotalMinutes);
            else
            {
                // 🟡 في حالة عدم وجود بيانات نستخدم الوقت الإجمالي الافتراضي
                var totalSeconds = await _context.QuestionAttemptNew
                    .Where(a => a.StudentId == studentId &&
                                (a.ExamId == reportExamId || a.ExamAssignmentId == reportAssignmentId))
                    .SumAsync(a => (int?)a.TimeTakenSeconds) ?? 0;

                solveMinutes = totalSeconds > 0 ? (int)Math.Round(totalSeconds / 60.0) : exam.DurationMinutes;
            }

            // 🧮 زمن الاختبار الكلي
            double totalMinutes = exam.DurationMinutes > 0
                ? exam.DurationMinutes
                : 60;

            // 🔹 نسبة الوقت المستهلك
            double percentTime = totalMinutes > 0
                ? Math.Round((solveMinutes / totalMinutes) * 100, 1)
                : 0.0;

            // 🔹 التوصية الذكية
            var recommendation = _recommendationService.GetRecommendation(overall, solveMinutes, totalMinutes);

            // 🔹 تحليل المحاور بنفس منطق تقرير الطالب: النسبة من الأسئلة المجابة داخل المحور
            var groupedSections = fullData
                .GroupBy(x => new { x.SectionName, x.CurriculumTitle })
                .Select(g => new
                {
                    g.Key.SectionName,
                    g.Key.CurriculumTitle,
                    TotalQuestions = g.Count(),
                    CorrectCount = g.Count(x => x.IsCorrect),
                    WrongCount = g.Count(x => x.HasAttempt && !x.IsCorrect),
                    Percent = g.Any(x => x.HasAttempt)
                        ? Math.Round(g.Count(x => x.IsCorrect) * 100.0 / g.Count(x => x.HasAttempt), 1)
                        : 0.0
                })
                .ToList();

            // 🔹 تحليل الكمي
            var quantGroups = groupedSections
                .Where(x => x.CurriculumTitle.IndexOf("كمي", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // 🔹 تحليل اللفظي
            var verbalGroups = groupedSections
                .Where(x => x.CurriculumTitle.IndexOf("لفظي", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            // ✅ بناء النموذج
            var model = new PlacementReportViewModel
            {
                Student = new StudentMiniVm
                {
                    FullName = student.FullName,
                    StudentID = student.StudentID,
                    Level = student.Level,
                    ParentName = student.Parent?.FullName ?? "—",
                    ParentPhone = student.Parent?.PhoneNumber ?? "—"
                },
                ExamDate = reportAssignedAt.ToString("yyyy-MM-dd"),
                SolveMinutes = solveMinutes,
                OverallPercent = overall,
                TotalMinutes = totalMinutes,
                PercentTime = percentTime,
                AssignmentId = reportAssignmentId,
                WrongAnswers = wrongAnswers,
                Recommendation = recommendation,

                QuantLabels = quantGroups.Select(x => x.SectionName).ToList(),
                QuantScores = quantGroups.Select(x => x.Percent).ToList(),
                QuantCorrectCounts = quantGroups.Select(x => x.CorrectCount).ToList(),
                QuantWrongCounts = quantGroups.Select(x => x.WrongCount).ToList(),

                VerbalLabels = verbalGroups.Select(x => x.SectionName).ToList(),
                VerbalScores = verbalGroups.Select(x => x.Percent).ToList(),
                VerbalCorrectCounts = verbalGroups.Select(x => x.CorrectCount).ToList(),
                VerbalWrongCounts = verbalGroups.Select(x => x.WrongCount).ToList(),

                HasQuantChart = quantGroups.Any(),
                HasVerbalChart = verbalGroups.Any(),

                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                AnsweredQuestions = answeredQuestions,
                SkippedQuestions = skippedQuestions,
                DontKnowAnswers = dontKnowAnswers
            };

            // ✅ تحليل المؤشرات داخل كل محور
            var sectionLessonGroups = fullData
                .GroupBy(x => new { x.SectionId, x.SectionName })
                .Select(g => new QdratNew.ViewModels.Exam.SectionLessonReportVm
                {
                    SectionId = g.Key.SectionId,
                    SectionName = g.Key.SectionName,
                    Lessons = g.GroupBy(l => new { l.LessonId, l.LessonName })
                        .Select(lessonGroup => new QdratNew.ViewModels.Exam.LessonPerformanceVm
                        {
                            LessonId = lessonGroup.Key.LessonId,
                            LessonName = lessonGroup.Key.LessonName,
                            TotalQuestions = lessonGroup.Count(),
                            CorrectCount = lessonGroup.Count(x => x.IsCorrect),
                            WrongCount = lessonGroup.Count(x => x.HasAttempt && !x.IsCorrect),
                            SkippedCount = lessonGroup.Count(x => !x.HasAttempt),
                            Percent = lessonGroup.Any(x => x.HasAttempt)
                                ? Math.Round(lessonGroup.Count(x => x.IsCorrect) * 100.0 / lessonGroup.Count(x => x.HasAttempt), 1)
                                : 0.0
                        }).OrderBy(x => x.LessonName).ToList()
                })
                .OrderBy(x => x.SectionName)
                .ToList();

            model.SectionLessonReports = sectionLessonGroups;

            return View(model);
        }



        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> AdminPlacementReportPrint(int examId, int studentId)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentID == studentId);
            var exam = await _context.Exams
                .FirstOrDefaultAsync(e => e.Id == examId);

            if (student == null || exam == null)
                return NotFound();

            // ✅ جلب BatchId من جدول ExamAssignmentsToBatches
            var batchId = await _context.ExamAssignmentsToBatches
                .Where(e => e.ExamId == examId)
                .Select(e => e.BatchId)
                .FirstOrDefaultAsync();

            // ✅ جلب محاولات الطالب لهذا الاختبار
            var attempts = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                join s in _context.Sections on l.SectionId equals s.Id
                where a.StudentId == studentId && a.ExamId == examId
                select new
                {
                    a.IsCorrect,
                    a.IsDontKnowAnswer,
                    a.TimeTakenSeconds,
                    SectionTitle = s.Title,
                    Type = q.IsQuantitative ? "Quantitative" : "Verbal"
                }
            ).ToListAsync();

            if (!attempts.Any())
            {
                TempData["Error"] = "لا توجد بيانات لهذا الطالب في هذا الاختبار.";
                return RedirectToAction("BatchExams", new { batchId });
            }

            // ✅ الحسابات العامة
            int total = attempts.Count;
            int correct = attempts.Count(x => x.IsCorrect);
            // 🆕 عدد إجابات "لا أعرف الإجابة"
            int dontKnow = attempts.Count(x => x.IsDontKnowAnswer);
            double percent = Math.Round((double)correct * 100 / total, 2);

            // ✅ حساب الزمن الفعلي من ExamStudentStatuses
            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s => s.StudentId == studentId && s.ExamId == examId);

            double avgTime = 0;
            if (status != null && status.StartedAt.HasValue && status.SubmittedAt.HasValue)
            {
                avgTime = Math.Round((status.SubmittedAt.Value - status.StartedAt.Value).TotalMinutes, 1);
            }

            // ✅ تحليل المحاور الكمية
            var quantData = attempts
                .Where(x => x.Type == "Quantitative")
                .GroupBy(x => x.SectionTitle)
                .Select(g => new
                {
                    Label = g.Key,
                    Score = Math.Round(g.Count(x => x.IsCorrect) * 100.0 / g.Count(), 2)
                }).ToList();

            // ✅ تحليل المحاور اللفظية
            var verbalData = attempts
                .Where(x => x.Type == "Verbal")
                .GroupBy(x => x.SectionTitle)
                .Select(g => new
                {
                    Label = g.Key,
                    Score = Math.Round(g.Count(x => x.IsCorrect) * 100.0 / g.Count(), 2)
                }).ToList();

            // ✅ تحديد التوصيات
            string recommendation;
            string adminDecision;

            if (percent < 50)
            {
                recommendation = "الطالب يعاني من ضعف واضح في أغلب المحاور، ويحتاج إلى خطة علاجية مركزة.";
                adminDecision = "يوصى بتفعيل جلسات دعم فردية ومتابعة أسبوعية من قبل المدرب.";
            }
            else if (percent < 70)
            {
                recommendation = "الطالب بمستوى متوسط ويحتاج إلى مراجعة المحاور الضعيفة وتعزيز الجانب الكمي أو اللفظي.";
                adminDecision = "يُفضل إعادة اختبار تقييم بعد أسبوعين لتحديد التحسن.";
            }
            else
            {
                recommendation = "الطالب يُظهر أداءً قويًا وثابتًا، ومستعد للترقية إلى المستوى التالي.";
                adminDecision = "اعتماد الترقية الأكاديمية وتحديث الخطة الدراسية بناءً على نتائجه.";
            }

            // ✅ إنشاء ViewModel
            var model = new AdminPlacementReportViewModel
            {
                ExamTitle = exam.Title,
                ExamDate = exam.CreatedAt,
                OverallPercent = percent,
                AverageSolveMinutes = avgTime,
                QuantLabels = quantData.Select(x => x.Label).ToList(),
                QuantScores = quantData.Select(x => x.Score).ToList(),
                VerbalLabels = verbalData.Select(x => x.Label).ToList(),
                VerbalScores = verbalData.Select(x => x.Score).ToList(),
                RecommendationText = recommendation,
                AdminDecisionTip = adminDecision,
                DontKnowAnswers = dontKnow,
            };

            return View("AdminPlacementReportPrint", model);
        }





        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]

        public async Task<IActionResult> DownloadReportChrome(int examId, int studentId)
    {
        // 🔹 عنوان التقرير الفعلي (صفحة HTML)
        var url = Url.Action("AdminPlacementReport", "PlacementExams",
            new { examId, studentId }, Request.Scheme);

        // 🔹 تحميل Chrome في أول مرة فقط
        var executablePath = await _reportBrowserProvider.GetExecutablePathAsync(HttpContext.RequestAborted);

        using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            ExecutablePath = executablePath,
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });

        var page = await browser.NewPageAsync();
        await page.GoToAsync(url, WaitUntilNavigation.Networkidle2);

        // 🔹 إنشاء PDF بجودة عالية واتجاه RTL سليم
        var pdfBytes = await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "10mm",
                Bottom = "10mm",
                Left = "10mm",
                Right = "10mm"
            }
        });

        return File(pdfBytes, "application/pdf", $"تقرير_{studentId}.pdf");
    }


        [HttpGet]
        //public async Task<IActionResult> AdminPlacementBatchReport(int examId, int batchId)
        //{
        //    var batch = await _context.Batches.FindAsync(batchId);
        //    var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId);

        //    if (batch == null || exam == null)
        //        return NotFound("الدفعة أو الاختبار غير موجودين.");

        //    // 🧠 هات نتائج كل طالب شارك فعلاً في الاختبار
        //    var results = await _context.StudentExamResults
        //        .Include(r => r.Student)
        //        .Where(r => r.ExamId == examId && r.Student.BatchId == batchId)
        //        .ToListAsync();

        //    if (!results.Any())
        //    {
        //        TempData["Error"] = "لا توجد نتائج طلاب لهذه الدفعة في هذا الاختبار.";
        //        return View(new AdminPlacementBatchReportViewModel
        //        {
        //            BatchName = batch.Name,
        //            ExamTitle = exam.Title,
        //            ExamDate = exam.CreatedAt,
        //            BatchAverage = 0,
        //            HighestScore = 0,
        //            LowestScore = 0,
        //            StudentResults = new List<StudentPerformanceVm>(),
        //            RecommendationText = "لم يخضع أي طالب للاختبار بعد.",
        //            AdminDecisionTip = "يُنصح بمتابعة حضور اختبار مقياس المستوى."
        //        });
        //    }

        //    // 🔹 تحليل درجات الدفعة
        //    double batchAverage = Math.Round(results.Average(r => r.Percent), 1);
        //    double highest = results.Max(r => r.Percent);
        //    double lowest = results.Min(r => r.Percent);

        //    // 🔹 تحليل الوقت (من ActivityLog)
        //    var times = await _context.StudentActivityLogs
        //        .Where(a => a.ExamAssignmentId == examId)
        //        .GroupBy(a => a.StudentId)
        //        .Select(g => new
        //        {
        //            StudentId = g.Key,
        //            AvgSeconds = g.Average(a => a.TimeSpent)
        //        })
        //        .ToListAsync();

        //    // 🔹 بناء قائمة الطلاب
        //    var students = results.Select(r =>
        //    {
        //        var time = times.FirstOrDefault(t => t.StudentId == r.StudentId)?.AvgSeconds ?? 0;
        //        return new StudentPerformanceVm
        //        {
        //            StudentId = r.StudentId,
        //            StudentName = r.Student.FullName,
        //            BatchName = batch.Name,
        //            CourseTitle = exam.Title,
        //            TotalScore = r.Percent,
        //            TotalQuestions = r.TotalQuestions,
        //            CorrectAnswers = r.CorrectAnswers,
        //            WrongAnswers = r.WrongAnswers,
        //            Unanswered = r.Unanswered,
        //            TimeTakenMinutes = Math.Round(time / 60.0, 2)
        //        };
        //    }).OrderByDescending(x => x.TotalScore).ToList();

        //    // 🔹 تحليل المنهج (كمي / لفظي)
        //    var quantAvg = results.Where(r => r.IsQuantitative).DefaultIfEmpty().Average(r => r.Percent);
        //    var verbalAvg = results.Where(r => !r.IsQuantitative).DefaultIfEmpty().Average(r => r.Percent);

        //    // 🔹 توصية ذكية
        //    string recommendation = batchAverage switch
        //    {
        //        >= 80 => "📈 أداء ممتاز! يمكن الانتقال مباشرة إلى المستوى المتقدم.",
        //        >= 60 => "⚖️ أداء متوسط. يُنصح بمراجعة المحاور الأضعف قبل الترقية.",
        //        _ => "🧩 أداء ضعيف. يجب تنفيذ خطة علاجية مكثفة قبل بدء الدورة."
        //    };

        //    // 🔹 نص الإداري
        //    string adminTip = "راجع أداء الطلاب لتحديد نقاط الضعف وتخصيص الخطة العلاجية المناسبة.";

        //    var model = new AdminPlacementBatchReportViewModel
        //    {
        //        BatchName = batch.Name,
        //        ExamTitle = exam.Title,
        //        ExamDate = exam.CreatedAt,
        //        BatchAverage = batchAverage,
        //        HighestScore = highest,
        //        LowestScore = lowest,
        //        QuantLabels = new List<string> { "كمي" },
        //        QuantScores = new List<double> { Math.Round(quantAvg, 1) },
        //        VerbalLabels = new List<string> { "لفظي" },
        //        VerbalScores = new List<double> { Math.Round(verbalAvg, 1) },
        //        StudentResults = students,
        //        RecommendationText = recommendation,
        //        AdminDecisionTip = adminTip
        //    };

        //    return View(model);
        //}



        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> ConfirmSendPost(int examId)
        {
            var result = await _managementService.ConfirmSendAsync(AdminScope, examId);
            SetResultMessage(result);

            if (!result.Succeeded)
                return RedirectToAction("Review", new { examId });

            return RedirectToAction("Index");
        }




        // ✅ تفاصيل اختبار محدد
        [HttpGet]
        [AdminPermission("PlacementExams", "Read")]
        public async Task<IActionResult> PlacementDetails(int id) // 👈 id = ExamId
        {
            var vm = await _managementService.GetPlacementDetailsAsync(AdminScope, id);
            if (vm == null)
                return NotFound();

            return View(vm);
        }


    }
}
