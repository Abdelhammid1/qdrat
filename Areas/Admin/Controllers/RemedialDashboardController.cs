using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Remedial;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class RemedialDashboardController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public RemedialDashboardController(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<IActionResult> Index()
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🧩 جلب الخطط العلاجية مع الطالب والفيديوهات والنتائج
            var data = await (
                from p in _context.RemedialPlans
                    .Include(p => p.Student)
                    .Include(p => p.Lessons)
                select new
                {
                    p.Id,
                    p.StudentID,
                    StudentName = p.Student.FullName,
                    p.Title,
                    p.CreatedAt,
                    p.AIAssessedLevel,
                    p.AIRecommendations,
                    LessonsCount = p.Lessons.Count(),
                    CompletedLessons = p.CompletedLessons,
                    p.AIAssessedRiskScore
                }
            ).ToListAsync();

            // 🧩 جلب السجلات المرتبطة بالمشاهدة والاختبارات
            var videoLogs = await _context.RemedialSessionLogs.AsNoTracking().ToListAsync();
            var quizResults = await _context.StudentRemedialQuizResults.AsNoTracking().ToListAsync();

            // 🧮 بناء نموذج التحليل لكل طالب
            var analysis = data.Select(plan =>
            {
                var studentLogs = videoLogs.Where(v => v.StudentId == plan.StudentID).ToList();
                var studentQuizzes = quizResults.Where(q => q.StudentId == plan.StudentID).ToList();

                int totalVideos = studentLogs.Count;
                int completedVideos = studentLogs.Count(v => v.IsCompleted);
                double totalWatchMinutes = Math.Round(studentLogs.Sum(v => v.DurationWatched) / 60.0, 1);
                double avgWatchPerVideo = totalVideos > 0 ? Math.Round(totalWatchMinutes / totalVideos, 1) : 0;

                double avgQuizScore = studentQuizzes.Any() ? Math.Round(studentQuizzes.Average(q => q.Score), 1) : 0;

                double progress = plan.LessonsCount == 0
                              ? 0.0
                              : Math.Round(((double)plan.CompletedLessons / plan.LessonsCount) * 100.0, 1);

                string recommendation;
                if (avgQuizScore >= 80)
                    recommendation = "الطالب أظهر تحسّنًا كبيرًا. يُوصى بالانتقال إلى المستوى التالي.";
                else if (avgQuizScore >= 60)
                    recommendation = "تحسّن جزئي. يُوصى بجلسة مراجعة إضافية على الفيديوهات غير المكتملة.";
                else
                    recommendation = "يحتاج خطة متابعة إضافية مع تدريبات تطبيقية مكثفة.";

                return new RemedialDashboardVm
                {
                    StudentName = plan.StudentName,
                    PlanTitle = plan.Title,
                    PlanCreatedAt = plan.CreatedAt,
                    TotalVideos = totalVideos,
                    CompletedVideos = completedVideos,
                    TotalWatchMinutes = totalWatchMinutes,
                    AverageWatchPerVideo = avgWatchPerVideo,
                    AverageQuizScore = avgQuizScore,
                    ProgressPercent = progress,
                    AIAssessedLevel = plan.AIAssessedLevel ?? "غير محدد",
                    AIRecommendations = plan.AIRecommendations ?? recommendation,
                    SmartRecommendation = recommendation,
                    AIAssessedRiskScore = plan.AIAssessedRiskScore ?? 0.0
                };
            }).OrderByDescending(x => x.ProgressPercent).ToList();

            return View("Index", analysis);
        }
    }
}
