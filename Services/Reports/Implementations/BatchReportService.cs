using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Reports.Interfaces;
using QdratNew.ViewModels.Partner.Reports;

namespace QdratNew.Services.Reports.Implementations
{
    public class BatchReportService : IBatchReportService
    {
        private readonly ApplicationDbContext _context;

        public BatchReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // التقرير العام للدفعة
        // =====================================================
        public BatchReportVM GetBatchReport(int batchId)
        {
            var batch = _context.Batches
                .AsNoTracking()
                .FirstOrDefault(b => b.Id == batchId && !b.IsDeleted);

            if (batch == null)
                return null;

            var totalStudents = _context.StudentBatchEnrollments
                .Count(x => x.BatchId == batchId && x.Status == "Active");

            // ===============================
            // الواجبات – JOIN صريح
            // ===============================
            var homeworkStats = (
                from hs in _context.HomeworkSets.AsNoTracking()
                join hss in _context.HomeworkSetStudents.AsNoTracking()
                    on hs.Id equals hss.HomeworkSetId
                where hs.BatchId == batchId
                      && hs.IsSent
                select new
                {
                    hss.IsSubmitted,
                    hss.Score
                }
            ).ToList();

            var totalHomeworkSets = _context.HomeworkSets
                .Count(h => h.BatchId == batchId && h.IsSent);

            var submittedHomeworks = homeworkStats.Count(x => x.IsSubmitted);

            var avgHomeworkScore = homeworkStats
                .Where(x => x.Score.HasValue)
                .Select(x => x.Score.Value)
                .DefaultIfEmpty(0)
                .Average();

            // ===============================
            // الاختبارات – JOIN صريح
            // ===============================
            var examStats = (
                from ea in _context.ExamAssignmentsToBatches.AsNoTracking()
                join es in _context.ExamStudentStatuses.AsNoTracking()
                    on ea.Id equals es.ExamAssignmentId
                where ea.BatchId == batchId
                      && ea.IsSentToStudents
                select new
                {
                    es.IsSubmitted,
                    es.Score
                }
            ).ToList();

            var totalExamAssignments = _context.ExamAssignmentsToBatches
                .Count(e => e.BatchId == batchId && e.IsSentToStudents);

            var submittedExams = examStats.Count(x => x.IsSubmitted);

            var avgExamScore = examStats
                .Where(x => x.Score.HasValue)
                .Select(x => (double)x.Score.Value)
                .DefaultIfEmpty(0)
                .Average();

            return new BatchReportVM
            {
                BatchId = batch.Id,
                BatchName = batch.Name,

                TotalStudents = totalStudents,

                TotalHomeworkSets = totalHomeworkSets,
                TotalExamAssignments = totalExamAssignments,

                HomeworkCompletionRate =
                    totalHomeworkSets == 0 || totalStudents == 0
                        ? 0
                        : (submittedHomeworks * 100.0) /
                          (totalHomeworkSets * totalStudents),

                ExamParticipationRate =
                    totalExamAssignments == 0 || totalStudents == 0
                        ? 0
                        : (submittedExams * 100.0) /
                          (totalExamAssignments * totalStudents),

                AverageHomeworkScore = avgHomeworkScore,
                AverageExamScore = avgExamScore
            };
        }

        // =====================================================
        // تقرير الواجبات داخل الدفعة
        // =====================================================
        public BatchHomeworkReportVM GetBatchHomeworkReport(int batchId)
        {
            var batch = _context.Batches
                .AsNoTracking()
                .FirstOrDefault(b => b.Id == batchId && !b.IsDeleted);

            if (batch == null)
                return null;

            var totalStudents = _context.StudentBatchEnrollments
                .Count(x => x.BatchId == batchId && x.Status == "Active");

            // =========================
            // 1️⃣ جلب الواجبات
            // =========================
            var homeworkSets = _context.HomeworkSets
                .AsNoTracking()
                .Where(h => h.BatchId == batchId && h.IsSent)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new
                {
                    h.Id,
                    h.Title,
                    h.CreatedAt,
                    h.EndAt
                })
                .ToList();

            if (homeworkSets.Count == 0)
            {
                return new BatchHomeworkReportVM
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name
                };
            }

