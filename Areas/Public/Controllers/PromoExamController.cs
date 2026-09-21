using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Promo;

namespace QdratNew.Areas.Public.Controllers
{
    [Area("Public")]
    public class PromoExamController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PromoExamController(ApplicationDbContext context)
        {
            _context = context;
        }

        // الصفحة الترحيبية
        public IActionResult Welcome()
        {
            var courseList = _context.Courses
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name
                }).ToList();

            ViewBag.Courses = courseList;
            return View();
        }

        // بدء الاختبار
        [HttpPost]
        public IActionResult StartExam(int courseId)
        {
            var questions = _context.Questions
                .Where(q => q.Lesson.Section.Curriculum.CourseCurriculums
                    .Any(cc => cc.CourseId == courseId))
                .OrderBy(r => Guid.NewGuid())
                .Take(20)
                .ToList();

            var session = new PromoExamSession
            {
                CourseId = courseId,
                TempIdentifier = Guid.NewGuid().ToString()
            };
            _context.PromoExamSessions.Add(session);
            _context.SaveChanges();

            HttpContext.Session.SetInt32("PromoSessionId", session.Id);

            // احفظ الأسئلة في جدول المحاولات
            foreach (var q in questions)
            {
                _context.PromoExamAttempts.Add(new PromoExamAttempt
                {
                    SessionId = session.Id,
                    QuestionId = q.Id
                });
            }
            _context.SaveChanges();

            return RedirectToAction("Solve", new { sessionId = session.Id, index = 0 });
        }

        // عرض السؤال
        public IActionResult Solve(int sessionId, int index = 0)
        {
            var attempts = _context.PromoExamAttempts
                .Include(a => a.Question)
                    .ThenInclude(q => q.Options)
                .Where(a => a.SessionId == sessionId)
                .OrderBy(a => a.Id)
                .ToList();

            if (!attempts.Any())
                return RedirectToAction("Welcome");

            if (index >= attempts.Count)
                return RedirectToAction("Result", new { sessionId });

            var current = attempts[index];

            var vm = new PromoExamSolveViewModel
            {
                SessionId = sessionId,
                CurrentQuestionId = current.QuestionId,
                Question = current.Question,
                SelectedAnswer = current.SelectedAnswer,
                AllQuestionIds = attempts.Select(a => a.QuestionId).ToList(),
                CurrentIndex = index + 1
            };

            // لو الطلب Ajax → نرجع البارشال فقط
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_SolveHomeworkQuestionPartial", vm);

            // وإلا نعرض الصفحة كاملة (احتمال رجوع مباشر)
            return View("Start", vm);
        }


        [HttpPost]
        public IActionResult SubmitAnswerFetch(int SessionId, Guid q, string? SelectedOption, string nav)
        {
            var attempt = _context.PromoExamAttempts
                .Include(a => a.Question)
                    .ThenInclude(qn => qn.Options) // ✅ هنا الأساس
                .FirstOrDefault(a => a.SessionId == SessionId && a.QuestionId == q);

            if (attempt != null)
            {
                attempt.SelectedAnswer = SelectedOption ?? "";
                attempt.IsCorrect = !string.IsNullOrEmpty(SelectedOption) && attempt.Question.CorrectAnswer == SelectedOption;
                attempt.AttemptedAt = DateTime.Now;
                _context.SaveChanges();
            }

            var allIds = _context.PromoExamAttempts
                .Where(a => a.SessionId == SessionId)
                .OrderBy(a => a.Id)
                .Select(a => a.QuestionId)
                .ToList();

            var currentIndex = allIds.IndexOf(q);
            int nextIndex = nav == "prev" ? currentIndex - 1 : currentIndex + 1;

            // ✅ الانتقال للنهاية = عرض النتيجة
            if (nextIndex >= allIds.Count)
                return RedirectToAction("Result", new { sessionId = SessionId });

            if (nextIndex < 0) nextIndex = 0;

            var nextQuestionId = allIds[nextIndex];
            var nextAttempt = _context.PromoExamAttempts
                .Include(a => a.Question)
                    .ThenInclude(qn => qn.Options) // ✅ مهم جدًا هنا أيضًا
                .FirstOrDefault(a => a.SessionId == SessionId && a.QuestionId == nextQuestionId);

            if (nextAttempt == null)
                return Content("<div class='alert alert-warning'>لم يتم تحميل السؤال التالي</div>", "text/html");

            var vm = new PromoExamSolveViewModel
            {
                SessionId = SessionId,
                CurrentQuestionId = nextQuestionId,
                Question = nextAttempt.Question,
                AllQuestionIds = allIds,
                CurrentIndex = nextIndex + 1,
                SelectedAnswer = nextAttempt.SelectedAnswer
            };

            return PartialView("_SolvePromoQuestionPartial", vm);
        }



        [HttpPost]
        public IActionResult Start(int courseId)
        {
            // 🟢 جلب 20 سؤالًا عشوائيًا من الدورة المحددة
            var questions = _context.Questions
                .Include(q => q.Options)
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                        .ThenInclude(s => s.Curriculum)
                            .ThenInclude(c => c.CourseCurriculums)
                .Where(q => q.Lesson.Section.Curriculum.CourseCurriculums
                    .Any(cc => cc.CourseId == courseId))
                .OrderBy(r => Guid.NewGuid())
                .Take(20)
                .ToList();

            if (!questions.Any())
            {
                TempData["ErrorMessage"] = "⚠️ لا توجد أسئلة متاحة لهذه الدورة حاليًا.";
                return RedirectToAction("Welcome");
            }

            // 🟢 إنشاء جلسة جديدة
            var session = new PromoExamSession
            {
                CourseId = courseId,
                TempIdentifier = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.Now
            };

            _context.PromoExamSessions.Add(session);
            _context.SaveChanges();

            // 🟢 إنشاء محاولات الأسئلة
            foreach (var q in questions)
            {
                _context.PromoExamAttempts.Add(new PromoExamAttempt
                {
                    SessionId = session.Id,
                    QuestionId = q.Id
                });
            }

            _context.SaveChanges();

            // 🟢 تجهيز أول سؤال للعرض
            var firstQuestion = questions.First();

            var vm = new PromoExamSolveViewModel
            {
                SessionId = session.Id,
                CurrentQuestionId = firstQuestion.Id,
                Question = firstQuestion,
                AllQuestionIds = questions.Select(q => q.Id).ToList(),
                CurrentIndex = 1
            };

            // تحميل الصفحة Start.cshtml (تحتوي على fetch logic)
            return View("Start", vm);
        }



        // إرسال إجابة
        [HttpPost]
        public IActionResult SubmitAnswer(int sessionId, Guid questionId, string selectedAnswer, int nextIndex)
        {
            var attempt = _context.PromoExamAttempts
                .Include(a => a.Question)
                .FirstOrDefault(a => a.SessionId == sessionId && a.QuestionId == questionId);

            if (attempt != null)
            {
                // 🔹 فقط إذا اختار المستخدم إجابة نحفظها
                if (!string.IsNullOrWhiteSpace(selectedAnswer))
                {
                    attempt.SelectedAnswer = selectedAnswer;
                    attempt.IsCorrect = attempt.Question.CorrectAnswer == selectedAnswer;
                }
                else
                {
                    // 🔹 لم يختَر أي إجابة
                    attempt.SelectedAnswer = null;
                    attempt.IsCorrect = false;
                }

                attempt.AttemptedAt = DateTime.Now;
                _context.SaveChanges();
            }


            return RedirectToAction("Solve", new { sessionId, index = nextIndex });
        }

        public IActionResult Result(int sessionId)
        {
            var attempts = _context.PromoExamAttempts
                .Include(a => a.Question)
                .Where(a => a.SessionId == sessionId)
                .ToList();

            if (!attempts.Any())
                return RedirectToAction("Welcome");

            var total = attempts.Count;
            var correct = attempts.Count(a => a.IsCorrect);
            var wrong = attempts.Count(a => !string.IsNullOrEmpty(a.SelectedAnswer) && !a.IsCorrect);
            var skipped = attempts.Count(a => string.IsNullOrEmpty(a.SelectedAnswer));

            // ✅ التحقق من وجود نتيجة سابقة
            var existingResult = _context.PromoExamResults
                .FirstOrDefault(r => r.SessionId == sessionId);

            if (existingResult == null)
            {
                var result = new PromoExamResult
                {
                    SessionId = sessionId,
                    TotalQuestions = total,
                    CorrectAnswers = correct,
                    WrongAnswers = wrong,
                    SkippedQuestions = skipped,
                    SubmittedAt = DateTime.Now
                };

                _context.PromoExamResults.Add(result);
                _context.SaveChanges();
            }
            else
            {
                existingResult.TotalQuestions = total;
                existingResult.CorrectAnswers = correct;
                existingResult.WrongAnswers = wrong;
                existingResult.SkippedQuestions = skipped;
                existingResult.SubmittedAt = DateTime.Now;
                _context.SaveChanges();
            }

            // ✅ تجهيز ViewModel للعرض
            var vm = new PromoExamResultViewModel
            {
                SessionId = sessionId,
                TotalQuestions = total,
                CorrectAnswers = correct,
                WrongAnswers = wrong,
                SkippedQuestions = skipped,
                ScorePercent = Math.Round(((double)correct / Math.Max(total, 1)) * 100, 2)
            };

            return View("Result", vm);
        }

        // نموذج إرسال البيانات للحصول على التقرير
        [HttpGet]
        public IActionResult RequestReport(int sessionId)
        {
            var vm = new PromoLeadInputViewModel { SessionId = sessionId };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RequestReport(PromoLeadInputViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                Console.WriteLine("❌ ModelState غير صالح");
                return View(vm);
            }

            // ✅ تأكد أن SessionId و FullName فيها قيم
            Console.WriteLine($"📩 RequestReport POST => SessionId={vm.SessionId}, Name={vm.FullName}, Phone={vm.PhoneNumber}");

            var lead = new PromoLead
            {
                SessionId = vm.SessionId,
                FullName = vm.FullName,
                PhoneNumber = vm.PhoneNumber,
                WhatsAppNumber = vm.WhatsAppNumber,
                CreatedAt = DateTime.Now
            };

            _context.PromoLeads.Add(lead);
            _context.SaveChanges();

            return RedirectToAction("ThankYou");
        }

        public IActionResult ThankYou() => View();
    }
}
