using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.ViewModels.Partner.Homework;
using QdratNew.ViewModels.Partner.HomeworkDraft;
using EFCore.BulkExtensions;


namespace QdratNew.Services.HomeworkEngine.Drafts
{
    public class HomeworkDraftEngineService : IHomeworkDraftEngineService
    {
        private readonly ApplicationDbContext _context;

        public HomeworkDraftEngineService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ======================================================
        // Draft List
        // ======================================================
        public List<HomeworkDraftListVM> GetDrafts(int ownerId, int? subscriptionPeriodId)
        {
            var query = _context.HomeworkDrafts
                .Include(d => d.Questions)
                .Where(d =>
                    d.PartnerId == ownerId &&
                    !d.IsArchived &&
                    !d.IsDeleted);

            if (subscriptionPeriodId.HasValue)
            {
                query = query.Where(d =>
                    d.SubscriptionPeriodId == subscriptionPeriodId.Value);
            }

            var drafts = query.ToList();

            // جلب كل الدورات بدون Contains
            var courses = _context.Courses
                .AsNoTracking()
                .ToDictionary(c => c.Id, c => c.Name);

            return drafts
                .Select(d => new HomeworkDraftListVM
                {
                    DraftId = d.Id,
                    Title = d.Title,
                    CourseName = courses.ContainsKey(d.CourseId)
                        ? courses[d.CourseId]
                        : "-",
                    TotalQuestions = d.Questions.Count,
                    CreatedAt = d.CreatedAt
                })
                .ToList();
        }
        // ======================================================
        // Auto Generate Draft
        // ======================================================

