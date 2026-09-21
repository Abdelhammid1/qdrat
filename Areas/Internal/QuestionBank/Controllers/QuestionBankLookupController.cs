using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Areas.Internal.QuestionBank.Controllers
{
    [Area("Internal")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,DataEntry")]
    [Route("internal/question-bank/lookup")]
    public class QuestionBankLookupController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QuestionBankLookupController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // Curriculums
        // ===============================
        [HttpGet("curriculums")]
        public async Task<IActionResult> GetCurriculums()
        {
            var data = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new
                {
                    id = c.Id,
                    title = c.Title
                })
                .ToListAsync();

            return Json(data);
        }

        // ===============================
        // Sections by Curriculum
        // ===============================
        [HttpGet("sections")]
        public async Task<IActionResult> GetSectionsByCurriculum(int curriculumId)
        {
            if (curriculumId <= 0)
                return Json(Array.Empty<object>());

            var sections = await _context.Sections
                .AsNoTracking()
                .Where(s => s.CurriculumId == curriculumId)
                .OrderBy(s => s.Title)
                .Select(s => new
                {
                    id = s.Id,
                    title = s.Title
                })
                .ToListAsync();

            return Json(sections);
        }

        // ===============================
        // Lessons by Section
        // ===============================
        [HttpGet("lessons")]
        public async Task<IActionResult> GetLessonsBySection(int sectionId)
        {
            if (sectionId <= 0)
                return Json(Array.Empty<object>());

            var lessons = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .OrderBy(l => l.Title)
                .Select(l => new
                {
                    id = l.Id,
                    title = l.Title
                })
                .ToListAsync();

            return Json(lessons);
        }
    }
}
