using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Modules.QuestionBank.Read.Contracts;
using QdratNew.Modules.QuestionBank.Read.Dtos;

namespace QdratNew.Modules.QuestionBank.Read.Services
{
    public class QuestionBankReadService : IQuestionBankReadService
    {
        private readonly ApplicationDbContext _context;

        public QuestionBankReadService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===============================
        // MAIN QUERY ENGINE
        // ===============================
        public async Task<QuestionBankQueryResultDto> QueryAsync(QuestionBankQueryRequestDto request)
        {
            var query = BuildBaseQuery(request);

            // =====================
            // SEARCH AND STATUS FILTERS IN SQL
            // =====================
            if (!string.IsNullOrWhiteSpace(request.SearchText))
            {
                var search = request.SearchText.Trim();

                query = query.Where(q =>
                    (q.Title != null && q.Title.Contains(search)) ||
                    (q.ReferenceNumber != null && q.ReferenceNumber.Contains(search)) ||
                    (q.InternalNote != null && q.InternalNote.Contains(search)));
            }

            query = ApplyStatusFilter(query, request.Status);

            var totalCount = await query.CountAsync();

            // =====================
            // DATABASE PAGING
            // =====================
            var page = request.Page < 1 ? 1 : request.Page;
            var pageSize = request.PageSize < 1 ? 50 : Math.Min(request.PageSize, 100);

            var items = await query
                .OrderByDescending(q => q.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new QuestionBankItemDto
                {
                    Id = q.Id,
                    ReferenceNumber = q.ReferenceNumber ?? string.Empty,
                    Title = q.Title ?? string.Empty,
                    InternalNote = q.InternalNote,

                    CurriculumTitle = q.CurriculumTitle ?? string.Empty,
                    SectionTitle = q.SectionTitle ?? string.Empty,
                    LessonTitle = q.LessonTitle ?? string.Empty,
                    IsRTL = q.IsRTL,

                    CreatedAt = q.CreatedAt,
                    Status = ResolveStatus(q.IsComplete, q.IsReviewed, q.CorrectAnswer)
                })
                .ToListAsync();

            return new QuestionBankQueryResultDto
            {
                TotalCount = totalCount,
                Items = items
            };
        }

        // ===============================
        // DECISION MAKER STATS
        // ===============================
        public async Task<QuestionBankStatsDto> GetStatsAsync(int? curriculumId, int? sectionId)
        {
            var query = _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsRejected);

            if (curriculumId.HasValue)
                query = query.Where(q => q.CurriculumId == curriculumId.Value);

            if (sectionId.HasValue)
                query = query.Where(q => q.SectionId == sectionId.Value);

            var stats = await query
                .GroupBy(q => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Incomplete = g.Count(q => !q.IsComplete),
                    MissingAnswer = g.Count(q => q.IsComplete && string.IsNullOrWhiteSpace(q.CorrectAnswer)),
                    Ready = g.Count(q => q.IsComplete && !string.IsNullOrWhiteSpace(q.CorrectAnswer) && !q.IsReviewed),
                    Approved = g.Count(q => q.IsComplete && q.IsReviewed && !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                })
                .FirstOrDefaultAsync();

            var total = stats?.Total ?? 0;
            var approved = stats?.Approved ?? 0;

            return new QuestionBankStatsDto
            {
                TotalQuestions = total,
                IncompleteCount = stats?.Incomplete ?? 0,
                MissingAnswerCount = stats?.MissingAnswer ?? 0,
                ReadyForReviewCount = stats?.Ready ?? 0,
                ApprovedCount = approved,
                CompletionRate = total == 0 ? 0 : Math.Round((approved * 100.0) / total, 2),
                ApprovalRate = total == 0 ? 0 : Math.Round((approved * 100.0) / total, 2),
                GeneratedAt = DateTime.UtcNow
            };
        }

        private IQueryable<QuestionBankQuestionRow> BuildBaseQuery(QuestionBankQueryRequestDto request)
        {
            var query = _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsRejected);

            if (request.CurriculumId.HasValue)
                query = query.Where(q => q.CurriculumId == request.CurriculumId.Value);

            if (request.SectionId.HasValue)
                query = query.Where(q => q.SectionId == request.SectionId.Value);

            if (request.LessonId.HasValue)
                query = query.Where(q => q.LessonId == request.LessonId.Value);

            if (request.OnlyApproved)
                query = query.Where(q => q.IsComplete && q.IsReviewed && !string.IsNullOrWhiteSpace(q.CorrectAnswer));

            if (!request.IncludeIncomplete)
                query = query.Where(q => q.IsComplete);

            if (!request.IncludeWithoutAnswer)
                query = query.Where(q => !string.IsNullOrWhiteSpace(q.CorrectAnswer));

            return query.Select(q => new QuestionBankQuestionRow
            {
                Id = q.Id,
                ReferenceNumber = q.ReferenceNumber,
                Title = q.Title,
                InternalNote = q.InternalNote,
                IsComplete = q.IsComplete,
                IsReviewed = q.IsReviewed,
                CorrectAnswer = q.CorrectAnswer,
                CreatedAt = q.CreatedAt,
                CurriculumTitle = q.Curriculum.Title,
                SectionTitle = q.Section != null ? q.Section.Title : null,
                LessonTitle = q.Lesson.Title,
                IsRTL = q.Curriculum.IsRTL
            });
        }

        private static IQueryable<QuestionBankQuestionRow> ApplyStatusFilter(
            IQueryable<QuestionBankQuestionRow> query,
            string? status)
        {
            return status switch
            {
                "Approved" => query.Where(q =>
                    q.IsComplete &&
                    q.IsReviewed &&
                    !string.IsNullOrWhiteSpace(q.CorrectAnswer)),

                "ReadyForReview" => query.Where(q =>
                    q.IsComplete &&
                    !q.IsReviewed &&
                    !string.IsNullOrWhiteSpace(q.CorrectAnswer)),

                "MissingAnswer" => query.Where(q =>
                    q.IsComplete &&
                    string.IsNullOrWhiteSpace(q.CorrectAnswer)),

                "Incomplete" => query.Where(q => !q.IsComplete),

                _ => query
            };
        }

        // ===============================
        // INTERNAL STATUS RESOLVER
        // ===============================
        private static string ResolveStatus(bool isComplete, bool isReviewed, string? correctAnswer)
        {
            if (!isComplete)
                return "Incomplete";

            if (string.IsNullOrWhiteSpace(correctAnswer))
                return "MissingAnswer";

            if (!isReviewed)
                return "ReadyForReview";

            return "Approved";
        }

        private sealed class QuestionBankQuestionRow
        {
            public Guid Id { get; set; }
            public string? ReferenceNumber { get; set; }
            public string? Title { get; set; }
            public string? InternalNote { get; set; }
            public bool IsComplete { get; set; }
            public bool IsReviewed { get; set; }
            public string? CorrectAnswer { get; set; }
            public DateTime CreatedAt { get; set; }
            public string? CurriculumTitle { get; set; }
            public string? SectionTitle { get; set; }
            public string? LessonTitle { get; set; }
            public bool IsRTL { get; set; }
        }
    }
}
