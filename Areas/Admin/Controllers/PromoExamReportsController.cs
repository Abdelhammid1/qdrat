using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Areas.Admin.Controllers
{
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class PromoExamReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PromoExamReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 📋 عرض جميع الطلبات
        public async Task<IActionResult> Index()
        {
            var data = await (
                from lead in _context.PromoLeads
                join session in _context.PromoExamSessions on lead.SessionId equals session.Id
                join result in _context.PromoExamResults on session.Id equals result.SessionId
                join course in _context.Courses on session.CourseId equals course.Id
                orderby lead.CreatedAt descending
                select new
                {
                    lead.FullName,
                    lead.PhoneNumber,
                    lead.WhatsAppNumber,
                    lead.CreatedAt,
                    course.Name,
                    result.ScorePercent,
                    session.Id
                }
            ).ToListAsync();

            return View(data);
        }

        // 📊 عرض تقرير اختبار طالب معين
        public async Task<IActionResult> Report(int sessionId)
        {
            var result = await _context.PromoExamResults
                .FirstOrDefaultAsync(r => r.SessionId == sessionId);

            var attempts = await _context.PromoExamAttempts
                .Include(a => a.Question)
                .Where(a => a.SessionId == sessionId)
                .ToListAsync();

            if (result == null)
                return NotFound();

            var vm = new
            {
                Result = result,
                Attempts = attempts
            };

            return View("Report", vm);
        }
    }
}
