using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security.Permissions;
using QdratNew.ViewModels.Admin;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class AdminPermissionProfilesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminPermissionProfilesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var profiles = await _context.AdminProfiles
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();

            return View(profiles);
        }

        [HttpGet]
        public async Task<IActionResult> Matrix(int id)
        {
            var profile = await _context.AdminProfiles
                .Include(p => p.ControllerPermissions)
                    .ThenInclude(cp => cp.CustomPermissions)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profile == null)
                return NotFound();

            var controllersCatalog = GetControllersCatalog();

            var vm = new AdminProfileMatrixVM
            {
                ProfileId = profile.Id,
                ProfileName = profile.Name,
                Description = profile.Description,

                Controllers = controllersCatalog.Select(c =>
                {
                    var permission = profile.ControllerPermissions
                        .FirstOrDefault(p => p.ControllerName == c.Key);

                    var actionsCatalog = AdminControllerActionCatalog.GetActions(c.Key);

                    return new AdminControllerPermissionVM
                    {
                        ControllerName = c.Key,
                        DisplayName = c.Name,
                        AccessLevel = permission?.AccessLevel ?? AdminControllerAccessLevel.None,
                        HasCustom = permission?.HasCustomPermissions ?? false,

                        CustomActions = actionsCatalog.Select(a => new AdminActionVM
                        {
                            Key = a.Key,
                            DisplayName = a.DisplayNameAr,
                            IsAllowed = permission?.CustomPermissions
                                ?.Any(cp => cp.ActionName == a.Key && cp.IsAllowed) ?? false
                        }).ToList()
                    };
                }).ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveMatrix(AdminProfileMatrixVM model)
        {
            var profile = await _context.AdminProfiles
                .Include(p => p.ControllerPermissions)
                    .ThenInclude(cp => cp.CustomPermissions)
                .FirstOrDefaultAsync(p => p.Id == model.ProfileId);

            if (profile == null)
                return NotFound();

            var allowedControllers = GetControllersCatalog()
                .Select(x => x.Key)
                .ToHashSet();

            foreach (var row in model.Controllers)
            {
                if (!allowedControllers.Contains(row.ControllerName))
                    continue;

                var permission = profile.ControllerPermissions
                    .FirstOrDefault(x => x.ControllerName == row.ControllerName);

                if (permission == null)
                {
                    permission = new AdminProfileControllerPermission
                    {
                        ControllerName = row.ControllerName,
                        AdminProfileId = profile.Id,
                        CustomPermissions = new List<AdminProfileCustomPermission>()
                    };

                    profile.ControllerPermissions.Add(permission);
                }

                permission.AccessLevel = row.AccessLevel;
                permission.HasCustomPermissions = row.HasCustom;

                var catalogActions = AdminControllerActionCatalog.GetActions(row.ControllerName)
                    .Select(x => x.Key)
                    .ToHashSet();

                if (!row.HasCustom)
                {
                    if (permission.CustomPermissions != null && permission.CustomPermissions.Count > 0)
                    {
                        permission.CustomPermissions.Clear();
                    }

                    continue;
                }

                row.CustomActions ??= new List<AdminActionVM>();
                permission.CustomPermissions ??= new List<AdminProfileCustomPermission>();

                var selectedActions = row.CustomActions
                    .Where(x => x.IsAllowed && catalogActions.Contains(x.Key))
                    .Select(x => x.Key)
                    .ToHashSet();

                var permissionsToRemove = permission.CustomPermissions
                    .Where(x => !selectedActions.Contains(x.ActionName))
                    .ToList();

                foreach (var item in permissionsToRemove)
                {
                    permission.CustomPermissions.Remove(item);
                }

                foreach (var actionName in selectedActions)
                {
                    var existing = permission.CustomPermissions
                        .FirstOrDefault(x => x.ActionName == actionName);

                    if (existing == null)
                    {
                        permission.CustomPermissions.Add(new AdminProfileCustomPermission
                        {
                            ActionName = actionName,
                            IsAllowed = true
                        });
                    }
                    else
                    {
                        existing.IsAllowed = true;
                    }
                }
            }

            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ تم حفظ الصلاحيات بنجاح";
            return RedirectToAction(nameof(Matrix), new { id = model.ProfileId });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new AdminProfileCreateVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminProfileCreateVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var name = model.Name.Trim();

            var exists = await _context.AdminProfiles
                .AsNoTracking()
                .AnyAsync(p => p.Name == name);

            if (exists)
            {
                ModelState.AddModelError(nameof(model.Name), "اسم البروفايل مستخدم بالفعل.");
                return View(model);
            }

            var profile = new AdminProfile
            {
                Name = name,
                Description = model.Description,
                IsSystemProfile = false
            };

            await _context.AdminProfiles.AddAsync(profile);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Matrix), new { id = profile.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var profile = await _context.AdminProfiles.FindAsync(id);

            if (profile == null)
                return NotFound();

            if (profile.IsSystemProfile)
                return Forbid();

            var vm = new AdminProfileEditVM
            {
                Id = profile.Id,
                Name = profile.Name,
                Description = profile.Description
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminProfileEditVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var profile = await _context.AdminProfiles.FindAsync(model.Id);

            if (profile == null)
                return NotFound();

            if (profile.IsSystemProfile)
                return Forbid();

            profile.Name = model.Name.Trim();
            profile.Description = model.Description;

            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ تم تحديث البروفايل بنجاح";
            return RedirectToAction(nameof(Matrix), new { id = profile.Id });
        }

        private static List<(string Key, string Name)> GetControllersCatalog()
        {
            return new List<(string Key, string Name)>
    {
        ("Questions", "بنك الأسئلة"),
        ("VerbalPassages", "القطع اللفظية"),
        ("Lectures", "إدارة المحاضرات"),
        ("ExamAssignments", "الاختبارات"),
        ("ExamIndividualAssignments", "الاختبارات الفردية"),
        ("AdminLessonCompletions", "إدارة توليد الواجبات"),
        ("HomeworkManagement", "إدارة الواجبات"),
        ("PlacementExams", "قياس المستوى"),
        ("PerformanceIndicatorExams", "اختبارات مؤشر الأداء"),
        ("PerformanceDashboard", "لوحة مؤشرات الأداء"),
        ("PerformanceExamReports", "تقارير مؤشر الأداء"),
        ("ProfessionalModels", "النماذج الاحترافية"),
        ("Attendance", "الحضور والانصراف"),
        ("Batches", "إدارة الدفعات"),
        ("EnhancementSkills", "المهارات التعزيزية"),
        ("Settings", "الإعدادات العامة"),
        ("IntegrityViolations", "مخالفات النزاهة (ترجمة المتصفح)")
    };
        }


    }
}