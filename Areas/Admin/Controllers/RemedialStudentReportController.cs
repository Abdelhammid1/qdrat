using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Remedial;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class RemedialStudentReportController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public RemedialStudentReportController(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // 📊 تقرير علاج الطالب الواحد
        public async Task<IActionResult> StudentReport(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentID == studentId);
            if (student == null)
                return NotFound("❌ لم يتم العثور على الطالب.");

            var plan = await _context.RemedialPlans
                .Include(p => p.Lessons)
                .FirstOrDefaultAsync(p => p.StudentID == studentId);

            if (plan == null)
                return View("NoRemedialPlan", student.FullName);

            // 🔹 جلب الجلسات العلاجية الخاصة بالطالب
            var sessions = await _context.RemedialSessions
                .Include(s => s.Section)
                .Where(s => s.StudentID == studentId)
                .ToListAsync();

            // 🔹 جلب سجلات الفيديوهات والاختبارات
            var videoLogs = await _context.RemedialSessionLogs
                .Where(v => v.StudentId == studentId)
                .ToListAsync();

            var quizResults = await _context.StudentRemedialQuizResults
                .Where(q => q.StudentId == studentId)
                .ToListAsync();

            // 🧠 بناء التقرير
            var report = new RemedialStudentReportVm
            {
                StudentName = student.FullName,
                PlanTitle = plan.Title,
                StartDate = plan.StartDate ?? DateTime.Now,
                EndDate = plan.EndDate ?? DateTime.Now.AddDays(14), // ✅

                TotalVideos = videoLogs.Count,
                CompletedVideos = videoLogs.Count(v => v.IsCompleted),

                // 🧩 جمع الثواني وتحويلها إلى دقائق
                TotalWatchMinutes = Math.Round(videoLogs.Sum(v => v.DurationWatched) / 60.0, 1),

                // 🧩 متوسط درجات الاختبارات التطبيقية
                AverageQuizScore = quizResults.Any()
           ? Math.Round(quizResults.Average(q => q.Score), 1)
           : 0,

                // 🟢 الجلسات
                MissedSessions = sessions.Count(s => !s.IsConfirmed),
                AttendedSessions = sessions.Count(s => s.IsConfirmed),

                // 📈 نسبة التقدم
                ProgressPercent = (plan.TotalLessons ?? 0) > 0
    ? Math.Round(((double)(plan.CompletedLessons ?? 0) / (double)(plan.TotalLessons ?? 1)) * 100.0, 1)
    : 0

            };



            return View("StudentReport", report);
        }
    }
}
