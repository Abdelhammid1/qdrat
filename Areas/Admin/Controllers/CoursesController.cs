using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Course;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class CoursesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CoursesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private void LoadDropDownLists(CourseFormViewModel model)
        {
            model.Branches = GetBranchesList();
            model.Projects = GetProjectsList(); // ✅ تم تعديلها لعرض (اسم المشروع - الفرع - الولاية)
        }

        public async Task<IActionResult> Index()
        {
            // الخطوة 1: تحميل البيانات
            var coursesData = await _context.Courses
                .Include(c => c.Branch)
                .Include(c => c.Project)
                    .ThenInclude(p => p.Branch)
                .ToListAsync();

            // الخطوة 2: معالجتها بالذاكرة
            var courses = coursesData.Select(c => new CourseFormViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                StartDate = c.StartDate,
                EndDate = c.EndDate ?? DateTime.MinValue,
                BranchId = c.BranchId,
                BranchName = c.Branch != null ? c.Branch.Name : "غير متوفر",
                ProjectId = c.ProjectId,
                ProjectName = c.Project != null
                    ? $"{c.Project.Name} ({c.Project.Branch?.Name ?? "غير متوفر"} - {c.Project.Branch?.State ?? "غير متوفر"})"
                    : "غير متوفر"
            }).ToList();


            return View(courses);
        }

        public IActionResult Create()
        {
            var model = new CourseFormViewModel
            {
                IsActive = true, // ✅ بشكل افتراضي الدورة تكون مفعلة
                Branches = GetBranchesList(),
                Projects = GetProjectsList()
            };
            LoadDropDownLists(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CourseFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadDropDownLists(model);
                return View(model);
            }

            try
            {
                var course = new Course
                {
                    Name = model.Name,
                    Description = model.Description,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    BranchId = model.BranchId,
                    ProjectId = model.ProjectId,
                    IsActive = model.IsActive // ✅ حفظ حالة التفعيل
                };

                _context.Courses.Add(course);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "✅ تم إضافة الدورة بنجاح!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                ModelState.AddModelError("", "حدث خطأ أثناء حفظ البيانات.");
                LoadDropDownLists(model);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            var model = new CourseFormViewModel
            {
                Id = course.Id,
                Name = course.Name,
                Description = course.Description,
                StartDate = course.StartDate,
                EndDate = course.EndDate ?? DateTime.Now,
                BranchId = course.BranchId,
                IsActive = course.IsActive, // ✅ حفظ حالة التفعيل

                ProjectId = course.ProjectId
            };

            LoadDropDownLists(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(CourseFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadDropDownLists(model);
                return View(model);
            }

            var course = await _context.Courses.FindAsync(model.Id);
            if (course == null) return NotFound();

            course.Name = model.Name;
            course.Description = model.Description;
            course.StartDate = model.StartDate;
            course.EndDate = model.EndDate;
            course.BranchId = model.BranchId;
            course.ProjectId = model.ProjectId;
            course.IsActive = model.IsActive; // ✅ حفظ حالة التفعيل

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ تم تعديل الدورة بنجاح!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var course = await _context.Courses
                .Include(c => c.Branch)
                .Include(c => c.Project).ThenInclude(p => p.Branch)
                .Include(c => c.CourseInstructors).ThenInclude(ci => ci.Instructor)
                .Include(c => c.CourseCurriculums).ThenInclude(cc => cc.Curriculum)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (course == null) return NotFound();

            // الدفعات مع عدد الطلاب
            var batches = await _context.Batches
                .AsNoTracking()
                .Where(b => b.CourseId == id && !b.IsDeleted)
                .Select(b => new CourseBatchSummary
                {
                    Id           = b.Id,
                    Name         = b.Name,
                    StartDate    = b.StartDate,
                    EndDate      = b.EndDate,
                    IsActive     = b.IsActive,
                    IsArchived   = b.IsArchived,
                    GenderLabel  = b.Gender.ToString(),
                    StudentCount = b.StudentBatchEnrollments.Count,
                })
                .OrderByDescending(b => b.StartDate)
                .ToListAsync();

            var model = new CourseDetailsViewModel
            {
                Id              = course.Id,
                Name            = course.Name,
                Description     = course.Description ?? "",
                StartDate       = course.StartDate,
                EndDate         = course.EndDate ?? DateTime.MinValue,
                IsActive        = course.IsActive,
                BranchName      = course.Branch?.Name ?? "—",
                ProjectName     = course.Project != null
                                  ? $"{course.Project.Name} ({course.Project.Branch?.Name})"
                                  : "—",
                Batches         = batches,
                TotalBatches    = batches.Count,
                TotalStudents   = batches.Sum(b => b.StudentCount),
                TotalInstructors= course.CourseInstructors?.Count ?? 0,
                TotalCurriculums= course.CourseCurriculums?.Count ?? 0,
                Instructors     = course.CourseInstructors?
                                  .Select(ci => ci.Instructor?.FullName ?? "")
                                  .Where(n => n.Length > 0).ToList() ?? new(),
                Curriculums     = course.CourseCurriculums?
                                  .Select(cc => cc.Curriculum?.Title ?? "")
                                  .Where(n => n.Length > 0).ToList() ?? new(),
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            var model = new CourseDeleteViewModel
            {
                Id = course.Id,
                Name = course.Name
            };

            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "🗑️ تم حذف الدورة بنجاح!";
            return RedirectToAction(nameof(Index));
        }

        private List<SelectListItem> GetBranchesList()
        {
            return _context.Branches
                .AsNoTracking()
                .Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.Name })
                .ToList();
        }

        private List<SelectListItem> GetProjectsList()
        {
            return _context.Projects
                .Include(p => p.Branch) // فقط نجيب الفرع لأن فيه الولاية كنص
                .AsNoTracking()
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.Name} ({p.Branch.Name} - {p.Branch.State})"
                }).ToList();
        }

    }
}
