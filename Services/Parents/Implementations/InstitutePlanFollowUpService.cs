using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Parents.Interfaces;
using QdratNew.ViewModels.Parents;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Implementations
{
    public class InstitutePlanFollowUpService : IInstitutePlanFollowUpService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly IParentAccessService _accessService;

        public InstitutePlanFollowUpService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            IParentAccessService accessService)
        {
            _contextFactory = contextFactory;
            _accessService = accessService;
        }

        public async Task<ParentInstitutePlanFollowUpViewModel> GetFollowUpAsync(string parentUserId, int studentId)
        {
            using var db = _contextFactory.CreateDbContext();

            var studentName = await db.Students
                .AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync() ?? "الطالب";

            // جلب الخطة العلاجية المعتمدة (آخر خطة للطالب)
            var plan = await db.RemedialPlans
                .AsNoTracking()
                .Where(p => p.StudentID == studentId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.StartDate,
                    p.EndDate,
                    p.IsCompleted,
                    p.TotalSessions,
                    p.CompletedSessions,
                    p.TotalLessons,
                    p.CompletedLessons,
                    p.AttendedInLab,
                    p.AIRecommendations
                })
                .FirstOrDefaultAsync();

            if (plan == null)
            {
                return new ParentInstitutePlanFollowUpViewModel
                {
                    StudentId = studentId,
                    StudentName = studentName,
                    HasApprovedPlan = false,
                    PlanStatusLabel = "لا توجد خطة",
                    SafeRecommendation = "لا توجد خطة متابعة معتمدة حاليًا. سيقوم المعهد باتخاذ الإجراء المناسب إذا احتاج الطالب إلى دعم إضافي."
                };
            }

            double completionPct = plan.TotalLessons.HasValue && plan.TotalLessons > 0
                ? System.Math.Round((plan.CompletedLessons ?? 0) / (double)plan.TotalLessons.Value * 100, 1)
                : 0;

            // صياغة آمنة لتوصية المعهد
            string? safeRec = null;
            if (!string.IsNullOrWhiteSpace(plan.AIRecommendations))
                safeRec = "توصية المعهد: " + TruncateSafe(plan.AIRecommendations, 150);
            else
                safeRec = "توجد خطة متابعة معتمدة من المعهد. يمكنك متابعة نسبة الإنجاز والحضور فقط.";

            return new ParentInstitutePlanFollowUpViewModel
            {
                StudentId = studentId,
                StudentName = studentName,
                HasApprovedPlan = true,
                CompletionPercentage = completionPct,
                AttendedInLab = plan.AttendedInLab,
                IsCompleted = plan.IsCompleted ?? false,
                PlanStatusLabel = plan.IsCompleted == true ? "مكتملة" : "قيد التنفيذ",
                StartDate = plan.StartDate.HasValue ? plan.StartDate.Value.ToString("yyyy/MM/dd") : null,
                EndDate = plan.EndDate.HasValue ? plan.EndDate.Value.ToString("yyyy/MM/dd") : null,
                TotalSessions = plan.TotalSessions,
                CompletedSessions = plan.CompletedSessions,
                SafeRecommendation = safeRec
            };
        }

        private static string TruncateSafe(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= maxLength ? text : text[..maxLength] + "...";
        }
    }
}
