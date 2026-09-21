using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.AI;
using QdratNew.ViewModels;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class SectionsAIDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ISectionAIService _sectionAIService;

        public SectionsAIDashboardController(ApplicationDbContext context, ISectionAIService sectionAIService)
        {
            _context = context;
            _sectionAIService = sectionAIService;
        }

        public async Task<IActionResult> Index(int? batchId, int? curriculumId)
        {
            var performancesQuery = _context.StudentPerformances
                .Include(p => p.Section)
                    .ThenInclude(s => s.Curriculum)
                .Include(p => p.Student)
                .AsQueryable();
            var totalAllSections = await _context.Sections.CountAsync();


            if (batchId.HasValue)
            {
                performancesQuery =
                    from p in performancesQuery
                    join e in _context.StudentBatchEnrollments
                        on p.Student.StudentID equals e.StudentID
                    where e.BatchId == batchId.Value
                    select p;
            }


            if (curriculumId.HasValue)
            {
                performancesQuery = performancesQuery.Where(p => p.CurriculumId == curriculumId.Value);
            }

            // ✅ استخدم الاستعلام بعد الفلاتر
            var performances = await performancesQuery
                .Where(p => p.SectionId != null)
                .ToListAsync();

            var sections = performances
                .Where(p => p.Section != null)
                .Select(p => p.Section!)
                .Distinct()
                .ToList();

            var viewModel = await _sectionAIService.AnalyzePerformancesAsync(performances, sections);
            // 🔻 يتم وضعه هنا مباشرة بعد تحليل البيانات
            viewModel.TotalSectionsWithPerformance = sections.Count;

            viewModel.TotalSectionsWithPerformance = sections.Count;
            viewModel.TotalSectionsInDatabase = await _context.Sections.CountAsync();

            // تعبئة الفلاتر في ViewModel
            viewModel.BatchList = await _context.Batches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToListAsync();

            viewModel.CurriculumList = await _context.Curriculums
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title
                }).ToListAsync();

            viewModel.SelectedBatchId = batchId;
            viewModel.SelectedCurriculumId = curriculumId;
            viewModel.SelectedBatchId = batchId;
            viewModel.SelectedCurriculumId = curriculumId;


            return View(viewModel);
        }

    }
}
