using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

public class QuestionDifficultyService
{
    private readonly ApplicationDbContext _context;

    public QuestionDifficultyService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> RecalculateAsync(
      int? curriculumId,
      int? sectionId,
      int? lessonId)
    {
        var query = _context.Questions.AsQueryable();

        if (curriculumId.HasValue)
            query = query.Where(q => q.CurriculumId == curriculumId.Value);

        if (sectionId.HasValue)
            query = query.Where(q => q.SectionId == sectionId.Value);

        if (lessonId.HasValue)
            query = query.Where(q => q.LessonId == lessonId.Value);

        var questions = await query.ToListAsync();

        var updated = new List<Question>();

        foreach (var q in questions)
        {
            if (q.AttemptsCount < 10)
                continue;

            double rate = (double)q.CorrectAnswersCount / q.AttemptsCount * 100;

            if (rate <= 15)
                q.Difficulty = DifficultyLevel.VeryHard;
            else if (rate <= 49)
                q.Difficulty = DifficultyLevel.Hard;
            else if (rate <= 79)
                q.Difficulty = DifficultyLevel.Medium;
            else
                q.Difficulty = DifficultyLevel.Easy;

            updated.Add(q);
        }

        if (updated.Count == 0)
            return 0;

        await _context.BulkUpdateAsync(updated);

        return updated.Count;
    }
}