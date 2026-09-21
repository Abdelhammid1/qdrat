using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Security;
using QdratNew.ViewModels.Admin;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class AdminUserProfilesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminUserProfilesController(ApplicationDbContext context)
        {
            _context = context;
        }



        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var data = await _context.AdminUserProfiles
                .Include(x => x.AdminProfile)
                .Include(x => x.User)
                .AsNoTracking()
                .Where(x => x.User.IsActive)
                .Select(x => new AdminUserProfileIndexVM
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    UserName = x.User.FullName ?? x.User.UserName,
                    ProfileName = x.AdminProfile.Name,
                    AssignedAt = x.AssignedAt
                })
                .ToListAsync();

            return View(data);
        }


   
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveUserFromProfile(string userId)
        {

            var link = await _context.AdminUserProfiles
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (link == null)
            {
                TempData["Error"] = "⚠️ المستخدم غير مرتبط بأي ملف صلاحيات.";
                return RedirectToAction(nameof(Index));
            }

            _context.AdminUserProfiles.Remove(link);
            await _context.SaveChangesAsync();

            TempData["Message"] = "✅ تم استبعاد المستخدم من ملف الصلاحيات بنجاح.";
            return RedirectToAction(nameof(Index));
        }





        // ===============================
        // ربط مستخدم ببروفايل
        // ===============================
        [HttpGet]
        public async Task<IActionResult> Assign(int? userId)
        {
            // =====================================================
            // 1️⃣ جلب المستخدمين مع أدوارهم (SQL بسيط)
            // =====================================================
            var usersWithRoles = await (
                from u in _context.Users
                join ur in _context.UserRoles on u.Id equals ur.UserId
                join r in _context.Roles on ur.RoleId equals r.Id
                where u.IsActive
                select new
                {
                    u.Id,
                    u.FullName,
                    u.Email,
                    RoleName = r.Name
                }
            ).ToListAsync();

            // =====================================================
            // 2️⃣ فلترة المستخدمين الإداريين (بدون Contains نهائيًا)
            // =====================================================
            var adminUsers = usersWithRoles
                .Where(x =>
                    x.RoleName == "SuperAdmin" ||
                    x.RoleName == "Owner" ||
                    x.RoleName == "Admin" ||
                    x.RoleName == "Manager" ||
                    x.RoleName == "Employee" ||
                    x.RoleName == "DataEntry"
                )
                .GroupBy(x => x.Id) // منع التكرار لو المستخدم له أكثر من Role
                .Select(g => g.First())
                .OrderBy(x => x.FullName)
                .ToList();

            // =====================================================
            // 3️⃣ تجهيز ViewModel (وضع الإنشاء)
            // =====================================================
            var model = new AdminUserProfileAssignVM
            {
                Users = adminUsers.Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = string.IsNullOrWhiteSpace(u.Email)
                        ? u.FullName
                        : $"{u.FullName} ({u.Email})"
                }).ToList(),

                Profiles = await _context.AdminProfiles
                    .OrderBy(p => p.Name)
                    .Select(p => new SelectListItem
                    {
                        Value = p.Id.ToString(),
                        Text = p.Name
                    })
                    .ToListAsync()
            };

            // =====================================================
            // 4️⃣ لو في userId → وضع التعديل
            // =====================================================
            if (userId.HasValue)
            {
                var link = await _context.AdminUserProfiles
                    .Include(x => x.User)
                    .Include(x => x.AdminProfile)
                    .Where(x => x.Id == userId.Value)
                    .FirstOrDefaultAsync();

                if (link == null)
                    return NotFound("الربط المطلوب غير موجود.");

                // جلب اسم المستخدم بشكل منفصل إذا لم يتحمل عبر Include
                string displayName  = link.User?.FullName ?? link.User?.UserName ?? "";
                string displayEmail = link.User?.Email ?? "";

                if (string.IsNullOrEmpty(displayName))
                {
                    var appUser = await _context.Users
                        .AsNoTracking()
                        .Where(u => u.Id == link.UserId)
                        .Select(u => new { u.FullName, u.UserName, u.Email })
                        .FirstOrDefaultAsync();

                    displayName  = appUser?.FullName ?? appUser?.UserName ?? "";
                    displayEmail = appUser?.Email ?? "";
                }

                model.UserId             = link.UserId;
                model.AdminProfileId     = link.AdminProfileId;
                model.IsEditMode         = true;
                model.EditUserName       = displayName;
                model.EditUserEmail      = displayEmail;
                model.EditCurrentProfile = link.AdminProfile?.Name ?? "";
            }

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(AdminUserProfileAssignVM model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // ===============================
            // 1️⃣ التحقق من وجود المستخدم وأنه فعّال
            // ===============================
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == model.UserId);

            if (user == null)
                return NotFound("المستخدم غير موجود.");

            if (!user.IsActive)
            {
                ModelState.AddModelError("", "لا يمكن ربط مستخدم موقوف ببروفايل صلاحيات.");
                return View(model);
            }

            // ===============================
            // 2️⃣ التحقق من وجود البروفايل
            // ===============================
            var profileExists = await _context.AdminProfiles
                .AnyAsync(p => p.Id == model.AdminProfileId);

            if (!profileExists)
            {
                ModelState.AddModelError("", "البروفايل المحدد غير صحيح.");
                return View(model);
            }

            // ===============================
            // 3️⃣ إزالة أي ربط سابق
            // ===============================
            var existing = await _context.AdminUserProfiles
                .FirstOrDefaultAsync(x => x.UserId == model.UserId);

            if (existing != null)
                _context.AdminUserProfiles.Remove(existing);

            // ===============================
            // 4️⃣ إضافة الربط الجديد
            // ===============================
            _context.AdminUserProfiles.Add(new AdminUserProfile
            {
                UserId = model.UserId,
                AdminProfileId = model.AdminProfileId,
                AssignedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            // ===============================
            // 5️⃣ 🔴 إجبار تحديث الصلاحيات
            // ===============================
            // هذا السطر هو حل المشكلة الأساسية
            await _context.Database.ExecuteSqlRawAsync(
                "UPDATE AspNetUsers SET SecurityStamp = NEWID() WHERE Id = {0}",
                model.UserId
            );

            TempData["Success"] =
                "✅ تم ربط المستخدم بالبروفايل بنجاح. سيتم تفعيل الصلاحيات بعد إعادة تسجيل الدخول.";

            return RedirectToAction(nameof(Index));
        }


        private async Task ReloadAssignLists(AdminUserProfileAssignVM model)
        {
            var adminRoleNames = new[]
            {
        "Admin", "SuperAdmin", "Owner", "Developer", "Employee", "DataEntry"
    };

            var users = await _context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .Select(u => new
                {
                    u.Id,
                    u.FullName,
                    u.UserName
                })
                .ToListAsync();

            var userRoles = await _context.UserRoles
                .AsNoTracking()
                .ToListAsync();

            var roles = await _context.Roles
                .AsNoTracking()
                .ToListAsync();

            model.Users = (
                from u in users
                join ur in userRoles on u.Id equals ur.UserId
                join r in roles on ur.RoleId equals r.Id
                where adminRoleNames.Contains(r.Name)
                select new SelectListItem
                {
                    Value = u.Id,
                    Text = u.FullName ?? u.UserName
                }
            ).Distinct().ToList();

            model.Profiles = await _context.AdminProfiles
                .AsNoTracking()
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.Name
                })
                .ToListAsync();
        }




    }
}
