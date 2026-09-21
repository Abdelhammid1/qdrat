using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QdratNew.Data;
using QdratNew.DTOs.Exams;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Enums.Abstractions;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Interfaces;
using QdratNew.Services.StudentProgress;
using QdratNew.ViewModels.Students;
using System.Text.Json;

namespace QdratNew.Areas.Students.Controllers
{
    [Area("Students")]
    public class StudentExamFlowController : StudentBaseController
    {
        private readonly IExamAttendanceTracker _examAttendanceTracker;
        private readonly IStudentProgressService _studentProgressService;
        private readonly IExamResultEngine _examResultEngine;
        private readonly ITimeZoneService _timeZoneService;
        private readonly ILogger<StudentExamFlowController> _logger;
        private readonly IIntegrityGuardService _integrityGuard;

        public StudentExamFlowController(
       IDbContextFactory<ApplicationDbContext> contextFactory,
       UserManager<ApplicationUser> userManager,
       IExamAttendanceTracker examAttendanceTracker, IStudentProgressService studentProgressService, IExamResultEngine examResultEngine, ITimeZoneService timeZoneService,
       ILogger<StudentExamFlowController> logger,
       IIntegrityGuardService integrityGuard
   ) : base(contextFactory, userManager)
        {
            _examAttendanceTracker = examAttendanceTracker;
            _studentProgressService = studentProgressService;
            _examResultEngine = examResultEngine;
            _timeZoneService = timeZoneService;
            _logger = logger;
            _integrityGuard = integrityGuard;
        }


        // =====================================================
        // StartExam (نقل مباشر – بدون أي منطق غير موجود)
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> StartExam(int examAssignmentId, Guid? q = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (await _integrityGuard.IsBlockedAsync(StudentId, IntegrityAttemptType.Exam, examAssignmentId))
                return RedirectToAction("Blocked", "IntegrityGuard", new { area = "Students", attemptType = (int)IntegrityAttemptType.Exam, attemptEntityId = examAssignmentId, returnUrl = $"{Request.Path}{Request.QueryString}" });

            // ============================
            // 1) جلب Assignment + Exam
            // ============================
            var assignment = await _context.ExamAssignmentsToBatches
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == examAssignmentId);

            if (assignment == null || assignment.Exam == null)
                return RedirectToAction("ErrorMessage", new { msg = "لم يتم العثور على بيانات الاختبار." });

            // ============================
            // 2) تحقق الاختبارات الحضورية
            // ============================
            if (assignment.IsInLab)
            {
                var verifiedKey = $"VerifiedGeneralExam_{examAssignmentId}";
                if (HttpContext.Session.GetString(verifiedKey) != "true")
                    return RedirectToAction("VerifyGeneralExamCode", new { id = examAssignmentId });
            }

            // ============================
            // 3) جلب الطالب الحالي
            // ============================
            var studentId = StudentId;

            // ============================
            // 4) تسجيل الحضور
            // ============================
            await _examAttendanceTracker
                .MarkStudentPresentAsync(examAssignmentId, studentId);

            // ============================
            // 5) جلب / إنشاء حالة الطالب
            // ============================
            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentId == examAssignmentId);

            var now = DateTime.UtcNow;

            if (status == null)
            {
                status = new ExamStudentStatus
                {
                    StudentId = studentId,
                    ExamId = assignment.Exam.Id,
                    ExamAssignmentId = examAssignmentId,
                    StartedAt = now,
                    EndAt = now.AddMinutes(assignment.DurationMinutes),
                    Status = ExamStatus.InProgress,
                    IsSubmitted = false
                };

                _context.ExamStudentStatuses.Add(status);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Migration-safe logic
                if (!status.StartedAt.HasValue)
                    status.StartedAt = now;

                if (!status.EndAt.HasValue)
                {
                    status.EndAt = status.StartedAt.Value
                        .AddMinutes(assignment.DurationMinutes);

                    await _context.SaveChangesAsync();
                }
            }

            // ============================
            // 6) حالات الانتهاء → توجيه لصفحة النتيجة (لا حظر نهائي)
            // ============================
            if (status.IsSubmitted || status.Status == ExamStatus.Completed)
            {
                return RedirectToAction("ExamResult", "StudentExamResults",
                    new { area = "Students", id = examAssignmentId });
            }

            if (now >= status.EndAt.Value)
            {
                // انتهت مدة المحاولة دون تسليم صريح → إنهاء تلقائي بما تم الإجابة عليه
                try
                {
                    await _examResultEngine.FinalizeAsync(new ExamResultContext
                    {
                        StudentId = studentId,
                        ExamAssignmentId = examAssignmentId,
                        Kind = ExamKind.General
                    }, reviewSeconds: 0);
                }
                catch { }

                return RedirectToAction("ExamResult", "StudentExamResults",
                    new { area = "Students", id = examAssignmentId });
            }

            // ============================
            // 7) الوقت المتبقي
            // ============================
            var remainingSeconds = Math.Max(
                0,
                (int)(status.EndAt.Value - now).TotalSeconds
            );