            // =========================
            // 2️⃣ جلب حالات الطلاب (JOIN صريح)
            // =========================
            var homeworkStudents = (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join hs in _context.HomeworkSets.AsNoTracking()
                    on hss.HomeworkSetId equals hs.Id
                where hs.BatchId == batchId
                      && hs.IsSent
                select new
                {
                    hss.HomeworkSetId,
                    hss.IsSubmitted,
                    hss.SubmittedAt,
                    hss.Score,
                    hs.EndAt
                }
            ).ToList();

            // =========================
            // 3️⃣ التجميع وبناء النتيجة
            // =========================
            var result = new BatchHomeworkReportVM
            {
                BatchId = batch.Id,
                BatchName = batch.Name
            };

            foreach (var hw in homeworkSets)
            {
                var students = homeworkStudents
                    .Where(x => x.HomeworkSetId == hw.Id)
                    .ToList();

                var submittedCount = students.Count(x => x.IsSubmitted);

                var lateCount = students.Count(x =>
                    x.IsSubmitted &&
                    hw.EndAt.HasValue &&
                    x.SubmittedAt.HasValue &&
                    x.SubmittedAt > hw.EndAt);

                var avgScore = students
                    .Where(x => x.Score.HasValue)
                    .Select(x => x.Score.Value)
                    .DefaultIfEmpty(0)
                    .Average();

                result.Homeworks.Add(new BatchHomeworkItemVM
                {
                    HomeworkSetId = hw.Id,
                    Title = hw.Title,
                    SentAt = hw.CreatedAt,

                    TotalStudents = totalStudents,
                    SubmittedCount = submittedCount,
                    LateCount = lateCount,

                    AverageScore = avgScore
                });
            }

            return result;
        }


        // =====================================================
        // تقرير الاختبارات داخل الدفعة
        // =====================================================
        public BatchExamReportVM GetBatchExamReport(int batchId)
        {
            var batch = _context.Batches
                .AsNoTracking()
                .FirstOrDefault(b => b.Id == batchId && !b.IsDeleted);

            if (batch == null)
                return null;

            var totalStudents = _context.StudentBatchEnrollments
                .Count(x => x.BatchId == batchId && x.Status == "Active");

            // =========================
            // 1️⃣ جلب الاختبارات المرسلة للدفعة
            // =========================
            var exams = _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .Where(e => e.BatchId == batchId && e.IsSentToStudents)
                .OrderByDescending(e => e.AssignedAt)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.AssignedAt
                })
                .ToList();

            if (exams.Count == 0)
            {
                return new BatchExamReportVM
                {
                    BatchId = batch.Id,
                    BatchName = batch.Name
                };
            }

            // =========================
            // 2️⃣ جلب حالات الطلاب (JOIN صريح)
            // =========================
            var examStatuses = (
                from es in _context.ExamStudentStatuses.AsNoTracking()
                join ea in _context.ExamAssignmentsToBatches.AsNoTracking()
                    on es.ExamAssignmentId equals ea.Id
                where ea.BatchId == batchId
                      && ea.IsSentToStudents
                select new
                {
                    es.ExamAssignmentId,
                    es.StartedAt,
                    es.IsSubmitted,
                    es.Score
                }
            ).ToList();

            // =========================
            // 3️⃣ التجميع وبناء النتيجة
            // =========================
            var result = new BatchExamReportVM
            {
                BatchId = batch.Id,
                BatchName = batch.Name
            };

            foreach (var exam in exams)
            {
                var statuses = examStatuses
                    .Where(x => x.ExamAssignmentId == exam.Id)
                    .ToList();

                var startedCount = statuses.Count(x => x.StartedAt.HasValue);
                var submittedCount = statuses.Count(x => x.IsSubmitted);

                var avgScore = statuses
                    .Where(x => x.Score.HasValue)
                    .Select(x => (double)x.Score.Value)
                    .DefaultIfEmpty(0)
                    .Average();

                result.Exams.Add(new BatchExamItemVM
                {
                    ExamAssignmentId = exam.Id,
                    Title = exam.Title,
                    AssignedAt = exam.AssignedAt,

                    TotalStudents = totalStudents,
                    StartedCount = startedCount,
                    SubmittedCount = submittedCount,

                    AverageScore = avgScore
                });
            }

            return result;
        }
    }
}
