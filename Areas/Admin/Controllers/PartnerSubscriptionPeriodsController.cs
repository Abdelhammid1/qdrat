using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.PartnerSubscriptionPeriods;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class PartnerSubscriptionPeriodsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PartnerSubscriptionPeriodsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // INDEX
        // ===============================
        public IActionResult Index(int subscriptionId)
        {
            var subscription = _context.PartnerSubscriptions
                .Include(s => s.Partner)
                .FirstOrDefault(s => s.Id == subscriptionId);

            if (subscription == null)
                return NotFound();

            var today = DateTime.Today;

            var periods = _context.PartnerSubscriptionPeriods
                .Where(p => p.PartnerSubscriptionId == subscriptionId)
                .OrderByDescending(p => p.StartDate)
                .Select(p => new QdratNew.ViewModels.PartnerSubscriptionPeriods.PartnerSubscriptionPeriodListItemViewModel
                {
                    Id = p.Id,
                    StartDate = p.StartDate,
                    EndDate = p.EndDate,
                    MaxStudents = p.MaxStudents,
                    Status =
                        today < p.StartDate ? "مستقبلية" :
                        today > p.EndDate ? "منتهية" :
                        p.IsActive ? "نشطة" : "موقوفة"
                })
                .ToList();

            return View(new PartnerSubscriptionPeriodIndexViewModel
            {
                SubscriptionId = subscription.Id,
                PartnerName = subscription.Partner.Name,
                Periods = periods
            });
        }

        // ===============================
        // CREATE
        // ===============================
        public IActionResult Create(int subscriptionId)
        {
            if (subscriptionId <= 0)
                return BadRequest("SubscriptionId مطلوب");

            var subscription = _context.PartnerSubscriptions
                .Include(s => s.Partner) // ⭐ الحل هنا
                .FirstOrDefault(s => s.Id == subscriptionId);

            if (subscription == null)
                return NotFound();

            var today = DateTime.Today;

            bool hasActive = _context.PartnerSubscriptionPeriods
                .Any(p =>
                    p.PartnerSubscriptionId == subscriptionId &&
                    p.StartDate <= today &&
                    p.EndDate >= today);

            if (hasActive)
            {
                TempData["Error"] = "لا يمكن إنشاء فترة جديدة لوجود فترة نشطة بالفعل.";
                return RedirectToAction(nameof(Index), new { subscriptionId });
            }

            var model = new PartnerSubscriptionPeriodCreateViewModel
            {
                PartnerSubscriptionId = subscriptionId,
                PartnerName = subscription.Partner.Name, // ✅ الآن آمنة
                StartDate = today,
                EndDate = today.AddMonths(3)
            };

            return View(model);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(PartnerSubscriptionPeriodCreateViewModel model)
        {
            // 🔹 إعادة تعبئة اسم الشريك دائمًا
            var subscription = _context.PartnerSubscriptions
                .Include(s => s.Partner)
                .FirstOrDefault(s => s.Id == model.PartnerSubscriptionId);

            if (subscription == null)
            {
                TempData["Error"] = "العقد غير موجود.";
                return RedirectToAction("Index", "PartnerSubscriptions");
            }

            model.PartnerName = subscription.Partner.Name;

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "البيانات المدخلة غير صحيحة.";
                return View(model);
            }

            // 🔹 تحقق منطقي من التواريخ
            if (model.EndDate <= model.StartDate)
            {
                ModelState.AddModelError(
                    "",
                    "تاريخ نهاية الفترة يجب أن يكون بعد تاريخ البداية."
                );
                return View(model);
            }

            var today = DateTime.Today;

            // 🔹 منع أكثر من فترة نشطة في نفس الوقت
            bool hasActive = _context.PartnerSubscriptionPeriods
                .Any(p =>
                    p.PartnerSubscriptionId == model.PartnerSubscriptionId &&
                    p.StartDate <= today &&
                    p.EndDate >= today);

            if (hasActive)
            {
                ModelState.AddModelError(
                    "",
                    "لا يمكن إضافة فترة جديدة لوجود فترة نشطة حالياً لهذا العقد."
                );
                return View(model);
            }

            try
            {
                var period = new PartnerSubscriptionPeriod
                {
                    PartnerSubscriptionId = model.PartnerSubscriptionId,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    MaxStudents = model.MaxStudents
                };

                _context.PartnerSubscriptionPeriods.Add(period);
                _context.SaveChanges();

                TempData["Success"] = "تم إنشاء فترة الاشتراك بنجاح.";

                return RedirectToAction(
                    "Index",
                    new { subscriptionId = model.PartnerSubscriptionId }
                );
            }
            catch
            {
                TempData["Error"] = "حدث خطأ أثناء حفظ فترة الاشتراك. حاول مرة أخرى.";
                return View(model);
            }
        }





        public IActionResult Details(int id)
        {
            var period = _context.PartnerSubscriptionPeriods
                .Include(p => p.PartnerSubscription)
                    .ThenInclude(s => s.Partner)
                .FirstOrDefault(p => p.Id == id);

            if (period == null)
                return NotFound();

            var students = _context.Students
                .Where(s => s.PartnerSubscriptionPeriodId == period.Id)
                .Select(s => new StudentInPeriodItem
                {
                    StudentId = s.StudentID,
                    FullName = s.FullName,
                    NationalId = s.NationalID,
                    RegistrationDate = s.RegistrationDate,
                    IsActiveForLearning = s.IsActiveForLearning
                })
                .ToList();

            // ✅ الحساب الصحيح للحالة
            var today = DateTime.Today;
            bool isActive =
                today >= period.StartDate.Date &&
                today <= period.EndDate.Date;

            var model = new PartnerSubscriptionPeriodDetailsViewModel
            {
                PeriodId = period.Id,
                PartnerName = period.PartnerSubscription.Partner.Name,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                MaxStudents = period.MaxStudents,
                CurrentStudentsCount = students.Count,
                IsActive = isActive, // ✅ السطر الحاسم
                Students = students
            };

            return View(model);
        }


        public IActionResult Students(int periodId)
        {
            if (periodId <= 0)
                return BadRequest();

            var period = _context.PartnerSubscriptionPeriods
                .AsNoTracking()
                .Include(p => p.PartnerSubscription)
                    .ThenInclude(s => s.Partner)
                .FirstOrDefault(p => p.Id == periodId);

            if (period == null)
                return NotFound();

            var students = _context.Students
                .AsNoTracking()
                .Where(s => s.PartnerSubscriptionPeriodId == periodId)
                .OrderBy(s => s.FullName)
                .Select(s => new PartnerSubscriptionPeriodStudentViewModel
                {
                    StudentId = s.StudentID,
                    UserId = s.UserId,
                    FullName = s.FullName,
                    NationalId = s.NationalID,
                    PhoneNumber = s.PhoneNumber,
                    IsActiveForLearning = s.IsActiveForLearning
                })
                .ToList();

            var model = new PartnerSubscriptionPeriodStudentsViewModel
            {
                PeriodId = period.Id,
                PartnerName = period.PartnerSubscription != null && period.PartnerSubscription.Partner != null
                    ? period.PartnerSubscription.Partner.Name
                    : string.Empty,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                Students = students
            };

            return View(model);
        }
        // ===============================
        // EDIT
        // ===============================
        public IActionResult Edit(int id)
        {
            var period = _context.PartnerSubscriptionPeriods
                .Include(p => p.PartnerSubscription)
                    .ThenInclude(s => s.Partner)
                .FirstOrDefault(p => p.Id == id);

            if (period == null)
                return NotFound();

            if (DateTime.Today > period.EndDate)
            {
                TempData["Error"] = "لا يمكن تعديل فترة منتهية.";
                return RedirectToAction(
                    nameof(Index),
                    new { subscriptionId = period.PartnerSubscriptionId }
                );
            }

            var model = new PartnerSubscriptionPeriodEditViewModel
            {
                Id = period.Id,
                PartnerSubscriptionId = period.PartnerSubscriptionId,
                PartnerName = period.PartnerSubscription.Partner.Name, // ⭐ مهم جدًا
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                MaxStudents = period.MaxStudents
                // ❌ لا IsActive
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(PartnerSubscriptionPeriodEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "البيانات المدخلة غير صحيحة.";
                return View(model);
            }

            var period = _context.PartnerSubscriptionPeriods.Find(model.Id);
            if (period == null)
            {
                TempData["Error"] = "لم يتم العثور على فترة الاشتراك.";
                return RedirectToAction(nameof(Index),
                    new { subscriptionId = model.PartnerSubscriptionId });
            }

            if (DateTime.Today > period.EndDate)
            {
                TempData["Error"] = "لا يمكن تعديل فترة منتهية.";
                return RedirectToAction(nameof(Index),
                    new { subscriptionId = period.PartnerSubscriptionId });
            }

            try
            {
                period.EndDate = model.EndDate;
                period.MaxStudents = model.MaxStudents;

                _context.SaveChanges();

                TempData["Success"] = "تم تحديث فترة الاشتراك بنجاح.";
                return RedirectToAction(nameof(Index),
                    new { subscriptionId = period.PartnerSubscriptionId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = "حدث خطأ أثناء حفظ التعديلات: " + ex.Message;
                return View(model);
            }
        }



    }

}
