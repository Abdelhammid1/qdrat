using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels;
using QdratNew.ViewModels.Question;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class QuestionReviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QuestionReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> ReviewByLesson(int lessonId, int sectionId, int curriculumId)
        {
            var questions = await _context.Questions
                .Where(q => q.LessonId == lessonId && q.IsReviewed && q.IsComplete && !q.IsRejected)
                .OrderByDescending(q => q.CreatedAt)
            .Select(q => new QuestionReviewItemViewModel
            {
                Id = q.Id,
                TitlePreview = q.Title.Length > 60 ? q.Title.Substring(0, 60) + "..." : q.Title,
                IsChecked = false,
                CorrectAnswer = q.CorrectAnswer // ✅ الجديد
            })

                .ToListAsync();

            var model = new QuestionReviewSelectionViewModel
            {
                CurriculumId = curriculumId,
                SectionId = sectionId,
                LessonId = lessonId,
                AvailableQuestions = questions
            };

            return View("ReviewByLesson", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmManualSelection(ManualQuestionSelectionViewModel model)
        {
            if (model.SelectedQuestionIds == null || !model.SelectedQuestionIds.Any())
            {
                TempData["Error"] = "يرجى اختيار أسئلة قبل المتابعة.";
                return RedirectToAction("ReviewByLesson", new
                {
                    lessonId = model.LessonId,
                    sectionId = model.SectionId,
                    curriculumId = model.CurriculumId,
                    batchId = model.BatchId
                });
            }

            // ✅ تخزين الأسئلة المختارة مؤقتًا (يُفضل نقلها إلى قاعدة البيانات لاحقًا)
            TempData[$"ManualQuestions_L{model.LessonId}_B{model.BatchId}"] = string.Join(",", model.SelectedQuestionIds);
            TempData["Message"] = "✅ تم تحديد الأسئلة يدويًا بنجاح.";

            return RedirectToAction("ConfirmHomework", "BatchLessonCompletions", new { batchId = model.BatchId });
        }


    }
}
