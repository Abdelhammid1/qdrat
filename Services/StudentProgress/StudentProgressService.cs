using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Services.StudentProgress
{
    public class StudentProgressService : IStudentProgressService
    {
        private readonly ApplicationDbContext _context;

        public StudentProgressService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ================================================================
        // 1) تسجيل التقدم في اختبار مؤشر الأداء Performance Indicator Exam
        // ================================================================
        public async Task RecordPerformanceIndicatorProgress(int studentId, int examId)
        {
            var exam = await _context.PerformanceIndicatorExams
                .Include(x => x.Batch)
                    .ThenInclude(b => b.Course)
                .Include(x => x.Sections)
                .FirstOrDefaultAsync(x => x.Id == examId);

            var studentExam = await _context.PerformanceIndicatorExamStudents
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.PerformanceIndicatorExamId == examId);

            // تأكيد البيانات الأساسية
            if (exam == null || exam.Batch?.Course == null || studentExam?.ScorePercent == null)
                return;

            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return;

            double score = studentExam.ScorePercent ?? 0;

            var progress = new QdratNew.Entities.StudentProgress
            {
                StudentID = studentId,
                Student = student,

                CourseID = exam.Batch.CourseId,
                Course = exam.Batch.Course,

                Date = DateTime.Now,
                Topic = "اختبار مؤشر الأداء",

                Score = score,
                ProgressPercentage = score,

                CompletedLessons = exam.Sections.Count,
                CompletedExercises = exam.Sections.Sum(s => s.Questions.Count),

                DifficultyLevel = score >= 80 ? "سهل"
                                : score >= 50 ? "متوسط"
                                : "صعب"
            };

            _context.StudentProgress.Add(progress);
            await _context.SaveChangesAsync();
        }


        // ================================================================
        // 2) تسجيل التقدم في الاختبارات العامة Exam Assignments
        // ================================================================
        public async Task RecordExamProgress(int studentId, int examAssignmentId)
        {
            // -------------------------------------------
            // 1) هل الاختبار فردي؟ موجود في ExamAssignments
            // -------------------------------------------
            var individualAssignment = await _context.ExamAssignments
                .Include(a => a.Exam)
                .FirstOrDefaultAsync(a => a.Id == examAssignmentId && a.StudentId == studentId);

            Exam exam = null;
            int? examId = null;
            int? courseId = null;
            Course course = null;

            if (individualAssignment != null)
            {
                // اختبار فردي
                exam = individualAssignment.Exam;
                examId = exam.Id;

                // جلب الدفعة للحصول على الكورس
                var batchLink = await _context.StudentBatchEnrollments
                    .Include(x => x.Batch)
                        .ThenInclude(b => b.Course)
                    .Where(x => x.StudentID == studentId)
                    .FirstOrDefaultAsync();

                if (batchLink != null)
                {
                    courseId = batchLink.Batch.CourseId;
                    course = batchLink.Batch.Course;
                }
            }
            else
            {
                // -------------------------------------------
                // 2) إذن الاختبار من اختبارات الدفعة
                //     ExamAssignmentsToBatches
                // -------------------------------------------

                var batchAssignment = await _context.ExamAssignmentsToBatches
                    .Include(a => a.Exam)
                    .Include(a => a.Batch)
                        .ThenInclude(b => b.Course)
                    .FirstOrDefaultAsync(a => a.Id == examAssignmentId);

                if (batchAssignment == null)
                    return;

                exam = batchAssignment.Exam;
                examId = exam.Id;
                courseId = batchAssignment.Batch?.CourseId;
                course = batchAssignment.Batch?.Course;
            }

            if (exam == null || courseId == null || course == null)
                return;

            // -------------------------------------------
            // 3) تحميل الطالب
            // -------------------------------------------
            var student = await _context.Students.FindAsync(studentId);
            if (student == null) return;

            // -------------------------------------------
            // 4) جلب محاولات الطالب
            // -------------------------------------------
            var attempts = await _context.QuestionAttemptNew
                .Where(a =>
                    a.StudentId == studentId &&
                    (
                        a.ExamAssignmentId == examAssignmentId ||
                        a.ExamId == examId
                    ))
                .ToListAsync();

            if (!attempts.Any())
                return;

            int total = attempts.Count;
            int correct = attempts.Count(a => a.IsCorrect);
            double score = Math.Round(correct * 100.0 / total, 2);

            int completedLessons = attempts
                .Where(a => a.LessonId != null)
                .Select(a => a.LessonId)
                .Distinct()
                .Count();

            // -------------------------------------------
            // 5) إنشاء سجل StudentProgress
            // -------------------------------------------
            var progress = new QdratNew.Entities.StudentProgress
            {
                StudentID = studentId,
                Student = student,

                CourseID = courseId.Value,
                Course = course,

                Date = DateTime.Now,
                Topic = $"اختبار: {exam.Title}",

                Score = score,
                ProgressPercentage = score,

                CompletedLessons = completedLessons,
                CompletedExercises = total,

                DifficultyLevel =
                    score >= 80 ? "سهل" :
                    score >= 50 ? "متوسط" :
                    "صعب"
            };

            _context.StudentProgress.Add(progress);
            await _context.SaveChangesAsync();
        }


        // ================================================================
        // 3) تسجيل التقدم في الواجبات Homework
        // ================================================================
        public async Task RecordHomeworkProgress(int studentId, int homeworkSetId)
        {
            var homeworkSet = await _context.HomeworkSets
                .Include(hs => hs.Batch)
                    .ThenInclude(b => b.Course)
                .FirstOrDefaultAsync(hs => hs.Id == homeworkSetId);

            if (homeworkSet == null || homeworkSet.Batch?.Course == null)
                return;

            var student = await _context.Students.FindAsync(studentId);
            if (student == null)
                return;

            var attempts = await _context.QuestionAttemptNew
                .Where(a => a.StudentId == studentId && a.HomeworkSetId == homeworkSetId)
                .ToListAsync();

            if (!attempts.Any()) return;

            int total = attempts.Count;
            int correct = attempts.Count(a => a.IsCorrect);
            double percentage = Math.Round(correct * 100.0 / total, 2);

            int completedLessons = await (
                from a in _context.QuestionAttemptNew
                join q in _context.Questions on a.QuestionId equals q.Id
                join l in _context.Lessons on q.LessonId equals l.Id
                where a.StudentId == studentId && a.HomeworkSetId == homeworkSetId
                select l.Id
            ).Distinct().CountAsync();

            var progress = new QdratNew.Entities.StudentProgress
            {
                StudentID = studentId,
                Student = student,

                CourseID = homeworkSet.Batch.CourseId,
                Course = homeworkSet.Batch.Course,

                Date = DateTime.Now,
                Topic = $"واجب: {homeworkSet.Title}",

                Score = percentage,
                ProgressPercentage = percentage,

                CompletedLessons = completedLessons,
                CompletedExercises = total,

                DifficultyLevel = percentage >= 80 ? "سهل"
                                : percentage >= 50 ? "متوسط"
                                : "صعب"
            };

            _context.StudentProgress.Add(progress);
            await _context.SaveChangesAsync();
        }
    }
}
