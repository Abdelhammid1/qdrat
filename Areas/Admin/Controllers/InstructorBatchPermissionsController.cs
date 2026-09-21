using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.InstructorPermissions;
using System.Security.Claims;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Owner,Developer")]
    public class InstructorBatchPermissionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InstructorBatchPermissionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/InstructorBatchPermissions
        // عرض قائمة المدربين مع ملخص صلاحياتهم
        public async Task<IActionResult> Index()
        {
            var instructors = await _context.Instructors
                .AsNoTracking()
                .Where(i => !i.IsDeleted && !i.IsPartnerInstructor)
                .Include(i => i.User)
                .OrderBy(i => i.FullName)
                .ToListAsync();

            var permissions = await _context.InstructorBatchPermissions
                .AsNoTracking()
                .Where(p => p.IsGranted)
                .ToListAsync();

            var viewModel = instructors.Select(i => new InstructorPermissionSummaryVM
            {
                InstructorId = i.Id,
                FullName = i.FullName,
                Email = i.Email,
                IsActive = i.IsActive,
                GrantedBatchesCount = permissions
                    .Where(p => p.InstructorId == i.Id)
                    .Select(p => p.BatchId)
                    .Distinct()
                    .Count(),
                TotalPermissionsCount = permissions
                    .Count(p => p.InstructorId == i.Id)
            }).ToList();

            return View(viewModel);
        }

        // GET: /Admin/InstructorBatchPermissions/Manage/5
        // إدارة صلاحيات مدرب محدد عبر الدفعات
        public async Task<IActionResult> Manage(int id)
        {
            var instructor = await _context.Instructors
                .AsNoTracking()
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.Id == id && !i.IsDeleted);

            if (instructor == null)
                return NotFound();

            var batches = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .Where(b => !b.IsDeleted)
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            var existingPermissions = await _context.InstructorBatchPermissions
                .AsNoTracking()
                .Where(p => p.InstructorId == id)
                .ToListAsync();

            var features = new[]
            {
                InstructorBatchFeature.ViewBatch,
                InstructorBatchFeature.Attendance,
                InstructorBatchFeature.Homework,
                InstructorBatchFeature.Exams
            };

            var batchRows = batches.Select(b => new InstructorBatchPermissionRowVM
            {
                BatchId = b.Id,
                BatchName = b.Name,
                CourseName = b.Course?.Name ?? "غير محدد",
                IsArchived = b.IsArchived,
                FeaturePermissions = features.Select(f =>
                {
                    var perm = existingPermissions.FirstOrDefault(p => p.BatchId == b.Id && p.Feature == f);
                    return new FeaturePermissionVM
                    {
                        Feature = f,
                        FeatureNameAr = GetFeatureNameAr(f),
                        IsGranted = perm?.IsGranted ?? false,
                        GrantedAt = perm?.GrantedAt
                    };
                }).ToList()
            }).ToList();

            var vm = new ManageInstructorPermissionsVM
            {
                InstructorId = instructor.Id,
                FullName = instructor.FullName,
                Email = instructor.Email,
                IsActive = instructor.IsActive,
                BatchRows = batchRows
            };

            return View(vm);
        }

        // POST: /Admin/InstructorBatchPermissions/UpdatePermission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePermission(int instructorId, int batchId, InstructorBatchFeature feature, bool isGranted)
        {
            var instructor = await _context.Instructors.FindAsync(instructorId);
            if (instructor == null || instructor.IsDeleted)
                return Json(new { success = false, message = "المدرب غير موجود." });

            var batch = await _context.Batches.FindAsync(batchId);
            if (batch == null || batch.IsDeleted)
                return Json(new { success = false, message = "الدفعة غير موجودة." });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var existing = await _context.InstructorBatchPermissions
                .FirstOrDefaultAsync(p => p.InstructorId == instructorId && p.BatchId == batchId && p.Feature == feature);

            if (existing != null)
            {
                existing.IsGranted = isGranted;
                existing.GrantedAt = DateTime.Now;
                existing.GrantedByUserId = userId;
            }
            else
            {
                _context.InstructorBatchPermissions.Add(new InstructorBatchPermission
                {
                    InstructorId = instructorId,
                    BatchId = batchId,
                    Feature = feature,
                    IsGranted = isGranted,
                    GrantedByUserId = userId,
                    GrantedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            var actionText = isGranted ? "مُنحت" : "سُحبت";
            return Json(new { success = true, message = $"الصلاحية {actionText} بنجاح." });
        }

        // POST: /Admin/InstructorBatchPermissions/GrantAll
        // منح جميع صلاحيات دفعة بعينها لمدرب
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantAll(int instructorId, int batchId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var features = Enum.GetValues<InstructorBatchFeature>();

            foreach (var feature in features)
            {
                var existing = await _context.InstructorBatchPermissions
                    .FirstOrDefaultAsync(p => p.InstructorId == instructorId && p.BatchId == batchId && p.Feature == feature);

                if (existing != null)
                {
                    existing.IsGranted = true;
                    existing.GrantedAt = DateTime.Now;
                    existing.GrantedByUserId = userId;
                }
                else
                {
                    _context.InstructorBatchPermissions.Add(new InstructorBatchPermission
                    {
                        InstructorId = instructorId,
                        BatchId = batchId,
                        Feature = feature,
                        IsGranted = true,
                        GrantedByUserId = userId,
                        GrantedAt = DateTime.Now
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم منح جميع الصلاحيات للدفعة المحددة.";
            return RedirectToAction(nameof(Manage), new { id = instructorId });
        }

        // POST: /Admin/InstructorBatchPermissions/RevokeAll
        // سحب جميع صلاحيات دفعة بعينها من مدرب
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeAll(int instructorId, int batchId)
        {
            var perms = await _context.InstructorBatchPermissions
                .Where(p => p.InstructorId == instructorId && p.BatchId == batchId)
                .ToListAsync();

            foreach (var p in perms)
                p.IsGranted = false;

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم سحب جميع الصلاحيات من الدفعة المحددة.";
            return RedirectToAction(nameof(Manage), new { id = instructorId });
        }

        // GET: /Admin/InstructorBatchPermissions/ManageBatch/5
        // إدارة صلاحيات دفعة بعينها: عرض المدربين والموظفين ومنح/سحب وصولهم
        public async Task<IActionResult> ManageBatch(int id)
        {
            var batch = await _context.Batches
                .AsNoTracking()
                .Include(b => b.Course)
                .FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted);

            if (batch == null) return NotFound();

            var features = Enum.GetValues<InstructorBatchFeature>().ToArray();

            // ---- المدربون ----
            var instructors = await _context.Instructors
                .AsNoTracking()
                .Where(i => !i.IsDeleted && !i.IsPartnerInstructor)
                .OrderBy(i => i.FullName)
                .ToListAsync();

            var instrPerms = await _context.InstructorBatchPermissions
                .AsNoTracking()
                .Where(p => p.BatchId == id)
                .ToListAsync();

            var instructorRows = instructors.Select(i => new BatchInstructorRowVM
            {
                InstructorId = i.Id,
                FullName = i.FullName,
                Email = i.Email,
                IsActive = i.IsActive,
                FeaturePermissions = features.Select(f =>
                {
                    var p = instrPerms.FirstOrDefault(x => x.InstructorId == i.Id && x.Feature == f);
                    return new FeaturePermissionVM
                    {
                        Feature = f,
                        FeatureNameAr = GetFeatureNameAr(f),
                        IsGranted = p?.IsGranted ?? false,
                        GrantedAt = p?.GrantedAt
                    };
                }).ToList()
            }).ToList();

            // ---- المستخدمون (Employee / Admin / SuperAdmin) ----
            var targetRoleNames = new[] { "Employee", "Admin", "SuperAdmin" };

            var targetRoleIds = await _context.Roles
                .AsNoTracking()
                .Where(r => targetRoleNames.Contains(r.Name))
                .Select(r => r.Id)
                .ToListAsync();

            var targetUserIds = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => targetRoleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

            var empPerms = await _context.EmployeeBatchAccesses
                .AsNoTracking()
                .Where(p => p.BatchId == id)
                .ToListAsync();

            // جلب اسم الرول لكل مستخدم (لإظهاره في الواجهة)
            var userRoleMap = await _context.UserRoles
                .AsNoTracking()
                .Where(ur => targetUserIds.Contains(ur.UserId) && targetRoleIds.Contains(ur.RoleId))
                .Join(_context.Roles.AsNoTracking(), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
                .ToListAsync();

            var employees = await _context.Users
                .AsNoTracking()
                .Where(u => targetUserIds.Contains(u.Id))
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var employeeRows = employees.Select(u =>
            {
                var roleName = userRoleMap.FirstOrDefault(x => x.UserId == u.Id)?.Name ?? "";
                return new BatchEmployeeRowVM
                {
                    UserId = u.Id,
                    FullName = u.UserName ?? u.Email ?? "مستخدم",
                    Email = u.Email ?? "",
                    RoleName = roleName,
                    FeaturePermissions = features.Select(f =>
                    {
                        var p = empPerms.FirstOrDefault(x => x.UserId == u.Id && x.Feature == f);
                        return new FeaturePermissionVM
                        {
                            Feature = f,
                            FeatureNameAr = GetFeatureNameAr(f),
                            IsGranted = p?.IsGranted ?? false,
                            GrantedAt = p?.GrantedAt
                        };
                    }).ToList()
                };
            }).ToList();

            var vm = new ManageBatchPermissionsVM
            {
                BatchId = batch.Id,
                BatchName = batch.Name,
                CourseName = batch.Course?.Name ?? "غير محدد",
                IsArchived = batch.IsArchived,
                Instructors = instructorRows,
                Employees = employeeRows
            };

            return View(vm);
        }

        // POST: /Admin/InstructorBatchPermissions/UpdateEmployeePermission
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateEmployeePermission(string userId, int batchId, InstructorBatchFeature feature, bool isGranted)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Json(new { success = false, message = "المستخدم غير موجود." });

            var batch = await _context.Batches.FindAsync(batchId);
            if (batch == null || batch.IsDeleted) return Json(new { success = false, message = "الدفعة غير موجودة." });

            var grantedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var existing = await _context.EmployeeBatchAccesses
                .FirstOrDefaultAsync(p => p.UserId == userId && p.BatchId == batchId && p.Feature == feature);

            if (existing != null)
            {
                existing.IsGranted = isGranted;
                existing.GrantedAt = DateTime.Now;
                existing.GrantedByUserId = grantedBy;
            }
            else
            {
                _context.EmployeeBatchAccesses.Add(new EmployeeBatchAccess
                {
                    UserId = userId,
                    BatchId = batchId,
                    Feature = feature,
                    IsGranted = isGranted,
                    GrantedByUserId = grantedBy,
                    GrantedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            var actionText = isGranted ? "مُنحت" : "سُحبت";
            return Json(new { success = true, message = $"الصلاحية {actionText} بنجاح." });
        }

        // POST: /Admin/InstructorBatchPermissions/GrantAllEmployee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantAllEmployee(string userId, int batchId)
        {
            var grantedBy = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            foreach (var feature in Enum.GetValues<InstructorBatchFeature>())
            {
                var existing = await _context.EmployeeBatchAccesses
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.BatchId == batchId && p.Feature == feature);

                if (existing != null) { existing.IsGranted = true; existing.GrantedAt = DateTime.Now; existing.GrantedByUserId = grantedBy; }
                else _context.EmployeeBatchAccesses.Add(new EmployeeBatchAccess { UserId = userId, BatchId = batchId, Feature = feature, IsGranted = true, GrantedByUserId = grantedBy, GrantedAt = DateTime.Now });
            }
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم منح جميع الصلاحيات للموظف.";
            return RedirectToAction(nameof(ManageBatch), new { id = batchId });
        }

        // POST: /Admin/InstructorBatchPermissions/RevokeAllEmployee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RevokeAllEmployee(string userId, int batchId)
        {
            var perms = await _context.EmployeeBatchAccesses
                .Where(p => p.UserId == userId && p.BatchId == batchId)
                .ToListAsync();
            foreach (var p in perms) p.IsGranted = false;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم سحب جميع الصلاحيات من الموظف.";
            return RedirectToAction(nameof(ManageBatch), new { id = batchId });
        }

        private static string GetFeatureNameAr(InstructorBatchFeature feature) => feature switch
        {
            InstructorBatchFeature.ViewBatch => "عرض الدفعة",
            InstructorBatchFeature.Attendance => "الحضور والانصراف",
            InstructorBatchFeature.Homework => "الواجبات",
            InstructorBatchFeature.Exams => "الاختبارات",
            _ => feature.ToString()
        };
    }
}
