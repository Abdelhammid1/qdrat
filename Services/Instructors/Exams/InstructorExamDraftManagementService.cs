using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

public class InstructorExamDraftManagementService
{
    private readonly ApplicationDbContext _context;

    public InstructorExamDraftManagementService(ApplicationDbContext context)
    {
        _context = context;
    }

    // =====================================================
    // ➕ ADD QUESTION
    // =====================================================
    public async Task<bool> AddQuestionAsync(int draftId, Guid questionId)
    {
        // تحقق من وجود السؤال
        var existsInDb = await _context.Questions
            .AnyAsync(q => q.Id == questionId);

        if (!existsInDb)
            return false;

        // منع التكرار
        var exists = await _context.ExamDraftQuestions
            .AnyAsync(x => x.ExamDraftId == draftId && x.QuestionId == questionId);

        if (exists)
            return false;

        // الحصول على الترتيب
        var maxOrder = await _context.ExamDraftQuestions
            .Where(x => x.ExamDraftId == draftId)
            .Select(x => (int?)x.Order)
            .MaxAsync() ?? 0;

        _context.ExamDraftQuestions.Add(new ExamDraftQuestion
        {
            ExamDraftId = draftId,
            QuestionId = questionId,
            Order = maxOrder + 1
        });

        await _context.SaveChangesAsync();

        return true;
    }

    // =====================================================
    // 🔁 REPLACE QUESTION
    // =====================================================
    public async Task<bool> ReplaceQuestionAsync(int draftId, Guid oldQuestionId, Guid newQuestionId)
    {
        // تحقق من وجود الجديد
        var newQuestion = await _context.Questions
            .AsNoTracking()
            .Where(q => q.Id == newQuestionId)
            .Select(q => new { q.Id, q.LessonId })
            .FirstOrDefaultAsync();

        if (newQuestion == null)
            return false;

        // منع التكرار
        var exists = await _context.ExamDraftQuestions
            .AnyAsync(x =>
                x.ExamDraftId == draftId &&
                x.QuestionId == newQuestionId);

        if (exists)
            return false;

        // جلب القديم
        var oldItem = await _context.ExamDraftQuestions
            .FirstOrDefaultAsync(x =>
                x.ExamDraftId == draftId &&
                x.QuestionId == oldQuestionId);

        if (oldItem == null)
            return false;

        // تحديث مباشر
        oldItem.QuestionId = newQuestionId;

        await _context.SaveChangesAsync();

        return true;
    }

    // =====================================================
    // ❌ REMOVE QUESTION
    // =====================================================
    public async Task<bool> RemoveQuestionAsync(int draftId, Guid questionId)
    {
        var item = await _context.ExamDraftQuestions
            .FirstOrDefaultAsync(x =>
                x.ExamDraftId == draftId &&
                x.QuestionId == questionId);

        if (item == null)
            return false;

        _context.ExamDraftQuestions.Remove(item);

        await _context.SaveChangesAsync();

        return true;
    }
}