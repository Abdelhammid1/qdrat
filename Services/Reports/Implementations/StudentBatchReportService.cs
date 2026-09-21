using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Reports.Interfaces;
using QdratNew.ViewModels.Partner.Reports;

namespace QdratNew.Services.Reports.Implementations
{
    /// <summary>
    /// تنفيذ تقارير طالب داخل دفعة
    /// (تفصيلي – إداري – قابل للطباعة لاحقًا)
    /// </summary>
    public class StudentBatchReportService : IStudentBatchReportService
    {
        private readonly ApplicationDbContext _context;

        public StudentBatchReportService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // التقرير الشامل للطالب داخل الدفعة
        // =====================================================
        public StudentInBatchReportVM GetStudentReport(int batchId, int studentId)
        {
            var student = _context.Students
                .AsNoTracking()
                .FirstOrDefault(s => s.StudentID == studentId);

            if (student == null)
                return null;

            var batch = _context.Batches
                .AsNoTracking()
                .FirstOrDefault(b => b.Id == batchId && !b.IsDeleted);

            if (batch == null)
                return null;

            // ===============================
            // واجبات الطالب (JOIN صريح)
            // ===============================
            var homeworkData = (
                from hs in _context.HomeworkSets.AsNoTracking()
                join hss in _context.HomeworkSetStudents.AsNoTracking()
                    on hs.Id equals hss.HomeworkSetId
                where hs.BatchId == batchId
                      && hss.StudentId == studentId
                      && hs.IsSent
                select new
                {
                    hs.Id,
                    hs.Title,
                    hss.IsSubmitted,
                    hss.SubmittedAt,
                    hss.Score,
                    hs.EndAt
                }
            ).ToList();

            var totalHomeworks = homeworkData.Count;
            var submittedHomeworks = homeworkData.Count(x => x.IsSubmitted);

            var avgHomeworkScore = homeworkData
                .Where(x => x.Score.HasValue)
                .Select(x => x.Score.Value)
                .DefaultIfEmpty(0)
                .Average();

            // ===============================
            // اختبارات الطالب (JOIN صريح)
            // ===============================
            var examData = (
                from ea in _context.ExamAssignmentsToBatches.AsNoTracking()
                join es in _context.ExamStudentStatuses.AsNoTracking()
                    on ea.Id equals es.ExamAssignmentId
                where ea.BatchId == batchId
                      && es.StudentId == studentId
                      && ea.IsSentToStudents
                select new
                {
                    ea.Id,
                    ea.Title,
                    es.StartedAt,
                    es.IsSubmitted,
                    es.Score
                }
            ).ToList();

            var totalExams = examData.Count;
            var submittedExams = examData.Count(x => x.IsSubmitted);

            var avgExamScore = examData
                .Where(x => x.Score.HasValue)
                .Select(x => (double)x.Score.Value)
                .DefaultIfEmpty(0)
                .Average();

            var vm = new StudentInBatchReportVM
            {
                StudentId = student.StudentID,
                StudentName = student.FullName,

                BatchId = batch.Id,
                BatchName = batch.Name,

                HomeworkCompletionRate =
                    totalHomeworks == 0
                        ? 0
                        : (submittedHomeworks * 100.0) / totalHomeworks,

                ExamCompletionRate =
                    totalExams == 0
                        ? 0
                        : (submittedExams * 100.0) / totalExams,

                AverageHomeworkScore = avgHomeworkScore,
                AverageExamScore = avgExamScore
            };

            // ===============================
            // ملخص الواجبات
            // ===============================
            foreach (var hw in homeworkData)
            {
                vm.Homeworks.Add(new StudentHomeworkSummaryVM
                {
                    HomeworkSetId = hw.Id,
                    Title = hw.Title,
                    IsSubmitted = hw.IsSubmitted,
                    IsLate =
                        hw.IsSubmitted &&
                        hw.EndAt.HasValue &&
                        hw.SubmittedAt.HasValue &&
                        hw.SubmittedAt > hw.EndAt,
                    Score = hw.Score
                });
            }

            // ===============================
            // ملخص الاختبارات
            // ===============================
            foreach (var ex in examData)
            {
                vm.Exams.Add(new StudentExamSummaryVM
                {
                    ExamAssignmentId = ex.Id,
                    Title = ex.Title,
                    HasStarted = ex.StartedAt.HasValue,
                    IsSubmitted = ex.IsSubmitted,
                    Score = ex.Score
                });
            }

            return vm;
        }

