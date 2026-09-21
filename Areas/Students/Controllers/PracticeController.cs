using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Students;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    [Authorize(Roles = "Student")]
    public class PracticeController : Controller
    {
        private readonly IStudentPracticeService _practiceService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public PracticeController(
            IStudentPracticeService practiceService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _practiceService = practiceService;
            _userManager = userManager;
            _context = context;
        }

        private async Task<int?> GetCurrentStudentIdAsync()
        {
            var userId = _userManager.GetUserId(User);
            return await _context.Students
                .Where(s => s.UserId == userId)
                .Select(s => s.StudentID)
                .FirstOrDefaultAsync();
        }

        // 🟢 تدريب على محور معين (45 سؤال)
        public async Task<IActionResult> StartWeaknessPractice(int sectionId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var questions = await _practiceService.GetWeaknessPracticeForSectionAsync(studentId.Value, sectionId, 45);

            // 🟢 لازم نرجع لستة مش null حتى لو مافيهاش أسئلة
            return View("PracticeSession", questions ?? new List<QdratNew.ViewModels.Students.PracticeQuestionVm>());
        }

        // 🟢 تدريب عام على المحاور الضعيفة
        [HttpGet]
        public async Task<IActionResult> WeaknessPractice()
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            var questions = await _practiceService.GetWeaknessPracticeAsync(studentId.Value, 45);
            return View("PracticeSession", questions);
        }


        [HttpPost]
        public async Task<IActionResult> SubmitFinalTraining(int sectionId, List<string> questionIds)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🟢 تحويل IDs من string إلى Guid
            var parsedIds = questionIds
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            if (!parsedIds.Any())
            {
                TempData["ErrorMessage"] = "⚠️ لم يتم العثور على أسئلة صالحة في هذه الجلسة.";
                return RedirectToAction("WeaknessPractice");
            }

            // 🟢 هات الإجابات مباشرة (تصفية في الذاكرة لاحقًا)
            var answers = await _context.StudentPracticeAnswers
                .Where(a => a.StudentId == studentId)
                .ToListAsync();

            // 🟢 جلب الأسئلة مع Options و Lesson
            var allQuestions = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.Lesson) // 👈 عشان نقدر نوصل لـ q.Lesson.SectionId
                .ToListAsync();

            // 🟢 فلترة في الذاكرة
            var questions = (from q in allQuestions
                             join id in parsedIds on q.Id equals id
                             select q).ToList();

            // 🟢 استخراج SectionId من الأسئلة
            var sectionIdFromQuestions = questions
                .Select(q => q.Lesson?.SectionId ?? 0) // 👈 نتأكد إنه مش null
                .FirstOrDefault();


            // 🟢 فلترة الإجابات في الذاكرة بالـ Join
            answers = (from a in answers
                       join id in parsedIds on a.QuestionId equals id
                       select a).ToList();

            int total = questions.Count;
            int correct = answers.Count(a => a.IsCorrect);

            // 🟢 حدد الأسئلة الخاطئة
            var wrongs = (from q in questions
                          join a in answers on q.Id equals a.QuestionId into gj
                          from ans in gj.DefaultIfEmpty()
                          where ans == null || !ans.IsCorrect
                          select new QdratNew.ViewModels.Students.TrainingWrongQuestionVm
                          {
                              QuestionTitle = q.Title,
                              StudentAnswer = ans?.SelectedAnswer,
                              CorrectAnswer = q.CorrectAnswer
                          }).ToList();

    
            // 🟢 حساب النسبة
            double percentage = total > 0 ? Math.Round((double)correct / total * 100, 2) : 0;

            // 🟢 سجل ملخص التدريب
            var training = new StudentWeaknessTraining
            {
                StudentId = studentId.Value,
                SectionId = sectionIdFromQuestions, // 👈 ضمان إنه Section صحيح

                TotalQuestions = total,
                CorrectAnswers = correct,
                ScorePercentage = percentage,
                TrainedAt = DateTime.Now
            };
            _context.StudentWeaknessTrainings.Add(training);
            await _context.SaveChangesAsync();

            // 🟢 ViewModel للعرض
            var vm = new QdratNew.ViewModels.Students.TrainingResultViewModel
            {
                SectionId = sectionId,
                TotalQuestions = total,
                CorrectAnswers = correct,
                ScorePercentage = percentage,
                WrongQuestions = wrongs,
                IsQuantitative = questions.Any(x => x.IsQuantitative)
            };

            return View("TrainingResult", vm);
        }

        // 🟢 تدريب على الأخطاء السابقة
        // 🟢 تدريب على الأخطاء السابقة فقط (باستخدام السيرفيس)
        [HttpGet]
        public async Task<IActionResult> MistakePractice(int page = 1, int pageSize = 10)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return RedirectToAction("Login", "Account", new { area = "" });

            // 🟢 1) جلب أخطاء الطالب (خطأ + تخطّي) من الخدمة بعد تعديلها
            var allMistakes = await _practiceService.GetMistakePracticeAsync(studentId.Value, 2000);

            if (allMistakes == null || !allMistakes.Any())
            {
                ViewBag.Page = 1;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRemaining = 0;
                ViewBag.HasMore = false;
                return View("MistakePracticeSession", new List<PracticeQuestionVm>());
            }

            // 🟢 2) IDs للأسئلة التي راجعها الطالب
            var reviewedIds = await _context.StudentReviewedMistakes
                .Where(r => r.StudentId == studentId)
                .Select(r => r.QuestionId)
                .ToListAsync();

            // 🟢 3) استبعاد الأسئلة المراجَعة بدون Contains على IQueryable
            var reviewedTable = reviewedIds.Select(x => new { QuestionId = x }).ToList();

            var notReviewed = (
                from m in allMistakes
                join rv in reviewedTable on m.Question.Id equals rv.QuestionId into gj
                from r in gj.DefaultIfEmpty()
                where r == null // لم تتم المراجعة
                select m
            ).ToList();

            // 🟢 4) Pagination آمن 100%
            int skip = (page - 1) * pageSize;

            var pageQuestions = notReviewed
                .Skip(skip)
                .Take(pageSize)
                .ToList();

            // 🟢 5) ضبط بيانات الـ ViewBag
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRemaining = notReviewed.Count;
            ViewBag.HasMore = notReviewed.Count > page * pageSize;

            return View("MistakePracticeSession", pageQuestions);
        }


        [HttpPost]
        public async Task<IActionResult> MarkReviewed(Guid questionId)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
                return Json(new { success = false });

            var answer = await _context.StudentPracticeAnswers
                .Where(a => a.StudentId == studentId && a.QuestionId == questionId)
                .OrderByDescending(a => a.AnsweredAt) // 🟢 آخر محاولة
                .FirstOrDefaultAsync();

            if (answer != null)
            {
                answer.IsReviewed = true;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }




        // 🟢 تسليم الإجابة وعرض النتيجة مباشرة
        [HttpPost]
        public async Task<IActionResult> SubmitPracticeAnswer(Guid questionId, string selectedAnswer)
        {
            var studentId = await GetCurrentStudentIdAsync();
            if (studentId == null)
            {
                return Json(new { success = false, message = "❌ لم يتم التعرف على الطالب." });
            }

            // 🟢 هات السؤال مع الخيارات
            var question = await _context.Questions
                .Include(q => q.Options)
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
            {
                return Json(new
                {
                    success = false,
                    message = "❌ لم يتم العثور على السؤال."
                });
            }

            // 🟢 تحقق من الإجابة
            bool isCorrect = selectedAnswer == question.CorrectAnswer;

            // 🟢 سجل المحاولة في جدول StudentPracticeAnswers
            var practiceAnswer = new StudentPracticeAnswer
            {
                StudentId = studentId.Value,
                QuestionId = questionId,
                SelectedAnswer = selectedAnswer,
                IsCorrect = isCorrect,
                AnsweredAt = DateTime.Now
            };

            _context.StudentPracticeAnswers.Add(practiceAnswer);
            await _context.SaveChangesAsync();

            // 🟢 رجع النتيجة للـ Frontend
            return Json(new
            {
                success = true,
                isCorrect = isCorrect,
                correctAnswer = question.CorrectAnswer ?? "غير متوفرة",
                questionTitle = question.Title
            });
        }


    }
}
