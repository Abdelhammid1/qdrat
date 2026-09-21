using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Areas.Internal.QuestionBank.ViewModels;
using QdratNew.Data;
using QdratNew.Modules.QuestionBank.Read.Contracts;
using QdratNew.Modules.QuestionBank.Read.Dtos;

namespace QdratNew.Areas.Internal.QuestionBank.Controllers
{
    [Area("Internal")]
    [Route("Internal/[controller]/[action]")]
    [Authorize(Roles = "Admin,SuperAdmin,Owner,Developer,DataEntry")]
    public class QuestionBankExplorerController : Controller
    {
        private readonly IQuestionBankReadService _readService;
        private readonly ApplicationDbContext _context;
        private readonly QuestionDifficultyService _difficultyService;

        public QuestionBankExplorerController(
            IQuestionBankReadService readService,
            ApplicationDbContext context,
            QuestionDifficultyService difficultyService)
        {
            _readService = readService;
            _context = context;
            _difficultyService = difficultyService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
        int? curriculumId,
        int? sectionId,
        int? lessonId)
        {
            // =========================
            // 1) Stats (Decision Maker)
            // =========================
            var statsDto = await _readService.GetStatsAsync(curriculumId, sectionId);

            // =========================
            // 2) Build ViewModel (Core)
            // =========================
            var vm = new QuestionBankExplorerVM
            {
                CurriculumId = curriculumId,
                SectionId = sectionId,
                LessonId = lessonId,
                Stats = new QuestionBankStatsVM
                {
                    TotalQuestions = statsDto.TotalQuestions,
                    IncompleteCount = statsDto.IncompleteCount,
                    MissingAnswerCount = statsDto.MissingAnswerCount,
                    ReadyForReviewCount = statsDto.ReadyForReviewCount,
                    ApprovedCount = statsDto.ApprovedCount,
                    CompletionRate = statsDto.CompletionRate,
                    ApprovalRate = statsDto.ApprovalRate
                }
            };

            // =========================
            // 3) Dropdowns (Lightweight – No Includes)
            // =========================

            // Curriculums
            vm.Curriculums = await _context.Curriculums
                .AsNoTracking()
                .OrderBy(c => c.Title)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Title,
                    Selected = curriculumId.HasValue && c.Id == curriculumId.Value
                })
                .ToListAsync();

            // Sections (Only if Curriculum selected)
            if (curriculumId.HasValue)
            {
                vm.Sections = await _context.Sections
                    .AsNoTracking()
                    .Where(s => s.CurriculumId == curriculumId.Value)
                    .OrderBy(s => s.Title)
                    .Select(s => new SelectListItem
                    {
                        Value = s.Id.ToString(),
                        Text = s.Title,
                        Selected = sectionId.HasValue && s.Id == sectionId.Value
                    })
                    .ToListAsync();
            }

            // Lessons (Only if Section selected)
            if (sectionId.HasValue)
            {
                vm.Lessons = await _context.Lessons
                    .AsNoTracking()
                    .Where(l => l.SectionId == sectionId.Value)
                    .OrderBy(l => l.Title)
                    .Select(l => new SelectListItem
                    {
                        Value = l.Id.ToString(),
                        Text = l.Title,
                        Selected = lessonId.HasValue && l.Id == lessonId.Value
                    })
                    .ToListAsync();
            }

            // =========================
            // 4) Return View
            // =========================
            return View(vm);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecalculateDifficulty(int? curriculumId, int? sectionId, int? lessonId)
        {
            var count = await _difficultyService.RecalculateAsync(curriculumId, sectionId, lessonId);
            TempData["Success"] = $"تم تحديث {count} سؤال";
            return RedirectToAction("Index", new { curriculumId, sectionId, lessonId });
        }





        [HttpGet]
        public async Task<IActionResult> LoadPage(
          int? curriculumId,
          int? sectionId,
          int? lessonId,
          string? searchText,
          string? status,
          int page = 1,
          int pageSize = 50)
        {
            var queryResult = await _readService.QueryAsync(new QuestionBankQueryRequestDto
            {
                CurriculumId = curriculumId,
                SectionId = sectionId,
                LessonId = lessonId,
                SearchText = searchText,
                Status = status,
                Page = page,
                PageSize = pageSize,
                IncludeIncomplete = true,
                IncludeWithoutAnswer = true
            });

            return Json(queryResult);
        }






    }
}
