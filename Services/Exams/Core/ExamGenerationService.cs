using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Abstractions;

namespace QdratNew.Services.Exams.Core
{
    public class ExamGenerationService : IExamGenerationService
    {
        private readonly ApplicationDbContext _context;

        public ExamGenerationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<int> GenerateExamDraftAsync(int instructorId, int curriculumId, string title)
        {
            var draft = new ExamDraft
            {
                Title = title,
                CurriculumId = curriculumId,
                CreatedByInstructorId = instructorId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ExamDrafts.Add(draft);
            await _context.SaveChangesAsync();

            return draft.Id;
        }
    }
}