            // ============================
            // 8) تحميل الأسئلة + Session
            // ============================
            // ============================
            // تحميل الأسئلة (Safe Fallback)
            // ============================
            var questionIds = await EnsureStudentQuestionOrderAsync(
                _context,
                studentId,
                examAssignmentId,
                assignment.Exam.Id,
                isIndividual: false);

            if (!questionIds.Any())
                return RedirectToAction("ErrorMessage", new { msg = "⚠️ لا توجد أسئلة لهذا الاختبار." });

            var sessionKey = $"ExamQuestions_{examAssignmentId}_{studentId}";
            HttpContext.Session.SetString(
                sessionKey,
                JsonSerializer.Serialize(questionIds)
            );

            // ============================
            // 9) السؤال الحالي
            // ============================
            var currentQuestion = q.HasValue && questionIds.Any(x => x == q.Value)
                ? q.Value
                : questionIds.First();

            // ============================
            // 10) بناء ViewModel كما كان
            // ============================
            var vm = await GetExamSolveViewModel(
                examAssignmentId,
                currentQuestion,
                studentId
            );

            vm.ExamId = assignment.Exam.Id;
            vm.ExamTitle = assignment.Exam.Title;
            vm.RemainingSeconds = remainingSeconds;
            vm.IsIndividual = false;
            vm.SubmitAnswerUrl = "/Students/StudentExamFlow/SubmitExamAnswerFetch";
            vm.FinalSubmitUrl  = "/Students/StudentExamFlow/SubmitFinalExam";
            vm.ExamType        = "General";

