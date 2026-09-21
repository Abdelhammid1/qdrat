using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    public class HomeworkAssignmentService : IHomeworkAssignmentService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
        private readonly ITimeZoneService _timeZoneService;

        public HomeworkAssignmentService(IDbContextFactory<ApplicationDbContext> contextFactory, ITimeZoneService timeZoneService)
        {
            _contextFactory = contextFactory;
            _timeZoneService = timeZoneService;
        }

        public async Task AssignMissingHomeworksToStudentAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            // 1) تأكيد وجود الطالب
            var studentExists = await _context.Students
                .AsNoTracking()
                .AnyAsync(s => s.StudentID == studentId);
            if (!studentExists) return;

            // 2) كل الدُفعات اللي الطالب منضم لها
            var myBatchIds = await _context.StudentBatchEnrollments
                .AsNoTracking()
                .Where(e => e.StudentID == studentId)
                .Select(e => e.BatchId)
                .ToListAsync();
            if (myBatchIds.Count == 0) return;

            // 3) جلب مجموعات الواجب باستخدام Join بدل Contains
            var allHomeworkSets = await _context.HomeworkSets
                .AsNoTracking()
                .Select(hs => new { hs.Id, hs.Title, hs.BatchId })
                .ToListAsync();

            var homeworkSets = allHomeworkSets
                .Where(hs => myBatchIds.Contains(hs.BatchId))
                .Select(hs => new { hs.Id, hs.Title })
                .ToList();

            if (homeworkSets.Count == 0) return;

            // 4) معرفة أي Set مُسند بالفعل لهذا الطالب
            var alreadyAssignedSetIds = await _context.Homeworks
                .AsNoTracking()
                .Where(h => h.StudentId == studentId)
                .Select(h => h.HomeworkSetId)
                .Distinct()
                .ToListAsync();

            // 5) تحديد الـ Sets المطلوب إسنادها (فلترة في الذاكرة لتفادي Contains في SQL)
            var setsToAssign = homeworkSets
                .Where(hs => !alreadyAssignedSetIds.Any(aid => aid == hs.Id))
                .ToList();

            if (setsToAssign.Count == 0) return;

            // 🕒 توقيت موحد
            var nowUtc = _timeZoneService.GetNowUtc();
            var nowSaudi = _timeZoneService.GetNowSaudi();

            // 6) تجهيز الإضافات
            var newHomeworks = new List<QdratNew.Entities.Homework>();
            var newNotifications = new List<Notification>();

            foreach (var set in setsToAssign)
            {
                var lessonQuestionPairs = await _context.Homeworks
                    .AsNoTracking()
                    .Where(h => h.HomeworkSetId == set.Id)
                    .Select(h => new { h.LessonId, h.QuestionId, h.LectureId })
                    .ToListAsync();

                foreach (var pair in lessonQuestionPairs)
                {
                    newHomeworks.Add(new QdratNew.Entities.Homework
                    {
                        StudentId = studentId,
                        LessonId = pair.LessonId,
                        QuestionId = pair.QuestionId,
                        LectureId = pair.LectureId,
                        HomeworkSetId = set.Id,
                        AssignedAt = nowUtc, // ✅ حفظ بالتوقيت العالمي
                        Status = HomeworkStatus.Pending
                    });
                }

                newNotifications.Add(new Notification
                {
                    StudentID = studentId,
                    Message = $"📌 تم إضافة واجب جديد يخص دفعتك: {set.Title}",
                    SentAt = nowSaudi, // ✅ توقيت محلي عند عرض الإشعار
                    Category = NotificationCategory.Reminder
                });
            }

            if (newHomeworks.Count > 0)
                _context.Homeworks.AddRange(newHomeworks);

            if (newNotifications.Count > 0)
                _context.Notifications.AddRange(newNotifications);

            await _context.SaveChangesAsync();
        }

        public async Task<int> GetTotalHomeworkCountForStudentBatchAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            var student = await _context.StudentBatchEnrollments
                .Where(s => s.StudentID == studentId)
                .Select(s => new { s.BatchId })
                .FirstOrDefaultAsync();

            if (student == null) return 0;

            return await _context.HomeworkSets
                .Where(h => h.BatchId == student.BatchId && h.IsSent)
                .CountAsync();
        }

        public async Task<int> GetCompletedHomeworkCountAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            return await _context.Homeworks
                .Where(h => h.StudentId == studentId && h.IsCompleted && h.IsSent)
                .Select(h => h.HomeworkSetId)
                .Distinct()
                .CountAsync();
        }

        public async Task<int> GetPendingHomeworkCountAsync(int studentId)
        {
            using var _context = _contextFactory.CreateDbContext();

            return await _context.Homeworks
                .Where(h => h.StudentId == studentId && !h.IsCompleted && h.IsSent)
                .Select(h => h.HomeworkSetId)
                .Distinct()
                .CountAsync();
        }
    }
}
