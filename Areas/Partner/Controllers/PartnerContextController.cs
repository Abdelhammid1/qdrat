using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using System.Security.Claims;

namespace QdratNew.Areas.Partner.Controllers
{
    [Area("Partner")]
    [Authorize(Policy = "PartnerOnly")] // PPL-S1.1
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
        public async Task<IActionResult> Select()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var partners = await _context.UserPartners
                .AsNoTracking()
                .Where(up => up.UserId == userId)
                .Select(up => up.Partner)
                .OrderBy(p => p.Name)
                .ToListAsync();

            // PPL-S1.1: الـ View يعرض حالة "لا توجد مدارس" عند القائمة الفارغة
            return View(partners);
        }

        // =========================
        // تثبيت الشريك في Session
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Set(int partnerId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // PPL-S1.1: المستخدم لا يختار إلا شريكًا مرتبطًا به فعلًا
            var linked = await _context.UserPartners
                .AnyAsync(up => up.UserId == userId && up.PartnerId == partnerId);

            if (!linked)
                return Forbid();

            HttpContext.Session.SetInt32("ActivePartnerId", partnerId);

            return RedirectToAction("Index", "Students");
        }
    }
}
