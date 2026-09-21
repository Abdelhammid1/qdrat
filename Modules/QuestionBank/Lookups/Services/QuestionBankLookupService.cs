using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Modules.QuestionBank.Lookups.Contracts;
using QdratNew.Modules.QuestionBank.Lookups.Dtos;

namespace QdratNew.Modules.QuestionBank.Lookups.Services
{
    public class QuestionBankLookupService : IQuestionBankLookupService
    {
        private readonly ApplicationDbContext _context;

        public QuestionBankLookupService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===================== CURRICULUM =====================
        public async Task<List<CurriculumLookupDto>> GetCurriculumsAsync()
        {
            return await _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsRejected)
                .Select(q => new
                {
                    q.CurriculumId,
                    q.Curriculum.Title
                })
                .Distinct()
                .OrderBy(x => x.Title)
                .Select(x => new CurriculumLookupDto
                {
                    Id = x.CurriculumId,
                    Title = x.Title
                })
                .ToListAsync();
        }

        // ===================== SECTIONS =====================
        public async Task<List<SectionLookupDto>> GetSectionsByCurriculumAsync(int curriculumId)
        {
            return await _context.Questions
                .AsNoTracking()
                .Where(q =>
                    q.CurriculumId == curriculumId &&
                    q.SectionId.HasValue &&
                    !q.IsRejected)
                .Select(q => new
                {
                    q.SectionId.Value,
                    q.Section.Title
                })
                .Distinct()
                .OrderBy(x => x.Title)
                .Select(x => new SectionLookupDto
                {
                    Id = x.Value,
                    Title = x.Title
                })
                .ToListAsync();
        }

        // ===================== LESSONS (ACTIVE ONLY) =====================
        public async Task<List<LessonLookupDto>> GetLessonsBySectionAsync(int sectionId)
        {
            return await _context.Questions
                .AsNoTracking()
                .Where(q =>
                    q.SectionId == sectionId &&
                    !q.IsRejected &&
                    q.Lesson != null &&
                    q.Lesson.IsActive)   // ✅ تأكيد أن الليسون فعّال
                .Select(q => new
                {
                    q.LessonId,
                    q.Lesson.Title
                })
                .Distinct()
                .OrderBy(x => x.Title)
                .Select(x => new LessonLookupDto
                {
                    Id = x.LessonId,
                    Title = x.Title
                })
                .ToListAsync();
        }
    }
}
