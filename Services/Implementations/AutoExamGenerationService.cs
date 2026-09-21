using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Implementations
{
    public class AutoExamGenerationService : IAutoExamGenerationService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ICurriculumExamGeneratorService _examGenerator;

        public AutoExamGenerationService(IDbContextFactory<ApplicationDbContext> contextFactory, ICurriculumExamGeneratorService examGenerator)
        {
            _contextFactory = contextFactory;

            _examGenerator = examGenerator;
        }

        public async Task<int?> TryGenerateExamIfSectionCompletedAsync(int batchId, int sectionId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // ✅ جلب المؤشرات المرتبطة بهذا المحور والدفعة وتم تسجيلها
            var completedLessons = await _context.BatchLessonCompletions
                .Where(b => b.BatchId == batchId && b.SectionId == sectionId)
                .Select(b => b.LessonId)
                .Distinct()
                .ToListAsync();

            if (!completedLessons.Any())
                return null;

            // ✅ التحقق من أن كل هذه المؤشرات نشطة
            var activeLessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId && l.IsActive)
                .Select(l => l.Id)
                .ToListAsync();

            var allActiveCompleted = activeLessons.All(id => completedLessons.Contains(id));

            if (!allActiveCompleted)
                return null;

            // ✅ جلب المنهج المرتبط بالمحور والدفعة
            var curriculumId = await (
                from b in _context.Batches
                join c in _context.Courses on b.CourseId equals c.Id
                join cc in _context.CourseCurriculums on c.Id equals cc.CourseId
                join cu in _context.Curriculums on cc.CurriculumId equals cu.Id
                where b.Id == batchId
                select cu.Id
            ).FirstOrDefaultAsync();



            if (curriculumId == 0)
                return null;

            // ✅ التحقق من أن الاختبار لم يتم توليده سابقًا
            var alreadyExists = await _context.ExamAssignmentsToBatches
                .AnyAsync(e => e.BatchId == batchId && e.SectionId == sectionId);

            if (alreadyExists)
                return null;

            // ✅ توليد الاختبار
            var assignmentId = await _examGenerator.GenerateExamForCurriculumAsync(curriculumId, batchId, sectionId);
            return assignmentId == 0 ? null : assignmentId;
        }
    }
}
