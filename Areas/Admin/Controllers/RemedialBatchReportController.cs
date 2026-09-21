using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Remedial;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class RemedialBatchReportController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public RemedialBatchReportController(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // 📊 تقرير الجلسات العلاجية للدفعة
        public async Task<IActionResult> BatchReport(int batchId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🟢 جلب بيانات الدفعة
            var batch = await _context.Batches
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == batchId);

            if (batch == null)
                return NotFound("⚠️ لم يتم العثور على الدفعة المطلوبة.");

            // 🧩 جلب الطلاب المسجلين في هذه الدفعة من جدول الربط
            var enrolledStudents = await (
                from e in _context.StudentBatchEnrollments
                    .Include(e => e.Student)
                where e.BatchId == batchId && e.Status == "Active"
                select e.Student
            ).ToListAsync();

            // 🧩 جلب بيانات الخطط والجلسات العلاجية للطلاب الحاليين
            var remedialPlans = await _context.RemedialPlans
                .AsNoTracking()
                .ToListAsync();

            var sessions = await _context.RemedialSessions
                .AsNoTracking()
                .ToListAsync();

            // 🧠 بناء التقرير
            var report = enrolledStudents.Select(s =>
            {
                bool hasPlan = remedialPlans.Any(p => p.StudentID == s.StudentID);
                bool hasSession = sessions.Any(ss => ss.StudentID == s.StudentID);
                bool attended = sessions.Any(ss => ss.StudentID == s.StudentID && ss.IsConfirmed);

                return new RemedialBatchReportVm
                {
                    StudentName = s.FullName,
                    HasPlan = hasPlan,
                    HasSession = hasSession,
                    Attended = attended
                };
            }).OrderBy(s => s.StudentName).ToList();

            ViewBag.BatchName = batch.Name;
            return View("BatchReport", report);
        }
    }
}
