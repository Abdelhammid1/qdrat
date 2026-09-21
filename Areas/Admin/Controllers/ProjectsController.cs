using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Project;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProjectsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Projects
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Projects.Include(p => p.Branch);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Admin/Projects/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var project = await _context.Projects
                .Include(p => p.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (project == null)
            {
                return NotFound();
            }

            return View(project);
        }

        // GET: Admin/Projects/Create
        [HttpGet]
        public IActionResult Create()
        {
            var model = new ProjectViewModel
            {
                Branches = _context.Branches
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name + " - " + b.State // ✅ الاسم مع الولاية
                    }).ToList()
            };

            return View(model);
        }


        // POST: Admin/Projects/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ProjectViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Branches = _context.Branches
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name + " - " + b.State
                    }).ToList();
                return View(model);
            }

            var project = new Project
            {
                Name = model.Name,
                Description = model.Description,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                ExpectedEndDate = model.ExpectedEndDate,
                IsActive = model.IsActive,
                BranchId = model.BranchId
            };

            _context.Projects.Add(project);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }


        [HttpGet]
        public IActionResult Dashboard()
        {
            var projects = _context.Projects
                .Include(p => p.Branch)
                .Include(p => p.Courses)
                    .ThenInclude(c => c.StudentCourses)
                .ToList();

            var today = DateTime.Today;

            var completedProjects = projects
                .Where(p => p.EndDate.HasValue && p.StartDate.HasValue)
                .ToList();

            var projectsOnTime = completedProjects.Count(p =>
                p.ExpectedEndDate.HasValue &&
                p.EndDate.Value <= p.ExpectedEndDate.Value
            );

            var projectsOverdue = completedProjects.Count(p =>
                p.ExpectedEndDate.HasValue &&
                p.EndDate.Value > p.ExpectedEndDate.Value
            );

            var executionDurations = completedProjects
                .Select(p => new ExecutionDurationViewModel
                {
                    Name = p.Name,
                    DurationDays = (p.EndDate.Value - p.StartDate.Value).TotalDays
                }).ToList();

            var avgExecutionDays = executionDurations.Any()
                ? executionDurations.Average(e => e.DurationDays)
                : 0;

            var successRate = completedProjects.Count > 0
                ? projectsOnTime * 100.0 / completedProjects.Count
                : 0;

            var upcomingProjects = projects
                .Where(p => p.StartDate.HasValue &&
                            p.StartDate.Value >= today &&
                            p.StartDate.Value <= today.AddDays(30))
                .Select(p => p.Name)
                .ToList();

            // ✅ الدورات لكل مشروع
            var coursesPerProject = projects.ToDictionary(
                p => p.Name,
                p => p.Courses.Count
            );

            // ✅ عدد الطلاب في كل دورة داخل المشاريع
            var studentsPerCourseInProjects = new Dictionary<string, Dictionary<string, int>>();
            foreach (var project in projects)
            {
                var courseData = new Dictionary<string, int>();
                foreach (var course in project.Courses)
                {
                    courseData[course.Name] = course.StudentCourses.Count;
                }
                studentsPerCourseInProjects[project.Name] = courseData;
            }

            // ✅ نسبة إكمال الدورات داخل البرامج
            var courseCompletionRate = projects.ToDictionary(
                p => p.Name,
                p => p.Courses.Count > 0
                    ? p.Courses.Count(c => c.EndDate.HasValue && c.EndDate.Value <= today) * 100.0 / p.Courses.Count
                    : 0
            );

            // ✅ إجمالي الطلاب في كل مشروع
            var studentsPerProject = projects.ToDictionary(
                p => p.Name,
                p => p.Courses.SelectMany(c => c.StudentCourses).Count()
            );

            // ✅ تطور تسجيل الطلاب شهرياً
            var studentsRegistrationPerMonth = _context.StudentCourses
                .AsEnumerable()  // ✅ ضروري نحول لـ LINQ to Objects عشان نقدر نستخدم ToString
                .Where(sc => sc.DateEnrolled != null)
                .GroupBy(sc => sc.DateEnrolled.ToString("yyyy-MM"))
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.Count()
                );

            var dashboardData = new ProjectsDashboardViewModel
            {
                TotalProjects = projects.Count,
                ActiveProjects = projects.Count(p => p.IsActive),
                CompletedProjects = completedProjects.Count,
                UpcomingProjects = projects.Count(p => p.StartDate.HasValue && p.StartDate.Value > today),

                AverageExecutionDays = Math.Round(avgExecutionDays, 1),
                SuccessRate = Math.Round(successRate, 1),
                UpcomingProjectsIn30Days = upcomingProjects.Count,

                ProjectsOnTime = projectsOnTime,
                ProjectsOverdue = projectsOverdue,

                ProjectsPerBranch = projects
                    .Where(p => p.Branch != null)
                    .GroupBy(p => p.Branch.Name)
                    .ToDictionary(x => x.Key, x => x.Count()),

                RecentProjects = projects
                    .OrderByDescending(p => p.StartDate)
                    .Take(5)
                    .ToList(),

                ExecutionDurations = executionDurations,
                UpcomingProjectNames = upcomingProjects,

                // ✅ البيانات الجديدة المضافة
                CoursesPerProject = coursesPerProject,
                StudentsPerCourseInProjects = studentsPerCourseInProjects,
                CourseCompletionRatePerProject = courseCompletionRate,
                StudentsPerProject = studentsPerProject,
                StudentsRegistrationPerMonth = studentsRegistrationPerMonth
            };

            return View(dashboardData);
        }


        // GET: Admin/Projects/Edit/5
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var project = _context.Projects.Find(id);

            if (project == null)
            {
                return NotFound();
            }

            var model = new ProjectViewModel
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                ExpectedEndDate = project.ExpectedEndDate,
                IsActive = project.IsActive,
                BranchId = project.BranchId,
                Branches = _context.Branches
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name + " - " + b.State // ✅ عرض الفرع مع الولاية
                    }).ToList()
            };

            return View(model);
        }


        // POST: Admin/Projects/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, ProjectViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                model.Branches = _context.Branches
                    .Select(b => new SelectListItem
                    {
                        Value = b.Id.ToString(),
                        Text = b.Name + " - " + b.State
                    }).ToList();
                return View(model);
            }

            var project = _context.Projects.Find(id);
            if (project == null)
            {
                return NotFound();
            }

            project.Name = model.Name;
            project.Description = model.Description;
            project.StartDate = model.StartDate;
            project.EndDate = model.EndDate;
            project.ExpectedEndDate = model.ExpectedEndDate;
            project.IsActive = model.IsActive;
            project.BranchId = model.BranchId;

            _context.Projects.Update(project);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Projects/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var project = await _context.Projects
                .Include(p => p.Branch)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (project == null)
            {
                return NotFound();
            }

            return View(project);
        }

        // POST: Admin/Projects/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project != null)
            {
                _context.Projects.Remove(project);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ProjectExists(int id)
        {
            return _context.Projects.Any(e => e.Id == id);
        }
    }
}
