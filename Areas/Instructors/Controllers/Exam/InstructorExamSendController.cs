using EFCore.BulkExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Abstractions;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.ViewModels.Instructor.Exam;

namespace QdratNew.Areas.Instructors.Controllers.Exam
{
    [Area("Instructors")]
    public class InstructorExamSendController : BaseInstructorController
    {
        private readonly IExamDispatchService _dispatchService;
        private readonly IInstructorScopeService _scopeService;
        private readonly ApplicationDbContext _context;

        public InstructorExamSendController(
            IExamDispatchService dispatchService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IInstructorScopeService scopeService)
            : base(userManager, scopeService)
        {
            _dispatchService = dispatchService;
            _context = context;
            _scopeService = scopeService;
        }


        [HttpGet]
        public async Task<IActionResult> SentExams()
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            // جلب الدفعات والطلاب المسموح بهم لهذا المدرب
            var allowedBatchIds = (await _scopeService.GetAllowedBatchIdsAsync(instructorId))
                .Union(await _scopeService.GetPermittedBatchIdsAsync(instructorId, InstructorBatchFeature.Exams))
                .Distinct()
                .ToList();

            var allowedStudentIds = await _scopeService.GetAllowedStudentIdsAsync(instructorId);

            // =========================
            // Batch Exams (العادية)
            // =========================
            var batchAssignments = await _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(x => x.CreatedByInstructorId == instructorId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new InstructorSentExamItemVM
                {
                    ExamId = x.ExamId ?? 0,
                    ExamAssignmentId = x.Id,
                    Title = x.Title,
                    ExamType = "Batch",
                    TargetName = x.Batch != null ? x.Batch.Name : "-",
                    StartAt = x.ScheduledDate,
                    EndAt = x.EndAt,
                    DurationMinutes = x.DurationMinutes,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            // =========================
            // Individual Exams
            // =========================
            var batchExamIdSet = (await _context.ExamAssignmentsToBatches
                .Select(x => x.ExamId)
                .ToListAsync())
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            var allStudentAssignments = await _context.ExamAssignmentsToStudents
                .AsNoTracking()
                .Where(x => allowedStudentIds.Contains(x.StudentId))
                .Select(x => new
                {
                    x.Id,
                    x.ExamId,
                    Title = x.Exam.Title,
                    StudentName = x.Student.FullName,
                    x.ScheduledDate,
                    x.EndAt,
                    x.DurationMinutes,
                    x.CreatedAt
                })
                .ToListAsync();

            var rawStudentAssignments = allStudentAssignments
                .Where(x => !batchExamIdSet.Contains(x.ExamId))
                .Select(x => new InstructorSentExamItemVM
                {
                    ExamId = x.ExamId,
                    ExamAssignmentId = x.Id,
                    Title = x.Title,
                    ExamType = "Individual",
                    TargetName = x.StudentName,
                    StartAt = x.ScheduledDate,
                    EndAt = x.EndAt,
                    DurationMinutes = x.DurationMinutes,
                    CreatedAt = x.CreatedAt
                })
                .ToList();

            // =========================
            // Performance Exams
            // =========================
            var performanceExams = await _context.PerformanceIndicatorExams
                .AsNoTracking()
                .Where(x => x.ExamToBatches.Any(b => allowedBatchIds.Contains(b.BatchId)))
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new InstructorSentExamItemVM
                {
                    ExamId = x.Id,
                    ExamAssignmentId = x.Id,
                    Title = x.Title,
                    ExamType = "Performance",
                    TargetName = "دفعات متعددة",
                    StartAt = x.StartAt,
                    EndAt = x.EndAt,
                    DurationMinutes = x.DurationMinutes,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();

            // =========================
            // VM
            // =========================
            var vm = new InstructorSentExamsPageVM
            {
                BatchExams = batchAssignments,
                IndividualExams = rawStudentAssignments,
                PlacementExams = new List<InstructorSentExamItemVM>(),
                PerformanceExams = performanceExams
            };

            return View(vm);
        }


        public async Task<IActionResult> SendDraft(int id)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            var batchIds = await _scopeService.GetAllowedBatchIdsAsync(instructorId);
            var permittedBatchIds = await _scopeService.GetPermittedBatchIdsAsync(instructorId, InstructorBatchFeature.Exams);
            var allAllowedBatchIds = batchIds.Union(permittedBatchIds).Distinct().ToList();

            var allBatches = await _context.Batches
                .AsNoTracking()
                .Where(b => !b.IsDeleted)
                .Select(b => new { b.Id, b.Name })
                .ToListAsync();

            var batches = allBatches
                .Where(b => allAllowedBatchIds.Any(x => x == b.Id))
                .Select(b => new SelectItemVM { Id = b.Id, Name = b.Name })
                .ToList();

            var studentIds = await _scopeService.GetAllowedStudentIdsAsync(instructorId);

            var allStudents = await _context.Students
                .AsNoTracking()
                .Select(s => new { s.StudentID, s.FullName })
                .ToListAsync();

            var students = allStudents
                .Where(s => studentIds.Any(x => x == s.StudentID))
                .Select(s => new SelectItemVM { Id = s.StudentID, Name = s.FullName })
                .ToList();

            return View(new SendExamDraftVM
            {
                DraftId = id,
                Batches = batches,
                Students = students
            });
        }

        // =========================
        // SEND BATCHES
        // =========================
        [HttpPost]
        public async Task<IActionResult> SendToBatches(InstructorExamSendVM model)
        {
            var instructorId = await RequireInstructorAsync();
            if (instructorId == 0)
                return Unauthorized();

            try
            {
                await _dispatchService.SendToBatchesAsync(
                    model.DraftId,
                    instructorId,
                    model.BatchIds ?? new List<int>(),
                    model.ExamTitle,
                    model.StartAt,
                    model.EndAt,
                    model.DurationMinutes,
                    model.ExamMode
                );

                TempData["Success"] = "تم إرسال الاختبار للدفعات";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Drafts", "InstructorExamDraft");
        }
        // =========================
        // SEND STUDENTS
        // =========================
        [HttpPost]
        public async Task<IActionResult> SendToStudents(SendExamDraftVM model)
        {
            var instructorId = await RequireInstructorAsync();

            try
            {
                await _dispatchService.SendToStudentsAsync(
                    model.DraftId,
                    instructorId,
                    model.StudentIds ?? new List<int>(),
                    model.ExamTitle,
                    model.StartAt,
                    model.EndAt,
                    model.DurationMinutes,
                    model.ExamMode
                );

                TempData["Success"] = "تم إرسال الاختبار للطلاب";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Drafts", "InstructorExamDraft");
        }

        public async Task SendPerformanceIndicatorAsync(
    int draftId,
    int instructorId,
    List<int> batchIds,
    List<int> studentIds)
        {
            if ((batchIds == null || batchIds.Count == 0) &&
                (studentIds == null || studentIds.Count == 0))
                return;

            var draft = await _context.ExamDrafts
                .Include(d => d.DraftQuestions)
                .FirstOrDefaultAsync(d => d.Id == draftId);

            if (draft == null)
                throw new Exception("Draft not found");

            var exam = new PerformanceIndicatorExam
            {
                CurriculumId = draft.CurriculumId,
                Title = draft.Title,
                CreatedAt = DateTime.UtcNow,
                IsOnline = true,
                StartAt = DateTime.UtcNow,
                EndAt = DateTime.UtcNow.AddDays(7),
                ReferenceCode = new Random().Next(100000, 999999).ToString(),
                TotalQuestions = draft.DraftQuestions.Count,
                IsSent = true
            };

            _context.PerformanceIndicatorExams.Add(exam);
            await _context.SaveChangesAsync();

            // =========================
            // ربط بالدفعات
            // =========================
            if (batchIds != null && batchIds.Count > 0)
            {
                var batchLinks = batchIds.Select(b => new PerformanceIndicatorExamToBatch
                {
                    PerformanceIndicatorExamId = exam.Id,
                    BatchId = b
                }).ToList();

                await _context.BulkInsertAsync(batchLinks);
            }

            // =========================
            // ربط بالطلاب
            // =========================
            if (studentIds != null && studentIds.Count > 0)
            {
                var students = studentIds.Select(s => new PerformanceIndicatorExamStudent
                {
                    PerformanceIndicatorExamId = exam.Id,
                    StudentId = s
                }).ToList();

                await _context.BulkInsertAsync(students);
            }
        }


        [HttpPost]
        public async Task<IActionResult> SendPerformance(SendExamDraftVM model)
        {
            var instructorId = await RequireInstructorAsync();

            try
            {
                await _dispatchService.SendPerformanceIndicatorAsync(
                    model.DraftId,
                    instructorId,
                    model.BatchIds ?? new List<int>(),
                    model.StudentIds ?? new List<int>()
                );

                TempData["Success"] = "تم إرسال اختبار مؤشر الأداء";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Drafts", "InstructorExamDraft");
        }


        private async Task<string> GenerateReferenceCodeAsync()
{
    var random = new Random();
    string code;

    do
    {
        code = random.Next(100000, 999999).ToString();
    }
    while (await _context.Exams.AnyAsync(e => e.ReferenceCode == code));

    return code;
}


    
    
    
    
    }
}