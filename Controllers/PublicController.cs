using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Public;
using System.Net;
using System.Net.Mail;

namespace QdratNew.Controllers
{
    public class PublicController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PublicController> _logger;
        private readonly IConfiguration _config;
        public PublicController(ApplicationDbContext context, ILogger<PublicController> logger, IConfiguration config)
        {
            _context = context;
            _logger = logger;
            _config = config;
        }

        public IActionResult Index() => View();


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendContact(ContactFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (model.Message.Contains("<script>") || model.FullName.Contains("<") || model.Email.Contains("<"))
                return BadRequest("تم رفض الرسالة لأسباب أمنية.");

            var contact = new ContactMessage
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                Message = model.Message.Trim(),
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                IsRead = false,
                Status = "جديدة",
                SentAt = DateTime.UtcNow
            };

            _context.ContactMessages.Add(contact);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "✅ تم إرسال الرسالة بنجاح، سيتم التواصل معك قريبًا." });
        }



        // ✅ عرض صفحة ثابتة حسب الـ Slug
        [Route("Public/Page/{slug}")]
        public async Task<IActionResult> Page(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
                return RedirectToAction("Index");

            var page = await _context.StaticPages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Slug == slug && p.IsActive);

            if (page == null)
                return NotFound("الصفحة غير موجودة أو تم تعطيلها.");

            return View("StaticPage", page);
        }
    }
}
