using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Extensions;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Homework;
using QdratNew.ViewModels.Reports;
using QdratNew.ViewModels.Students;
using System.Security.Claims;



namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class PlacementExamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPlacementExamManagementService _managementService; // PPL-S2
        private readonly IPlacementExamReportService _reportService; // PPL-S3
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAdminActivityLogger _activityLogger;

        public PlacementExamsController(
            ApplicationDbContext context,
            IPlacementExamManagementService managementService,
            IPlacementExamReportService reportService,
            UserManager<ApplicationUser> userManager,
            IAdminActivityLogger activityLogger)
        {
            _context = context;
            _managementService = managementService;
            _reportService = reportService;
            _userManager = userManager;
            _activityLogger = activityLogger;
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
            var result = await _reportService.GetBatchPerformanceReportAsync(AdminScope, examId, batchId);

            if (result.Outcome == PlacementReportOutcome.NotFound)
                return Content($"<div class='alert alert-danger text-center mt-5'>{result.Message}</div>", "text/html; charset=utf-8");

            if (result.Outcome == PlacementReportOutcome.NoData)
                return Content($"<div class='alert alert-warning text-center mt-5'>{result.Message}</div>", "text/html; charset=utf-8");

            return View("~/Areas/Admin/Views/PlacementExams/BatchPerformanceReport.cshtml", result.Model);
        }





        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> ReviewLessonQuestionsBatch(int examId, int lessonId)
        {
            var model = await _reportService.GetReviewLessonQuestionsBatchAsync(AdminScope, examId, lessonId);
            if (model == null) return NotFound();

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
            var result = await _reportService.GetSectionAnalysisAsync(AdminScope, assignmentId, studentId);

            if (result.Outcome == PlacementReportOutcome.NotFound)
                return NotFound(result.Message);

            return View(result.Model);
        }





        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> ReviewLessonQuestions(int assignmentId, int studentId, int lessonId)
        {
            var result = await _reportService.GetReviewLessonQuestionsAsync(AdminScope, assignmentId, studentId, lessonId);

            if (result.Outcome == PlacementReportOutcome.NotFound)
                return NotFound(result.Message);

            if (result.Outcome == PlacementReportOutcome.NoData)
                return Content($"<div class='alert alert-warning text-center mt-4'>{result.Message}</div>", "text/html");

            // ✅ عرض الصفحة بالمسار الكامل
            return View("~/Areas/Admin/Views/PlacementExams/ReviewLessonQuestions.cshtml", result.Model);
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
            var data = await _reportService.GetBatchStudentsAsync(AdminScope, examId, batchId);
            if (data == null)
                return NotFound();

            // ✅ تمرير القيم الضرورية للـ View بدون تغيير الموديل
            ViewBag.BatchName = data.BatchName;
            ViewBag.ExamTitle = data.ExamTitle;
            ViewBag.ExamId = examId;
            ViewBag.BatchId = batchId;

            return View(data.Students);
        }


        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> DownloadStudentReport(int examId, int studentId)
        {
            var file = await _reportService.BuildStudentReportPdfAsync(AdminScope, examId, studentId);
            if (file == null)
                return NotFound();

            return File(file.Value.Pdf, "application/pdf", file.Value.FileName);
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
            var model = await _reportService.GetStudentExamDetailsAsync(AdminScope, examId, studentId);
            if (model == null)
                return NotFound();

            ViewBag.ExamId = examId;

            return View(model);
        }





        // ✅ تقرير الطالب - اختبار تحديد المستوى
        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> AdminPlacementReport(int studentId, int assignmentId, int examId = 0, int batchId = 0)
        {
            var result = await _reportService.GetStudentPlacementReportAsync(AdminScope, studentId, assignmentId, examId, batchId);

            if (result.Outcome == PlacementReportOutcome.NotFound)
                return NotFound(result.Message);

            if (result.Outcome == PlacementReportOutcome.NoData)
                return Content($"<div class='alert alert-warning text-center mt-5'>{result.Message}</div>", "text/html");

            return View(result.Model);
        }



        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]
        public async Task<IActionResult> AdminPlacementReportPrint(int examId, int studentId)
        {
            var result = await _reportService.GetStudentPlacementReportPrintAsync(AdminScope, examId, studentId);

            if (result.Outcome == PlacementReportOutcome.NotFound)
                return NotFound();

            if (result.Outcome == PlacementReportOutcome.NoData)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction("BatchExams", new { batchId = result.BatchId });
            }

            return View("AdminPlacementReportPrint", result.Model);
        }





        [HttpGet]
        [AdminPermission("PlacementExams", "Results")]

        public async Task<IActionResult> DownloadReportChrome(int examId, int studentId)
    {
            // 🔹 عنوان التقرير الفعلي (صفحة HTML)
            var url = Url.Action("AdminPlacementReport", "PlacementExams",
                new { examId, studentId }, Request.Scheme);

            var file = await _reportService.BuildStudentReportChromePdfAsync(AdminScope, examId, studentId, url!, HttpContext.RequestAborted);
            if (file == null)
                return NotFound();

            return File(file.Value.Pdf, "application/pdf", file.Value.FileName);
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




        // PPL-S4: معاينة سؤال من صفحة المراجعة بصلاحية هذا الموديول
        [HttpGet]
        [AdminPermission("PlacementExams", "Read")]
        public async Task<IActionResult> PreviewQuestion(int examId, Guid questionId)
        {
            var model = await _managementService.GetQuestionPreviewAsync(AdminScope, examId, questionId, HttpContext.RequestAborted);
            if (model == null)
                return NotFound();

            return PartialView("~/Views/Shared/_QuestionPreviewPartial.cshtml", model);
        }

        // PPL-S4: ملخص النموذج في صفحة الإنشاء من نموذج
        [HttpGet]
        [AdminPermission("PlacementExams", "Create")]
        public async Task<IActionResult> GetModelSummary(int modelId)
        {
            var summary = await _managementService.GetModelSummaryAsync(AdminScope, modelId, HttpContext.RequestAborted);
            if (summary == null)
                return NotFound();

            return Json(summary);
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
