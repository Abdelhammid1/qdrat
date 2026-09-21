using Microsoft.AspNetCore.Mvc;
using QdratNew.Data;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    public class PartnerContextController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PartnerContextController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // اختيار الشريك
        // =========================
        public IActionResult Select()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var partners = _context.UserPartners
                .Where(up => up.UserId == userId)
                .Select(up => up.Partner)
                .OrderBy(p => p.Name)
                .ToList();

            // حماية إضافية
            if (!partners.Any())
            {
                return View("NoPartners"); // أو رسالة توضيحية
            }

            return View(partners);
        }

        // =========================
        // تثبيت الشريك في Session
        // =========================
        [HttpPost]
        public IActionResult Set(int partnerId)
        {
            var partnerExists = _context.Partners.Any(p => p.Id == partnerId);
            if (!partnerExists)
                return NotFound();

            HttpContext.Session.SetInt32("ActivePartnerId", partnerId);

            return RedirectToAction("Index", "Students");
        }
    }
}
