using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using QdratNew.Helpers;
using QdratNew.Services.Frontend.Packages;
using QdratNew.ViewModels.Frontend.Packages;

namespace QdratNew.Controllers
{
    public class PackagesController : Controller
    {
        private readonly ICoursePackagePublicService _packages;

        public PackagesController(ICoursePackagePublicService packages)
        {
            _packages = packages;
        }

        // /packages — كل الباقات الفعّالة
        [HttpGet("/packages")]
        public async Task<IActionResult> Index(CancellationToken ct)
            => View(await _packages.GetAllActiveAsync(ct));

        // /packages/{slug} — 404 إن لم توجد أو غير فعّالة
        [HttpGet("/packages/{slug}")]
        public Task<IActionResult> Details(string slug, CancellationToken ct)
            => RenderDetailsAsync(slug, new PackageRegisterFormViewModel(), submitted: false, ct);

        // صفحة الشكر (PRG) — نفس صفحة التفاصيل بحالة «تم الإرسال»، بدون عرض أي بيانات من الطلب
        [HttpGet("/packages/{slug}/thanks")]
        public Task<IActionResult> Thanks(string slug, CancellationToken ct)
            => RenderDetailsAsync(slug, new PackageRegisterFormViewModel(), submitted: true, ct);

        [HttpPost("/packages/{slug}")]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting("public-forms")]
        public async Task<IActionResult> Submit(string slug, PackageRegisterFormViewModel form, CancellationToken ct)
        {
            // Honeypot: البوت يملأ الحقل المخفي — نجاح شكلي دون حفظ
            if (!string.IsNullOrEmpty(form.Website))
                return RedirectToAction(nameof(Thanks), new { slug });

            // تطبيع الأرقام العربية-الهندية قبل التحقق من نمط الجوال
            form.PhoneNumber = NumberHelper.NormalizeDigits(form.PhoneNumber);
            ModelState.Clear();
            TryValidateModel(form);

            if (ModelState.IsValid)
            {
                var result = await _packages.SubmitAsync(
                    slug, form, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);

                if (result.NotFound) return NotFound();
                if (result.Success) return RedirectToAction(nameof(Thanks), new { slug });

                foreach (var e in result.Errors) ModelState.AddModelError(e.Key, e.Value);
            }

            return await RenderDetailsAsync(slug, form, submitted: false, ct);
        }

        private async Task<IActionResult> RenderDetailsAsync(
            string slug, PackageRegisterFormViewModel form, bool submitted, CancellationToken ct)
        {
            var package = await _packages.GetBySlugAsync(slug, ct);
            if (package == null) return NotFound();

            return View("Details", new PackageDetailsPageViewModel
            {
                Package = package,
                Form = form,
                Submitted = submitted
            });
        }
    }
}
