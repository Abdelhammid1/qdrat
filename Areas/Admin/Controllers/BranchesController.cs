using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.AI.Helpers;
using QdratNew.AI.MLModels.Branches;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.AI;
using QdratNew.ViewModels.Branches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class BranchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BranchesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // GET: Admin/Branches
        public async Task<IActionResult> Index(string filter = "active")
        {
            ViewBag.Filter = filter;
            var query = _context.Branches.AsQueryable();

            query = filter switch
            {
                "all"      => query,
                "inactive" => query.Where(b => !b.IsActive && !b.IsArchived),
                "archived" => query.Where(b => b.IsArchived),
                _          => query.Where(b => b.IsActive && !b.IsArchived)
            };

            return View(await query.ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            branch.IsActive = !branch.IsActive;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = branch.IsActive
                ? $"✅ تم تفعيل فرع \"{branch.Name}\" بنجاح"
                : $"⏸️ تم إيقاف فرع \"{branch.Name}\" مؤقتاً";

            return RedirectToAction(nameof(Index), new { filter = ViewBag.Filter ?? "active" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            branch.IsArchived = true;
            branch.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"📦 تم أرشفة فرع \"{branch.Name}\" بنجاح";
            return RedirectToAction(nameof(Index), new { filter = "active" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unarchive(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null) return NotFound();

            branch.IsArchived = false;
            branch.IsActive = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ تم استعادة فرع \"{branch.Name}\" من الأرشيف";
            return RedirectToAction(nameof(Index), new { filter = "active" });
        }

        // GET: Admin/Branches/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches
                .FirstOrDefaultAsync(m => m.Id == id);
            if (branch == null)
            {
                return NotFound();
            }

            return View(branch);
            }


        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var branches = await _context.Branches
                .Include(b => b.Students).ThenInclude(s => s.StudentPerformances)
                .Include(b => b.Courses)
                .Include(b => b.Projects)
                .ToListAsync();

            // 🧠 التحليلات الزمنية
            var performanceData = TimeSeriesDataHelper.PrepareMonthlyAveragePerformance(branches);
            var courseData = TimeSeriesDataHelper.PrepareMonthlyCourseCounts(branches);
            var studentData = TimeSeriesDataHelper.PrepareMonthlyStudentCounts(branches);

            var studentForecast = TimeSeriesChartPredictor.Predict("MLModels/Branches/StudentsTrend/StudentCountModel.zip", 6);
            var performanceForecast = TimeSeriesChartPredictor.Predict("MLModels/Branches/PerformanceTrend/PerformanceModel.zip", 6);
            var courseForecast = TimeSeriesChartPredictor.Predict("MLModels/Branches/CoursesTrend/CoursesModel.zip", 6);

            var user = await _userManager.GetUserAsync(User);

            var model = new BranchesDashboardViewModel
            {
                FullName = user?.FullName ?? "مستخدم",
                BranchReports = branches.Select(BranchAIAnalyzer.Analyze).ToList(),
                MonthlyStudentCounts = studentData,
                MonthlyAveragePerformance = performanceData,
                MonthlyCourseCounts = courseData,
                StudentForecast = studentForecast.Select(p => p.ForecastedValue).ToList(),
                PerformanceForecast = performanceForecast.Select(p => p.ForecastedValue).ToList(),
                CoursesForecast = courseForecast.Select(p => p.ForecastedValue).ToList(),
                PerformanceForecastPoints = performanceForecast
            };

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            var branches = await _context.Branches
                .Include(b => b.Students)
                    .ThenInclude(s => s.StudentPerformances)
                .Include(b => b.Courses)
                .Include(b => b.Projects)
                .ToListAsync();

            var viewModel = new BranchDashboardViewModel
            {
                BranchCards = branches.Select(b => new BranchCardViewModel
                {
                    Id = b.Id,
                    BranchName = b.Name,
                    TotalStudents = b.Students?.Count ?? 0,
                    TotalCourses = b.Courses?.Count ?? 0,
                    TotalProjects = b.Projects?.Count ?? 0,
                    AveragePerformance = b.AveragePerformance,
                    AiComment = BranchAIAnalyzer.Analyze(b).AiComment,
                    Recommendation = BranchAIAnalyzer.Analyze(b).Recommendation
                }).ToList(),

                PerformanceChartData = branches.Select(b => new BranchChartData
                {
                    Label = b.Name,
                    Value = b.AveragePerformance
                }).ToList(),

                StudentCountChartData = branches.Select(b => new BranchChartData
                {
                    Label = b.Name,
                    Value = b.Students?.Count ?? 0
                }).ToList()
            };

            return Json(viewModel);
        }

        // GET: Admin/Branches/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Branches/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Name,Location,City,State,Country,IsPartner,IsActive,EstablishedDate")] Branch branch)
        {
            if (ModelState.IsValid)
            {
                _context.Add(branch);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "✅ تم إضافة الفرع بنجاح!";
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }
        public string SuggestNewBranchLocation()
        {
            var mostPopularCity = _context.Branches
                .GroupBy(b => b.City)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();

            return mostPopularCity ?? "لم يتم العثور على بيانات كافية";
        }
    


        // GET: Admin/Branches/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches.FindAsync(id);
            if (branch == null)
            {
                return NotFound();
            }
            return View(branch);
        }

        // POST: Admin/Branches/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Location,City,State,Country,IsPartner,IsActive,EstablishedDate")] Branch branch)
        {
            if (id != branch.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(branch);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BranchExists(branch.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(branch);
        }

        // GET: Admin/Branches/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches
                .FirstOrDefaultAsync(m => m.Id == id);
            if (branch == null)
            {
                return NotFound();
            }

            return View(branch);
        }

        // POST: Admin/Branches/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch != null)
            {
                _context.Branches.Remove(branch);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BranchExists(int id)
        {
            return _context.Branches.Any(e => e.Id == id);
        }
    }
}
