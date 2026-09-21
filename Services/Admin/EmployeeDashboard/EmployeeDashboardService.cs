using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Security.AdminPermissions;
using QdratNew.ViewModels.Admin.EmployeeDashboard;
using System.Security.Claims;

namespace QdratNew.Services.Admin.EmployeeDashboard
{
    public class EmployeeDashboardService : IEmployeeDashboardService
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IEmployeeBatchAccessService _batchAccess;
        private readonly ApplicationDbContext _context;

        public EmployeeDashboardService(
            IAuthorizationService authorizationService,
            IEmployeeBatchAccessService batchAccess,
            ApplicationDbContext context)
        {
            _authorizationService = authorizationService;
            _batchAccess = batchAccess;
            _context = context;
        }

        public async Task<EmployeeDashboardViewModel> GetDashboardAsync(ClaimsPrincipal user)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var now = DateTime.Now;
            var today = now.Date;
            var sevenDaysLater = today.AddDays(7);
            var in24h = now.AddHours(24);
            var in48h = now.AddHours(48);

            // ── Header ──────────────────────────────────────────────────────────
            var employeeName = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.FullName ?? u.UserName)
                .FirstOrDefaultAsync() ?? string.Empty;

            var profileName = await _context.AdminUserProfiles
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.AdminProfile != null)
                .Select(x => x.AdminProfile!.Name)
                .FirstOrDefaultAsync();

            // ── Bulk permission check (single pass, reused for modules + KPIs + queue) ──
            var grantedPolicies = new HashSet<string>();
            foreach (var policy in AllCandidatePolicies)
            {
                var result = await _authorizationService.AuthorizeAsync(user, policy);
                if (result.Succeeded) grantedPolicies.Add(policy);
            }

            bool Can(string p) => grantedPolicies.Contains(p);

            // ── Modules ──────────────────────────────────────────────────────────
            var modules = new List<EmployeeDashboardModuleVm>();
            foreach (var candidate in BuildModuleCandidates())
            {
                var allowedActions = candidate.PolicyActionPairs
                    .Where(pa => Can(pa.PolicyName))
                    .Select(pa => pa.ActionVm)
                    .ToList();

                if (allowedActions.Count == 0) continue;

                if (candidate.BatchFeature.HasValue)
                {
                    var batchIds = await _batchAccess.GetPermittedBatchIdsAsync(userId, candidate.BatchFeature.Value);
                    if (batchIds.Count == 0) continue;
                }

                modules.Add(new EmployeeDashboardModuleVm
                {
                    Key = candidate.Key,
                    Title = candidate.Title,
                    Description = candidate.Description,
                    IconCssClass = candidate.IconCssClass,
                    AccentCssClass = candidate.AccentCssClass,
                    Actions = allowedActions
                });
            }

            var reportActionsCount = modules
                .SelectMany(m => m.Actions)
                .Count(a => a.Key is "Reports" or "Results" or "Details" or "BatchDetails" or "ExamReport");

            var notificationActionsCount = modules
                .SelectMany(m => m.Actions)
                .Count(a => a.Key is "Send" or "Resend");

            // ── KPI Cards ─────────────────────────────────────────────────────────
            var kpiCards = new List<EmployeeKpiCardVm>();

            if (Can(AdminPermissionPolicies.Homework_Read))
            {
                var openHw = await (
                    from h in _context.HomeworkSets
                    join eba in _context.EmployeeBatchAccesses on h.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Homework
                          && eba.IsGranted
                          && h.IsSent && !h.IsClosed && !h.IsArchived
                          && (h.EndAt == null || h.EndAt >= now)
                    select h.Id
                ).Distinct().CountAsync();

                kpiCards.Add(new EmployeeKpiCardVm
                {
                    Title = "واجبات مفتوحة",
                    Value = openHw.ToString(),
                    IconCssClass = "fas fa-tasks",
                    AccentCssClass = "accent-blue",
                    TargetUrl = "/Admin/HomeworkManagement"
                });

                var closingHw = await (
                    from h in _context.HomeworkSets
                    join eba in _context.EmployeeBatchAccesses on h.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Homework
                          && eba.IsGranted
                          && h.IsSent && !h.IsClosed && !h.IsArchived
                          && h.EndAt != null && h.EndAt >= now && h.EndAt <= in24h
                    select h.Id
                ).Distinct().CountAsync();

                kpiCards.Add(new EmployeeKpiCardVm
                {
                    Title = "تُغلق خلال 24 ساعة",
                    Value = closingHw.ToString(),
                    IconCssClass = "fas fa-clock",
                    AccentCssClass = closingHw > 0 ? "accent-red" : "accent-green",
                    TargetUrl = "/Admin/HomeworkManagement"
                });
            }

            if (Can(AdminPermissionPolicies.Exams_Read))
            {
                var openExams = await (
                    from eab in _context.ExamAssignmentsToBatches
                    join eba in _context.EmployeeBatchAccesses on eab.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Exams
                          && eba.IsGranted
                          && eab.IsSentToStudents && !eab.IsArchived
                          && (eab.EndAt == null || eab.EndAt >= now)
                    select eab.Id
                ).Distinct().CountAsync();

                kpiCards.Add(new EmployeeKpiCardVm
                {
                    Title = "اختبارات مفتوحة",
                    Value = openExams.ToString(),
                    IconCssClass = "fas fa-pencil-alt",
                    AccentCssClass = "accent-purple",
                    TargetUrl = "/Admin/ExamAssignments"
                });

                var closingExams = await (
                    from eab in _context.ExamAssignmentsToBatches
                    join eba in _context.EmployeeBatchAccesses on eab.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Exams
                          && eba.IsGranted
                          && eab.IsSentToStudents && !eab.IsArchived
                          && eab.EndAt != null && eab.EndAt >= now && eab.EndAt <= in24h.AddDays(6)
                    select eab.Id
                ).Distinct().CountAsync();

                kpiCards.Add(new EmployeeKpiCardVm
                {
                    Title = "اختبارات تُغلق خلال 7 أيام",
                    Value = closingExams.ToString(),
                    IconCssClass = "fas fa-hourglass-half",
                    AccentCssClass = closingExams > 0 ? "accent-orange" : "accent-teal",
                    TargetUrl = "/Admin/ExamAssignments"
                });
            }

            if (Can(AdminPermissionPolicies.Attendance_Read))
            {
                var todayData = await (
                    from ar in _context.AttendanceRecords
                    join lec in _context.Lecture on ar.LectureId equals lec.Id
                    join eba in _context.EmployeeBatchAccesses on lec.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Attendance
                          && eba.IsGranted
                          && lec.Date >= today && lec.Date < today.AddDays(1)
                    select new { ar.IsPresent }
                ).AsNoTracking().ToListAsync();

                int total = todayData.Count;
                int present = todayData.Count(x => x.IsPresent);
                string rate = total > 0 ? $"{present * 100 / total}٪" : "—";

                kpiCards.Add(new EmployeeKpiCardVm
                {
                    Title = "نسبة حضور اليوم",
                    Value = rate,
                    IconCssClass = "fas fa-user-check",
                    AccentCssClass = "accent-green",
                    TargetUrl = "/Admin/Attendance"
                });
            }

            if (Can(AdminPermissionPolicies.PerformanceIndicatorExams_Read)
                && Can(AdminPermissionPolicies.PerformanceIndicatorExams_ConfirmSend))
            {
                var pendingPies = await (
                    from pie in _context.PerformanceIndicatorExams
                    join pieToBatch in _context.PerformanceIndicatorExamToBatch
                        on pie.Id equals pieToBatch.PerformanceIndicatorExamId
                    join eba in _context.EmployeeBatchAccesses on pieToBatch.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Exams
                          && eba.IsGranted
                          && !pie.IsSent && !pie.IsArchived
                    select pie.Id
                ).Distinct().CountAsync();

                kpiCards.Add(new EmployeeKpiCardVm
                {
                    Title = "اختبارات أداء بانتظار الإرسال",
                    Value = pendingPies.ToString(),
                    IconCssClass = "fas fa-chart-line",
                    AccentCssClass = pendingPies > 0 ? "accent-orange" : "accent-indigo",
                    TargetUrl = "/Admin/PerformanceIndicatorExams"
                });
            }

            // ── Action Queue ──────────────────────────────────────────────────────
            var actionQueue = new List<EmployeeActionQueueItemVm>();

            if (Can(AdminPermissionPolicies.Homework_Resend))
            {
                var urgentHw = await (
                    from h in _context.HomeworkSets
                    join b in _context.Batches on h.BatchId equals b.Id
                    join eba in _context.EmployeeBatchAccesses on h.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Homework
                          && eba.IsGranted
                          && h.IsSent && !h.IsClosed && !h.IsArchived
                          && h.EndAt != null && h.EndAt >= now && h.EndAt <= in48h
                    select new { h.Id, h.Title, BatchName = b.Name, h.EndAt }
                ).AsNoTracking().Distinct().OrderBy(x => x.EndAt).Take(5).ToListAsync();

                foreach (var hw in urgentHw)
                {
                    actionQueue.Add(new EmployeeActionQueueItemVm
                    {
                        ModuleKey = "Homework",
                        Title = $"واجب يُغلق قريباً: {hw.Title}",
                        Context = hw.BatchName,
                        DueAt = hw.EndAt,
                        PrimaryActionTitle = "عرض الواجبات",
                        PrimaryActionUrl = "/Admin/HomeworkManagement",
                        IsInlineAction = false
                    });
                }
            }

            if (Can(AdminPermissionPolicies.Exams_Read))
            {
                var urgentExams = await (
                    from eab in _context.ExamAssignmentsToBatches
                    join b in _context.Batches on eab.BatchId equals b.Id
                    join eba in _context.EmployeeBatchAccesses on eab.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Exams
                          && eba.IsGranted
                          && eab.IsSentToStudents && !eab.IsArchived
                          && eab.EndAt != null && eab.EndAt >= now && eab.EndAt <= in48h
                    select new { eab.Id, eab.Title, BatchName = b.Name, eab.EndAt }
                ).AsNoTracking().Distinct().OrderBy(x => x.EndAt).Take(5).ToListAsync();

                foreach (var ex in urgentExams)
                {
                    actionQueue.Add(new EmployeeActionQueueItemVm
                    {
                        ModuleKey = "Exams",
                        Title = $"اختبار يُغلق قريباً: {ex.Title}",
                        Context = ex.BatchName,
                        DueAt = ex.EndAt,
                        PrimaryActionTitle = "عرض الاختبارات",
                        PrimaryActionUrl = "/Admin/ExamAssignments",
                        IsInlineAction = false
                    });
                }
            }

            if (Can(AdminPermissionPolicies.PerformanceIndicatorExams_ConfirmSend))
            {
                var pendingPieItems = await (
                    from pie in _context.PerformanceIndicatorExams
                    join pieToBatch in _context.PerformanceIndicatorExamToBatch
                        on pie.Id equals pieToBatch.PerformanceIndicatorExamId
                    join b in _context.Batches on pieToBatch.BatchId equals b.Id
                    join eba in _context.EmployeeBatchAccesses on pieToBatch.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Exams
                          && eba.IsGranted
                          && !pie.IsSent && !pie.IsArchived
                    select new { pie.Id, pie.Title, BatchName = b.Name, pie.CreatedAt }
                ).AsNoTracking().Distinct().OrderBy(x => x.CreatedAt).Take(5).ToListAsync();

                foreach (var pie in pendingPieItems)
                {
                    actionQueue.Add(new EmployeeActionQueueItemVm
                    {
                        ModuleKey = "PerformanceIndicatorExams",
                        Title = $"اختبار مؤشر أداء بانتظار الإرسال: {pie.Title}",
                        Context = pie.BatchName,
                        DueAt = null,
                        PrimaryActionTitle = "إرسال الاختبار",
                        PrimaryActionUrl = $"/Admin/PerformanceIndicatorExams/ConfirmSend/{pie.Id}",
                        IsInlineAction = false
                    });
                }
            }

            // Sort by DueAt (nulls last), take top 10
            actionQueue = actionQueue
                .OrderBy(x => x.DueAt.HasValue ? 0 : 1)
                .ThenBy(x => x.DueAt)
                .Take(10)
                .ToList();

            // ── Upcoming Items (7 days) ───────────────────────────────────────────
            var upcomingItems = new List<EmployeeUpcomingItemVm>();

            if (Can(AdminPermissionPolicies.Attendance_Read) || Can(AdminPermissionPolicies.Lectures_Read))
            {
                var upcomingLectures = await (
                    from lec in _context.Lecture
                    join b in _context.Batches on lec.BatchId equals b.Id
                    join eba in _context.EmployeeBatchAccesses on lec.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Attendance
                          && eba.IsGranted
                          && lec.Date >= today && lec.Date < sevenDaysLater
                    select new { lec.Title, BatchName = b.Name, lec.Date }
                ).AsNoTracking().Distinct().OrderBy(x => x.Date).Take(20).ToListAsync();

                upcomingItems.AddRange(upcomingLectures.Select(l => new EmployeeUpcomingItemVm
                {
                    TypeLabel = "محاضرة",
                    Title = l.Title,
                    BatchName = l.BatchName,
                    ScheduledAt = l.Date,
                    DetailsUrl = null
                }));
            }

            if (Can(AdminPermissionPolicies.Homework_Read))
            {
                var closingHomeworks = await (
                    from h in _context.HomeworkSets
                    join b in _context.Batches on h.BatchId equals b.Id
                    join eba in _context.EmployeeBatchAccesses on h.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Homework
                          && eba.IsGranted
                          && h.IsSent && !h.IsClosed && !h.IsArchived
                          && h.EndAt != null && h.EndAt >= now && h.EndAt < sevenDaysLater
                    select new { h.Title, BatchName = b.Name, h.EndAt }
                ).AsNoTracking().Distinct().OrderBy(x => x.EndAt).Take(10).ToListAsync();

                upcomingItems.AddRange(closingHomeworks.Select(h => new EmployeeUpcomingItemVm
                {
                    TypeLabel = "إغلاق واجب",
                    Title = h.Title,
                    BatchName = h.BatchName,
                    ScheduledAt = h.EndAt!.Value,
                    DetailsUrl = null
                }));
            }

            if (Can(AdminPermissionPolicies.Exams_Read))
            {
                var closingExamItems = await (
                    from eab in _context.ExamAssignmentsToBatches
                    join b in _context.Batches on eab.BatchId equals b.Id
                    join eba in _context.EmployeeBatchAccesses on eab.BatchId equals eba.BatchId
                    where eba.UserId == userId
                          && eba.Feature == InstructorBatchFeature.Exams
                          && eba.IsGranted
                          && eab.IsSentToStudents && !eab.IsArchived
                          && eab.EndAt != null && eab.EndAt >= now && eab.EndAt < sevenDaysLater
                    select new { eab.Title, BatchName = b.Name, eab.EndAt }
                ).AsNoTracking().Distinct().OrderBy(x => x.EndAt).Take(10).ToListAsync();

                upcomingItems.AddRange(closingExamItems.Select(e => new EmployeeUpcomingItemVm
                {
                    TypeLabel = "إغلاق اختبار",
                    Title = e.Title,
                    BatchName = e.BatchName,
                    ScheduledAt = e.EndAt!.Value,
                    DetailsUrl = null
                }));
            }

            upcomingItems = upcomingItems.OrderBy(x => x.ScheduledAt).ToList();

            // ── Assemble ViewModel ────────────────────────────────────────────────
            return new EmployeeDashboardViewModel
            {
                EmployeeName = employeeName,
                ProfileName = profileName,
                Today = now,
                AllowedModulesCount = modules.Count,
                ReportActionsCount = reportActionsCount,
                NotificationActionsCount = notificationActionsCount,
                Modules = modules,
                KpiCards = kpiCards,
                ActionQueue = actionQueue,
                UpcomingItems = upcomingItems
            };
        }

        // ── Module definitions ──────────────────────────────────────────────────

        private sealed class ModuleCandidate
        {
            public string Key { get; init; } = string.Empty;
            public string Title { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public string IconCssClass { get; init; } = string.Empty;
            public string AccentCssClass { get; init; } = string.Empty;
            public InstructorBatchFeature? BatchFeature { get; init; }
            public List<(string PolicyName, EmployeeDashboardActionVm ActionVm)> PolicyActionPairs { get; init; } = new();
        }

        // All policies that need to be checked — used for bulk authorization at startup
        private static readonly string[] AllCandidatePolicies =
        [
            AdminPermissionPolicies.Homework_Read,
            AdminPermissionPolicies.Homework_Reports,
            AdminPermissionPolicies.Homework_Details,
            AdminPermissionPolicies.Homework_Resend,
            AdminPermissionPolicies.Homework_Archive,
            AdminPermissionPolicies.Exams_Read,
            AdminPermissionPolicies.Exams_Details,
            AdminPermissionPolicies.Exams_Results,
            AdminPermissionPolicies.Exams_Archive,
            AdminPermissionPolicies.PlacementExams_Read,
            AdminPermissionPolicies.PlacementExams_Results,
            AdminPermissionPolicies.PlacementExams_Archive,
            AdminPermissionPolicies.PerformanceDashboard_Read,
            AdminPermissionPolicies.PerformanceDashboard_BatchDetails,
            AdminPermissionPolicies.PerformanceDashboard_ExamReport,
            AdminPermissionPolicies.PerformanceIndicatorExams_Read,
            AdminPermissionPolicies.PerformanceIndicatorExams_Results,
            AdminPermissionPolicies.PerformanceIndicatorExams_Archive,
            AdminPermissionPolicies.PerformanceIndicatorExams_ConfirmSend,
            AdminPermissionPolicies.EnhancementSkills_Read,
            AdminPermissionPolicies.EnhancementSkills_Send,
            AdminPermissionPolicies.EnhancementSkills_Resend,
            AdminPermissionPolicies.EnhancementSkills_Archive,
            AdminPermissionPolicies.Attendance_Read,
            AdminPermissionPolicies.Attendance_Mark,
            AdminPermissionPolicies.Attendance_Students,
            AdminPermissionPolicies.Attendance_Archive,
            AdminPermissionPolicies.Lectures_Read,
        ];

        // ⚠️ Action route notes (per Section 19.5 of the implementation brief):
        // - HomeworkManagement/Reports, Details, Resend → no standalone GET pages, link to Index.
        // - HomeworkManagement/Archive → actual action is "Archived" (GET, no params).
        // - ExamAssignments/Results, Details → no standalone pages; link to Index.
        // - ExamAssignments/Archive → uses Index?showArchived=true (archive actions are POST).
        // - PlacementExams/Results → BatchPerformanceReport needs (examId, batchId); link to Index.
        // - PlacementExams/Archive → actual action is "ArchivedPlacementExams" (GET, no params).
        // - PerformanceDashboard/BatchDetails, ExamReport → need route params; link to Index.
        // - PerformanceIndicatorExams/Results → ExamStudents needs id; link to Index.
        // - PerformanceIndicatorExams/Archive → uses Index?showArchived=true.
        // - EnhancementSkills/Send, Resend → Send(int id) and ResendToStudent(int,int) need params; link to Index.
        // - EnhancementSkills/Archive → Archive(int id) needs param; link to Index.
        // - Attendance/Mark → AttendanceDashboard is a suitable parameterless GET entry point.
        // - Attendance/Students → no "Students" action; link to Index.
        // - Attendance/Archive → actual action is "ArchivedAttendance" (GET, no params).
        private static List<ModuleCandidate> BuildModuleCandidates() =>
        [
            new ModuleCandidate
            {
                Key = "Homework",
                Title = "الواجبات",
                Description = "متابعة الواجبات والتقارير",
                IconCssClass = "fas fa-journal-whills",
                AccentCssClass = "accent-blue",
                BatchFeature = InstructorBatchFeature.Homework,
                PolicyActionPairs =
                [
                    (AdminPermissionPolicies.Homework_Read, new EmployeeDashboardActionVm
                    {
                        Key = "Read", Title = "قائمة الواجبات",
                        Controller = "HomeworkManagement", Action = "Index",
                        IconCssClass = "fas fa-list", IsPrimary = true
                    }),
                    (AdminPermissionPolicies.Homework_Reports, new EmployeeDashboardActionVm
                    {
                        Key = "Reports", Title = "التقارير",
                        Controller = "HomeworkManagement", Action = "Index",
                        IconCssClass = "fas fa-chart-bar"
                    }),
                    (AdminPermissionPolicies.Homework_Details, new EmployeeDashboardActionVm
                    {
                        Key = "Details", Title = "التفاصيل",
                        Controller = "HomeworkManagement", Action = "Index",
                        IconCssClass = "fas fa-info-circle"
                    }),
                    (AdminPermissionPolicies.Homework_Resend, new EmployeeDashboardActionVm
                    {
                        Key = "Resend", Title = "إعادة إرسال",
                        Controller = "HomeworkManagement", Action = "Index",
                        IconCssClass = "fas fa-paper-plane"
                    }),
                    (AdminPermissionPolicies.Homework_Archive, new EmployeeDashboardActionVm
                    {
                        Key = "Archive", Title = "الأرشيف",
                        Controller = "HomeworkManagement", Action = "Archived",
                        IconCssClass = "fas fa-archive"
                    }),
                ]
            },

            new ModuleCandidate
            {
                Key = "Exams",
                Title = "الاختبارات",
                Description = "اختبارات المجموعات ونتائجها",
                IconCssClass = "fas fa-pencil-alt",
                AccentCssClass = "accent-purple",
                BatchFeature = InstructorBatchFeature.Exams,
                PolicyActionPairs =
                [
                    (AdminPermissionPolicies.Exams_Read, new EmployeeDashboardActionVm
                    {
                        Key = "Read", Title = "قائمة الاختبارات",
                        Controller = "ExamAssignments", Action = "Index",
                        IconCssClass = "fas fa-list", IsPrimary = true
                    }),
                    (AdminPermissionPolicies.Exams_Details, new EmployeeDashboardActionVm
                    {
                        Key = "Details", Title = "التفاصيل",
                        Controller = "ExamAssignments", Action = "Index",
                        IconCssClass = "fas fa-info-circle"
                    }),
                    (AdminPermissionPolicies.Exams_Results, new EmployeeDashboardActionVm
                    {
                        Key = "Results", Title = "النتائج",
                        Controller = "ExamAssignments", Action = "Index",
                        IconCssClass = "fas fa-chart-bar"
                    }),
                    (AdminPermissionPolicies.Exams_Archive, new EmployeeDashboardActionVm
                    {
                        Key = "Archive", Title = "الأرشيف",
                        Controller = "ExamAssignments", Action = "Index",
                        RouteValues = new { showArchived = true },
                        IconCssClass = "fas fa-archive"
                    }),
                ]
            },

            new ModuleCandidate
            {
                Key = "PlacementExams",
                Title = "اختبارات تحديد المستوى",
                Description = "تحديد مستوى الطلاب الجدد",
                IconCssClass = "fas fa-award",
                AccentCssClass = "accent-teal",
                PolicyActionPairs =
                [
                    (AdminPermissionPolicies.PlacementExams_Read, new EmployeeDashboardActionVm
                    {
                        Key = "Read", Title = "قائمة الاختبارات",
                        Controller = "PlacementExams", Action = "Index",
                        IconCssClass = "fas fa-list", IsPrimary = true
                    }),
                    (AdminPermissionPolicies.PlacementExams_Results, new EmployeeDashboardActionVm
                    {
                        Key = "Results", Title = "النتائج",
                        Controller = "PlacementExams", Action = "Index",
                        IconCssClass = "fas fa-chart-bar"
                    }),
                    (AdminPermissionPolicies.PlacementExams_Archive, new EmployeeDashboardActionVm
                    {
                        Key = "Archive", Title = "الأرشيف",
                        Controller = "PlacementExams", Action = "ArchivedPlacementExams",
                        IconCssClass = "fas fa-archive"
                    }),
                ]
            },

            new ModuleCandidate
            {
                Key = "PerformanceDashboard",
                Title = "لوحة قياس الأداء",
                Description = "متابعة أداء الدفعات والاختبارات",
                IconCssClass = "fas fa-tachometer-alt",
                AccentCssClass = "accent-orange",
                PolicyActionPairs =
                [
                    (AdminPermissionPolicies.PerformanceDashboard_Read, new EmployeeDashboardActionVm
                    {
                        Key = "Read", Title = "عرض اللوحة",
                        Controller = "PerformanceDashboard", Action = "Index",
                        IconCssClass = "fas fa-tachometer-alt", IsPrimary = true
                    }),
                    (AdminPermissionPolicies.PerformanceDashboard_BatchDetails, new EmployeeDashboardActionVm
                    {
                        Key = "BatchDetails", Title = "تفاصيل الدفعة",
                        Controller = "PerformanceDashboard", Action = "Index",
                        IconCssClass = "fas fa-users"
                    }),
                    (AdminPermissionPolicies.PerformanceDashboard_ExamReport, new EmployeeDashboardActionVm
                    {
                        Key = "ExamReport", Title = "تقرير الاختبار",
                        Controller = "PerformanceDashboard", Action = "Index",
                        IconCssClass = "fas fa-file-alt"
                    }),
                ]
            },

            new ModuleCandidate
            {
                Key = "PerformanceIndicatorExams",
                Title = "اختبارات مؤشر الأداء",
                Description = "قياس مؤشرات الأداء التدريبي",
                IconCssClass = "fas fa-chart-line",
                AccentCssClass = "accent-indigo",
                PolicyActionPairs =
                [
                    (AdminPermissionPolicies.PerformanceIndicatorExams_Read, new EmployeeDashboardActionVm
                    {
                        Key = "Read", Title = "قائمة الاختبارات",
                        Controller = "PerformanceIndicatorExams", Action = "Index",
                        IconCssClass = "fas fa-list", IsPrimary = true
                    }),
                    (AdminPermissionPolicies.PerformanceIndicatorExams_Results, new EmployeeDashboardActionVm
                    {
                        Key = "Results", Title = "النتائج",
                        Controller = "PerformanceIndicatorExams", Action = "Index",
                        IconCssClass = "fas fa-chart-bar"
                    }),
                    (AdminPermissionPolicies.PerformanceIndicatorExams_ConfirmSend, new EmployeeDashboardActionVm
                    {
                        Key = "ConfirmSend", Title = "إرسال معلّق",
                        Controller = "PerformanceIndicatorExams", Action = "Index",
                        IconCssClass = "fas fa-paper-plane"
                    }),
                    (AdminPermissionPolicies.PerformanceIndicatorExams_Archive, new EmployeeDashboardActionVm
                    {
                        Key = "Archive", Title = "الأرشيف",
                        Controller = "PerformanceIndicatorExams", Action = "Index",
                        RouteValues = new { showArchived = true },
                        IconCssClass = "fas fa-archive"
                    }),
                ]
            },

            new ModuleCandidate
            {
                Key = "EnhancementSkills",
                Title = "المهارات التعزيزية",
                Description = "إرسال ومتابعة المهارات التعزيزية",
                IconCssClass = "fas fa-bolt",
                AccentCssClass = "accent-yellow",
                PolicyActionPairs =
                [
                    (AdminPermissionPolicies.EnhancementSkills_Read, new EmployeeDashboardActionVm
                    {
                        Key = "Read", Title = "قائمة المهارات",
                        Controller = "EnhancementSkills", Action = "Index",
                        IconCssClass = "fas fa-list", IsPrimary = true
                    }),
                    (AdminPermissionPolicies.EnhancementSkills_Send, new EmployeeDashboardActionVm
                    {
                        Key = "Send", Title = "إرسال",
                        Controller = "EnhancementSkills", Action = "Index",
                        IconCssClass = "fas fa-paper-plane"
                    }),
                    (AdminPermissionPolicies.EnhancementSkills_Resend, new EmployeeDashboardActionVm
                    {
                        Key = "Resend", Title = "إعادة إرسال",
                        Controller = "EnhancementSkills", Action = "Index",
                        IconCssClass = "fas fa-redo"
                    }),
                    (AdminPermissionPolicies.EnhancementSkills_Archive, new EmployeeDashboardActionVm
                    {
                        Key = "Archive", Title = "الأرشيف",
                        Controller = "EnhancementSkills", Action = "Index",
                        IconCssClass = "fas fa-archive"
                    }),
                ]
            },

            new ModuleCandidate
            {
                Key = "Attendance",
                Title = "الحضور والغياب",
                Description = "تسجيل ومتابعة الحضور",
                IconCssClass = "fas fa-user-check",
                AccentCssClass = "accent-green",
                BatchFeature = InstructorBatchFeature.Attendance,
                PolicyActionPairs =
                [
                    (AdminPermissionPolicies.Attendance_Read, new EmployeeDashboardActionVm
                    {
                        Key = "Read", Title = "عرض الحضور",
                        Controller = "Attendance", Action = "Index",
                        IconCssClass = "fas fa-list", IsPrimary = true
                    }),
                    (AdminPermissionPolicies.Attendance_Mark, new EmployeeDashboardActionVm
                    {
                        Key = "Mark", Title = "تسجيل الحضور",
                        Controller = "Attendance", Action = "AttendanceDashboard",
                        IconCssClass = "fas fa-check-square"
                    }),
                    (AdminPermissionPolicies.Attendance_Students, new EmployeeDashboardActionVm
                    {
                        Key = "Students", Title = "الطلاب",
                        Controller = "Attendance", Action = "Index",
                        IconCssClass = "fas fa-users"
                    }),
                    (AdminPermissionPolicies.Attendance_Archive, new EmployeeDashboardActionVm
                    {
                        Key = "Archive", Title = "الأرشيف",
                        Controller = "Attendance", Action = "ArchivedAttendance",
                        IconCssClass = "fas fa-archive"
                    }),
                ]
            },
        ];
    }
}
