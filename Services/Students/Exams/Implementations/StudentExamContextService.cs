using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Students.Exams.Abstractions;
using QdratNew.ViewModels.Exam;
using QdratNew.ViewModels.Students;

namespace QdratNew.Services.Students.Exams.Implementations
{
    public class StudentExamContextService : IStudentExamContextService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public StudentExamContextService(
            IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<StudentExamCardViewModel>> GetAllAsync(
        int studentId,
        int activeCourseId,
        int activeBatchId)
        {
            using var db = _contextFactory.CreateDbContext();

            // =========================================
            // 1️⃣ جلب دفعات الطالب
            // =========================================
            var studentBatchIds = await db.StudentBatchEnrollments
                .AsNoTracking()
                .Where(x => x.StudentID == studentId)
                .Select(x => x.BatchId)
                .ToListAsync();

            if (!studentBatchIds.Any())
                return new List<StudentExamCardViewModel>();

            // =========================================
            // 2️⃣ جلب Assignments (بدون فلترة SQL معقدة)
            // =========================================
            var assignmentsRaw = await db.ExamAssignmentsToBatches
                .AsNoTracking()
                .Include(a => a.Exam)
                .Include(a => a.Batch)
                .ToListAsync();

            // =========================================
            // 3️⃣ فلترة في الذاكرة (SQL 2014 SAFE)
            // =========================================
            var assignments = assignmentsRaw
                .Where(a =>
                    studentBatchIds.Any(b => b == a.BatchId) &&
                    a.Batch.CourseId == activeCourseId
                )
                .ToList();

            // دعم السويتش بين الدفعات
            if (activeBatchId > 0)
            {
                assignments = assignments
                    .Where(a => a.BatchId == activeBatchId)
                    .ToList();
            }

            // =========================================
            // 4️⃣ جلب حالات الطالب
            // =========================================
            var statuses = await db.ExamStudentStatuses
                .AsNoTracking()
                .Where(s => s.StudentId == studentId)
                .ToListAsync();

            // =========================================
            // 5️⃣ بناء ViewModel (مطابق للمشروع)
            // =========================================
            var result = assignments.Select(a =>
            {
                var status = statuses
                    .FirstOrDefault(s => s.ExamAssignmentId == a.Id);

                return new StudentExamCardViewModel
                {
                    ExamAssignmentId = a.Id,
                    ExamTitle = a.Title ?? a.Exam.Title,
                    BatchName = a.Batch.Name,

                    AssignedAt = a.AssignedAt,
                    EndAt = a.EndAt,
                    DurationMinutes = a.DurationMinutes,

                    Status = status != null ? status.Status : ExamStatus.Pending,
                    IsSubmitted = status?.IsSubmitted ?? false
                };
            })
            .OrderByDescending(x => x.AssignedAt)
            .ToList();

            return result;
        }


        public async Task<List<StudentExamCardViewModel>> GetRequiredAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            using var ctx = _contextFactory.CreateDbContext();

            var exams = await BaseQuery(ctx, studentId, courseId, batchId)
                .Where(e => !ctx.ExamStudentStatuses.Any(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentId == e.Id &&
                    s.IsSubmitted))
                .ToListAsync();

            return exams.Select(MapToCard).ToList();
        }

        public async Task<List<StudentExamCardViewModel>> GetCompletedAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            using var ctx = _contextFactory.CreateDbContext();

            var exams = await BaseQuery(ctx, studentId, courseId, batchId)
                .Where(e => ctx.ExamStudentStatuses.Any(s =>
                    s.StudentId == studentId &&
                    s.ExamAssignmentId == e.Id &&
                    s.IsSubmitted))
                .ToListAsync();

            return exams.Select(MapToCard).ToList();
        }

        public async Task<List<StudentExamCardViewModel>> GetLateAsync(
            int studentId,
            int courseId,
            int batchId)
        {
            using var ctx = _contextFactory.CreateDbContext();
            var now = DateTime.UtcNow;

            var exams = await BaseQuery(ctx, studentId, courseId, batchId)
                .Where(e =>
                    e.EndAt.HasValue &&
                    e.EndAt < now &&
                    !ctx.ExamStudentStatuses.Any(s =>
                        s.StudentId == studentId &&
                        s.ExamAssignmentId == e.Id &&
                        s.IsSubmitted))
                .ToListAsync();

            return exams.Select(MapToCard).ToList();
        }

        // =========================================
        // 🔒 Query موحدة (سياق الطالب + الدورة)
        // =========================================
        private IQueryable<ExamAssignmentToBatch> BaseQuery(
          ApplicationDbContext ctx,
          int studentId,
          int courseId,
          int batchId)
        {
            var query = ctx.ExamAssignmentsToBatches
                .Where(e =>
                    // الطالب مشترك في هذه الدفعة
                    ctx.StudentBatchEnrollments.Any(sb =>
                        sb.StudentID == studentId &&
                        sb.BatchId == e.BatchId
                    )
                )

                // ✅ فلترة الدورة (مهم جدًا)
                .Where(e => e.Batch.CourseId == courseId)

                // ✅ فقط الاختبارات المرسلة
                .Where(e => e.IsSentToStudents)

                // ✅ استبعاد تحديد المستوى
                .Where(e => e.Exam.Type != ExamType.LevelAssessment);

            // ✅ فلترة على الدفعة المختارة (السويتش)
            if (batchId > 0)
            {
                query = query.Where(e => e.BatchId == batchId);
            }

            return query;
        }
        // =========================================
        // 🧱 Mapper
        // =========================================
        private static StudentExamCardViewModel MapToCard(
            ExamAssignmentToBatch e)
        {
            return new StudentExamCardViewModel
            {
                ExamAssignmentId = e.Id,
                ExamId = e.ExamId!.Value,
                Title = e.Title ?? e.Exam.Title,
                AssignedAt = e.AssignedAt,
                IsOnline = e.IsOnline,
                RequiresPassword = e.IsInLab
            };
        }
    }
}
