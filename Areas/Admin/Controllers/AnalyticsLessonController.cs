using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Analytics;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Route("Admin/Analytics/[action]")]
    [Authorize(Roles = "Owner,Developer")]
    public class AnalyticsLessonController : Controller
    {
        private readonly IBatchAnalyticsService _batchService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AnalyticsLessonController> _logger;

        public AnalyticsLessonController(
            IBatchAnalyticsService batchService,
            ApplicationDbContext context,
            ILogger<AnalyticsLessonController> logger)
        {
            _batchService = batchService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> WeakLessonDetails(
            int lessonId, int? curriculumId, int? batchId, int? instructorId)
        {
            var vm = await _batchService.BuildWeakLessonAsync(lessonId, curriculumId, batchId, instructorId);

            if (vm == null) return NotFound();

            return View("~/Areas/Admin/Views/Analytics/WeakLessonDetails.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendWeakLessonGuidanceToInstructor(
            int lessonId, int? batchId, int? instructorId)
        {
            var errorCode = $"CQ-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..22].ToUpper();

            try
            {
                if (lessonId <= 0)
                {
                    TempData["Error"] = "لم يتم تحديد الدرس بشكل صحيح.";
                    TempData["ErrorCode"] = errorCode;
                    return RedirectToAction("AdvancedDashboard", "AnalyticsDashboard");
                }

                var lesson = await _context.Set<Lesson>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == lessonId);

                if (lesson == null)
                {
                    TempData["Error"] = "الدرس المحدد غير موجود.";
                    TempData["ErrorCode"] = errorCode;
                    return RedirectToAction("AdvancedDashboard", "AnalyticsDashboard");
                }

                // ── تحليل محاولات الدرس لهذه الدفعة ──────────────────────────
                IQueryable<QuestionAttemptNew> attemptsQuery = _context.QuestionAttemptNew
                    .AsNoTracking()
                    .Where(x => x.LessonId == lessonId);

                var resolvedBatchId = batchId ?? 0;
                var resolvedInstructorId = instructorId ?? 0;

                if (resolvedBatchId > 0)
                {
                    var batchStudentIds = _context.Set<StudentBatchEnrollment>()
                        .Where(x => x.BatchId == resolvedBatchId)
                        .Select(x => x.StudentID);

                    attemptsQuery = attemptsQuery.Where(x => batchStudentIds.Contains(x.StudentId));
                }

                var attemptsDb = await attemptsQuery.ToListAsync();

                // ── حل الدفعة إذا لم تُحدد ────────────────────────────────────
                if (resolvedBatchId <= 0)
                {
                    var firstStudentId = attemptsDb.Select(x => x.StudentId).FirstOrDefault();
                    if (firstStudentId > 0)
                    {
                        var enroll = await _context.Set<StudentBatchEnrollment>()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.StudentID == firstStudentId);
                        if (enroll != null) resolvedBatchId = enroll.BatchId;
                    }
                }

                if (resolvedBatchId <= 0)
                {
                    TempData["Error"] = "تعذر تحديد الدفعة المرتبطة بهذا الدرس.";
                    TempData["ErrorCode"] = errorCode;
                    return RedirectToAction("WeakLessonDetails", new { lessonId, batchId, instructorId });
                }

                // ── حل المدرب إذا لم يُحدد ────────────────────────────────────
                if (resolvedInstructorId <= 0)
                {
                    var icb = await _context.Set<InstructorCurriculumBatch>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(x => x.BatchId == resolvedBatchId);
                    if (icb != null) resolvedInstructorId = icb.InstructorId;
                }

                if (resolvedInstructorId <= 0)
                {
                    TempData["Error"] = "تعذر تحديد المدرب المرتبط بهذه الدفعة.";
                    TempData["ErrorCode"] = errorCode;
                    return RedirectToAction("WeakLessonDetails", new { lessonId, batchId = resolvedBatchId, instructorId });
                }

                var instructorExists = await _context.Set<Instructor>()
                    .AnyAsync(x => x.Id == resolvedInstructorId);

                if (!instructorExists)
                {
                    TempData["Error"] = "المدرب المحدد غير موجود.";
                    TempData["ErrorCode"] = errorCode;
                    return RedirectToAction("WeakLessonDetails", new { lessonId, batchId = resolvedBatchId, instructorId = resolvedInstructorId });
                }

                var batch = await _context.Set<Batch>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == resolvedBatchId);

                if (batch == null)
                {
                    TempData["Error"] = "الدفعة المحددة غير موجودة.";
                    TempData["ErrorCode"] = errorCode;
                    return RedirectToAction("WeakLessonDetails", new { lessonId, batchId = resolvedBatchId, instructorId = resolvedInstructorId });
                }

                var questionsDb = await _context.Set<Question>()
                    .AsNoTracking()
                    .Where(q => q.LessonId == lessonId)
                    .ToListAsync();

                var questionMap = questionsDb.ToDictionary(q => q.Id);

                var criticalItems = attemptsDb
                    .GroupBy(x => x.QuestionId)
                    .Select(g =>
                    {
                        questionMap.TryGetValue(g.Key, out var question);

                        var total = g.Count();
                        var wrong = g.Count(x => !x.IsCorrect);
                        var errPct = total == 0 ? 0 : Math.Round((wrong * 100.0) / total, 1);

                        return new
                        {
                            QuestionId = g.Key,
                            LessonId = lessonId,
                            SectionId = lesson.SectionId,
                            HomeworkSetId = g.Where(x => x.HomeworkSetId.HasValue).Select(x => x.HomeworkSetId).FirstOrDefault(),
                            ExamAssignmentId = g.Where(x => x.ExamAssignmentId.HasValue).Select(x => x.ExamAssignmentId).FirstOrDefault(),
                            ExamId = g.Where(x => x.ExamId.HasValue).Select(x => x.ExamId).FirstOrDefault(),
                            PerformanceIndicatorExamId = g.Where(x => x.PerformanceIndicatorExamId.HasValue).Select(x => x.PerformanceIndicatorExamId).FirstOrDefault(),
                            TotalAttempts = total,
                            WrongAttempts = wrong,
                            CorrectAttempts = g.Count(x => x.IsCorrect),
                            AffectedStudentsCount = g.Where(x => !x.IsCorrect).Select(x => x.StudentId).Distinct().Count(),
                            ErrorPercentage = errPct,
                            SourceType = g.Any(x => x.HomeworkSetId.HasValue) ? "Homework"
                                : g.Any(x => x.ExamAssignmentId.HasValue) ? "ExamAssignment"
                                : g.Any(x => x.ExamId.HasValue) ? "Exam"
                                : g.Any(x => x.PerformanceIndicatorExamId.HasValue) ? "PerformanceIndicatorExam"
                                : "Unknown",
                            QuestionTitleSnapshot = question?.Title ?? "سؤال غير معروف",
                            ReferenceNumberSnapshot = question?.ReferenceNumber ?? ""
                        };
                    })
                    .Where(x => x.ErrorPercentage >= 60 && x.WrongAttempts > 0)
                    .OrderByDescending(x => x.ErrorPercentage)
                    .ThenByDescending(x => x.WrongAttempts)
                    .ToList();

                if (!criticalItems.Any())
                {
                    TempData["Error"] = "لا توجد أسئلة حرجة تتجاوز نسبة الخطأ المحددة لهذا الدرس.";
                    TempData["ErrorCode"] = errorCode;
                    return RedirectToAction("WeakLessonDetails", new { lessonId, batchId = resolvedBatchId, instructorId = resolvedInstructorId });
                }

                var existingTask = await _context.InstructorCriticalQuestionTasks
                    .AsNoTracking()
                    .Where(x =>
                        x.InstructorId == resolvedInstructorId &&
                        x.BatchId == resolvedBatchId &&
                        x.LessonId == lessonId &&
                        x.Status == "Pending" &&
                        !x.IsReviewedByInstructor)
                    .FirstOrDefaultAsync();

                if (existingTask != null)
                {
                    TempData["Error"] = "توجد بالفعل مهمة مفتوحة لنفس المدرب والدفعة والدرس. لا يمكن تكرار الإرسال قبل مراجعة المهمة السابقة.";
                    TempData["ErrorCode"] = errorCode;
                    return RedirectToAction("WeakLessonDetails", new { lessonId, batchId = resolvedBatchId, instructorId = resolvedInstructorId });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var task = new InstructorCriticalQuestionTask
                    {
                        InstructorId = resolvedInstructorId,
                        BatchId = resolvedBatchId,
                        LessonId = lessonId,
                        CurriculumId = null,
                        SectionId = lesson.SectionId,
                        Title = $"مهمة معالجة الأسئلة الحرجة - {lesson.Title}",
                        AdminNote = "يرجى مراجعة الأسئلة التي ارتفعت فيها نسبة الخطأ، وإعادة شرحها للدفعة مع التركيز على الطلاب المتأثرين.",
                        CriticalQuestionsCount = criticalItems.Count,
                        AffectedStudentsCount = criticalItems.Sum(x => x.AffectedStudentsCount),
                        AverageErrorPercentage = Math.Round(criticalItems.Average(x => x.ErrorPercentage), 1),
                        IsSent = true,
                        IsReviewedByInstructor = false,
                        CreatedAt = DateTime.Now,
                        Status = "Pending"
                    };

                    await _context.InstructorCriticalQuestionTasks.AddAsync(task);
                    await _context.SaveChangesAsync();

                    var taskItems = criticalItems
                        .Select(x => new InstructorCriticalQuestionTaskItem
                        {
                            InstructorCriticalQuestionTaskId = task.Id,
                            QuestionId = x.QuestionId,
                            LessonId = x.LessonId,
                            SectionId = x.SectionId,
                            HomeworkSetId = x.HomeworkSetId,
                            ExamAssignmentId = x.ExamAssignmentId,
                            ExamId = x.ExamId,
                            PerformanceIndicatorExamId = x.PerformanceIndicatorExamId,
                            TotalAttempts = x.TotalAttempts,
                            WrongAttempts = x.WrongAttempts,
                            CorrectAttempts = x.CorrectAttempts,
                            AffectedStudentsCount = x.AffectedStudentsCount,
                            ErrorPercentage = x.ErrorPercentage,
                            SourceType = x.SourceType,
                            QuestionTitleSnapshot = x.QuestionTitleSnapshot,
                            ReferenceNumberSnapshot = x.ReferenceNumberSnapshot,
                            CreatedAt = DateTime.Now
                        })
                        .ToList();

                    if (taskItems.Count > 20)
                        await _context.BulkInsertAsync(taskItems);
                    else
                    {
                        await _context.InstructorCriticalQuestionTaskItems.AddRangeAsync(taskItems);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    TempData["Success"] = $"تم إرسال مهمة للمدرب تحتوي على {taskItems.Count} سؤال حرج للدفعة {batch.Name}.";
                    TempData["SuccessCode"] = errorCode;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();

                    _logger.LogError(ex,
                        "Critical question task failed. Code={Code} LessonId={L} BatchId={B} InstructorId={I}",
                        errorCode, lessonId, resolvedBatchId, resolvedInstructorId);

                    TempData["Error"] = "فشل إرسال مهمة الأسئلة الحرجة للمدرب.";
                    TempData["ErrorCode"] = errorCode;
                    TempData["ErrorDetails"] = ex.GetBaseException().Message;

                    return RedirectToAction("WeakLessonDetails", new { lessonId, batchId = resolvedBatchId, instructorId = resolvedInstructorId });
                }

                return RedirectToAction("WeakLessonDetails", new { lessonId, batchId = resolvedBatchId, instructorId = resolvedInstructorId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Critical question task preparation failed. Code={Code} LessonId={L} BatchId={B} InstructorId={I}",
                    errorCode, lessonId, batchId, instructorId);

                TempData["Error"] = "فشل تجهيز بيانات مهمة الأسئلة الحرجة قبل الإرسال.";
                TempData["ErrorCode"] = errorCode;
                TempData["ErrorDetails"] = ex.GetBaseException().Message;

                return RedirectToAction("WeakLessonDetails", new { lessonId, batchId, instructorId });
            }
        }
    }
}
