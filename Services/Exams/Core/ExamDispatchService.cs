using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Abstractions;

namespace QdratNew.Services.Exams.Core
{
    public class ExamDispatchService : IExamDispatchService
    {
        private readonly ApplicationDbContext _context;

        public ExamDispatchService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        private async Task<Exam> BuildExamAsync(int draftId)
        {
            var draft = await _context.ExamDrafts
                .Include(d => d.DraftQuestions)
                .FirstOrDefaultAsync(d => d.Id == draftId);

            if (draft == null)
                throw new Exception("Draft not found");

            var exam = new Exam
            {
                Title = draft.Title,
                CurriculumId = draft.CurriculumId,
                ReferenceCode = await GenerateReferenceCodeAsync(),
                DurationMinutes = 30,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            var questions = draft.DraftQuestions
                .OrderBy(q => q.Order)
                .Select((q, i) => new ExamQuestion
                {
                    ExamId = exam.Id,
                    QuestionId = q.QuestionId,
                    Order = i + 1,
                    IsManuallySelected = true
                }).ToList();

            await _context.BulkInsertAsync(questions, new BulkConfig
            {
                UseTempDB = false
            });

            return exam;
        }

        // =========================
        public async Task SendToBatchesAsync(
      int draftId,
      int instructorId,
      List<int> batchIds,
      string examTitle,
      DateTime startAt,
      DateTime endAt,
      int durationMinutes,
      string examMode)
        {
            if (batchIds == null || batchIds.Count == 0)
                return;

            // =========================
            // 🔥 FIX 1: حماية القيم
            // =========================
            if (startAt == default)
                throw new Exception("StartAt is required");

            if (endAt == default)
                throw new Exception("EndAt is required");

            if (durationMinutes <= 0)
                durationMinutes = 30;

            examMode = string.IsNullOrEmpty(examMode) ? "online" : examMode;

            // =========================
            // BUILD EXAM
            // =========================
            var exam = await BuildExamAsync(draftId);

            exam.Title = string.IsNullOrEmpty(examTitle) ? exam.Title : examTitle;
            exam.DurationMinutes = durationMinutes;

            await _context.SaveChangesAsync();

            // =========================
            // ASSIGNMENTS
            // =========================
            var assignments = batchIds.Select(batchId => new ExamAssignmentToBatch
            {
                ExamId = exam.Id,
                BatchId = batchId,
                Title = exam.Title,

                ScheduledDate = startAt,
                EndAt = endAt,
                DurationMinutes = durationMinutes,

                IsOnline = examMode == "online",
                IsInLab = examMode == "lab",

                CreatedAt = DateTime.UtcNow,
                AssignedAt = DateTime.UtcNow,
                CreatedByInstructorId = instructorId,
                ReferenceCode = exam.ReferenceCode
            }).ToList();

            await _context.BulkInsertAsync(assignments, new BulkConfig
            {
                UseTempDB = false
            });

            // =========================
            // STUDENTS
            // =========================
            var enrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Select(e => new { e.StudentID, e.BatchId })
                .ToListAsync();

            var studentIds = enrollments
                .Where(e => batchIds.Any(b => b == e.BatchId))
                .Select(e => e.StudentID)
                .Distinct()
                .ToList();

            await SendStudentsInternal(exam.Id, studentIds, startAt, endAt, durationMinutes);
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

        // =========================
        public async Task SendToStudentsAsync(
            int draftId,
            int instructorId,
            List<int> studentIds,
            string examTitle,
            DateTime startAt,
            DateTime endAt,
            int durationMinutes,
            string examMode)
        {
            var exam = await BuildExamAsync(draftId);

            exam.Title = examTitle;
            exam.DurationMinutes = durationMinutes;

            await _context.SaveChangesAsync();

            await SendStudentsInternal(exam.Id, studentIds, startAt, endAt, durationMinutes);
        }
        // =========================
        private async Task SendStudentsInternal(
            int examId,
            List<int> studentIds,
            DateTime startAt,
            DateTime endAt,
            int durationMinutes)
        {
            var list = studentIds.Select(studentId => new ExamAssignmentToStudent
            {
                ExamId = examId,
                StudentId = studentId,
                ScheduledDate = startAt,
                EndAt = endAt,
                DurationMinutes = durationMinutes,
                CreatedAt = DateTime.UtcNow,
                IsSent = true
            }).ToList();

            await _context.BulkInsertAsync(list, new BulkConfig { UseTempDB = false });
        }
        // =========================
        public async Task SendPlacementTestAsync(
            int draftId,
            int instructorId,
            List<int> studentIds,
            string examTitle,
            DateTime startAt,
            DateTime endAt,
            int durationMinutes,
            string examMode)
        {
            var exam = await BuildExamAsync(draftId);

            exam.Title = examTitle;
            exam.DurationMinutes = durationMinutes;
            exam.Type = ExamType.LevelAssessment;

            await _context.SaveChangesAsync();

            await SendStudentsInternal(exam.Id, studentIds, startAt, endAt, durationMinutes);
        }
        // =========================
        public async Task SendPerformanceIndicatorAsync(
        int draftId,
        int instructorId,
        List<int> batchIds,
        List<int> studentIds)
        {
            if (batchIds == null || batchIds.Count == 0)
                return;

            // =========================
            // 1) تحميل المسودة + الأسئلة
            // =========================
            var draft = await _context.ExamDrafts
                .Include(d => d.DraftQuestions)
                .FirstOrDefaultAsync(d => d.Id == draftId);

            if (draft == null)
                throw new Exception("Draft not found");

            // =========================
            // 2) إنشاء اختبار مؤشر الأداء
            // =========================
            var exam = new PerformanceIndicatorExam
            {
                Title = draft.Title,
                CurriculumId = draft.CurriculumId,
                DurationMinutes = 30,
                StartAt = DateTime.UtcNow,
                EndAt = DateTime.UtcNow.AddDays(7),
                IsOnline = true,
                CreatedAt = DateTime.UtcNow,
                ReferenceCode = GenerateReferenceCode(),
                IsSent = true
            };

            _context.PerformanceIndicatorExams.Add(exam);
            await _context.SaveChangesAsync();

            // =========================
            // 3) ربط الدفعات
            // =========================
            var batchLinks = batchIds.Select(b => new PerformanceIndicatorExamToBatch
            {
                PerformanceIndicatorExamId = exam.Id,
                BatchId = b
            }).ToList();

            await _context.BulkInsertAsync(batchLinks);

            // =========================
            // 4) جلب الطلاب من الدفعات
            // =========================
            var enrollments = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Select(e => new { e.StudentID, e.BatchId })
                .ToListAsync();

            var students = enrollments
                .Where(e => batchIds.Any(b => b == e.BatchId))
                .Select(e => e.StudentID)
                .Distinct()
                .ToList();

            // =========================
            // 5) ربط الطلاب بالاختبار
            // =========================
            var studentLinks = students.Select(s => new PerformanceIndicatorExamStudent
            {
                PerformanceIndicatorExamId = exam.Id,
                StudentId = s,
                IsCompleted = false
            }).ToList();

            await _context.BulkInsertAsync(studentLinks);

            // =========================
            // 6) جلب الأسئلة + SectionId بشكل آمن
            // =========================
            var draftQuestions = await (
                from dq in _context.ExamDraftQuestions
                join q in _context.Questions on dq.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                where dq.ExamDraftId == draftId
                select new
                {
                    dq.QuestionId,
                    SectionId = l.SectionId
                }
            ).ToListAsync();

            if (!draftQuestions.Any())
                throw new Exception("لا توجد أسئلة في المسودة");

            // =========================
            // 7) إنشاء أسئلة الاختبار
            // =========================
            int order = 1;

            var examQuestions = draftQuestions
                .Where(q => q.SectionId > 0)
                .Select(q => new PerformanceIndicatorExamQuestion
                {
                    PerformanceIndicatorExamId = exam.Id,
                    QuestionId = q.QuestionId,
                    SectionId = q.SectionId,
                    OrderNumber = order++
                }).ToList();

            if (!examQuestions.Any())
                throw new Exception("الأسئلة لا تحتوي على Sections صحيحة");

            await _context.BulkInsertAsync(examQuestions);

            // =========================
            // 8) تحديث العدد
            // =========================
            exam.TotalQuestions = examQuestions.Count;

            await _context.SaveChangesAsync();
        }
        // =========================
        private string GenerateReferenceCode()
        {
            var random = new Random();
            return random.Next(100000, 999999).ToString();
        }
    }
}