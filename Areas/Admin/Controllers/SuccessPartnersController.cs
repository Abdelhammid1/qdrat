using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class SuccessPartnersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SuccessPartnersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ عرض القائمة
        public async Task<IActionResult> Index()
        {
            var partners = await _context.SuccessPartners
                .AsNoTracking()
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();

            return View(partners);
        }


        // 🟡 تحديث ترتيب العرض عبر السحب والإفلات
        [HttpPost]
        public async Task<IActionResult> Reorder([FromBody] List<int> orderedIds)
        {
            if (orderedIds == null || !orderedIds.Any())
                return BadRequest();

            var partners = await _context.SuccessPartners
                .Where(p => orderedIds.Contains(p.Id))
                .ToListAsync();

            for (int i = 0; i < orderedIds.Count; i++)
            {
                var id = orderedIds[i];
                var partner = partners.FirstOrDefault(p => p.Id == id);
                if (partner != null)
                    partner.DisplayOrder = i + 1;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }



        // ✅ إنشاء شريك جديد
        [HttpGet]
        public IActionResult Create()
        {
            // ✅ مرر نموذج فارغ لتجنب NullReferenceException
            return View(new SuccessPartner());
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SuccessPartner model, IFormFile? LogoFile)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (LogoFile != null)
            {
                var folder = Path.Combine("wwwroot", "uploads", "successpartners");
                Directory.CreateDirectory(folder);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(LogoFile.FileName)}";
                var path = Path.Combine(folder, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                    await LogoFile.CopyToAsync(stream);

                model.LogoPath = $"/uploads/successpartners/{fileName}";
            }

            _context.SuccessPartners.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم إضافة الشريك بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // ✅ تعديل شريك
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var partner = await _context.SuccessPartners.FindAsync(id);
            if (partner == null) return NotFound();
            return View(partner);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, SuccessPartner model, IFormFile? LogoFile)
        {
            if (id != model.Id) return BadRequest();

            var partner = await _context.SuccessPartners.FindAsync(id);
            if (partner == null) return NotFound();

            partner.Name = model.Name;
            partner.Url = model.Url;
            partner.IsActive = model.IsActive;
            partner.DisplayOrder = model.DisplayOrder;

            if (LogoFile != null)
            {
                var folder = Path.Combine("wwwroot", "uploads", "successpartners");
                Directory.CreateDirectory(folder);

                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(LogoFile.FileName)}";
                var path = Path.Combine(folder, fileName);

                using (var stream = new FileStream(path, FileMode.Create))
                    await LogoFile.CopyToAsync(stream);

                partner.LogoPath = $"/uploads/successpartners/{fileName}";
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ تم تعديل بيانات الشريك بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // ✅ حذف
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var partner = await _context.SuccessPartners.FindAsync(id);
            if (partner == null) return NotFound();

            _context.SuccessPartners.Remove(partner);
            await _context.SaveChangesAsync();

            TempData["Success"] = "🗑️ تم حذف الشريك بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}
