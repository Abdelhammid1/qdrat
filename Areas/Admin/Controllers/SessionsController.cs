using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class SessionsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SessionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var sessions = await _context.Sessions
                .Include(s => s.Course)
                .Include(s => s.Section)
                .Include(s => s.Instructor)
                .OrderByDescending(s => s.Date)
                .ToListAsync();

            return View(sessions);
        }

        public IActionResult Create()
        {
            var model = new SessionFormViewModel
            {
                Session = new Session(),
                Courses = _context.Courses.ToList(),
                Sections = _context.Sections.ToList(),
                Instructors = _context.Instructors.ToList()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Create(SessionFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Courses = await _context.Courses.ToListAsync();
                model.Sections = await _context.Sections.ToListAsync();
                model.Instructors = await _context.Instructors.ToListAsync();
                return View(model);
            }

            _context.Sessions.Add(model.Session);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }


        public async Task<IActionResult> Edit(int id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session == null) return NotFound();

            var model = new SessionFormViewModel
            {
                Session = session,
                Courses = _context.Courses.ToList(),
                Sections = _context.Sections.ToList(),
                Instructors = _context.Instructors.ToList()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(SessionFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.Courses = _context.Courses.ToList();
                model.Sections = _context.Sections.ToList();
                model.Instructors = _context.Instructors.ToList();
                return View(model);
            }

            _context.Sessions.Update(model.Session);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var session = await _context.Sessions.FindAsync(id);
            if (session == null) return NotFound();

            _context.Sessions.Remove(session);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var session = await _context.Sessions
                .Include(s => s.Course)
                .Include(s => s.Section)
                .Include(s => s.Instructor)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null) return NotFound();

            return View(session);
        }
    }
}
