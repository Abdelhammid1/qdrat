using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    public class SectionExamGeneratorService : ISectionExamGeneratorService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        public SectionExamGeneratorService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }


        public async Task CheckAndGenerateExamAsync(int batchId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // هل تم توليد الاختبار سابقًا؟
            bool exists = await _context.ExamAssignmentsToBatches
                .AnyAsync(e => e.BatchId == batchId && e.SectionId == sectionId);

            if (exists) return;

            // التأكد من اكتمال كل دروس المحور
            var unitIds = await _context.SectionUnits
                .Where(su => su.SectionId == sectionId)
                .Select(su => su.UnitId)
                .ToListAsync();

            var allLessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId && l.IsActive && unitIds.Contains(l.UnitId))
                .ToListAsync();

            var lessonIds = allLessons.Select(l => l.Id).ToList();

            var completedLessons = await _context.BatchLessonCompletions
                .Where(bc => bc.BatchId == batchId && lessonIds.Contains(bc.LessonId))
                .Select(bc => bc.LessonId)
                .Distinct()
                .ToListAsync();

            if (completedLessons.Count < allLessons.Count)
                return; // لم تكتمل كل المؤشرات

            // استخراج الأسئلة
            var questions = await _context.Questions
                .Where(q => lessonIds.Contains(q.LessonId)
                    && q.IsComplete
                    && q.IsReviewed
                    && !q.IsRejected
                    && !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .ToListAsync();

            if (questions.Count < 10)
                return; // أسئلة غير كافية

            // توليد الاختبار
            var exam = new Exam
            {
                Title = $"اختبار تلقائي - المحور {DateTime.Now:yyyy-MM-dd}",
                CurriculumId = await _context.Sections.Where(s => s.Id == sectionId).Select(s => s.CurriculumId).FirstOrDefaultAsync(),
                CourseId = await _context.Batches.Where(b => b.Id == batchId).Select(b => b.CourseId).FirstOrDefaultAsync(),
                LessonId = lessonIds.First(),
                TotalQuestions = questions.Count,
                DurationMinutes = 30,
                CreatedAt = DateTime.Now
            };
            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            var assignment = new ExamAssignmentToBatch
            {
                BatchId = batchId,
                CurriculumId = exam.CurriculumId,
                SectionId = sectionId,
                LessonId = exam.LessonId,
                Title = $"اختبار تلقائي على المحور {DateTime.Now:yyyy-MM-dd}",
                TotalQuestions = questions.Count,
                DurationMinutes = 30,
                IsSentToStudents = false,
                CreatedAt = DateTime.Now,
                ExamId = exam.Id
            };

            _context.ExamAssignmentsToBatches.Add(assignment);
            await _context.SaveChangesAsync();

            int order = 1;
            foreach (var q in questions.Take(assignment.TotalQuestions))
            {
                _context.ExamQuestions.Add(new ExamQuestion
                {
                    ExamAssignmentId = assignment.Id,
                    ExamId = exam.Id,
                    QuestionId = q.Id,
                    Order = order++,
                    IsManuallySelected = false
                });
            }

            await _context.SaveChangesAsync();
        }
    }

}
