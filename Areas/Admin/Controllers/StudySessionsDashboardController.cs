using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Shared;
using System.Globalization;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class StudySessionsDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudySessionsDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // كل الجلسات المعتمدة فقط لتحليل العائد
            var approvedSessions = await _context.StudySessionReservations
                .Include(s => s.Instructor)
                .Where(s => s.Status == StudySessionStatus.Approved)
                .ToListAsync();

            // جميع الجلسات لكل الحالات
            var allSessions = await _context.StudySessionReservations.ToListAsync();

            // 💸 إجمالي العوائد
            var totalRevenue = approvedSessions.Sum(s => s.FinalFee ?? s.BaseFee ?? 0);

            // متوسط العائد للجلسة
            var averageRevenue = approvedSessions.Count > 0
                ? Math.Round(totalRevenue / approvedSessions.Count, 2)
                : 0;

            // 🧩 كروت الحالات
            var approvedCount = allSessions.Count(s => s.Status == StudySessionStatus.Approved);
            var rejectedCount = allSessions.Count(s => s.Status == StudySessionStatus.Rejected);
            var pendingCount = allSessions.Count(s => s.Status == StudySessionStatus.Pending);

            // 📊 الرسم البياني الزمني للعوائد
            var revenueOverTime = approvedSessions
                .GroupBy(s => s.RequestedDate.Date)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key.ToString("yyyy-MM-dd"),
                    Value = (float)g.Sum(s => s.FinalFee ?? s.BaseFee ?? 0)
                })
                .OrderBy(x => x.Label)
                .ToList();

            // 📈 الرسم البياني لتفاعل المدربين (الموافقات فقط)
            var trainerEngagement = approvedSessions
                .Where(s => s.Instructor != null)
                .GroupBy(s => s.Instructor.FullName)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value)
                .ToList();

            // 🧠 توصية AI مبسطة
            var aiComment = AnalyzeSessionRevenue(totalRevenue, averageRevenue);

            var viewModel = new StudySessionsDashboardViewModel
            {
                TotalRevenue = totalRevenue,
                TotalSessions = approvedSessions.Count,
                AverageRevenuePerSession = averageRevenue,
                RevenueOverTimeChart = revenueOverTime,
                SessionsByStatus = allSessions
                    .GroupBy(s => s.Status)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count()),

                ApprovedCount = approvedCount,
                RejectedCount = rejectedCount,
                PendingCount = pendingCount,
                TrainerEngagementChartData = trainerEngagement,

                AIRecommendation = aiComment
            };

            return View(viewModel);
        }


        private string AnalyzeSessionRevenue(decimal total, decimal avg)
        {
            if (avg >= 150)
                return "📈 الجلسات تحقق عائد ممتاز! ننصح بالاستمرار والتوسع.";
            else if (avg >= 50)
                return "⚖️ العائد متوسط، يمكن تحسينه بتسويق أفضل وخدمات إضافية.";
            else
                return "🔻 الجلسات غير مجدية ماليًا حاليًا، راجع الأسعار والتنفيذ.";
        }
    }
}
