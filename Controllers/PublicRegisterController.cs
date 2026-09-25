using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QdratNew.Helpers;
using QdratNew.Services.Frontend.PublicRegistration;
using QdratNew.ViewModels.Frontend.Register;

namespace QdratNew.Controllers
{
    public class PublicRegisterController : Controller
    {
        private readonly IPublicRegistrationService _registration;

        public PublicRegisterController(IPublicRegistrationService registration)
        {
            _registration = registration;
        }

        // /register — كتالوج البرامج والدورات + فورم الطلب
        [HttpGet("/register")]
        public async Task<IActionResult> Index(CancellationToken ct)
            => View(new RegisterPageVM { Programs = await _registration.GetCatalogAsync(ct) });

        // POST /register — PRG إلى /register/success
        [HttpPost("/register")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("public-forms")]
        public async Task<IActionResult> Submit([Bind(Prefix = "Form")] RegisterLeadFormVM form, CancellationToken ct)
        {
            // Honeypot: البوت يملأ الحقل المخفي — نرد بنجاح شكلي دون حفظ
            if (!string.IsNullOrEmpty(form.Website)) return RedirectToAction(nameof(Success));

            // تطبيع الأرقام العربية-الهندية قبل التحقق من نمط الجوال
            form.PhoneNumber = NumberHelper.NormalizeDigits(form.PhoneNumber);
            form.ParentPhone = string.IsNullOrWhiteSpace(form.ParentPhone)
                ? null
                : NumberHelper.NormalizeDigits(form.ParentPhone);
            ModelState.Clear();
            TryValidateModel(form, "Form");

            if (ModelState.IsValid)
            {
                var result = await _registration.SubmitLeadAsync(
                    form, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

                if (result.Success) return RedirectToAction(nameof(Success));

                foreach (var e in result.Errors) ModelState.AddModelError($"Form.{e.Key}", e.Value);
            }

            return View("Index", new RegisterPageVM
            {
                Programs = await _registration.GetCatalogAsync(ct),
                Form = form
            });
        }

        [HttpGet("/register/success")]
        public IActionResult Success() => View();
    }
}
