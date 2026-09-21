using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.Partner.HomeworkDraft;
using EFCore.BulkExtensions;

namespace QdratNew.Services.PartnerHomework
{
    public class PartnerHomeworkAssignmentService : IPartnerHomeworkAssignmentService
    {
        private readonly ApplicationDbContext _context;

        public PartnerHomeworkAssignmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public void SendDraftToBatches(
            SendHomeworkDraftVM model,
            int partnerId,
            int subscriptionPeriodId)
        {
            var draft = _context.HomeworkDrafts
                .Include(d => d.Questions)
                .FirstOrDefault(d =>
                    d.Id == model.DraftId &&
                    d.PartnerId == partnerId &&
                    d.SubscriptionPeriodId == subscriptionPeriodId);

            if (draft == null)
                throw new InvalidOperationException("المسودة غير موجودة أو لا تخص هذا الشريك.");

            if (draft.Questions == null || !draft.Questions.Any())
                throw new InvalidOperationException("المسودة لا تحتوي على أسئلة.");

            if (model.BatchIds == null || !model.BatchIds.Any())
                throw new InvalidOperationException("لم يتم اختيار أي دفعة للإرسال.");

            var draftQuestionIds = draft.Questions
                .Select(q => q.QuestionId)
                .Distinct()
                .ToList();

            // ==========================
            // جلب البيانات من قاعدة البيانات بدون Contains
            // ==========================
            var previousQuestionsRaw = (
                from h in _context.Homeworks
                join hs in _context.HomeworkSets
                    on h.HomeworkSetId equals hs.Id
                select new
                {
                    h.QuestionId,
                    hs.BatchId
                }
            ).ToList();

            // ==========================
            // التصفية داخل الذاكرة
            // ==========================
            var previousQuestions = previousQuestionsRaw
                .Where(x => draftQuestionIds.Any(q => q == x.QuestionId))
                .ToList();

            var repeatedQuestions = previousQuestions
                .Where(x => model.BatchIds.Any(b => b == x.BatchId))
                .Select(x => x.QuestionId)
                .Distinct()
                .ToList();

            if (repeatedQuestions.Any())
                throw new InvalidOperationException(
                    $"⚠️ يوجد {repeatedQuestions.Count} سؤال تم إرسالهم سابقًا لنفس الدفعة.");

            SendDraftToStudents(
                draft,
                model,
                model.BatchIds,
                partnerId,
                subscriptionPeriodId
            );
        }

        public void SendToStudentsWithQuestions(
            int partnerId,
            int subscriptionPeriodId,
            int courseId,
            string title,
            List<Guid> questionIds)
        {
            if (questionIds == null || !questionIds.Any())
                throw new InvalidOperationException("قائمة الأسئلة فارغة.");

            var batches = _context.Batches
                .Where(b => b.Branch.PartnerId == partnerId &&
                            b.CourseId == courseId)
                .ToList();

            foreach (var batch in batches)
            {
                var homeworkSet = new HomeworkSet
                {
                    Title = title,
                    BatchId = batch.Id,
                    CreatedAt = DateTime.UtcNow,
                    IsSent = true
                };

                _context.HomeworkSets.Add(homeworkSet);
                _context.SaveChanges();

                var studentIds = _context.StudentBatchEnrollments
                    .Where(e => e.BatchId == batch.Id)
                    .Select(e => e.StudentID)
                    .ToList();

                foreach (var studentId in studentIds)
                {
                    _context.HomeworkSetStudents.Add(new HomeworkSetStudent
                    {
                        HomeworkSetId = homeworkSet.Id,
                        StudentId = studentId,
                        IsSubmitted = false
                    });

                    foreach (var qId in questionIds)
                    {
                        _context.Homeworks.Add(new QdratNew.Entities.Homework
                        {
                            HomeworkSetId = homeworkSet.Id,
                            StudentId = studentId,
                            QuestionId = qId,
                            AssignedAt = DateTime.UtcNow
                        });
                    }
                }

                _context.SaveChanges();
            }
        }

        private void SendDraftToStudents(
            QdratNew.Entities.HomeworkDraft draft,
            SendHomeworkDraftVM model,
            List<int> selectedBatchIds,
            int partnerId,
            int subscriptionPeriodId)
        {
            if (draft.PartnerId != partnerId ||
                draft.SubscriptionPeriodId != subscriptionPeriodId)
                throw new InvalidOperationException("محاولة غير مصرح بها.");

            var partnerBatchIds = _context.Batches
                .Where(b => b.Branch.PartnerId == partnerId)
                .Select(b => b.Id)
                .ToList();

            var validBatchIds = selectedBatchIds
                .Where(id => partnerBatchIds.Contains(id))
                .ToList();

            if (!validBatchIds.Any())
                throw new InvalidOperationException("الدفعات المختارة غير صالحة.");

            var draftQuestionIds = draft.Questions
                .Select(q => q.QuestionId)
                .Distinct()
                .ToList();

            var questionsLookup = _context.Questions
                .AsNoTracking()
                .ToList()
                .Where(q => draftQuestionIds.Any(id => id == q.Id))
                .Select(q => new { q.Id, q.LessonId })
                .ToDictionary(q => q.Id, q => q);

            var allEnrollmentsRaw = _context.StudentBatchEnrollments
      .Select(e => new
      {
          e.BatchId,
          e.StudentID
      })
      .ToList();

            var allEnrollments = allEnrollmentsRaw
                .Where(e => validBatchIds.Any(id => id == e.BatchId))
                .ToList();

            var homeworkSets = new List<HomeworkSet>();
            var homeworkSetStudents = new List<HomeworkSetStudent>();
            var homeworks = new List<QdratNew.Entities.Homework>();

            foreach (var batchId in validBatchIds)
            {
                var homeworkSet = new HomeworkSet
                {
                    Title = draft.Title,
                    CompletionTitle = draft.Title,
                    BatchId = batchId,
                    IsSent = true,
                    StartAt = model.StartAt,
                    EndAt = model.EndAt,
                    CreatedAt = DateTime.UtcNow
                };

                homeworkSets.Add(homeworkSet);

                var studentIds = allEnrollments
                    .Where(x => x.BatchId == batchId)
                    .Select(x => x.StudentID)
                    .Distinct()
                    .ToList();

                foreach (var studentId in studentIds)
                {
                    homeworkSetStudents.Add(new HomeworkSetStudent
                    {
                        HomeworkSet = homeworkSet,
                        StudentId = studentId,
                        AssignedAt = DateTime.UtcNow,
                        IsSubmitted = false
                    });

                    foreach (var dq in draft.Questions.OrderBy(q => q.Order))
                    {
                        if (!questionsLookup.TryGetValue(dq.QuestionId, out var question))
                            continue;

                        homeworks.Add(new QdratNew.Entities.Homework
                        {
                            HomeworkSet = homeworkSet,
                            StudentId = studentId,
                            QuestionId = question.Id,
                            LessonId = question.LessonId,
                            AssignedAt = DateTime.UtcNow,
                            Status = HomeworkStatus.Pending,
                            IsSent = true
                        });
                    }
                }
            }

            _context.BulkInsert(homeworkSets);
            _context.BulkInsert(homeworkSetStudents);
            _context.BulkInsert(homeworks);
        }
    }
}