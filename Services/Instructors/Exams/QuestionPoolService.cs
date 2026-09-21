using QdratNew.Data;
using QdratNew.Services.Instructors.Interfaces;

public class QuestionPoolService : IQuestionPoolService
{
    private readonly ApplicationDbContext _context;

    public QuestionPoolService(ApplicationDbContext context)
    {
        _context = context;
    }

    public List<Guid> FilterUsedQuestions(List<Guid> questionIds, int studentId)
    {
        if (questionIds == null || !questionIds.Any())
            return new List<Guid>();

        // ============================
        // جلب كل الأسئلة التي حلها الطالب
        // ============================
        var usedQuestions = _context.QuestionAttemptNew
            .Where(x => x.StudentId == studentId)
            .Select(x => x.QuestionId)
            .ToList();

        // ============================
        // فلترة في الذاكرة (SQL 2014 SAFE)
        // ============================
        var filtered = questionIds
            .Where(q => !usedQuestions.Any(u => u == q))
            .ToList();

        return filtered;
    }
}