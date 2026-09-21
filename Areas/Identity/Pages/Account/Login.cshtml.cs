using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace QdratNew.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    [Route("/LMS/login")]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginModel> _logger;
        private readonly ImpersonationService _impersonation;

        public LoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<LoginModel> logger,
            ImpersonationService impersonation)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _impersonation = impersonation;
        }

        [BindProperty]
        public InputModel Input { get; set; }
        public string ReturnUrl { get; set; } // ✅ الحل هنا

        public class InputModel
        {
            [Required(ErrorMessage = "يرجى إدخال البريد الإلكتروني أو اسم المستخدم أو رقم الهوية")]
            public string Email { get; set; }

            [Required(ErrorMessage = "يرجى إدخال كلمة المرور")]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            public bool RememberMe { get; set; }
        }
        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            // 🧹 إزالة أي كوكي تخفّي (Impersonation) متبقٍ من جلسة سابقة
            // حتى لا يُفرض على المستخدم الجديد هوية المستخدم المتخفى عليه سابقاً
            _impersonation.Clear();

            if (!ModelState.IsValid)
                return Page();

            ApplicationUser user = null;

            // ===============================
            // 1️⃣ البحث عن المستخدم
            // ===============================
            user = await _userManager.FindByNameAsync(Input.Email);

            if (user == null)
                user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.NationalID == Input.Email);
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة");
                return Page();
            }
            // ✅ منع الدخول لو المستخدم موقوف
            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "❌ هذا الحساب موقوف.");
                return Page();
            }

            // ✅ منع دخول الطالب لو تم سلب صلاحية الدخول لاريا الطالب من المالك/المبرمج
            var studentAccess = await _context.Students
                .AsNoTracking()
                .Where(s => s.UserId == user.Id)
                .Select(s => (bool?)s.CanAccessStudentArea)
                .FirstOrDefaultAsync();

            if (studentAccess.HasValue && !studentAccess.Value)
            {
                ModelState.AddModelError(string.Empty, "❌ تم إيقاف صلاحية الدخول لحسابك من إدارة المنصة.");
                return Page();
            }
            // =====================================
            // 🔥 تنظيف أي Session قديمة (حل مشكلة Lab)
            // =====================================
            await _signInManager.SignOutAsync();
            HttpContext.Session.Clear();

            // ===============================
            // 2️⃣ تسجيل الدخول
            // ===============================
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: false
            );

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "بيانات الدخول غير صحيحة");
                return Page();
            }
            // =====================================
            // 🔐 ربط Session بالمستخدم الحالي
            // =====================================
            HttpContext.Session.SetString("UserSessionKey", user.Id);

            // تسجيل سجل الدخول
            var loginLog = new QdratNew.Entities.UserLoginLog
            {
                UserId    = user.Id,
                LoginAt   = DateTime.Now,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                DeviceInfo = HttpContext.Request.Headers["User-Agent"].ToString().Length > 300
                             ? HttpContext.Request.Headers["User-Agent"].ToString()[..300]
                             : HttpContext.Request.Headers["User-Agent"].ToString(),
            };
            _context.UserLoginLogs.Add(loginLog);
            await _context.SaveChangesAsync();

            // ===============================
            // 3️⃣ جلب الأدوار
            // ===============================
            var roles = await _userManager.GetRolesAsync(user);

            if (roles == null || roles.Count == 0)
            {
                await _signInManager.SignOutAsync();
                ModelState.AddModelError(string.Empty, "لا يوجد دور مرتبط بالحساب");
                return Page();
            }

            // ===============================
            // 4️⃣ تحديد الدور النشط (أولوية صحيحة)
            // ===============================
            string activeRole =
                roles.Contains("SuperAdmin") ? "SuperAdmin" :
                roles.Contains("Owner") ? "Owner" :
                roles.Contains("Developer") ? "Developer" :
                roles.Contains("Admin") ? "Admin" :
                roles.Contains("Employee") ? "Employee" :
                roles.Contains("DataEntry") ? "DataEntry" :
                roles.Contains("PartnerAdmin") ? "PartnerAdmin" :
                roles.Contains("Partner") ? "Partner" :
                roles.Contains("PartnerInstructor") ? "PartnerInstructor" :
                roles.Contains("Instructor") ? "Instructor" :
                roles.Contains("Student") ? "Student" :
                roles.Contains("Parent") ? "Parent" :
                roles.First();

            HttpContext.Session.SetString("ActiveRole", activeRole);
            HttpContext.Session.SetString("UserSessionKey", user.Id);
            HttpContext.Session.SetString(
                "AvailableRoles",
                JsonSerializer.Serialize(roles.ToList())
            );

            // ===============================
            // 5️⃣ Admin Profile Claim Injection
            // ===============================
            bool isAdminUser =
                activeRole == "SuperAdmin" ||
                activeRole == "Owner" ||
                activeRole == "Developer" ||
                activeRole == "Admin" ||
                activeRole == "Employee" ||
                activeRole == "DataEntry";

            bool isEmployee = activeRole == "Employee";

            if (isAdminUser)
            {
                var adminProfileId = await _context.AdminUserProfiles
                    .Where(x => x.UserId == user.Id)
                    .Select(x => (int?)x.AdminProfileId)
                    .FirstOrDefaultAsync();

                if (adminProfileId.HasValue)
                {
                    var claims = new List<System.Security.Claims.Claim>
            {
                new System.Security.Claims.Claim(
                    "AdminProfileId",
                    adminProfileId.Value.ToString()
                )
            };

                    await _signInManager.SignOutAsync();

                    await _signInManager.SignInWithClaimsAsync(
                        user,
                        Input.RememberMe,
                        claims
                    );
                }
            }

            // ===============================
            // 6️⃣ Redirect
            // ===============================

            if (isEmployee)
                return RedirectToAction("Index", "EmployeeDashboard", new { area = "Admin" });

            if (isAdminUser)
                return RedirectToAction("Index", "AdminOperationsDashboard", new { area = "Admin" });

            return activeRole switch
            {
                "Instructor" or "PartnerInstructor"
                    => RedirectToAction("Index", "InstructorDashboard", new { area = "Instructors" }),

                "Student"
                    => RedirectToAction("Index", "Dashboard", new { area = "Students" }),

                "Partner" or "PartnerAdmin"
                    => RedirectToAction("Index", "Home", new { area = "Partner" }),

                "Parent"
                    => RedirectToAction("Index", "Dashboard", new { area = "Parents" }),

                _ => RedirectToAction("Index", "Home")
            };
        }

        private async Task<IActionResult> RedirectByRoleAsync(string role, string userId)
        {
            switch (role)
            {
                case "Student":
                    var studentId = await _context.Students
                        .AsNoTracking()
                        .Where(s => s.UserId == userId)
                        .Select(s => s.StudentID)
                        .FirstOrDefaultAsync();

                    HttpContext.Session.SetInt32("StudentId", studentId);
                    return RedirectToAction("Index", "Dashboard", new { area = "Students" });

                case "Instructor":
                    var instructorId = await _context.Instructors
                        .AsNoTracking()
                        .Where(i => i.UserId == userId)
                        .Select(i => i.Id)
                        .FirstOrDefaultAsync();

                    HttpContext.Session.SetInt32("InstructorId", instructorId);
                    return RedirectToAction("Index", "InstructorDashboard", new { area = "Instructors" });

                case "Admin":
                    return RedirectToAction("Index", "AdminOperationsDashboard", new { area = "Admin" });

                case "Partner":
                    return RedirectToAction("Index", "Home", new { area = "Partner" });

                default:
                    return RedirectToAction("Index", "Home");
            }
        }
    }
}
