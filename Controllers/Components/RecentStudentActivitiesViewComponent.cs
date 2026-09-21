using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Students;

namespace QdratNew.Controllers.Components
{
    public class RecentStudentActivitiesViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RecentStudentActivitiesViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            if (student == null)
                return View("Empty");

            var logs = new List<StudentActivityEntry>();

            // 🟢 1. واجبات
            var homeworks = await _context.StudentActivityLogs
                .Where(l => l.StudentId == student.StudentID && l.Source.Contains("Homework"))
                .OrderByDescending(l => l.Timestamp)
                .Take(3)
                .ToListAsync();

            logs.AddRange(homeworks.Select(h => new StudentActivityEntry
            {
                Timestamp = h.Timestamp,
                Type = "حل واجب",
                Description = $"حل واجب ({h.Source}) بدرجة {h.Score}%"
            }));

            // 🟡 2. اختبارات
            var exams = await _context.StudentActivityLogs
                .Where(l => l.StudentId == student.StudentID && l.Source.Contains("Exam"))
                .OrderByDescending(l => l.Timestamp)
                .Take(3)
                .ToListAsync();

            logs.AddRange(exams.Select(e => new StudentActivityEntry
            {
                Timestamp = e.Timestamp,
                Type = "اختبار",
                Description = $"أكمل اختبار ({e.Source}) بدرجة {e.Score}%"
            }));

            // 🔵 3. تفاعلات علاجية
            var plans = await _context.RemedialPlanInteractions
                .Where(i => i.StudentId == student.StudentID)
                .OrderByDescending(i => i.InteractionTime)
                .Take(3)
                .ToListAsync();

            logs.AddRange(plans.Select(p => new StudentActivityEntry
            {
                Timestamp = p.InteractionTime,
                Type = "خطة علاجية",
                Description = !string.IsNullOrWhiteSpace(p.Comment)
                    ? $"علّق على الخطة: \"{p.Comment}\""
                    : $"قام بنشاط علاجي: {p.ActionTaken}"
            }));

            // 🔴 4. جلسات تدريبية
            var sessions = await _context.StudySessionRatings
                .Where(r => r.StudentId == student.StudentID)
                .OrderByDescending(r => r.RatedAt)
                .Take(2)
                .ToListAsync();

            logs.AddRange(sessions.Select(s => new StudentActivityEntry
            {
                Timestamp = s.RatedAt,
                Type = "جلسة تدريبية",
                Description = $"قيّمت جلسة بمعدل {s.SessionBenefit}/5"
            }));

            // 🔽 ترتيب حسب الوقت
            var recent = logs.OrderByDescending(x => x.Timestamp).Take(6).ToList();

            return View("Default", recent);
        }
    }
}
