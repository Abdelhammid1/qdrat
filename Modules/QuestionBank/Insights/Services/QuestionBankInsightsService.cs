using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Modules.QuestionBank.Insights.Dtos;

namespace QdratNew.Modules.QuestionBank.Insights.Services
{
    public class QuestionBankInsightsService
    {
        private readonly ApplicationDbContext _context;

        public QuestionBankInsightsService(ApplicationDbContext context)
        {
            _context = context;
        }

        private IQueryable<Question> BuildBaseQuery(
            int? curriculumId,
            int? sectionId,
            int? lessonId)
        {
            var query = _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsRejected);

            if (curriculumId.HasValue)
                query = query.Where(q => q.CurriculumId == curriculumId.Value);

            if (sectionId.HasValue)
                query = query.Where(q => q.SectionId == sectionId.Value);

            if (lessonId.HasValue)
                query = query.Where(q => q.LessonId == lessonId.Value);

            return query;
        }

        public async Task<QuestionBankInsightsDto> GetInsightsAsync(
            int? curriculumId,
            int? sectionId,
            int? lessonId)
        {
            var query = BuildBaseQuery(curriculumId, sectionId, lessonId);

            // =====================
            // 1️⃣ الحالة العامة
            // =====================
            var aggregate = await query
                .GroupBy(q => 1)
                .Select(g => new
                {
                    Approved = g.Count(q =>
                        q.IsComplete &&
                        q.IsReviewed &&
                        !string.IsNullOrWhiteSpace(q.CorrectAnswer)),

                    ReadyForReview = g.Count(q =>
                        q.IsComplete &&
                        !q.IsReviewed &&
                        !string.IsNullOrWhiteSpace(q.CorrectAnswer)),

                    MissingAnswer = g.Count(q =>
                        q.IsComplete &&
                        string.IsNullOrWhiteSpace(q.CorrectAnswer)),

                    Incomplete = g.Count(q => !q.IsComplete),
                    Complete = g.Count(q => q.IsComplete),
                    Reviewed = g.Count(q => q.IsReviewed),
                    WithCorrectAnswer = g.Count(q => !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                })
                .FirstOrDefaultAsync();

            var overall = new OverallStatusDto
            {
                Approved = aggregate?.Approved ?? 0,
                ReadyForReview = aggregate?.ReadyForReview ?? 0,
                MissingAnswer = aggregate?.MissingAnswer ?? 0,
                Incomplete = aggregate?.Incomplete ?? 0
            };

            // =====================
            // 2️⃣ جودة الأسئلة
            // =====================
            var quality = new QualityDto
            {
                Complete = aggregate?.Complete ?? 0,
                Reviewed = aggregate?.Reviewed ?? 0,
                WithCorrectAnswer = aggregate?.WithCorrectAnswer ?? 0
            };

            // =====================
            // 3️⃣ تغطية المناهج
            // =====================
            var coverage = await query
                .GroupBy(q => q.Curriculum.Title)
                .Select(g => new CurriculumCoverageDto
                {
                    Name = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(x => x.Count)
                .ToListAsync();

            return new QuestionBankInsightsDto
            {
                OverallStatus = overall,
                Quality = quality,
                CurriculumCoverage = coverage
            };
        }
    }
}
