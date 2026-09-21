using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.HomeworkTracking.Interfaces;
using QdratNew.ViewModels.Partner.Homework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QdratNew.Services.HomeworkTracking.Implementations
{
    public class SentHomeworkService : ISentHomeworkService
    {
        private readonly ApplicationDbContext _context;

        public SentHomeworkService(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SentHomeworkListVM> GetSentHomeworksForPartner(int partnerId)
        {
            // ===============================
            // 1️⃣ جلب كل HomeworkSets المرسلة
            // ===============================
            var allHomeworkSets = _context.HomeworkSets
                .Where(hs => hs.IsSent && !hs.IsArchived)
                .ToList();

            if (!allHomeworkSets.Any())
                return new List<SentHomeworkListVM>();

            // ===============================
            // 2️⃣ جلب دفعات الشريك
            // ===============================
            var partnerBatches = _context.Batches
                .Where(b => b.Branch.PartnerId == partnerId)
                .Select(b => new
                {
                    b.Id,
                    b.Name,
                    b.CourseId
                })
                .ToList();

            if (!partnerBatches.Any())
                return new List<SentHomeworkListVM>();

            // ===============================
            // 3️⃣ ربط HomeworkSet بالدفعات (في الذاكرة)
            // ===============================
            var partnerHomeworkSets = allHomeworkSets
                .Where(hs => partnerBatches.Any(b => b.Id == hs.BatchId))
                .ToList();

            if (!partnerHomeworkSets.Any())
                return new List<SentHomeworkListVM>();

            // ===============================
            // 4️⃣ جلب كل HomeworkSetStudents (DB)
            // ===============================
            var allHomeworkSetStudents = _context.HomeworkSetStudents
                .ToList();

            // ===============================
            // 5️⃣ Courses (DB)
            // ===============================
            var courses = _context.Courses.ToList();

            // ===============================
            // 6️⃣ بناء ViewModel (كل الربط في الذاكرة)
            // ===============================
            return partnerHomeworkSets
                .Select(hs =>
                {
                    var batch = partnerBatches.First(b => b.Id == hs.BatchId);

                    var states = allHomeworkSetStudents
                        .Where(s => s.HomeworkSetId == hs.Id)
                        .ToList();

                    return new SentHomeworkListVM
                    {
                        HomeworkSetId = hs.Id,
                        Title = hs.Title,
                        CourseName = courses
                            .First(c => c.Id == batch.CourseId).Name,

                        BatchName = batch.Name,

                        TotalStudents = states.Count,
                        SubmittedStudents = states.Count(s => s.IsSubmitted),
                        PendingStudents = states.Count(s => !s.IsSubmitted),

                        SentAt = hs.CreatedAt
                    };
                })
                .OrderByDescending(x => x.SentAt)
                .ToList();
        }


        public void ResetHomeworkForStudent(int homeworkSetId, int studentId)
        {
            // ===============================
            // 1️⃣ HomeworkSetStudent
            // ===============================
            var hss = _context.HomeworkSetStudents
                .FirstOrDefault(x =>
                    x.HomeworkSetId == homeworkSetId &&
                    x.StudentId == studentId);

            if (hss == null)
                throw new InvalidOperationException("الطالب غير مرتبط بهذا الواجب.");

            // ===============================
            // 2️⃣ إنشاء Attempt جديد (بدون إغلاق القديم)
            // ===============================
            _context.HomeworkSetAttempts.Add(new HomeworkSetAttempt
            {
                HomeworkSetId = homeworkSetId,
                StudentId = studentId,
                StartedAt = DateTime.UtcNow
                // لا يوجد IsClosed
            });

            // ===============================
            // 3️⃣ إعادة حالة الواجب
            // ===============================
            hss.IsSubmitted = false;
            hss.SubmittedAt = null;
            hss.Score = null;

            _context.SaveChanges();
        }

        public List<SentHomeworkStudentVM> GetHomeworkStudents(int homeworkSetId)
        {
            var isArchived = _context.HomeworkSets
                .Any(x => x.Id == homeworkSetId && x.IsArchived);

            if (isArchived)
                return new List<SentHomeworkStudentVM>();

            // ===============================
            // 1️⃣ كل HomeworkSetStudents (DB)
            // ===============================
            var allHomeworkSetStudents = _context.HomeworkSetStudents
                .ToList();

            var homeworkStudents = allHomeworkSetStudents
                .Where(s => s.HomeworkSetId == homeworkSetId)
                .ToList();

            if (!homeworkStudents.Any())
                return new List<SentHomeworkStudentVM>();

            // ===============================
            // 2️⃣ كل الطلاب (DB)
            // ===============================
            var students = _context.Students
                .Select(s => new
                {
                    s.StudentID,
                    s.FullName
                })
                .ToList();

            // ===============================
            // 3️⃣ الربط في الذاكرة
            // ===============================
            return homeworkStudents
                .Select(s =>
                {
                    var st = students.First(x => x.StudentID == s.StudentId);

                    return new SentHomeworkStudentVM
                    {
                        StudentId = st.StudentID,
                        StudentName = st.FullName,
                        IsSubmitted = s.IsSubmitted,
                        SubmittedAt = s.SubmittedAt
                    };
                })
                .ToList();
        }
    }
}
