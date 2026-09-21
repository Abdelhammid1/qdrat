using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Remedial;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class RemedialAnalysisController : Controller
    {
        private readonly IPerformanceComparisonService _performanceComparisonService;
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public RemedialAnalysisController(IPerformanceComparisonService performanceComparisonService, IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _performanceComparisonService = performanceComparisonService;
            _contextFactory = contextFactory;
        }

        // 📊 تحليل المحاور الضعيفة للطالب داخل منهج محدد
        [HttpGet]
        public async Task<IActionResult> WeakSections(int studentId, int? curriculumId = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 🔹 تحديد المنهج تلقائيًا إذا لم يتم تمريره
            if (curriculumId == null)
            {
                curriculumId = await (
                    from e in _context.StudentBatchEnrollments
                    join b in _context.Batches on e.BatchId equals b.Id
                    join c in _context.Courses on b.CourseId equals c.Id
                    join cc in _context.CourseCurriculums on c.Id equals cc.CourseId
                    join cur in _context.Curriculums on cc.CurriculumId equals cur.Id
                    where e.StudentID == studentId && e.Status == "Active"
                    select (int?)cur.Id
                ).FirstOrDefaultAsync();
            }

            if (curriculumId == null)
                return NotFound("⚠️ لا يمكن تحديد المنهج المرتبط بالطالب.");

            // 🧩 التحليل الموحد من جدول StudentPerformance
            var weakSections = await _performanceComparisonService.AnalyzeWeakSectionsAsync(studentId, curriculumId.Value);

            // 🧍 اسم الطالب والمنهج
            var studentName = await _context.Students
                .Where(x => x.StudentID == studentId)
                .Select(x => x.FullName)
                .FirstOrDefaultAsync();

            var curriculumTitle = await _context.Curriculums
                .Where(c => c.Id == curriculumId)
                .Select(c => c.Title)
                .FirstOrDefaultAsync();

            var vm = new WeakSectionsListVm
            {
                StudentId = studentId,
                StudentName = studentName ?? "غير معروف",
                CurriculumTitle = curriculumTitle ?? "—",
                WeakSections = weakSections
            };

            return View(vm);
        }

        // 🔍 مقارنة أداء الطالب في محور محدد بالتفصيل
        [HttpGet]
        public async Task<IActionResult> CompareStudentSectionPerformance(int studentId, int sectionId)
        {
            var analysis = await _performanceComparisonService.AnalyzeStudentSectionPerformanceAsync(studentId, sectionId);
            return View("CompareStudentSectionPerformance", analysis);
        }
    }
}
