using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QdratNew.Data;
using System.Linq;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [Authorize(Policy = "PartnerOnly")]
    public class PartnersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PartnersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // 🏫 اختيار المدرسة (الشريك)
        // ===============================
        public IActionResult SelectPartner()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Redirect("/");

            var partners = _context.UserPartners
                .Where(up => up.UserId == userId)
                .Select(up => new
                {
                    up.Partner.Id,
                    up.Partner.Name,
                    up.Partner.LogoPath
                })
                .ToList();

            if (partners.Count == 0)
                return Redirect("/Identity/Account/AccessDenied");

            // ✅ لو مدرسة واحدة فقط → تفعيل تلقائي
            if (partners.Count == 1)
            {
                HttpContext.Session.SetInt32("ActivePartnerId", partners.First().Id);
                return RedirectToAction("Index", "Home", new { area = "Partner" });
            }

            // 🔁 أكثر من مدرسة → عرض صفحة الاختيار
            return View(partners);
        }

        // ===============================
        // 🔐 تفعيل المدرسة المختارة
        // ===============================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActivatePartner(int partnerId)
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Redirect("/");

            // 🔒 تحقق أمني: هل هذا المستخدم مرتبط فعلاً بهذه المدرسة؟
            var isAllowed = _context.UserPartners
                .Any(up => up.UserId == userId && up.PartnerId == partnerId);

            if (!isAllowed)
                return Redirect("/Identity/Account/AccessDenied");

            HttpContext.Session.SetInt32("ActivePartnerId", partnerId);

            return RedirectToAction(
                "Index",
                "Home",
                new { area = "Partner" }
            );
        }
    }
}
