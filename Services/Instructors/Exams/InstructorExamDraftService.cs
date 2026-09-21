using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Models;
using QdratNew.Services.Instructors.Exams.Interfaces;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Instructors.Exams
{
    public class InstructorExamDraftService : IInstructorExamDraftService
    {
        private readonly ApplicationDbContext _context;

        public InstructorExamDraftService(ApplicationDbContext context)
        {
            _context = context;
        }

        public int SaveDraft(
            GeneratedExamResult generated,
            int instructorId)
        {
            if (generated == null || generated.Questions == null || !generated.Questions.Any())
                throw new InvalidOperationException("لا توجد أسئلة للحفظ");

            var strategy = _context.Database.CreateExecutionStrategy();
            int draftId = 0;

            strategy.Execute(() =>
            {
                using var transaction = _context.Database.BeginTransaction();

                // ===============================
                // 1️⃣ إنشاء Draft
                // ===============================
                var draft = new ExamDraft
                {
                    Title = generated.ExamTitle,
                    CreatedAt = DateTime.Now,
                    IsArchived = false,
                    CreatedByInstructorId = instructorId
                };

                _context.ExamDrafts.Add(draft);
                _context.SaveChanges();

                draftId = draft.Id;

                // ===============================
                // 2️⃣ تجهيز الأسئلة Bulk
                // ===============================
                var draftQuestions = new List<ExamDraftQuestion>();

                int order = 1;

                foreach (var q in generated.Questions)
                {
                    draftQuestions.Add(new ExamDraftQuestion
                    {
                        ExamDraftId = draftId,
                        QuestionId = q.QuestionId,
                        Order = order++
                    });
                }

                // ===============================
                // 3️⃣ Bulk Insert
                // ===============================
                _context.BulkInsert(draftQuestions);

                transaction.Commit();
            });

            return draftId;
        }

        // =====================================================
        // Preview Draft (بدون Lazy + بدون N+1)
        // =====================================================
        public ExamDraftPreviewVM GetDraft(int draftId, int instructorId)
        {
            var draft = _context.ExamDrafts
                .Where(d =>
                    d.Id == draftId &&
                    d.CreatedByInstructorId == instructorId &&
                    !d.IsArchived)
                .Select(d => new
                {
                    d.Id,
                    d.Title
                })
                .FirstOrDefault();

            if (draft == null)
                throw new InvalidOperationException("المسودة غير موجودة");

            var questions = _context.ExamDraftQuestions
                .Where(q => q.ExamDraftId == draftId)
                .Select(q => new
                {
                    q.QuestionId,
                    q.Order,
                    q.Question.Title,
                    q.Question.SectionId,
                    SectionTitle = q.Question.Section.Title,
                    q.Question.LessonId,
                    LessonTitle = q.Question.Lesson.Title
                })
                .OrderBy(q => q.Order)
                .ToList();

            return new ExamDraftPreviewVM
            {
                DraftId = draft.Id,
                Title = draft.Title,
                TotalQuestions = questions.Count,
                Sections = questions
                    .GroupBy(q => new { q.SectionId, q.SectionTitle })
                    .Select(g => new ExamDraftSectionGroupVM
                    {
                        SectionId = g.Key.SectionId,
                        SectionTitle = g.Key.SectionTitle,
                        Questions = g.Select(x => new ExamDraftQuestionItemVM
                        {
                            QuestionId = x.QuestionId,
                            Title = x.Title,
                            LessonId = x.LessonId,
                            LessonTitle = x.LessonTitle
                        }).ToList()
                    }).ToList()
            };
        }
    }
}