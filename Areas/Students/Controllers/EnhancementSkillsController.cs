using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Helpers;
using QdratNew.ViewModels.EnhancementSkills;
using QdratNew.ViewModels.Question;
using QdratNew.ViewModels.Students;
using System.Security.Claims;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class EnhancementSkillsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EnhancementSkillsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<int?> GetCurrentStudentIdAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == userId);
            return student?.StudentID;
        }

        public async Task<IActionResult> Index()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null) return Unauthorized();

            var student = await _context.StudentBatchEnrollments
       .Include(s => s.Batch)
           .ThenInclude(b => b.Course)
               .ThenInclude(c => c.CourseCurriculums)
                   .ThenInclude(cc => cc.Curriculum)
       .FirstOrDefaultAsync(s => s.StudentID == studentId);


            if (student?.Batch == null)
                return NotFound("الدفعة غير موجودة.");

            var curriculum = await _context.CourseCurriculums
    .Where(cc => cc.CourseId == student.Batch.CourseId)
    .Select(cc => cc.Curriculum)
    .FirstOrDefaultAsync();


            if (curriculum == null)
                return NotFound("المنهج غير موجود.");

            var instructor = await _context.CurriculumInstructors
                .Include(ci => ci.Instructor)
                .FirstOrDefaultAsync(ci => ci.CurriculumId == curriculum.Id);

            var questionCount = await _context.Questions
                .CountAsync(q => q.CurriculumId == curriculum.Id &&
                                 q.UsageTypes.HasFlag(QuestionUsageType.Enhancement));

            var vm = new List<EnhancementSkillBatchViewModel>
    {
        new EnhancementSkillBatchViewModel
        {
            BatchId = student.BatchId,
            BatchName = student.Batch.Name,
            CurriculumTitle = curriculum.Title,
            InstructorName = instructor?.Instructor?.FullName ?? "—",
            TotalQuestions = questionCount
        }
    };

            return View(vm);
        }

        public async Task<IActionResult> StartSession(int batchId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null) return Unauthorized();

            var questions = await (from question in _context.Questions
                                   join curriculum in _context.Curriculums on question.CurriculumId equals curriculum.Id
                                   join cc in _context.CourseCurriculums on curriculum.Id equals cc.CurriculumId
                                   join course in _context.Courses on cc.CourseId equals course.Id
                                   join batch in _context.Batches on course.Id equals batch.CourseId
                                   where batch.Id == batchId && question.UsageTypes.HasFlag(QuestionUsageType.Enhancement)
                                   select question)
                        .Include(q => q.Options)
                        .OrderBy(q => Guid.NewGuid())
                        .Take(10)
                        .Select(q => q.ToDisplayModel())
                        .ToListAsync();


            if (!questions.Any())
                return NotFound("لا توجد مهارات متاحة لهذه الدفعة.");

            HttpContext.Session.SetObject("EnhancementSessionQuestions", questions);
            HttpContext.Session.SetInt32("EnhancementCurrentIndex", 0);
            HttpContext.Session.SetInt32("EnhancementBatchId", batchId);

            return View("StartSession", questions); // 🟥 مهم هنا: Model هو List<QuestionDisplayViewModel>
        }



        [HttpPost]
        public IActionResult RenderQuestion([FromBody] QuestionDisplayViewModel model)
        {
            ViewBag.SelectedAnswer = model.SelectedAnswer;
            return PartialView("_QuestionPreviewPartial", model);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitAll([FromBody] List<QuestionDisplayViewModel> questions)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null) return Unauthorized();

            int batchId = HttpContext.Session.GetInt32("EnhancementBatchId") ?? 0;
            int total = questions.Count;
            int correct = questions.Count(q => q.SelectedAnswer == q.CorrectAnswer);

            var result = new EnhancementSkillSessionResult
            {
                StudentId = studentId.Value,
                BatchId = batchId,
                TotalQuestions = total,
                CorrectAnswers = correct,
                CreatedAt = DateTime.Now
            };
            _context.EnhancementSkillSessionResults.Add(result);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }


        [HttpPost]
        public async Task<IActionResult> SubmitAnswers([FromBody] List<QuestionDisplayViewModel> submitted)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Unauthorized();

            var batchId = HttpContext.Session.GetInt32("EnhancementBatchId");

            var originalQuestions = HttpContext.Session.GetObject<List<QuestionDisplayViewModel>>("EnhancementSessionQuestions") ?? new List<QuestionDisplayViewModel>();

            for (int i = 0; i < originalQuestions.Count; i++)
            {
                var answer = submitted.FirstOrDefault(s => s.Id == originalQuestions[i].Id)?.SelectedAnswer;
                originalQuestions[i].SelectedAnswer = answer;
            }

            int correct = originalQuestions.Count(q => q.SelectedAnswer == q.CorrectAnswer);

            var result = new EnhancementSkillSessionResult
            {
                StudentId = studentId.Value,
                BatchId = batchId ?? 0,
                TotalQuestions = originalQuestions.Count,
                CorrectAnswers = correct,
                CreatedAt = DateTime.Now
            };

            _context.EnhancementSkillSessionResults.Add(result);
            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("EnhancementSessionQuestions");
            HttpContext.Session.Remove("EnhancementCurrentIndex");
            HttpContext.Session.Remove("EnhancementBatchId");

            return Json(new { success = true });
        }


        [HttpPost]
        public IActionResult UpdateSession([FromBody] List<QuestionDisplayViewModel> questions)
        {
            HttpContext.Session.SetObject("EnhancementSessionQuestions", questions);
            return Ok();
        }


        [HttpGet]
        public async Task<IActionResult> Result()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Unauthorized();

            var lastResult = await _context.EnhancementSkillSessionResults
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            if (lastResult == null)
                return RedirectToAction("Index");

            var vm = new EnhancementSkillSessionResultViewModel
            {
                Id = lastResult.Id,
                TotalQuestions = lastResult.TotalQuestions,
                CorrectAnswers = lastResult.CorrectAnswers,
                CreatedAt = lastResult.CreatedAt
            };

            return View(vm);
        }


    }
}
