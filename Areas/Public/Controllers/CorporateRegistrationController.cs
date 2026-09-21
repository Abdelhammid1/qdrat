using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;

namespace QdratNew.Areas.Public.Controllers
{
    [Area("Public")]
    public class CorporateRegistrationController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CorporateRegistrationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        // ⚡ مدة قصيرة (60 ثانية) لأن الـ Action نفسه يغلق التسجيل تلقائياً (AutoCloseAt) عبر SaveChanges
        [OutputCache(Duration = 60, VaryByQueryKeys = new[] { "company" })]
        public IActionResult Register(string company)
        {
            var setting = _context.CorporateRegistrationSettings
                .FirstOrDefault(x => x.CompanyName.ToLower() == company.ToLower());

            // ⭐ تفعيل الإغلاق التلقائي
            if (setting != null && setting.AutoCloseAt.HasValue)
            {
                if (DateTime.Now >= setting.AutoCloseAt.Value)
                {
                    setting.IsClosed = true;
                    setting.ClosedAt = DateTime.Now;
                    _context.SaveChanges();
                }
            }

            if (setting != null && setting.IsClosed)
                return View("ClosedMessage", company);

            return View(new CorporateRegistrationViewModel { CompanyName = company });
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(CorporateRegistrationViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var entity = new CorporateRegistrationRequest
            {
                FullName = vm.FullName,
                EmployeeNumber = vm.EmployeeNumber,
                Email = vm.Email,
                NationalID = vm.NationalID,
                PhoneNumber = vm.PhoneNumber,
                Residence = vm.Residence,
                CompanyName = vm.CompanyName
            };

            _context.CorporateRegistrationRequests.Add(entity);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إرسال بياناتك بنجاح! سيتم التواصل معك قريبًا.";
            return RedirectToAction("Register", new { company = vm.CompanyName });
        }
    }

}
