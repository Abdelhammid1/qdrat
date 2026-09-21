using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Curriculum;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class CurriculumAIDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CurriculumAIDashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? batchId, DateTime? fromDate, DateTime? toDate)
        {
            // 1. إنشاء الاستعلام الأساسي
            var performancesQuery = _context.StudentPerformances
                .Include(p => p.Section)
                .Include(p => p.Curriculum)
                .Include(p => p.Student)
                .Where(p => p.SectionId != null && p.Curriculum != null); // ✅ تأكيد وجود منهج

            // 2. فلترة حسب الدفعة
            if (batchId.HasValue)
            {
                performancesQuery = performancesQuery
                    .Where(p => p.Student != null &&
                                _context.StudentBatchEnrollments
                                    .Any(e => e.StudentID == p.Student.StudentID && e.BatchId == batchId.Value));
            }

            // 3. فلترة حسب الفترة الزمنية
            if (fromDate.HasValue)
                performancesQuery = performancesQuery.Where(p => p.ExamDate >= fromDate.Value);

            if (toDate.HasValue)
                performancesQuery = performancesQuery.Where(p => p.ExamDate <= toDate.Value);

            // 4. جلب البيانات بعد الفلترة
            var data = await performancesQuery.ToListAsync();

            // 5. تجميع الأداء حسب كل منهج
            var grouped = data
                .GroupBy(p => p.Curriculum)
                .Select(g => new CurriculumPerformanceViewModel
                {
                    CurriculumTitle = g.Key.Title,
                    TotalPerformances = g.Count(),
                    AverageScore = g.Average(p => p.Score),
                    SuccessRate = Math.Round(g.Count(p => p.Score >= 60) * 100.0 / g.Count(), 1),
                    FailureRate = Math.Round(g.Count(p => p.Score < 60) * 100.0 / g.Count(), 1)
                }).ToList();

            // 6. تحميل قائمة الدفعات
            var batchList = await _context.Batches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToListAsync();

            // 7. حساب عدد المناهج الكلية
            var totalCurriculums = await _context.Curriculums.CountAsync();

            // 8. تعبئة ViewModel
            var vm = new CurriculumAIDashboardViewModel
            {
                CurriculumStats = grouped,
                FromDate = fromDate,
                ToDate = toDate,
                BatchList = batchList,
                SelectedBatchId = batchId,
                TotalCurriculumsInSystem = totalCurriculums,
                TotalCurriculumsWithPerformance = grouped.Count
            };

            return View(vm);
        }


    }
}
