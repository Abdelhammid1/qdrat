using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class RegisterUserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public RegisterUserController(UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();

            var model = new List<RegisterUserViewModel>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                model.Add(new RegisterUserViewModel
                {
                    UserName = u.UserName,
                    Email = u.Email,
                    NationalID = u.NationalID,
                    Role = roles.FirstOrDefault() ?? "غير محدد"
                });
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            var model = new RegisterUserViewModel
            {
                Branches = _context.Branches.Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToList(),

                Batches = _context.Batches.Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RegisterUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // في حالة فشل التحقق، نعيد تعبئة القوائم عشان تظهر بشكل سليم في الصفحة
                model.Branches = _context.Branches.Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToList();

                model.Batches = _context.Batches.Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.Name
                }).ToList();

                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.UserName,
                FullName = model.FullName,
                Email = model.Email,
                NationalID = model.NationalID
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);

                // الربط بكيانات النظام حسب الدور
                switch (model.Role)
                {
                    case "Student":
                        {
                            // 1) إنشاء الطالب بدون BatchId
                            var student = new Student
                            {
                                FullName = model.UserName,
                                NationalID = model.NationalID,
                                UserId = user.Id,
                                Gender = model.Gender ?? "ذكر",
                                Age = model.Age ?? 18,
                                School = model.School ?? "غير محددة",
                                PhoneNumber = model.PhoneNumber ?? "0000000000",
                                WhatsAppNumber = model.WhatsAppNumber,
                                Level = model.Level ?? "غير محددة",
                                BranchId = model.BranchId ?? 1,
                                EnrollmentStatus = "نشط",
                                IsRegular = true,
                                RegistrationDate = DateTime.UtcNow
                            };

                            _context.Students.Add(student);
                            await _context.SaveChangesAsync(); // لازم نحفظ علشان student.StudentID يتولد

                            // 2) ربط الطالب بالدفعة/الدفعات عبر جدول الربط
                            // يدعم حالتين:
                            // - model.SelectedBatchIds[] (متعدد)
                            // - أو model.BatchId (مفرد) لو لسه بتستخدمه في النموذج الحالي
                            var batchIds = model.BatchId.HasValue
                                          ? new[] { model.BatchId.Value }
                                          : Array.Empty<int>();


                            foreach (var bid in batchIds)
                            {
                                // تحقق بسيط إن الدفعة موجودة
                                if (await _context.Batches.AnyAsync(b => b.Id == bid))
                                {
                                    _context.StudentBatchEnrollments.Add(new StudentBatchEnrollment
                                    {
                                        StudentID = student.StudentID,
                                        BatchId = bid,
                                        EnrolledAt = DateTime.UtcNow,
                                        Status = "Active"
                                    });
                                }
                            }

                            if (batchIds.Length > 0)
                                await _context.SaveChangesAsync();

                            return RedirectToAction("Index", "Students", new { area = "Admin" });
                        }

                }

                TempData["Message"] = "تم إنشاء الحساب بنجاح.";
                return RedirectToAction("Create");
            }

            // في حالة فشل إنشاء المستخدم
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            // نعيد تعبئة القوائم إذا فشل الإنشاء
            model.Branches = _context.Branches.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            }).ToList();

            model.Batches = _context.Batches.Select(b => new SelectListItem
            {
                Value = b.Id.ToString(),
                Text = b.Name
            }).ToList();

            return View(model);
        }


    }
}