        // =====================================================
        // تقرير مفصل لواجب واحد للطالب
        // =====================================================
        public StudentHomeworkDetailVM GetStudentHomeworkDetails(
            int homeworkSetId,
            int studentId)
        {
            var data = (
                from hs in _context.HomeworkSets.AsNoTracking()
                join hss in _context.HomeworkSetStudents.AsNoTracking()
                    on hs.Id equals hss.HomeworkSetId
                join s in _context.Students.AsNoTracking()
                    on hss.StudentId equals s.StudentID
                where hs.Id == homeworkSetId
                      && hss.StudentId == studentId
                select new
                {
                    hs.Id,
                    hs.Title,
                    hs.CreatedAt,
                    hs.EndAt,
                    hss.SubmittedAt,
                    hss.Score,
                    StudentName = s.FullName
                }
            ).FirstOrDefault();

            if (data == null)
                return null;

            var attemptsCount = _context.HomeworkSetAttempts
                .Count(a =>
                    a.HomeworkSetId == homeworkSetId &&
                    a.StudentId == studentId);

            return new StudentHomeworkDetailVM
            {
                HomeworkSetId = data.Id,
                HomeworkTitle = data.Title,

                StudentId = studentId,
                StudentName = data.StudentName,

                AssignedAt = data.CreatedAt,
                SubmittedAt = data.SubmittedAt,

                AttemptCount = attemptsCount,
                Score = data.Score,

                IsLate =
                    data.SubmittedAt.HasValue &&
                    data.EndAt.HasValue &&
                    data.SubmittedAt > data.EndAt
            };
        }

        // =====================================================
        // تقرير مفصل لاختبار واحد للطالب
        // =====================================================
        public StudentExamDetailVM GetStudentExamDetails(
            int examAssignmentId,
            int studentId)
        {
            var data = (
                from ea in _context.ExamAssignmentsToBatches.AsNoTracking()
                join es in _context.ExamStudentStatuses.AsNoTracking()
                    on ea.Id equals es.ExamAssignmentId
                join s in _context.Students.AsNoTracking()
                    on es.StudentId equals s.StudentID
                where ea.Id == examAssignmentId
                      && es.StudentId == studentId
                select new
                {
                    ea.Id,
                    ea.Title,
                    ea.AssignedAt,
                    ea.DurationMinutes,
                    es.StartedAt,
                    es.SubmittedAt,
                    es.Score,
                    es.Status,
                    StudentName = s.FullName
                }
            ).FirstOrDefault();

            if (data == null)
                return null;

            return new StudentExamDetailVM
            {
                ExamAssignmentId = data.Id,
                ExamTitle = data.Title,

                StudentId = studentId,
                StudentName = data.StudentName,

                AssignedAt = data.AssignedAt,
                StartedAt = data.StartedAt,
                SubmittedAt = data.SubmittedAt,

                DurationMinutes = data.DurationMinutes,
                Score = data.Score,

                Status = data.Status.ToString()
            };
        }


        public HomeworkStudentsVM GetHomeworkStudents(int homeworkSetId)
        {
            var homework = _context.HomeworkSets
                .AsNoTracking()
                .FirstOrDefault(h => h.Id == homeworkSetId);

            if (homework == null)
                return null;

            var data = (
                from hss in _context.HomeworkSetStudents.AsNoTracking()
                join s in _context.Students.AsNoTracking()
                    on hss.StudentId equals s.StudentID
                where hss.HomeworkSetId == homeworkSetId
                select new
                {
                    s.StudentID,
                    s.FullName,
                    hss.IsSubmitted,
                    hss.SubmittedAt,
                    hss.Score,
                    homework.EndAt
                }
            ).ToList();

            var vm = new HomeworkStudentsVM
            {
                HomeworkSetId = homework.Id,
                HomeworkTitle = homework.Title
            };

            foreach (var x in data)
            {
                vm.Students.Add(new HomeworkStudentItemVM
                {
                    StudentId = x.StudentID,
                    StudentName = x.FullName,
                    IsSubmitted = x.IsSubmitted,
                    SubmittedAt = x.SubmittedAt,
                    Score = x.Score,
                    IsLate =
                        x.IsSubmitted &&
                        x.SubmittedAt.HasValue &&
                        homework.EndAt.HasValue &&
                        x.SubmittedAt > homework.EndAt
                });
            }

            return vm;
        }


        public ExamStudentsVM GetExamStudents(int examAssignmentId)
        {
            var exam = _context.ExamAssignmentsToBatches
                .AsNoTracking()
                .FirstOrDefault(e => e.Id == examAssignmentId);

            if (exam == null)
                return null;

            var data = (
                from es in _context.ExamStudentStatuses.AsNoTracking()
                join s in _context.Students.AsNoTracking()
                    on es.StudentId equals s.StudentID
                where es.ExamAssignmentId == examAssignmentId
                select new
                {
                    s.StudentID,
                    s.FullName,
                    es.StartedAt,
                    es.SubmittedAt,
                    es.IsSubmitted,
                    es.Score,
                    es.Status
                }
            ).ToList();

            var vm = new ExamStudentsVM
            {
                ExamAssignmentId = exam.Id,
                ExamTitle = exam.Title
            };

            foreach (var x in data)
            {
                vm.Students.Add(new ExamStudentItemVM
                {
                    StudentId = x.StudentID,
                    StudentName = x.FullName,

                    HasStarted = x.StartedAt.HasValue,
                    IsSubmitted = x.IsSubmitted,

                    StartedAt = x.StartedAt,
                    SubmittedAt = x.SubmittedAt,

                    Score = x.Score,
                    Status = x.Status.ToString()
                });
            }

            return vm;
        }





    }
}