            // ============================
            // 11) منع الكاش
            // ============================
            Response.Headers["Cache-Control"] = "no-cache,no-store,must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return View("StartExam", vm);
        }


        [HttpGet]
        public async Task<IActionResult> StartIndividualExam(int examAssignmentId, Guid? q = null)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = StudentId;

            // ======================================================
            // 1) تحميل بيانات التكليف الفردي + الامتحان
            // ======================================================
            var assignment = await _context.ExamAssignmentsToStudents
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a =>
                    a.Id == examAssignmentId &&
                    a.StudentId == studentId);

            if (assignment == null || assignment.Exam == null)
                return RedirectToAction("ErrorMessage", new { msg = "لا يمكن بدء هذا الاختبار." });

            var exam = assignment.Exam;

            // ======================================================
            // 2) التحقق بالكود المرجعي (فقط إذا كان موجودًا فعليًا)
            // ======================================================
            bool requireReferenceCode =
                !string.IsNullOrWhiteSpace(exam.ReferenceCode);

            if (requireReferenceCode)
            {
                var verifiedKey = $"VerifiedGeneralExam_{examAssignmentId}";
                if (HttpContext.Session.GetString(verifiedKey) != "true")
                {
                    return RedirectToAction("VerifyGeneralExamCode", new { id = examAssignmentId });
                }
            }

            // ======================================================
            // 3) تحميل حالة الطالب في هذا الامتحان
            // ExamAssignmentId == null للاختبار الفردي
            // ======================================================
            var status = await _context.ExamStudentStatuses
         .FirstOrDefaultAsync(s =>
             s.StudentId == studentId &&
             s.ExamAssignmentToStudentId == examAssignmentId &&
             s.ExamId == assignment.ExamId
         );



            // ملاحظة:
            // لا يتم منع الدخول من StartIndividualExam
            // المنع النهائي يتم فقط من SubmitFinalExam

            // ======================================================
            // 3.5) إذا كان الاختبار منتهي → توجيه للنتيجة
            // ======================================================
            if (status != null &&
                status.IsSubmitted &&
                status.Status == ExamStatus.Completed)
            {
                return RedirectToAction(
                    "ExamResult",
                    "StudentExamResults",
                    new
                    {
                        area = "Students",
                        id = examAssignmentId
                    }
                );
            }


            // أول مرة يبدأ الامتحان
            if (status == null)
            {
                status = new ExamStudentStatus
                {
                    StudentId = studentId,
                    ExamId = exam.Id,
                    ExamAssignmentId = null,
                    ExamAssignmentToStudentId = examAssignmentId,
                    StartedAt = DateTime.UtcNow,
                    Status = ExamStatus.InProgress,
                    IsSubmitted = false
                };


                _context.ExamStudentStatuses.Add(status);
                await _context.SaveChangesAsync();
            }

            // ======================================================
            // 4) حساب الوقت المتبقي
            // ======================================================
            var elapsed = DateTime.UtcNow - (status.StartedAt ?? DateTime.UtcNow);
            var remainingSeconds = Math.Max(
                0,
                (assignment.DurationMinutes * 60) - (int)elapsed.TotalSeconds
            );

            // ======================================================
            // 5) تحميل أسئلة الامتحان
            // ======================================================
            var questionIds = await EnsureStudentQuestionOrderAsync(
                _context,
                studentId,
                examAssignmentId,
                exam.Id,
                isIndividual: true);

            if (!questionIds.Any())
                return RedirectToAction(
                    "ErrorMessage",
                    new { msg = "⚠️ لا توجد أسئلة لهذا الاختبار." }
                );

            var sessionKey = $"IndividualExam_{examAssignmentId}_{studentId}";
            HttpContext.Session.SetString(
                sessionKey,
                JsonSerializer.Serialize(questionIds)
            );

            // ======================================================
            // 6) تحديد السؤال الحالي
            // ======================================================
            var currentQuestion = q.HasValue && questionIds.Any(x => x == q.Value)
                ? q.Value
                : questionIds.First();

            var vm = await GetExamSolveViewModel(
                examAssignmentId,
                currentQuestion,
                studentId
            );

            vm.ExamId = exam.Id;
            vm.ExamTitle = exam.Title;
            vm.RemainingSeconds = remainingSeconds;
            vm.IsIndividual = true;
            vm.SubmitAnswerUrl = "/Students/StudentExamFlow/SubmitExamAnswerFetch";
            vm.FinalSubmitUrl  = "/Students/StudentExamFlow/SubmitFinalExam";
            vm.ExamType        = "General";

            // ======================================================
            // 7) منع الكاش
            // ======================================================
            Response.Headers["Cache-Control"] = "no-cache,no-store,must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return View("StartExam", vm);
        }


        // =====================================================
        // إعادة محاولة اختبار عام/دفعة — يحتفظ بأفضل درجة سابقة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> RetakeExam(int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = StudentId;

            var assignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a => a.Id == examAssignmentId);

            if (assignment == null)
                return RedirectToAction("ErrorMessage", new { msg = "لم يتم العثور على بيانات الاختبار." });

            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentId == examAssignmentId);

            if (status == null || !status.IsSubmitted || status.Status != ExamStatus.Completed)
                return RedirectToAction("ErrorMessage", new { msg = "لا يمكن إعادة محاولة اختبار لم يتم إكماله بعد." });

            var now = DateTime.UtcNow;

            status.StartedAt = now;
            status.EndAt = now.AddMinutes(assignment.DurationMinutes);
            status.IsSubmitted = false;
            status.Status = ExamStatus.InProgress;
            status.SubmittedAt = null;
            status.ReviewSeconds = 0;
            status.ReviewBehaviorJson = null;
            status.AttemptCount += 1;

            var previousAttempts = _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.ExamAssignmentId == examAssignmentId);
            _context.QuestionAttemptNew.RemoveRange(previousAttempts);

            await _context.SaveChangesAsync();

            HttpContext.Session.Remove($"ExamReviewMarked_{examAssignmentId}_{studentId}");

            return RedirectToAction("StartExam", new { examAssignmentId });
        }

        // =====================================================
        // إعادة محاولة اختبار فردي — يحتفظ بأفضل درجة سابقة
        // =====================================================
        [HttpGet]
        public async Task<IActionResult> RetakeIndividualExam(int examAssignmentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = StudentId;

            var assignment = await _context.ExamAssignmentsToStudents
                .FirstOrDefaultAsync(a => a.Id == examAssignmentId && a.StudentId == studentId);

            if (assignment == null)
                return RedirectToAction("ErrorMessage", new { msg = "لا يمكن إعادة محاولة هذا الاختبار." });

            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentToStudentId == examAssignmentId);

            if (status == null || !status.IsSubmitted || status.Status != ExamStatus.Completed)
                return RedirectToAction("ErrorMessage", new { msg = "لا يمكن إعادة محاولة اختبار لم يتم إكماله بعد." });

            status.StartedAt = DateTime.UtcNow;
            status.IsSubmitted = false;
            status.Status = ExamStatus.InProgress;
            status.SubmittedAt = null;
            status.ReviewSeconds = 0;
            status.ReviewBehaviorJson = null;
            status.AttemptCount += 1;

            var previousAttempts = _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.ExamAssignmentToStudentId == examAssignmentId);
            _context.QuestionAttemptNew.RemoveRange(previousAttempts);

            await _context.SaveChangesAsync();

            HttpContext.Session.Remove($"ExamReviewMarked_Individual_{examAssignmentId}_{studentId}");

            return RedirectToAction("StartIndividualExam", new { examAssignmentId });
        }


        [HttpGet]
        public IActionResult ErrorMessage(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg))
                msg = "حدث خطأ غير متوقع أثناء معالجة الطلب.";

            ViewBag.ErrorMessage = msg;

            // منع التخزين في المتصفح
            Response.Headers["Cache-Control"] = "no-cache,no-store,must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return View();
        }



        [HttpGet]
        public async Task<IActionResult> VerifyGeneralExamCode(int id)
        {
            using var _context = _contextFactory.CreateDbContext();

            // البحث أولاً في اختبارات الدُفعات
            var batchAssignment = await _context.ExamAssignmentsToBatches
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (batchAssignment != null)
                return View("~/Areas/Students/Views/StudentExamFlow/VerifyGeneralExamCode.cshtml", id);


            // البحث في الاختبارات الفردية
            var individualAssignment = await _context.ExamAssignmentsToStudents
                .Include(x => x.Exam)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (individualAssignment != null)
                return View("~/Areas/Students/Views/StudentExamFlow/VerifyGeneralExamCode.cshtml", id);

            return RedirectToAction("ErrorMessage", new { msg = "لم يتم العثور على الاختبار." });
        }


        [HttpPost]
        public async Task<IActionResult> VerifyExamPassword(int examId, string password)
        {
            using var _context = _contextFactory.CreateDbContext();

            var studentId = StudentId;

            var assignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a => a.ExamId == examId);

            if (assignment == null || string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "❌ كلمة المرور غير صالحة أو لا يوجد اختبار.";
                return RedirectToAction("Index");
            }

            // 🔐 تحقق من كلمة المرور (قارن مع كلمة المرور المخزنة في جدول الاختبارات أو اجعلها ثابتة مؤقتًا)
            if (password.Trim() != "qdrat2025") // مثال: كلمة مرور ثابتة أو ديناميكية
            {
                TempData["ErrorMessage"] = "❌ كلمة المرور غير صحيحة.";
                return RedirectToAction("Index");
            }

            // ✅ إعادة التوجيه إلى StartExam
            return RedirectToAction("StartExam", new { examAssignmentId = examId });
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyGeneralExamCode(int id, string code)
        {
            using var _context = _contextFactory.CreateDbContext();

            if (string.IsNullOrWhiteSpace(code))
                return RedirectToAction("ErrorMessage", new { msg = "لم يتم إدخال الكود." });

            code = code.Trim();

            // جلب الامتحان بالرقم المرجعي
            var exam = await _context.Exams
                .FirstOrDefaultAsync(e => e.ReferenceCode == code);

            if (exam == null)
                return RedirectToAction("ErrorMessage", new { msg = "❌ الكود غير صحيح." });

            // جلب الطالب

            var studentId = StudentId;



            // === محاولة إيجاد تكليف دُفعة
            var batchAssignment = await _context.ExamAssignmentsToBatches
                .FirstOrDefaultAsync(a => a.Id == id && a.ExamId == exam.Id);

            if (batchAssignment != null)
            {
                HttpContext.Session.SetString($"VerifiedGeneralExam_{batchAssignment.Id}", "true");
                return RedirectToAction("StartExam", new { examAssignmentId = batchAssignment.Id });
            }

            // === محاولة إيجاد تكليف فردي للطالب
            var individualAssignment = await _context.ExamAssignmentsToStudents
                .FirstOrDefaultAsync(a => a.Id == id
                                          && a.ExamId == exam.Id
                                          && a.StudentId == studentId);

            if (individualAssignment != null)
            {
                HttpContext.Session.SetString($"VerifiedGeneralExam_{individualAssignment.Id}", "true");
                return RedirectToAction("StartIndividualExam", new { examAssignmentId = individualAssignment.Id });
            }

            // لا يوجد أي تكليف مرتبط بهذا الكود
            return RedirectToAction("ErrorMessage", new { msg = "❌ هذا الكود لا يخصك أو لا يخص هذا الاختبار." });
        }



        private async Task<List<Guid>> EnsureStudentQuestionOrderAsync(
            ApplicationDbContext context,
            int studentId,
            int examAssignmentId,
            int? examId,
            bool isIndividual)
        {
            List<Guid> savedOrder;

            if (isIndividual)
            {
                savedOrder = await context.ExamStudentQuestionOrders
                    .AsNoTracking()
                    .Where(x =>
                        x.StudentId == studentId &&
                        x.ExamAssignmentToStudentId == examAssignmentId &&
                        x.ExamAssignmentId == null)
                    .OrderBy(x => x.OrderNumber)
                    .Select(x => x.QuestionId)
                    .ToListAsync();
            }
            else
            {
                savedOrder = await context.ExamStudentQuestionOrders
                    .AsNoTracking()
                    .Where(x =>
                        x.StudentId == studentId &&
                        x.ExamAssignmentId == examAssignmentId &&
                        x.ExamAssignmentToStudentId == null)
                    .OrderBy(x => x.OrderNumber)
                    .Select(x => x.QuestionId)
                    .ToListAsync();
            }

            if (savedOrder.Any())
                return savedOrder;

            List<Guid> questionIds;

            if (isIndividual)
            {
                questionIds = await context.ExamQuestions
                    .AsNoTracking()
                    .Where(eq => eq.ExamAssignmentToStudentId == examAssignmentId)
                    .OrderBy(eq => eq.Order)
                    .Select(eq => eq.QuestionId)
                    .ToListAsync();
            }
            else
            {
                questionIds = await context.ExamQuestions
                    .AsNoTracking()
                    .Where(eq => eq.ExamAssignmentId == examAssignmentId)
                    .OrderBy(eq => eq.Order)
                    .Select(eq => eq.QuestionId)
                    .ToListAsync();
            }

            if (!questionIds.Any() && examId.HasValue)
            {
                questionIds = await context.ExamQuestions
                    .AsNoTracking()
                    .Where(eq => eq.ExamId == examId.Value)
                    .OrderBy(eq => eq.Order)
                    .Select(eq => eq.QuestionId)
                    .ToListAsync();
            }

            if (!questionIds.Any())
                return questionIds;

            bool shouldRandomize = true;
            if (examId.HasValue)
            {
                shouldRandomize = await context.Exams
                    .AsNoTracking()
                    .Where(e => e.Id == examId.Value)
                    .Select(e => e.RandomizeQuestions)
                    .FirstOrDefaultAsync();
            }

            if (shouldRandomize)
                questionIds = ShuffleList(questionIds);

            var now = DateTime.UtcNow;
            var rows = questionIds
                .Select((questionId, index) => new ExamStudentQuestionOrder
                {
                    StudentId = studentId,
                    ExamAssignmentId = isIndividual ? null : examAssignmentId,
                    ExamAssignmentToStudentId = isIndividual ? examAssignmentId : null,
                    ExamId = examId,
                    QuestionId = questionId,
                    OrderNumber = index + 1,
                    CreatedAt = now
                })
                .ToList();

            context.ExamStudentQuestionOrders.AddRange(rows);
            await context.SaveChangesAsync();

            return questionIds;
        }

        private static List<T> ShuffleList<T>(List<T> source)
        {
            var random = new Random();

            for (int i = source.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (source[i], source[j]) = (source[j], source[i]);
            }

            return source;
        }



        private async Task<ExamSolveViewModel> GetExamSolveViewModel(
      int examAssignmentId,
      Guid questionId,
      int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            bool isIndividual = await _context.ExamAssignmentsToStudents
                .AnyAsync(x => x.Id == examAssignmentId && x.StudentId == studentId);

            string questionsKey = isIndividual
                ? $"IndividualExam_{examAssignmentId}_{studentId}"
                : $"ExamQuestions_{examAssignmentId}_{studentId}";

            // =========================
            // 1) تحميل IDs فقط (من Session أو الترتيب المحفوظ)
            // =========================
            List<Guid> allQuestionIds;

            if (HttpContext.Session.TryGetValue(questionsKey, out var raw))
            {
                allQuestionIds = JsonSerializer.Deserialize<List<Guid>>(raw) ?? new List<Guid>();
            }
            else
            {
                int? examId;

                if (isIndividual)
                {
                    examId = await _context.ExamAssignmentsToStudents
                        .AsNoTracking()
                        .Where(a => a.Id == examAssignmentId && a.StudentId == studentId)
                        .Select(a => (int?)a.ExamId)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    examId = await _context.ExamAssignmentsToBatches
                        .AsNoTracking()
                        .Where(a => a.Id == examAssignmentId)
                        .Select(a => a.ExamId)
                        .FirstOrDefaultAsync();
                }

                allQuestionIds = await EnsureStudentQuestionOrderAsync(
                    _context,
                    studentId,
                    examAssignmentId,
                    examId,
                    isIndividual);

                HttpContext.Session.SetString(
                    questionsKey,
                    JsonSerializer.Serialize(allQuestionIds));
            }

            if (!allQuestionIds.Any())
                throw new Exception("No questions found.");

            int index = allQuestionIds.IndexOf(questionId);
            if (index < 0) index = 0;
            questionId = allQuestionIds[index];

            // =========================
            // 2) تحميل السؤال الحالي فقط
            // =========================
            var question = await _context.Questions
                .Include(q => q.Options)
                .Include(q => q.VerbalPassage)
                .Include(q => q.Lesson)
                    .ThenInclude(l => l.Section)
                            .ThenInclude(s => s.Curriculum)
                .AsNoTracking()
                .FirstOrDefaultAsync(q => q.Id == questionId);

            if (question == null)
                throw new Exception("Question not found.");

            var vmQuestion = question.ToDisplayModel();
            vmQuestion.IsRTL = question.Lesson?.Section?.Curriculum?.IsRTL ?? true;
            // =========================
            // 3) محاولة الطالب (السؤال الحالي)
            // =========================
            var attempt = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    a.QuestionId == questionId &&
                    (
                        (!isIndividual && a.ExamAssignmentId == examAssignmentId) ||
                        (isIndividual && a.ExamAssignmentToStudentId == examAssignmentId)
                    ))
                .OrderByDescending(a => a.AttemptedAt)
                .FirstOrDefaultAsync();

            if (attempt != null)
            {
                foreach (var opt in vmQuestion.Options)
                    opt.IsSelected = opt.Text == attempt.SelectedAnswer;
            }

            // =========================
            // 4) الأسئلة المجابة (جميعها مرة واحدة)
            // =========================
            var answeredIds = await _context.QuestionAttemptNew
                .AsNoTracking()
                .Where(a =>
                    a.StudentId == studentId &&
                    (
                        (!isIndividual && a.ExamAssignmentId == examAssignmentId) ||
                        (isIndividual && a.ExamAssignmentToStudentId == examAssignmentId)
                    ))
                .Select(a => a.QuestionId)
                .Distinct()
                .ToListAsync();

            var answeredSet = answeredIds.ToHashSet();

            var answeredMap = allQuestionIds.ToDictionary(
                q => q,
                q => answeredSet.Contains(q)
            );

            // =========================
            // 5) Review Flags (Session فقط)
            // =========================
            string reviewKey = isIndividual
                ? $"ExamReviewMarked_Individual_{examAssignmentId}_{studentId}"
                : $"ExamReviewMarked_{examAssignmentId}_{studentId}";

            var reviewList = HttpContext.Session.TryGetValue(reviewKey, out var rv)
                ? JsonSerializer.Deserialize<List<Guid>>(rv)!
                : new List<Guid>();

            // =========================
            // 6) بناء الـ ViewModel
            // =========================
            return new ExamSolveViewModel
            {
                ExamAssignmentId = examAssignmentId,
                CurrentQuestionId = questionId,
                AllQuestionIds = allQuestionIds,
                Question = vmQuestion,
                SelectedAnswer = attempt?.SelectedAnswer ?? "",
                IsIndividual = isIndividual,

                // ⭐⭐ المطلوب لتلوين الأزرار
                AnsweredQuestions = answeredMap,
                ReviewMarkedIds = reviewList,

                // ⚠️ لا نلمس المؤقت هنا
                QuestionStartTime = DateTime.UtcNow
            };

        }
        private string NormalizeDigits(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            var map = new Dictionary<char, char>
            {
                ['٠'] = '0',
                ['١'] = '1',
                ['٢'] = '2',
                ['٣'] = '3',
                ['٤'] = '4',
                ['٥'] = '5',
                ['٦'] = '6',
                ['٧'] = '7',
                ['٨'] = '8',
                ['٩'] = '9'
            };

            return new string(input.Select(c => map.ContainsKey(c) ? map[c] : c).ToArray());
        }


        [HttpPost]
        public async Task<IActionResult> SubmitExamAnswerFetch(
      int id,
      Guid q,
      string nav,
      string? SelectedOption,
      int? timeTakenSeconds = null)
        {
            using var _context = _contextFactory.CreateDbContext();
            HttpContext.RequestAborted.ThrowIfCancellationRequested();

            try
            {
                // ============================
                // 🔑 الطالب (من StudentBaseController)
                // ============================
                var studentId = StudentId;

                // ======================================================
                // 🔐 تثبيت وقت السؤال (إجباري)
                // ======================================================
                int safeTimeTaken = (timeTakenSeconds.HasValue && timeTakenSeconds.Value > 0)
                    ? timeTakenSeconds.Value
                    : 1;

                // كشف التلاعب الزمني
                var _qStartKey = $"QStart_{id}_{studentId}_{q}";
                var _qStartStr = HttpContext.Session.GetString(_qStartKey);
                if (!string.IsNullOrEmpty(_qStartStr) && DateTime.TryParse(_qStartStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var _qStartTime))
                {
                    double serverTimeTaken = (DateTime.UtcNow - _qStartTime).TotalSeconds;
                    if (Math.Abs(serverTimeTaken - safeTimeTaken) > 30)
                    {
                        _context.SuspiciousActivities.Add(new SuspiciousActivity
                        {
                            StudentId = studentId,
                            ExamContextId = id,
                            QuestionId = q,
                            ClientTimeTakenSeconds = safeTimeTaken,
                            ServerTimeTakenSeconds = serverTimeTaken,
                            ExamType = "General",
                            DetectedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                // ======================================================
                // 🔍 تحديد نوع الامتحان
                // ======================================================
                bool isIndividual = await _context.ExamAssignmentsToStudents
                    .AnyAsync(x => x.Id == id && x.StudentId == studentId);

                // ======================================================
                // 🟦 تحميل الأسئلة (IDs فقط)
                // ======================================================
                int? examId;

                if (isIndividual)
                {
                    examId = await _context.ExamAssignmentsToStudents
                        .AsNoTracking()
                        .Where(a => a.Id == id && a.StudentId == studentId)
                        .Select(a => (int?)a.ExamId)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    examId = await _context.ExamAssignmentsToBatches
                        .AsNoTracking()
                        .Where(a => a.Id == id)
                        .Select(a => a.ExamId)
                        .FirstOrDefaultAsync();
                }

                List<Guid> allQuestions = await EnsureStudentQuestionOrderAsync(
                    _context,
                    studentId,
                    id,
                    examId,
                    isIndividual);

                if (!allQuestions.Any())
                    return Json(new { success = false, message = "⚠️ لا توجد أسئلة للامتحان." });

                // ✅ رفض أي سؤال ليس ضمن أسئلة هذا التكليف تحديدًا
                if (!allQuestions.Contains(q))
                {
                    _logger.LogWarning(
                        "SubmitExamAnswerFetch: محاولة تسجيل سؤال {QuestionId} غير منتمٍ لتكليف {AssignmentId} " +
                        "(طالب {StudentId}) — تم الرفض.", q, id, studentId);

                    return Json(new { success = false, message = "⚠️ هذا السؤال لا ينتمي لهذا الاختبار." });
                }

                // ======================================================
                // 🟩 حفظ الإجابة + الوقت
                // ======================================================
                if (!string.IsNullOrWhiteSpace(SelectedOption))
                {
                    var question = await _context.Questions
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.Id == q);

                    if (question != null)
                    {
                        bool isCorrect = string.Equals(
                            SelectedOption.Trim(),
                            question.CorrectAnswer?.Trim(),
                            StringComparison.OrdinalIgnoreCase);

                        var attempt = await _context.QuestionAttemptNew
                                     .Where(a =>
                                         a.StudentId == studentId &&
                                         a.QuestionId == q &&
                                         (
                                             (!isIndividual && a.ExamAssignmentId == id) ||
                                             (isIndividual && a.ExamAssignmentToStudentId == id)
                                         ))
                                     .OrderByDescending(a => a.AttemptedAt)
                                     .FirstOrDefaultAsync();


                        if (attempt == null)
                        {
                            attempt = new QuestionAttemptNew
                            {
                                StudentId = studentId,
                                QuestionId = q,
                                SelectedAnswer = SelectedOption,
                                IsCorrect = isCorrect,
                                AttemptedAt = DateTime.UtcNow,
                                TimeTakenSeconds = safeTimeTaken,
                                LessonId = question.LessonId,
                                SectionId = question.SectionId,
                                ExamAssignmentId = isIndividual ? null : id,
                                ExamAssignmentToStudentId = isIndividual ? id : null
                            };

                            _context.QuestionAttemptNew.Add(attempt);
                        }
                        else
                        {
                            attempt.SelectedAnswer = SelectedOption;
                            attempt.IsCorrect = isCorrect;
                            attempt.AttemptedAt = DateTime.UtcNow;
                            attempt.TimeTakenSeconds = safeTimeTaken;
                            attempt.ExamAssignmentId = isIndividual ? null : id;
                            attempt.ExamAssignmentToStudentId = isIndividual ? id : null;
                        }

                        await _context.SaveChangesAsync();
                    }
                }

                // ======================================================
                // 🟧 علامة المراجعة
                // ======================================================
                if (nav == "review")
                {
                    string key = isIndividual
                        ? $"ExamReviewMarked_Individual_{id}_{studentId}"
                        : $"ExamReviewMarked_{id}_{studentId}";

                    var existing = HttpContext.Session.GetString(key);
                    var list = string.IsNullOrWhiteSpace(existing)
                        ? new List<Guid>()
                        : JsonSerializer.Deserialize<List<Guid>>(existing)!;

                    if (!list.Contains(q))
                        list.Add(q);

                    HttpContext.Session.SetString(key, JsonSerializer.Serialize(list));
                }

                // ======================================================
                // 🟥 عرض المراجعة
                // ======================================================
                if (nav == "showReview")
                {
                    var vm = await GetExamSolveViewModel(id, q, studentId);
                    vm.IsReviewMode = true;
                    vm.ForceReviewVisible = true;
                    vm.SubmitAnswerUrl = "/Students/StudentExamFlow/SubmitExamAnswerFetch";
                    vm.FinalSubmitUrl  = "/Students/StudentExamFlow/SubmitFinalExam";
                    vm.ExamType        = "General";
                    HttpContext.Session.SetString($"QStart_{id}_{studentId}_{q}", DateTime.UtcNow.ToString("O"));
                    return PartialView("_SolveUnifiedExamPartial", vm);
                }

                if (nav == "goto")
                {
                    Guid targetQ = Guid.Parse(Request.Form["target"]);
                    var gotoVm = await GetExamSolveViewModel(id, targetQ, studentId);
                    gotoVm.IsReviewMode = true;
                    gotoVm.ForceReviewVisible = true;
                    gotoVm.SubmitAnswerUrl = "/Students/StudentExamFlow/SubmitExamAnswerFetch";
                    gotoVm.FinalSubmitUrl  = "/Students/StudentExamFlow/SubmitFinalExam";
                    gotoVm.ExamType        = "General";
                    HttpContext.Session.SetString($"QStart_{id}_{studentId}_{targetQ}", DateTime.UtcNow.ToString("O"));
                    return PartialView("_SolveUnifiedExamPartial", gotoVm);
                }

                // ======================================================
                // 🟦 التنقل
                // ======================================================
                int currentIndex = allQuestions.IndexOf(q);
                if (currentIndex < 0) currentIndex = 0;
                int nextIndex = currentIndex;

                if (nav == "next" && currentIndex < allQuestions.Count - 1) nextIndex++;
                if (nav == "prev" && currentIndex > 0) nextIndex--;

                Guid nextQ = allQuestions[nextIndex];

                var nextVm = await GetExamSolveViewModel(id, nextQ, studentId);
                nextVm.IsReviewMode = false;
                nextVm.ForceReviewVisible = false;
                nextVm.SubmitAnswerUrl = "/Students/StudentExamFlow/SubmitExamAnswerFetch";
                nextVm.FinalSubmitUrl  = "/Students/StudentExamFlow/SubmitFinalExam";
                nextVm.ExamType        = "General";
                HttpContext.Session.SetString($"QStart_{id}_{studentId}_{nextQ}", DateTime.UtcNow.ToString("O"));
                return PartialView("_SolveUnifiedExamPartial", nextVm);
            }
            catch
            {
                return Json(new { success = false, message = "⚠️ حدث خطأ أثناء معالجة الإجابة." });
            }
        }




        [HttpPost]
        public async Task<IActionResult> SaveReviewBehavior(
            [FromBody] SaveReviewBehaviorDto dto)
        {
            using var _context = _contextFactory.CreateDbContext();
            var studentId = StudentId;

            var status = await _context.ExamStudentStatuses
                .FirstOrDefaultAsync(s =>
                    s.StudentId == studentId &&
                    (s.ExamAssignmentId == dto.ExamAssignmentId || s.ExamId == dto.ExamAssignmentId));

            if (status != null)
            {
                status.ReviewBehaviorJson = dto.ReviewNavLog;
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitFinalExam(
            int id,
            bool forceSubmit = false,
            int reviewSeconds = 0)
        {
            using var _context = _contextFactory.CreateDbContext();

            try
            {
                var studentId = StudentId;

                // ======================================================
                // 1) تحديد هل الاختبار فردي
                // ======================================================
                var individualAssignment = await _context.ExamAssignmentsToStudents
                    .AsNoTracking()
                    .Include(a => a.Exam)
                    .FirstOrDefaultAsync(a =>
                        a.Id == id &&
                        a.StudentId == studentId);

                bool isIndividual = individualAssignment != null;

                // ======================================================
                // 2) جلب / إنشاء ExamStudentStatus
                // ======================================================
                ExamStudentStatus status;

                if (isIndividual)
                {
                    var examId = individualAssignment!.ExamId;

                    status = await _context.ExamStudentStatuses
                        .FirstOrDefaultAsync(s =>
                            s.StudentId == studentId &&
                            s.ExamAssignmentToStudentId == id);

                    if (status == null)
                    {
                        status = new ExamStudentStatus
                        {
                            StudentId = studentId,
                            ExamId = examId,
                            ExamAssignmentId = null,
                            ExamAssignmentToStudentId = id,
                            StartedAt = DateTime.UtcNow,
                            Status = ExamStatus.InProgress,
                            IsSubmitted = false
                        };

                        _context.ExamStudentStatuses.Add(status);
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    status = await _context.ExamStudentStatuses
                        .FirstOrDefaultAsync(s =>
                            s.StudentId == studentId &&
                            s.ExamAssignmentId == id);

                    if (status == null)
                    {
                        status = new ExamStudentStatus
                        {
                            StudentId = studentId,
                            ExamAssignmentId = id,
                            StartedAt = DateTime.UtcNow,
                            Status = ExamStatus.InProgress,
                            IsSubmitted = false
                        };

                        _context.ExamStudentStatuses.Add(status);
                        await _context.SaveChangesAsync();
                    }
                }

                // ======================================================
                // 3) منع إعادة التسليم
                // ======================================================
                if (status.IsSubmitted && status.Status == ExamStatus.Completed)
                {
                    return Json(new
                    {
                        success = true,
                        redirectUrl = Url.Action(
                            "ExamResult",
                            "StudentExamResults",
                            new
                            {
                                area = "Students",
                                id = id,
                                source = isIndividual ? "individual" : "batch"
                            })
                    });
                }

                // ======================================================
                // 4) التنفيذ النهائي (Engine)
                // ======================================================
                var resultContext = new ExamResultContext
                {
                    StudentId = studentId,
                    ExamAssignmentId = isIndividual ? null : id,
                    ExamAssignmentToStudentId = isIndividual ? id : null,
                    Kind = ExamKind.General
                };

                await _examResultEngine.FinalizeAsync(resultContext, reviewSeconds);

                // ======================================================
                // 6) تسجيل التقدم (غير حرج)
                // ======================================================
                try
                {
                    await _studentProgressService.RecordExamProgress(studentId, id);
                }
                catch { }

                // ======================================================
                // 7) منع الكاش + التوجيه
                // ======================================================
                Response.Headers["Cache-Control"] = "no-cache,no-store,must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";

                return Json(new
                {
                    success = true,
                    redirectUrl = Url.Action(
                        "ExamResult",
                        "StudentExamResults",
                        new
                        {
                            area = "Students",
                            id = id,
                            source = isIndividual ? "individual" : "batch"
                        })
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = $"⚠️ خطأ أثناء إتمام الاختبار: {ex.Message}"
                });
            }
        }






    }
}