        public int GenerateAutoDraft(
            int ownerId,
            int? subscriptionPeriodId,
            HomeworkAutoGenerateVM model)
        {
            if (model.LessonQuestionCounts == null || !model.LessonQuestionCounts.Any())
                throw new InvalidOperationException("يجب تحديد عدد الأسئلة لكل مؤشر.");

            var strategy = _context.Database.CreateExecutionStrategy();

            return strategy.Execute(() =>
            {
                using var transaction = _context.Database.BeginTransaction();

                try
                {
                    var selectedQuestions = new List<Question>();

                    // =========================================
                    // تحميل كل الأسئلة مرة واحدة
                    // =========================================
                    var allQuestions = _context.Questions
                        .AsNoTracking()
                        .Where(q =>
                            q.IsComplete &&
                            q.IsReviewed &&
                            !q.IsRejected &&
                            !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                        .ToList();

                    // =========================================
                    // فلترة بنك الأسئلة
                    // =========================================
                    if (model.UsePlatformQuestionBank && !model.UsePrivateQuestionBank)
                    {
                        allQuestions = allQuestions.Where(q => q.PartnerId == null).ToList();
                    }
                    else if (!model.UsePlatformQuestionBank && model.UsePrivateQuestionBank)
                    {
                        allQuestions = allQuestions.Where(q => q.PartnerId != null).ToList();
                    }

                    // =========================================
                    // جلب المستخدم سابقًا
                    // =========================================
                    var usedQuestionIds = _context.HomeworkDraftQuestions
                        .AsNoTracking()
                        .Select(x => x.QuestionId)
                        .ToList();

                    var warnings = new List<string>();

                    foreach (var item in model.LessonQuestionCounts)
                    {
                        if (item.QuestionCount <= 0)
                            continue;

                        var lessonId = item.LessonId;

                        var freshQuestions = allQuestions
                            .Where(q => q.LessonId == lessonId && !usedQuestionIds.Contains(q.Id))
                            .OrderBy(x => Guid.NewGuid())
                            .Take(item.QuestionCount)
                            .ToList();

                        if (freshQuestions.Count < item.QuestionCount)
                        {
                            var needed = item.QuestionCount - freshQuestions.Count;

                            var fallback = allQuestions
                                .Where(q => q.LessonId == lessonId)
                                .OrderBy(x => Guid.NewGuid())
                                .Take(needed)
                                .ToList();

                            if (fallback.Any())
                            {
                                warnings.Add($"⚠️ تم استخدام أسئلة مكررة في مؤشر رقم ({lessonId}) بسبب نقص الأسئلة.");
                            }

                            freshQuestions.AddRange(fallback);
                        }

                        selectedQuestions.AddRange(freshQuestions);
                    }

                    if (!selectedQuestions.Any())
                        throw new InvalidOperationException("تعذّر إنشاء الواجب.");

                    // =========================================
                    // إنشاء المسودة
                    // =========================================
                    var draft = new QdratNew.Entities.HomeworkDraft 
                    {
                        PartnerId = ownerId,
                        SubscriptionPeriodId = subscriptionPeriodId ?? 0,
                        Title = model.Title,
                        CourseId = model.CourseId,
                        QuestionsPerLesson = model.QuestionsPerLesson,
                        UsePlatformQuestionBank = model.UsePlatformQuestionBank,
                        UsePrivateQuestionBank = model.UsePrivateQuestionBank,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.HomeworkDrafts.Add(draft);
                    _context.SaveChanges();

                    // =========================================
                    // تجهيز البيانات
                    // =========================================
                    var draftQuestions = new List<HomeworkDraftQuestion>();

                    int order = 1;

                    foreach (var q in selectedQuestions)
                    {
                        draftQuestions.Add(new HomeworkDraftQuestion
                        {
                            HomeworkDraftId = draft.Id,
                            QuestionId = q.Id,
                            LessonId = q.LessonId,
                            SectionId = q.SectionId ?? 0,
                            Order = order++
                        });
                    }

                    // =========================================
                    // Bulk Insert
                    // =========================================
                    _context.BulkInsert(draftQuestions);

                    transaction.Commit();

                    if (warnings.Any())
                    {
                        throw new InvalidOperationException(string.Join(" | ", warnings));
                    }

                    return draft.Id;
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            });
        }
        // ======================================================
        // Preview Draft
        // ======================================================
        public HomeworkDraftPreviewVM GetDraftForPreview(int draftId)
        {
            var draft = _context.HomeworkDrafts
      .Include(d => d.Questions)
      .ThenInclude(q => q.Question)
      .Include(d => d.Questions)
      .ThenInclude(q => q.Question.Lesson)
      .FirstOrDefault(d => d.Id == draftId);

            if (draft == null)
                return null;

            var vm = new HomeworkDraftPreviewVM
            {
                DraftId = draft.Id,
                Title = draft.Title,
                CourseName = _context.Courses
                    .Where(c => c.Id == draft.CourseId)
                    .Select(c => c.Name)
                    .FirstOrDefault(),

                TotalQuestions = draft.Questions.Count
            };

            vm.LessonGroups = draft.Questions
     .GroupBy(q => new
     {
         q.LessonId,
         LessonTitle = q.Question.Lesson.Title
     })
     .Select(g => new HomeworkDraftLessonGroupVM
     {
         LessonId = g.Key.LessonId,
         LessonTitle = g.Key.LessonTitle,

         Questions = g.Select(q => new HomeworkDraftQuestionItemVM
         {
             QuestionId = q.QuestionId,
             Title = q.Question.Title,
             LessonId = q.LessonId,
             SectionId = q.SectionId
         }).ToList()
     })
     .ToList();

            return vm;
        }
        // ======================================================
        // Add Question Candidates
        // ======================================================
        public List<QuestionCandidateVM> GetAddCandidates(
            int draftId,
            int lessonId)
        {
            return _context.Questions
                .Where(q => q.LessonId == lessonId)
                .Take(50)
                .Select(q => new QuestionCandidateVM
                {
                    Id = q.Id,
                    Title = q.Title
                })
                .ToList();
        }

        // ======================================================
        // Add Question
        // ======================================================
        public void AddQuestion(int draftId, Guid questionId)
        {
            int order = _context.HomeworkDraftQuestions
                .Where(q => q.HomeworkDraftId == draftId)
                .Count() + 1;

            var question = _context.Questions
                .First(q => q.Id == questionId);

            _context.HomeworkDraftQuestions.Add(
                new HomeworkDraftQuestion
                {
                    HomeworkDraftId = draftId,
                    QuestionId = questionId,
                    LessonId = question.LessonId,
                    SectionId = question.SectionId ?? 0,
                    Order = order
                });

            _context.SaveChanges();
        }

        // ======================================================
        // Replace Question
        public ReplaceHomeworkDraftQuestionVM GetReplaceCandidates(
           int draftId,
           Guid oldQuestionId,
           int lessonId)
        {
            var oldQuestion = _context.Questions
                .Where(q => q.Id == oldQuestionId)
                .Select(q => q.Title)
                .FirstOrDefault();

            var questions = _context.Questions
                .Where(q =>
                    q.LessonId == lessonId &&
                    q.IsComplete &&
                    q.IsReviewed &&
                    !q.IsRejected &&
                    !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .OrderByDescending(q => q.CreatedAt)
                .Take(50)
                .Select(q => new QuestionCandidateVM
                {
                    QuestionId = q.Id,
                    Title = q.Title
                })
                .ToList();

            return new ReplaceHomeworkDraftQuestionVM
            {
                DraftId = draftId,
                OldQuestionId = oldQuestionId,
                OldQuestionTitle = oldQuestion,
                Candidates = questions
          .Select(q => new ReplaceCandidateQuestionVM
          {
              QuestionId = q.QuestionId,
              Title = q.Title
          })
          .ToList()
            };
        }

        public void ReplaceQuestion(
            int draftId,
            Guid oldQuestionId,
            Guid newQuestionId)
        {
            var q = _context.HomeworkDraftQuestions
                .First(x =>
                    x.HomeworkDraftId == draftId &&
                    x.QuestionId == oldQuestionId);

            q.QuestionId = newQuestionId;

            _context.SaveChanges();
        }
    }
}