using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Admin;
using QdratNew.ViewModels.Partner;
using System;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace QdratNew.Areas.Admin.Controllers { 

    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class PartnersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public PartnersController(
       ApplicationDbContext context,
       IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ==========================
        // GET: Create Partner
        // ==========================
        public IActionResult Create()
        {
            var model = new CreatePartnerViewModel
            {
                PartnershipStart = DateTime.Today,
                PartnershipEnd = DateTime.Today.AddYears(1)
            };

            return View(model);
        }

        // ==========================
        // POST: Create Partner
        // ==========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreatePartnerViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            string? logoPath = null;

            // =========================
            // 🔹 حفظ اللوجو إن وُجد
            // =========================
            if (model.LogoFile != null && model.LogoFile.Length > 0)
            {
                var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "partners");

                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var fileExt = Path.GetExtension(model.LogoFile.FileName);
                var fileName = $"{Guid.NewGuid()}{fileExt}";
                var fullPath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    model.LogoFile.CopyTo(stream);
                }

                logoPath = $"/uploads/partners/{fileName}";
            }

            // =========================
            // 🔹 إنشاء الشريك
            // =========================
            var partner = new QdratNew.Entities.Partner
            {
                Name = model.Name,
                Code = model.Code,
                LogoPath = logoPath,
                PartnershipStart = model.PartnershipStart,
                PartnershipEnd = model.PartnershipEnd
            };

            _context.Partners.Add(partner);
            _context.SaveChanges(); // 🔴 مهم جدًا للحصول على PartnerId

            // =========================
            // 🔹 إنشاء الفرع الرئيسي تلقائيًا
            // =========================
            var mainBranch = new Branch
            {
                Name = $"{partner.Name} - الفرع الرئيسي",
                Location = "غير محدد",
                City = "غير محدد",
                State = "غير محدد",
                Country = "السعودية",
                IsPartner = true,
                EstablishedDate = DateTime.Now,
                PartnerId = partner.Id
            };

            _context.Branches.Add(mainBranch);
            _context.SaveChanges();

            TempData["Success"] = "تم إنشاء الشريك مع الفرع الرئيسي بنجاح";

            return RedirectToAction("Details", new { id = partner.Id });
        }



        [HttpGet]
        public IActionResult Edit(int id)
        {
            var partner = _context.Partners.Find(id);
            if (partner == null)
                return NotFound();

            var model = new EditPartnerViewModel
            {
                Id = partner.Id,
                Name = partner.Name,
                Code = partner.Code,
                PartnershipStart = partner.PartnershipStart,
                PartnershipEnd = partner.PartnershipEnd,
                CurrentLogoPath = partner.LogoPath
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(EditPartnerViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var partner = _context.Partners.Find(model.Id);
            if (partner == null)
                return NotFound();

            // =========================
            // 🔹 تحديث البيانات الأساسية
            // =========================
            partner.Name = model.Name;
            partner.Code = model.Code;
            partner.PartnershipStart = model.PartnershipStart;
            partner.PartnershipEnd = model.PartnershipEnd;

            // =========================
            // 🔹 تحديث اللوجو إن وُجد
            // =========================
            if (model.LogoFile != null && model.LogoFile.Length > 0)
            {
                var uploadsRoot = Path.Combine(_env.WebRootPath, "uploads", "partners");

                if (!Directory.Exists(uploadsRoot))
                    Directory.CreateDirectory(uploadsRoot);

                var ext = Path.GetExtension(model.LogoFile.FileName);
                var fileName = $"{Guid.NewGuid()}{ext}";
                var fullPath = Path.Combine(uploadsRoot, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    model.LogoFile.CopyTo(stream);
                }

                // (اختياري) حذف اللوجو القديم
                if (!string.IsNullOrEmpty(partner.LogoPath))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, partner.LogoPath.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);
                }

                partner.LogoPath = $"/uploads/partners/{fileName}";
            }

            _context.SaveChanges();

            TempData["Success"] = "تم تحديث بيانات الشريك بنجاح";
            return RedirectToAction("Details", new { id = partner.Id });
        }





        [HttpGet]
        public IActionResult CheckCodeAvailability(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Json(false);

            code = code.Trim().ToLower();

            bool exists = _context.Partners
                .Any(p => p.Code.ToLower() == code);

            // true = متاح ، false = مستخدم
            return Json(!exists);
        }


        public IActionResult Index()
        {
            var today = DateTime.Today;

            var partners = _context.Partners
                .AsNoTracking()
                .Select(p => new PartnerListItemDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    IsActive = p.PartnershipEnd >= today
                })
                .OrderByDescending(p => p.IsActive)
                .ThenBy(p => p.Name)
                .ToList();

            return View(partners);
        }




        // ==========================
        // Details (اختياري الآن)
        // ==========================
        public IActionResult Details(int id)
        {
            var partner = _context.Partners.Find(id);
            if (partner == null) return NotFound();

            return View(partner);
        }
    }
}
