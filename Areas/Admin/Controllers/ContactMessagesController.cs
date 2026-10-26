using Microsoft.AspNetCore.Authorization;
using QdratNew.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Policy = "AdminArea")]
    public class ContactMessagesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactMessagesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ عرض كل الرسائل
        [AdminPermission("ContactMessages", "Read")]
        public async Task<IActionResult> Index()
        {
            var messages = await _context.ContactMessages
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            return View(messages);
        }

        // 👁️ عرض التفاصيل + تحديث حالة القراءة
        [AdminPermission("ContactMessages", "Read")]
        public async Task<IActionResult> Details(int id)
        {
            var message = await _context.ContactMessages.FindAsync(id);
            if (message == null) return NotFound();

            if (!message.IsRead)
            {
                message.IsRead = true;
                message.LastUpdated = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return View(message);
        }

        // ✏️ تعديل الحالة والملاحظات
        [HttpPost]
        [ValidateAntiForgeryToken]
        [AdminPermission("ContactMessages", "Manage")]
        public async Task<IActionResult> UpdateStatus(int id, string status, string adminNotes)
        {
            var msg = await _context.ContactMessages.FindAsync(id);
            if (msg == null) return NotFound();

            msg.Status = status;
            msg.AdminNotes = adminNotes;
            msg.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم تحديث حالة الرسالة.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
