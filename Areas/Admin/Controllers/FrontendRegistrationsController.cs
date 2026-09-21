using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Entities.Frontend;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]

    public class FrontendRegistrationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public FrontendRegistrationsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }


        // 🟢 عرض جميع التسجيلات
        public async Task<IActionResult> Index()
        {
            var registrations = await _context.FrontendCourseRegistrations
                .Include(r => r.SubCourse)
                .ThenInclude(s => s.FrontendCourse)
                .OrderByDescending(r => r.CreatedAt) // ⏱️ الأحدث أولاً
                .ToListAsync();

            return View(registrations);
        }


        // 🟡 تعديل بيانات المتقدم
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var reg = await _context.FrontendCourseRegistrations
                .Include(r => r.SubCourse)
                .ThenInclude(s => s.FrontendCourse)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null)
                return NotFound("❌ لم يتم العثور على طلب التسجيل.");

            return View(reg);
        }

        // 🟢 حفظ التعديلات
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FrontendCourseRegistration model)
        {
            if (id != model.Id)
                return BadRequest();

            var reg = await _context.FrontendCourseRegistrations.FindAsync(id);
            if (reg == null)
                return NotFound();

            // ✅ تعديل الحقول القابلة للتحديث فقط
            reg.FullName = model.FullName;
            reg.NationalId = model.NationalId;
            reg.Email = model.Email;
            reg.Phone = model.Phone;
            reg.IsContacted = false; // إعادة للحالة غير المعتمدة بعد التعديل

            // 🧩 تتبع التعديل
            reg.UpdatedAt = DateTime.Now;
            reg.UpdatedBy = User?.Identity?.Name ?? "Admin System";

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تعديل بيانات المتقدم بنجاح وتم تسجيل من قام بالتعديل.";
            return RedirectToAction(nameof(Index));
        }








        [HttpPost]
        public async Task<IActionResult> Approve(
         int id,
         [FromServices] UserManager<ApplicationUser> userManager,
         [FromServices] RoleManager<IdentityRole> roleManager)
        {
            var reg = await _context.FrontendCourseRegistrations
                .Include(r => r.SubCourse)
                .ThenInclude(s => s.FrontendCourse)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null)
                return Json(new { success = false, message = "❌ لم يتم العثور على التسجيل." });

            // ✅ تحقق من التكرار
            var existingUser = await userManager.FindByNameAsync(reg.NationalId);
            if (existingUser != null)
                return Json(new { success = false, message = "⚠️ المستخدم مسجل مسبقًا." });

            // 🧩 إنشاء المستخدم
            var email = $"{reg.NationalId}@qdrat.edu.sa";
            var password = reg.NationalId;

            var newUser = new ApplicationUser
            {
                UserName = reg.NationalId,
                Email = email,
                FullName = reg.FullName,
                PhoneNumber = reg.Phone,
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(newUser, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return Json(new { success = false, message = $"❌ فشل إنشاء المستخدم: {errors}" });
            }

            // 🟩 إضافة الدور
            if (!await roleManager.RoleExistsAsync("Student"))
                await roleManager.CreateAsync(new IdentityRole("Student"));

            await userManager.AddToRoleAsync(newUser, "Student");

            // 🧠 إنشاء الطالب
            var student = new QdratNew.Entities.Student
            {
                FullName = reg.FullName,
                NationalID = reg.NationalId,
                Gender = "غير محدد",
                Email = email,
                PhoneNumber = reg.Phone,
                Level = "غير محدد",
                Age = 18,
                School = "غير محددة",
                BranchId = 1,
                UserId = newUser.Id,
                RegistrationDate = DateTime.Now,
                EnrollmentStatus = "نشط",
                IsRegular = true
            };

            _context.Students.Add(student);

            // ✅ حذف التسجيل أو اعتباره معتمدًا
            _context.FrontendCourseRegistrations.Remove(reg);
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                name = reg.FullName,
                course = reg.SubCourse?.FrontendCourse?.Title ?? "غير محددة"
            });
        }


        [HttpPost]
        public async Task<IActionResult> ToggleContactStatus(int id)
        {
            var reg = await _context.FrontendCourseRegistrations.FindAsync(id);
            if (reg == null)
                return NotFound();

            reg.IsContacted = !reg.IsContacted; // 🔁 يقلب الحالة
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }




        // 🔵 عرض التفاصيل
        public async Task<IActionResult> Details(int id)
        {
            var reg = await _context.FrontendCourseRegistrations
                .Include(r => r.SubCourse)
                .ThenInclude(s => s.FrontendCourse)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null)
                return NotFound();

            return View(reg);
        }

        // 🔴 حذف
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var reg = await _context.FrontendCourseRegistrations.FindAsync(id);
            if (reg != null)
            {
                _context.FrontendCourseRegistrations.Remove(reg);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
