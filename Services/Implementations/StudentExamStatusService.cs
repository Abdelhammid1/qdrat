using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Implementations
{
    public class StudentExamStatusService : IStudentExamStatusService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ITimeZoneService _time;

        public StudentExamStatusService(
            IDbContextFactory<ApplicationDbContext> contextFactory,
            ITimeZoneService time)
        {
            _contextFactory = contextFactory;
            _time = time;
        }

        // ======================================================
        // 🟢 DASHBOARD SUMMARY (Scoped – Production)
        // ======================================================
        public async Task<ExamStatusSummaryVm> GetStatusSummaryAsync(
           int studentId,
           int activeCourseId,
           int activeBatchId)
        {
            // نفس مصدر الصفحات
            var all = await GetAllExamsAsync(studentId, activeCourseId, activeBatchId);
            var solved = await GetSolvedExamsAsync(studentId, activeCourseId, activeBatchId);
            var required = await GetRequiredExamsAsync(studentId, activeCourseId, activeBatchId);
            var late = await GetLateExamsPageAsync(studentId, activeCourseId, activeBatchId);

            return new ExamStatusSummaryVm
            {
                Total = all.Count,
                Solved = solved.Count,
                Required = required.Count,
                Late = late.Count,
                ExpiringSoon = 0 // غير مستخدمة حاليًا
            };
        }


        // ============================================================
        // 🔑 المصدر الموحد لكل صفحات الاختبارات
        // ============================================================
        private async Task<List<ExamListVm>> LoadAllRawAsync(int studentId)
        {
            using var ctx = _contextFactory.CreateDbContext();
            var now = _time.GetNowSaudi();

            // ===============================
            // 1) اختبارات الدُفعات
            // ===============================
            var batchExams = await (
                from sbe in ctx.StudentBatchEnrollments
                join be in ctx.ExamAssignmentsToBatches
                    on sbe.BatchId equals be.BatchId
                join ex in ctx.Exams
                    on be.ExamId equals ex.Id
                join st in ctx.ExamStudentStatuses
                    .Where(s => s.StudentId == studentId)
                    on be.Id equals st.ExamAssignmentId into stj
                from status in stj.DefaultIfEmpty()
                where sbe.StudentID == studentId
                select new ExamListVm
                {
                    ExamAssignmentId = be.Id,
                    ExamId = ex.Id,
                    Title = be.Title ?? ex.Title,

                    AssignedAt = be.AssignedAt,
                    ScheduledDate = be.ScheduledDate,
                    DurationMinutes = be.DurationMinutes,
                    EndAt = be.EndAt,

                    IsIndividual = false,

                    CourseId = be.Batch.CourseId,
                    BatchId = be.BatchId,

                    IsSubmitted = status != null && status.IsSubmitted,

                    // ✅ FIXED
                    Score = status != null && status.Score.HasValue
                        ? (int?)Math.Round((double)status.Score.Value)
                        : null
                }
            ).AsNoTracking().ToListAsync();

            // ===============================
            // 2) الاختبارات الفردية
            // ===============================
            var individualExams = await (
                from ia in ctx.ExamAssignmentsToStudents
                join ex in ctx.Exams
                    on ia.ExamId equals ex.Id
                join st in ctx.ExamStudentStatuses
                    .Where(s => s.StudentId == studentId)
                    on ia.Id equals st.ExamAssignmentToStudentId into stj
                from status in stj.DefaultIfEmpty()
                where ia.StudentId == studentId
                select new ExamListVm
                {
                    ExamAssignmentId = ia.Id,
                    ExamId = ex.Id,
                    Title = ex.Title,

                    AssignedAt = ia.ScheduledDate.Value,
                    ScheduledDate = ia.ScheduledDate,
                    DurationMinutes = ia.DurationMinutes,
                    EndAt = ia.EndAt,

                    IsIndividual = true,

                    CourseId = ctx.StudentBatchEnrollments
                        .Where(sb => sb.StudentID == studentId)
                        .Select(sb => sb.Batch.CourseId)
                        .FirstOrDefault(),

                    BatchId = ctx.StudentBatchEnrollments
                        .Where(sb => sb.StudentID == studentId)
                        .Select(sb => sb.BatchId)
                        .FirstOrDefault(),

                    IsSubmitted = status != null && status.IsSubmitted,

                    // ✅ FIXED
                    Score = status != null && status.Score.HasValue
                        ? (int?)Math.Round((double)status.Score.Value)
                        : null
                }
            ).AsNoTracking().ToListAsync();

            // ===============================
            // 3) دمج + حساب الحالة الزمنية
            // ===============================
            var all = batchExams.Concat(individualExams).ToList();

            foreach (var x in all)
            {
                x.IsExpired = false;
                x.IsExpiringSoon = false;

                var start = x.ScheduledDate ?? x.AssignedAt;
                var end = x.EndAt ?? start.AddMinutes(x.DurationMinutes);

                if (x.IsSubmitted)
                {
                    x.StatusText = "تم الحل";
                }
                else if (now < start)
                {
                    x.StatusText = "قادِم";
                }
                else if (now >= start && now <= end)
                {
                    var remainingHours = (end - now).TotalHours;

                    if (remainingHours <= 48)
                    {
                        x.StatusText = $"سيتأخر خلال {Math.Round(remainingHours, 1)} ساعة";
                        x.IsExpiringSoon = true;
                    }
                    else
                    {
                        x.StatusText = "متاح الآن";
                    }
                }
                else
                {
                    x.StatusText = "متأخر";
                    x.IsExpired = true;
                }
            }

            return all
                .OrderByDescending(x => x.AssignedAt)
                .ToList();
        }


        // ======================================================
        // 🟢 ALL EXAMS (Scoped)
        // ======================================================
        public async Task<List<StudentExamCardViewModel>> GetAllExamsAsync(
        int studentId,
        int activeCourseId,
        int activeBatchId)
        {
            using var ctx = _contextFactory.CreateDbContext();

            // =========================================
            // 1️⃣ جلب دفعات الطالب
            // =========================================
            var enrollments = await ctx.StudentBatchEnrollments
                .AsNoTracking()
                .Where(x => x.StudentID == studentId)
                .Select(x => new
                {
                    x.BatchId,
                    CourseId = x.Batch.CourseId
                })
                .ToListAsync();

            if (!enrollments.Any())
                return new List<StudentExamCardViewModel>();

            // =========================================
            // 2️⃣ تحديد الدفعة الحالية فقط
            // =========================================
            var validBatchId = enrollments
                .Where(e =>
                    e.BatchId == activeBatchId &&
                    e.CourseId == activeCourseId
                )
                .Select(e => e.BatchId)
                .FirstOrDefault();

            if (validBatchId == 0)
                return new List<StudentExamCardViewModel>();

            // =========================================
            // 3️⃣ جلب الاختبارات لهذه الدفعة فقط
            // =========================================
            var assignments = await ctx.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(a =>
                    a.BatchId == validBatchId &&
                    a.IsSentToStudents &&
                    a.Exam.Type != ExamType.LevelAssessment
                )
                .Include(a => a.Exam)
                .ToListAsync();

            // =========================================
            // 4️⃣ الحالات
            // =========================================
            var statuses = await ctx.ExamStudentStatuses
                .AsNoTracking()
                .Where(s => s.StudentId == studentId)
                .ToListAsync();

            // =========================================
            // 5️⃣ mapping
            // =========================================
            var result = assignments.Select(a =>
            {
                var status = statuses
                    .FirstOrDefault(s => s.ExamAssignmentId == a.Id);

                return new StudentExamCardViewModel
                {
                    ExamAssignmentId = a.Id,
                    ExamId = a.ExamId ?? 0,
                    Title = a.Title ?? a.Exam.Title,

                    AssignedAt = a.AssignedAt,
                    EndAt = a.EndAt,

                    IsSubmitted = status?.IsSubmitted ?? false,
                    Score = status?.Score,

                    IsOnline = a.IsOnline,
                    RequiresPassword = a.IsInLab
                };
            })
            .OrderByDescending(x => x.AssignedAt)
            .ToList();

            return result;
        }
        // ======================================================
        // 🟢 REQUIRED EXAMS
        // ======================================================
        public async Task<List<StudentExamCardViewModel>> GetRequiredExamsAsync(
            int studentId,
            int activeCourseId,
            int activeBatchId)
        {
            using var ctx = _contextFactory.CreateDbContext();

            var data = await (
                from e in BaseQuery(ctx, studentId, activeCourseId, activeBatchId)
                join s in ctx.ExamStudentStatuses
                    .Where(x => x.StudentId == studentId)
                    on e.Id equals s.ExamAssignmentId into sj
                from status in sj.DefaultIfEmpty()
                where status == null || !status.IsSubmitted
                select MapToCard(e, status)
            ).AsNoTracking().ToListAsync();

            return data;
        }

        // ======================================================
        // 🟢 SOLVED EXAMS
        // ======================================================
        public async Task<List<StudentExamCardViewModel>> GetSolvedExamsAsync(
            int studentId,
            int activeCourseId,
            int activeBatchId)
        {
            using var ctx = _contextFactory.CreateDbContext();

            var data = await (
                from e in BaseQuery(ctx, studentId, activeCourseId, activeBatchId)
                join s in ctx.ExamStudentStatuses
                    on e.Id equals s.ExamAssignmentId
                where s.StudentId == studentId && s.IsSubmitted
                select MapToCard(e, s)
            ).AsNoTracking().ToListAsync();

            return data;
        }

        // ======================================================
        // 🟢 LATE EXAMS
        // ======================================================
        public async Task<List<StudentExamCardViewModel>> GetLateExamsPageAsync(
            int studentId,
            int activeCourseId,
            int activeBatchId)
        {
            using var ctx = _contextFactory.CreateDbContext();
            var now = _time.GetNowSaudi();

            var data = await (
                from e in BaseQuery(ctx, studentId, activeCourseId, activeBatchId)
                join s in ctx.ExamStudentStatuses
                    on e.Id equals s.ExamAssignmentId
                where s.StudentId == studentId
                      && !s.IsSubmitted
                      && s.EndAt.HasValue
                      && s.EndAt < now
                select MapToCard(e, s)
            ).AsNoTracking().ToListAsync();

            return data;
        }

        // ======================================================
        // 🔴 LEGACY IMPLEMENTATIONS (Interface Only)
        // ======================================================
        public async Task<ExamStatusSummaryVm> GetStatusSummaryAsync(int studentId)
        {
            using var ctx = _contextFactory.CreateDbContext();

            var firstEnrollment = await ctx.StudentBatchEnrollments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudentID == studentId);

            if (firstEnrollment == null)
                return new ExamStatusSummaryVm();

            var batch = await ctx.Batches
                .AsNoTracking()
                .FirstAsync(b => b.Id == firstEnrollment.BatchId);

            return await GetStatusSummaryAsync(
                studentId,
                batch.CourseId,
                batch.Id);
        }

        public async Task<List<ExamListVm>> GetAllExamsAsync(int studentId)
        {
            return new(); // Legacy no longer supported
        }

        public async Task<List<ExamListVm>> GetRequiredExamsAsync(int studentId)
        {
            return new();
        }

        public async Task<List<ExamListVm>> GetSolvedExamsAsync(int studentId)
        {
            return new();
        }

        public async Task<List<ExamListVm>> GetLateExamsAsync(int studentId)
        {
            return new();
        }

        public async Task<LateExamsPageVm> GetLateExamsPageAsync(int studentId)
        {
            return new LateExamsPageVm();
        }

        // ======================================================
        // 🔒 BASE QUERY
        // ======================================================
        private IQueryable<ExamAssignmentToBatch> BaseQuery(
            ApplicationDbContext ctx,
            int studentId,
            int courseId,
            int batchId)
        {
            var query = ctx.ExamAssignmentsToBatches
                .Where(e =>
                    ctx.StudentBatchEnrollments.Any(sb =>
                        sb.StudentID == studentId &&
                        sb.BatchId == e.BatchId
                    )
                    && e.Exam.Type != ExamType.LevelAssessment
                );

            if (batchId > 0)
            {
                query = query.Where(e => e.BatchId == batchId);
            }

            return query;
        }
        // ======================================================
        // 🧱 CARD MAPPER
        // ======================================================
        private static StudentExamCardViewModel MapToCard(
            ExamAssignmentToBatch e,
            ExamStudentStatus? status)
        {
            return new StudentExamCardViewModel
            {
                ExamAssignmentId = e.Id,
                ExamId = e.ExamId ?? throw new InvalidOperationException("ExamId is null"),
                Title = e.Title ?? e.Exam.Title,

                AssignedAt = e.AssignedAt,
                EndAt = e.EndAt,

                IsSubmitted = status != null && status.IsSubmitted,
                Score = status?.Score,

                IsOnline = e.IsOnline,
                RequiresPassword = e.IsInLab
            };
        }
    }
}
