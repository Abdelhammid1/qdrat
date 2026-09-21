using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities.Frontend;
using System.ComponentModel.DataAnnotations;

namespace QdratNew.Areas.Public.Controllers
{
    [Area("Public")]
    public class EnrollmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EnrollmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ عرض نموذج التسجيل في دورة فرعية
        // ✅ عرض نموذج التسجيل في دورة فرعية
        [HttpGet("Public/Enrollments/Register/{subCourseId}")]
        public async Task<IActionResult> Register(int subCourseId)
        {
            var subCourse = await _context.SubCourses
                .Include(s => s.FrontendCourse)
                .FirstOrDefaultAsync(s => s.Id == subCourseId && s.IsActive);

            if (subCourse == null)
                return NotFound("❌ لم يتم العثور على الدورة المطلوبة.");

            var vm = new EnrollmentFormViewModel
            {
                SubCourseId = subCourse.Id,
                SubCourseTitle = subCourse.Title,
                FrontCourseTitle = subCourse.FrontendCourse?.Title ?? ""
            };

            return View(vm);
        }

        // ✅ استقبال البيانات وتخزينها

        [HttpPost("Public/Enrollments/Register/{subCourseId}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(int subCourseId, EnrollmentFormViewModel model)
        {
            try
            {
                // 🧩 التحقق من صلاحية الإدخال
                if (!ModelState.IsValid)
                {
                    // نجمع الأخطاء التفصيلية من ModelState
                    var errorList = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToList();

                    // دمجها في رسالة واحدة لعرضها بشكل واضح
                    string allErrors = string.Join(" - ", errorList);
                    TempData["Error"] = $"⚠️ يوجد أخطاء في البيانات المدخلة: {allErrors}";
                    return View(model);
                }

                // 🧩 التحقق من وجود الدورة
                var subCourse = await _context.SubCourses
                    .Include(s => s.FrontendCourse)
                    .FirstOrDefaultAsync(s => s.Id == subCourseId);

                if (subCourse == null)
                {
                    TempData["Error"] = "❌ لم يتم العثور على الدورة المحددة أو أنها غير مفعلة.";
                    return View(model);
                }

                // 🧩 التحقق من عدم وجود تسجيل سابق بنفس رقم الهوية
                bool alreadyExists = await _context.FrontendCourseRegistrations
                    .AnyAsync(r => r.NationalId == model.NationalId && r.SubCourseId == subCourseId);

                if (alreadyExists)
                {
                    TempData["Error"] = "⚠️ هذا الرقم الوطني مسجل مسبقًا في نفس الدورة.";
                    return View(model);
                }

                // 🟩 توليد البريد وكلمة المرور تلقائيًا
                string generatedEmail = $"{model.NationalId}@qdrat.edu.sa";
                string generatedPassword = model.NationalId;

                // 🧩 إنشاء سجل التسجيل
                var registration = new FrontendCourseRegistration
                {
                    FrontendCourseId = subCourse.FrontendCourseId,
                    SubCourseId = subCourse.Id,
                    FullName = model.FullName.Trim(),
                    NationalId = model.NationalId.Trim(),
                    Phone = model.Phone.Trim(),
                    Email = generatedEmail,
                    Password = BCrypt.Net.BCrypt.HashPassword(generatedPassword),
                    CreatedAt = DateTime.Now
                };

                _context.FrontendCourseRegistrations.Add(registration);
                await _context.SaveChangesAsync();

                TempData["Success"] = "✅ تم التسجيل بنجاح! سيتم التواصل معك قريبًا.";
                return RedirectToAction("Thanks", new { id = registration.Id });
            }
            catch (DbUpdateException dbEx)
            {
                // 🔻 أخطاء قاعدة البيانات (مثل فشل إدخال أو تكرار مفتاح)
                TempData["Error"] = $"❌ خطأ في قاعدة البيانات أثناء الحفظ: {dbEx.InnerException?.Message ?? dbEx.Message}";
                return View(model);
            }
            catch (Exception ex)
            {
                // 🔻 أي استثناء عام آخر
                TempData["Error"] = $"❌ حدث خطأ غير متوقع: {ex.Message}";
                return View(model);
            }
        }

        // ✅ صفحة الشكر بعد التسجيل
        [HttpGet("/Enrollments/Thanks/{id}")]
        public async Task<IActionResult> Thanks(int id)
        {
            var reg = await _context.FrontendCourseRegistrations
                .Include(r => r.SubCourse)
                .ThenInclude(s => s.FrontendCourse)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reg == null)
                return NotFound();

            return View(reg);
        }
    }

    // ✅ ViewModel خاص بالنموذج
    public class EnrollmentFormViewModel
    {
        public int SubCourseId { get; set; }

        [Display(Name = "اسم الدورة الفرعية")]
        public string? SubCourseTitle { get; set; } // ❌ أزل [Required]

        [Display(Name = "البرنامج الرئيسي")]
        public string? FrontCourseTitle { get; set; } // ❌ أزل [Required]

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "رقم الهوية مطلوب")]
        [StringLength(20, ErrorMessage = "رقم الهوية يجب ألا يتجاوز 20 رقمًا")]
        [Display(Name = "رقم الهوية الوطنية")]
        public string NationalId { get; set; }

        [Required(ErrorMessage = "رقم الجوال مطلوب")]
        [StringLength(20)]
        [Display(Name = "رقم الجوال")]
        public string Phone { get; set; }
    }


}
