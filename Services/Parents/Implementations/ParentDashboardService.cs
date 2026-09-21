using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Implementations
{
    public class ParentDashboardService : IParentDashboardService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IParentAccessService _accessService;
        private readonly IParentSafeInsightService _insightService;
        private readonly IInstitutePlanFollowUpService _planService;

        public ParentDashboardService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IParentAccessService accessService,
            IParentSafeInsightService insightService,
            IInstitutePlanFollowUpService planService)
        {
            _contextFactory = contextFactory;
            _accessService = accessService;
            _insightService = insightService;
            _planService = planService;
        }

        public async Task<ParentDashboardViewModel> GetDashboardAsync(string parentUserId, int? selectedStudentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var parentId = await _accessService.GetCurrentParentIdAsync(parentUserId);
            var parent = parentId.HasValue
                ? await db.Parents.AsNoTracking()
                    .Where(p => p.ParentID == parentId)
                    .Select(p => p.FullName)
                    .FirstOrDefaultAsync()
                : "ولي الأمر";

            // أبناء ولي الأمر
            var students = await db.Students
                .AsNoTracking()
                .Where(s => s.ParentId == parentId)
                .Select(s => new { s.StudentID, s.FullName, s.Level, s.School })
                .ToListAsync();

            var children = students.Select(s => new ParentChildCardViewModel
            {
                StudentId = s.StudentID,
                StudentName = s.FullName,
                Level = s.Level,
                School = s.School
            }).ToList();

            if (!children.Any())
            {
                return new ParentDashboardViewModel
                {
                    ParentName = parent ?? "ولي الأمر",
                    Children = children
                };
            }

            // اختيار الطالب الأول إذا لم يُحدد
            int activeStudentId = selectedStudentId ?? students.First().StudentID;

            ParentStudentInsightViewModel? insight = null;
            if (parentId.HasValue)
            {
                insight = await _insightService.GetSafeInsightAsync(parentId.Value, activeStudentId);
            }

            // خطة المعهد
            var planVm = await _planService.GetFollowUpAsync(parentUserId, activeStudentId);

            // إشعارات بسيطة (آخر 5 من جدول الإشعارات)
            var userId = await db.Parents
                .AsNoTracking()
                .Where(p => p.ParentID == parentId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync();

            var notifications = new List<ParentNotificationItemViewModel>();
            if (!string.IsNullOrEmpty(userId))
            {
                var dbNotifs = await db.Notifications
                    .AsNoTracking()
                    .Where(n => n.UserId == userId)
                    .OrderByDescending(n => n.SentAt)
                    .Take(5)
                    .ToListAsync();

                notifications = dbNotifs.Select(n => new ParentNotificationItemViewModel
                {
                    Id = n.NotificationId,
                    Title = "إشعار",
                    Message = n.Message,
                    TargetUrl = n.TargetUrl,
                    IsRead = n.IsRead,
                    TimeAgo = GetTimeAgo(n.SentAt),
                    IconClass = "fa-bell",
                    BadgeColor = n.IsRead ? "secondary" : "primary"
                }).ToList();
            }

            string bestAction = insight?.RecommendedAction ?? "اقرأ التقرير الأسبوعي";
            string bestActionUrl = GetBestActionUrl(bestAction, activeStudentId);

            return new ParentDashboardViewModel
            {
                ParentName = parent ?? "ولي الأمر",
                SelectedStudentId = activeStudentId,
                Children = children,
                CurrentInsight = insight,
                BestActionNow = bestAction,
                BestActionUrl = bestActionUrl,
                HasInstitutePlan = planVm.HasApprovedPlan,
                InstitutePlanSummary = planVm.HasApprovedPlan
                    ? "توجد خطة متابعة معتمدة من المعهد."
                    : null,
                InstitutePlanProgress = planVm.CompletionPercentage,
                RecentNotifications = notifications,
                UnreadNotificationCount = notifications.Count(n => !n.IsRead)
            };
        }

        private static string GetBestActionUrl(string action, int studentId)
        {
            return action switch
            {
                "تابع واجبًا متأخرًا" => $"/Parents/HomeworkFollowUp?studentId={studentId}",
                "ابدأ اختبار تعزيز قصير" => $"/Parents/SmartPractice/Create?studentId={studentId}",
                "اقرأ التقرير الأسبوعي" => $"/Parents/Reports/Weekly?studentId={studentId}",
                _ => $"/Parents/Dashboard?studentId={studentId}"
            };
        }

        private static string GetTimeAgo(System.DateTime dt)
        {
            var diff = System.DateTime.Now - dt;
            if (diff.TotalMinutes < 60) return $"منذ {(int)diff.TotalMinutes} دقيقة";
            if (diff.TotalHours < 24) return $"منذ {(int)diff.TotalHours} ساعة";
            return $"منذ {(int)diff.TotalDays} يوم";
        }
    }
}
