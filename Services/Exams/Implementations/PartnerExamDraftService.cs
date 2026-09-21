using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Exams.Interfaces;
using QdratNew.Services.Exams.Models;
using QdratNew.ViewModels.Partner.Exam;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QdratNew.Services.Exams.Implementations
{
    public class PartnerExamDraftService : IPartnerExamDraftService
    {
        private readonly ApplicationDbContext _context;

        public PartnerExamDraftService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================================
        // 1) توليد مؤقت (Preview)
        // =========================================
        public GeneratedExamResult GeneratePreview(GenerateExamRequestVM request)
        {
            if (request == null || !request.Curriculums.Any())
                throw new InvalidOperationException("لم يتم اختيار أي مناهج.");

            var result = new GeneratedExamResult
            {
                ExamTitle = request.ExamTitle,
                CourseId = request.CourseId
            };

            foreach (var curriculum in request.Curriculums)
            {
                if (curriculum.QuestionCount <= 0)
                    continue;

                if (curriculum.UseAllSections)
                {
                    var questions = _context.Questions
                        .Where(q =>
                            q.IsComplete &&
                            q.IsAnswerConfirmed &&
                            q.Section.CurriculumId == curriculum.CurriculumId)
                        .OrderBy(x => Guid.NewGuid())
                        .Take(curriculum.QuestionCount)
                        .Select(q => new GeneratedQuestionItem
                        {
                            QuestionId = q.Id,
                            CurriculumId = curriculum.CurriculumId,
                            SectionId = q.SectionId,
                            LessonId = q.LessonId
                        })
                        .ToList();

                    result.Questions.AddRange(questions);
                }
                else
                {
                    foreach (var section in curriculum.Sections.Where(s => s.QuestionCount > 0))
                    {
                        var questions = _context.Questions
                            .Where(q =>
                                q.IsComplete &&
                                q.IsAnswerConfirmed &&
                                q.SectionId == section.SectionId)
                            .OrderBy(x => Guid.NewGuid())
                            .Take(section.QuestionCount)
                            .Select(q => new GeneratedQuestionItem
                            {
                                QuestionId = q.Id,
                                CurriculumId = curriculum.CurriculumId,
                                SectionId = q.SectionId,
                                LessonId = q.LessonId
                            })
                            .ToList();

                        result.Questions.AddRange(questions);
                    }
                }
            }

            if (!result.Questions.Any())
                throw new InvalidOperationException("لم يتم توليد أي أسئلة.");

            return result;
        }

        // =========================================
        // 2) حفظ المسودة
        // =========================================
        public int SaveDraft(GeneratedExamResult generated, int partnerId)
        {
            using var tx = _context.Database.BeginTransaction();

            var draft = new ExamDraft
            {
                Title = generated.ExamTitle,
                PartnerId = partnerId,
                CreatedAt = DateTime.Now,
                IsArchived = false
            };

            _context.ExamDrafts.Add(draft);
            _context.SaveChanges();

            int order = 1;
            foreach (var q in generated.Questions)
            {
                _context.ExamDraftQuestions.Add(new ExamDraftQuestion
                {
                    ExamDraftId = draft.Id,
                    QuestionId = q.QuestionId,
                    Order = order++
                });
            }

            _context.SaveChanges();
            tx.Commit();

            return draft.Id;
        }

        // =========================================
        // الباقي (PreviewDraft / Replace / Send)
        // سيتم ربطه بالكنترولر لاحقًا
        // =========================================
        // =========================================
        // 3) قراءة مسودة محفوظة (PreviewDraft)
        // =========================================
        public ExamDraftPreviewVM GetDraft(int draftId, int partnerId)
        {
            var draft = _context.ExamDrafts
                .Where(d =>
                    d.Id == draftId &&
                    d.PartnerId == partnerId &&
                    !d.IsArchived)
                .Select(d => new
                {
                    d.Id,
                    d.Title
                })
                .FirstOrDefault();

            if (draft == null)
                throw new InvalidOperationException("المسودة غير موجودة.");

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

            var sectionGroups = questions
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
                })
                .ToList();

            return new ExamDraftPreviewVM
            {
                DraftId = draft.Id,
                Title = draft.Title,
                Sections = sectionGroups,
                TotalQuestions = questions.Count
            };
        }


        // =========================================
        // 4) تجهيز إرسال المسودة
        // =========================================
        public SendExamDraftVM PrepareSend(int draftId, int partnerId)
        {
            var draft = _context.ExamDrafts
                .Where(d =>
                    d.Id == draftId &&
                    d.PartnerId == partnerId &&
                    !d.IsArchived)
                .Select(d => new
                {
                    d.Id,
                    d.Title
                })
                .FirstOrDefault();

            if (draft == null)
                throw new InvalidOperationException("المسودة غير موجودة.");

            var courseId = _context.ExamDraftQuestions
                .Where(q => q.ExamDraftId == draftId)
                .Select(q => q.Question.Section.Curriculum.CourseCurriculums
                    .Select(cc => cc.CourseId)
                    .FirstOrDefault())
                .FirstOrDefault();

            if (courseId == 0)
                throw new InvalidOperationException("لا توجد دورة مرتبطة بالمسودة.");

            var batches = _context.Batches
                .Where(b =>
                    b.CourseId == courseId &&
                    b.Branch.PartnerId == partnerId)
                .Select(b => new BatchSelectItemVM
                {
                    BatchId = b.Id,
                    BatchName = b.Name
                })
                .ToList();

            return new SendExamDraftVM
            {
                DraftId = draft.Id,
                ExamTitle = draft.Title,
                CourseId = courseId,
                AvailableBatches = batches
            };
        }

        // =========================================
        // 5) إرسال المسودة لدفعات
        // =========================================
        public void SendToBatches(SendExamDraftVM model, int partnerId)
        {
            var draft = _context.ExamDrafts
                .Where(d =>
                    d.Id == model.DraftId &&
                    d.PartnerId == partnerId &&
                    !d.IsArchived)
                .FirstOrDefault();

            if (draft == null)
                throw new InvalidOperationException("المسودة غير موجودة.");

            var questionIds = _context.ExamDraftQuestions
                .Where(q => q.ExamDraftId == draft.Id)
                .OrderBy(q => q.Order)
                .Select(q => q.QuestionId)
                .ToList();

            foreach (var batchId in model.BatchIds)
            {
                var batch = _context.Batches
                    .Where(b =>
                        b.Id == batchId &&
                        b.Branch.PartnerId == partnerId)
                    .Select(b => new { b.Id })
                    .FirstOrDefault();

                if (batch == null)
                    continue;

                var exam = new Exam
                {
                    Title = draft.Title,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Exams.Add(exam);
                _context.SaveChanges();

                var assignment = new ExamAssignmentToBatch
                {
                    ExamId = exam.Id,
                    BatchId = batchId,
                    AssignedAt = DateTime.Now,
                    IsSentToStudents = true,
                    Title = exam.Title,
                    TotalQuestions = questionIds.Count
                };

                _context.ExamAssignmentsToBatches.Add(assignment);
                _context.SaveChanges();

                int order = 1;
                foreach (var qId in questionIds)
                {
                    _context.ExamQuestions.Add(new ExamQuestion
                    {
                        ExamAssignmentId = assignment.Id,
                        QuestionId = qId,
                        Order = order++
                    });
                }

                _context.SaveChanges();
            }
        }

        // =========================================
        // 6) جلب بدائل سؤال
        // =========================================
        public ReplaceExamDraftQuestionVM GetReplaceCandidates(
            int draftId,
            Guid oldQuestionId,
            int lessonId)
        {
            var usedIds = _context.ExamDraftQuestions
                .Where(x => x.ExamDraftId == draftId)
                .Select(x => x.QuestionId)
                .ToList();

            var candidates = _context.Questions
                .Where(q =>
                    q.LessonId == lessonId &&
                    q.IsComplete &&
                    q.IsAnswerConfirmed)
                .Select(q => new
                {
                    q.Id,
                    q.Title
                })
                .ToList()
                .Where(q => !usedIds.Contains(q.Id))
                .Select(q => new ReplaceCandidateQuestionVM
                {
                    QuestionId = q.Id,
                    Title = q.Title
                })
                .ToList();

            return new ReplaceExamDraftQuestionVM
            {
                DraftId = draftId,
                OldQuestionId = oldQuestionId,
                LessonId = lessonId,
                Candidates = candidates
            };
        }

        // =========================================
        // 7) تنفيذ الاستبدال
        // =========================================
        public void ReplaceQuestion(
            int draftId,
            Guid oldQuestionId,
            Guid newQuestionId)
        {
            var draftQuestion = _context.ExamDraftQuestions
                .FirstOrDefault(x =>
                    x.ExamDraftId == draftId &&
                    x.QuestionId == oldQuestionId);

            if (draftQuestion == null)
                throw new InvalidOperationException("السؤال غير موجود في المسودة.");

            var oldLessonId = _context.Questions
                .Where(q => q.Id == oldQuestionId)
                .Select(q => q.LessonId)
                .FirstOrDefault();

            var newLessonId = _context.Questions
                .Where(q => q.Id == newQuestionId)
                .Select(q => q.LessonId)
                .FirstOrDefault();

            if (oldLessonId != newLessonId)
                throw new InvalidOperationException("يجب أن يكون السؤال من نفس المؤشر.");

            var exists = _context.ExamDraftQuestions
                .Any(x =>
                    x.ExamDraftId == draftId &&
                    x.QuestionId == newQuestionId);

            if (exists)
                throw new InvalidOperationException("السؤال موجود بالفعل في المسودة.");

            draftQuestion.QuestionId = newQuestionId;
            _context.SaveChanges();
        }

    }
}
