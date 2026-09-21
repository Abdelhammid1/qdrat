using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.Partner.HomeworkDraft;

namespace QdratNew.Services.HomeworkEngine.Assignment
{
    public class HomeworkEngineAssignmentService : IHomeworkEngineAssignmentService
    {
        private readonly ApplicationDbContext _context;

        public HomeworkEngineAssignmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public void SendDraftToBatches(
     SendHomeworkDraftVM model,
     int draftId,
     string assignedByUserId)
        {
            var draft = _context.HomeworkDrafts
                .Include(d => d.Questions)
                .FirstOrDefault(d => d.Id == draftId);

            if (draft == null)
                throw new InvalidOperationException("المسودة غير موجودة.");

            if (string.IsNullOrWhiteSpace(draft.Title))
                throw new InvalidOperationException("لا يمكن إرسال واجب بدون عنوان. يرجى تعديل عنوان المسودة أولاً.");

            if (!draft.Questions.Any())
                throw new InvalidOperationException("المسودة لا تحتوي على أسئلة.");

            if (model.BatchIds == null || !model.BatchIds.Any())
                throw new InvalidOperationException("لم يتم اختيار دفعات.");

            // =========================================
            // 🔥 1️⃣ تنظيف BatchIds
            // =========================================
            var selectedBatchIds = model.BatchIds.Distinct().ToList();

            // =========================================
            // 🔥 2️⃣ تحميل كل الدفعات (ثم فلترة في الذاكرة)
            // =========================================
            var allBatches = _context.Batches
                .Where(b => !b.IsDeleted)
                .Select(b => b.Id)
                .ToList();

            var validBatchIds = new List<int>();

            foreach (var id in selectedBatchIds)
            {
                foreach (var b in allBatches)
                {
                    if (b == id)
                    {
                        validBatchIds.Add(id);
                        break;
                    }
                }
            }

            if (validBatchIds.Count != selectedBatchIds.Count)
                throw new Exception("❌ يوجد دفعات غير صالحة.");

            // =========================================
            // 🔥 3️⃣ إنشاء HomeworkSets
            // =========================================
            var homeworkSets = new List<HomeworkSet>();

            foreach (var batchId in validBatchIds)
            {
                homeworkSets.Add(new HomeworkSet
                {
                    Title = draft.Title.Trim(),
                    CompletionTitle = draft.Title.Trim(),
                    BatchId = batchId,
                    LectureId = model.LectureId,
                    StartAt = model.StartAt,
                    EndAt = model.EndAt,
                    AssignedByUserId = assignedByUserId,
                    CreatedAt = DateTime.UtcNow,
                    IsSent = true
                });
            }

            var bulkConfig = new BulkConfig
            {
                SetOutputIdentity = true
            };

            _context.BulkInsert(homeworkSets, bulkConfig);

            // =========================================
            // 🔥 4️⃣ تحميل كل enrollments (بدون Contains)
            // =========================================
            var allEnrollments = _context.StudentBatchEnrollments
                .Select(x => new
                {
                    x.BatchId,
                    x.StudentID,
                    x.Status
                })
                .ToList();

            var enrollments = new List<(int BatchId, int StudentId)>();

            foreach (var e in allEnrollments)
            {
                foreach (var batchId in validBatchIds)
                {
                    if (e.BatchId == batchId &&
                        string.Equals(e.Status, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        enrollments.Add((e.BatchId, e.StudentID));
                        break;
                    }
                }
            }

            if (!enrollments.Any())
                throw new InvalidOperationException("لا يوجد طلاب نشطون في الدفعات المختارة.");

            // =========================================
            // 🔥 5️⃣ تحميل الأسئلة مرة واحدة
            // =========================================
            var questionLookup = _context.Questions
                .AsNoTracking()
                .Select(q => new { q.Id, q.LessonId })
                .ToList();

            var questionDict = new Dictionary<Guid, int>();

            foreach (var q in questionLookup)
            {
                if (!questionDict.ContainsKey(q.Id))
                    questionDict.Add(q.Id, q.LessonId);
            }

            // =========================================
            // 🔥 6️⃣ تجهيز البيانات
            // =========================================
            var homeworkSetStudents = new List<HomeworkSetStudent>();
            var homeworks = new List<QdratNew.Entities.Homework>();

            foreach (var homeworkSet in homeworkSets)
            {
                var students = new List<int>();

                foreach (var e in enrollments)
                {
                    if (e.BatchId == homeworkSet.BatchId)
                    {
                        bool exists = false;

                        foreach (var s in students)
                        {
                            if (s == e.StudentId)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                            students.Add(e.StudentId);
                    }
                }

                foreach (var studentId in students)
                {
                    homeworkSetStudents.Add(new HomeworkSetStudent
                    {
                        HomeworkSetId = homeworkSet.Id,
                        StudentId = studentId,
                        AssignedAt = DateTime.UtcNow,
                        IsSubmitted = false
                    });

                    foreach (var dq in draft.Questions.OrderBy(q => q.Order))
                    {
                        int lessonId = 0;

                        if (questionDict.ContainsKey(dq.QuestionId))
                            lessonId = questionDict[dq.QuestionId];

                        homeworks.Add(new QdratNew.Entities.Homework
                        {
                            HomeworkSetId = homeworkSet.Id,
                            StudentId = studentId,
                            QuestionId = dq.QuestionId,
                            LessonId = lessonId,
                            AssignedAt = DateTime.UtcNow,
                            Status = HomeworkStatus.Pending,
                            IsSent = true
                        });
                    }
                }
            }

            // =========================================
            // 🔥 7️⃣ Bulk Insert
            // =========================================
            _context.BulkInsert(homeworkSetStudents);
            _context.BulkInsert(homeworks);
        }


        public int SendDirectToStudents(
            string title,
            int batchId,
            int? lectureId,
            DateTime? startAt,
            DateTime endAt,
            List<Guid> questionIds,
            List<int> studentIds,
            string assignedByUserId)
        {
            if (questionIds == null || !questionIds.Any())
                throw new InvalidOperationException("لا توجد أسئلة لإرسالها كواجب.");
            if (studentIds == null || !studentIds.Any())
                throw new InvalidOperationException("لا يوجد طلاب محددون.");

            var homeworkSet = new HomeworkSet
            {
                Title = title,
                CompletionTitle = title,
                BatchId = batchId,
                LectureId = lectureId,
                StartAt = startAt,
                EndAt = endAt,
                AssignedByUserId = assignedByUserId,
                CreatedAt = DateTime.UtcNow,
                IsSent = true
            };

            _context.Set<HomeworkSet>().Add(homeworkSet);
            _context.SaveChanges();

            var allQIds = questionIds.Distinct().ToList();
            var questionLookup = _context.Questions
                .AsNoTracking()
                .Where(q => allQIds.Contains(q.Id))
                .Select(q => new { q.Id, q.LessonId })
                .ToList()
                .ToDictionary(q => q.Id, q => q.LessonId);

            var setStudents = new List<HomeworkSetStudent>();
            var homeworks   = new List<QdratNew.Entities.Homework>();

            foreach (var studentId in studentIds.Distinct())
            {
                setStudents.Add(new HomeworkSetStudent
                {
                    HomeworkSetId = homeworkSet.Id,
                    StudentId     = studentId,
                    AssignedAt    = DateTime.UtcNow,
                    IsSubmitted   = false
                });

                foreach (var qId in allQIds)
                {
                    homeworks.Add(new QdratNew.Entities.Homework
                    {
                        HomeworkSetId = homeworkSet.Id,
                        StudentId     = studentId,
                        QuestionId    = qId,
                        LessonId      = questionLookup.TryGetValue(qId, out var lid) ? lid : 0,
                        AssignedAt    = DateTime.UtcNow,
                        Status        = HomeworkStatus.Pending,
                        IsSent        = true
                    });
                }
            }

            _context.BulkInsert(setStudents);
            _context.BulkInsert(homeworks);

            return homeworkSet.Id;
        }
    }
}
