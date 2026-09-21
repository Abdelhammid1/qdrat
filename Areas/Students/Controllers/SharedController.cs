using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewModels.Question;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class SharedController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SharedController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> RenderQuestionPartial(Guid qId)
        {
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == qId);

            if (question == null)
                return Content("<div class='alert alert-danger'>❌ لم يتم العثور على السؤال.</div>", "text/html");

            var vm = new QuestionDisplayViewModel
            {
                Id = question.Id,
                QuestionId = question.Id,
                Title = question.Title,
                ComparisonValue1 = question.ValueA,
                ComparisonValue2 = question.ValueB,
                ImageUrl = question.ImageUrl,
                IsAnswerConfirmed = question.IsAnswerConfirmed,
                Template = question.Template,
                Difficulty = question.Difficulty,
                IsQuantitative = question.IsQuantitative,
                Explanation = question.Explanation,
                CorrectAnswer = question.CorrectAnswer,
                Options = question.Options.Select(o => new QuestionOptionDisplayViewModel
                {
                    Text = o.Text,
                    ImageUrl = o.ImageUrl
                }).ToList(),
                DisplayType = QuestionHelper.GetDisplayType(question)
            };

            return PartialView("~/Views/Shared/_HomeworkQuestionPartial.cshtml", vm);
        }


    }
}
