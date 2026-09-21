using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.Admin;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class FrontendLeadsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FrontendLeadsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================
        // 📋 Dashboard الطلبات + فلاتر
        // =========================================
        public async Task<IActionResult> Index(string filter = "all")
        {
            IQueryable<QdratNew.Entities.Frontend.FrontendLead> query =
                _context.FrontendLeads.AsNoTracking();

            // =====================
            // Counters
            // =====================
            var totalCount = await query.CountAsync();
            var contactedCount = await query.CountAsync(x => x.IsContacted);
            var notContactedCount = totalCount - contactedCount;

            // =====================
            // Filters
            // =====================
            if (filter == "contacted")
                query = query.Where(x => x.IsContacted);
            else if (filter == "not-contacted")
                query = query.Where(x => !x.IsContacted);

            // =====================
            // Data
            // =====================
            var leads = await query
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new FrontendLeadAdminVM
                {
                    Id = l.Id,
                    StudentName = l.StudentName,
                    PhoneNumber = l.PhoneNumber,
                    SelectedProgram = l.SelectedProgram,
                    CreatedAt = l.CreatedAt,
                    IsContacted = l.IsContacted
                })
                .ToListAsync();

            var vm = new FrontendLeadsIndexVM
            {
                TotalCount = totalCount,
                ContactedCount = contactedCount,
                NotContactedCount = notContactedCount,
                CurrentFilter = filter,
                Leads = leads
            };

            return View(vm);
        }

        // =========================================
        // ✔ تعليم أنه تم التواصل
        // =========================================
        [HttpPost]
        public async Task<IActionResult> MarkContacted(int id)
        {
            var lead = await _context.FrontendLeads.FindAsync(id);
            if (lead == null)
                return NotFound();

            lead.IsContacted = true;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
