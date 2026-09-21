using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Admin;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class UserPartnersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public UserPartnersController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ===============================
        // LIST
        // ===============================
        public IActionResult Index()
        {
            var data =
                from up in _context.UserPartners
                join u in _context.Users on up.UserId equals u.Id
                join p in _context.Partners on up.PartnerId equals p.Id
                select new UserPartnerListItemViewModel
                {
                    Id = up.Id,
                    UserId = u.Id,
                    UserName = u.FullName,
                    UserEmail = u.Email,
                    PartnerName = p.Name
                };

            return View(data.ToList());
        }

        // ===============================
        // CREATE (GET)
        // ===============================
        public IActionResult Create()
        {
            var model = new UserPartnerCreateViewModel();

            // ===== Users =====
            model.Users = _context.Users
                .Select(u => new UserPartnerCreateViewModel.UserItem
                {
                    Id = u.Id,
                    Name = u.FullName + " (" + u.Email + ")"
                })
                .ToList();

            // ===== Partners =====
            // ⚠️ بدون Where(p => p.IsActive) داخل SQL
            model.Partners = _context.Partners
                .ToList()               // نجلب أولًا
                .Where(p => p.IsActive) // ثم نفلتر في الذاكرة
                .Select(p => new UserPartnerCreateViewModel.PartnerItem
                {
                    Id = p.Id,
                    Name = p.Name
                })
                .ToList();

            return View(model);
        }
        // ===============================
        // CREATE (POST)
        // ===============================


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserPartnerCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            if (model.PartnerIds == null || model.PartnerIds.Count == 0)
            {
                ModelState.AddModelError("", "يجب اختيار مدرسة واحدة على الأقل");
                return View(model);
            }

            // جلب الروابط الحالية
            var existingPartnerIds = _context.UserPartners
                .Where(x => x.UserId == model.UserId)
                .Select(x => x.PartnerId)
                .ToList();

            // إضافة الجديد فقط
            var newLinks = model.PartnerIds
                .Where(pid => !existingPartnerIds.Contains(pid))
                .Select(pid => new UserPartner
                {
                    UserId = model.UserId,
                    PartnerId = pid
                })
                .ToList();

            if (newLinks.Any())
            {
                _context.UserPartners.AddRange(newLinks);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }


        public IActionResult Edit(string userId)
        {
            var model = new UserPartnerEditViewModel
            {
                UserId = userId,
                UserName = _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.FullName ?? u.UserName)
                    .FirstOrDefault(),

                SelectedPartnerIds = _context.UserPartners
                    .Where(up => up.UserId == userId)
                    .Select(up => up.PartnerId)
                    .ToList(),

                AllPartners = _context.Partners
                    .Select(p => new UserPartnerEditViewModel.PartnerItem
                    {
                        Id = p.Id,
                        Name = p.Name
                    })
                    .ToList()
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserPartnerEditViewModel model)
        {
            var existingLinks = _context.UserPartners
                .Where(up => up.UserId == model.UserId)
                .ToList();

            // حذف غير المختار
            var toRemove = existingLinks
                .Where(x => !model.SelectedPartnerIds.Contains(x.PartnerId))
                .ToList();

            _context.UserPartners.RemoveRange(toRemove);

            // إضافة الجديد
            var existingPartnerIds = existingLinks.Select(x => x.PartnerId).ToList();

            var toAdd = model.SelectedPartnerIds
                .Where(pid => !existingPartnerIds.Contains(pid))
                .Select(pid => new UserPartner
                {
                    UserId = model.UserId,
                    PartnerId = pid
                });

            _context.UserPartners.AddRange(toAdd);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { userId = model.UserId });
        }


        public IActionResult Details(string userId)
        {
            var model = new UserPartnerDetailsViewModel
            {
                UserId = userId,
                UserName = _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => u.FullName ?? u.UserName)
                    .FirstOrDefault(),

                Partners = _context.UserPartners
                    .Where(up => up.UserId == userId)
                    .Select(up => new UserPartnerDetailsViewModel.PartnerItem
                    {
                        Id = up.Partner.Id,
                        Name = up.Partner.Name,
                        LogoPath = up.Partner.LogoPath
                    })
                    .ToList()
            };

            return View(model);
        }





        // ===============================
        // DELETE
        // ===============================
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var link = await _context.UserPartners.FindAsync(id);
            if (link == null)
                return NotFound();

            _context.UserPartners.Remove(link);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
