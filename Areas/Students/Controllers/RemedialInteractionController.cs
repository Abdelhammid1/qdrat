using Microsoft.AspNetCore.Mvc;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class RemedialInteractionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RemedialInteractionController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult RecordInteraction([FromBody] RemedialPlanInteraction input)
        {
            if (input.StudentId == 0 || string.IsNullOrEmpty(input.ActionType))
                return BadRequest("بيانات غير مكتملة");

            var interaction = new RemedialPlanInteraction
            {
                StudentId = input.StudentId,
                ActionType = input.ActionType,
                InteractionTime = DateTime.Now
            };

            _context.RemedialPlanInteractions.Add(interaction);
            _context.SaveChanges();

            return Ok(new { success = true });
        }
    }
}
