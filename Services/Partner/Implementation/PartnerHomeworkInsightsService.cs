using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Partner.Interfaces;
using QdratNew.ViewModels.Partner.Homework;

namespace QdratNew.Services.Partner.Implementation
{
    public class PartnerHomeworkInsightsService : IPartnerHomeworkInsightsService
    {
        private readonly ApplicationDbContext _context;

        public PartnerHomeworkInsightsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HomeworkDashboardVM> GetDashboardAsync(int partnerId)
        {
            // ===============================
            // 1️⃣ تحميل الواجبات الخاصة بالشريك فقط
            // ===============================
            var homeworkSets = await _context.HomeworkSets
                .Include(h => h.Batch)
                    .ThenInclude(b => b.Branch)
                .Where(h =>
                    h.Batch != null &&
                    h.Batch.Branch.PartnerId == partnerId)
                .ToListAsync();

            if (!homeworkSets.Any())
                return new HomeworkDashboardVM();

            // ===============================
            // 2️⃣ تحميل الطلاب
            // ===============================
            var students = await _context.Students
                .Where(s => s.Branch.PartnerId == partnerId)
                .ToListAsync();

            var studentMap = new Dictionary<int, bool>();
            foreach (var s in students)
            {
                if (!studentMap.ContainsKey(s.StudentID))
                    studentMap.Add(s.StudentID, true);
            }

            // ===============================
            // 3️⃣ تحميل Enrollment
            // ===============================
            var allEnrollments = await _context.StudentBatchEnrollments
                .ToListAsync();

            var enrollments = new List<StudentBatchEnrollment>();

            foreach (var e in allEnrollments)
            {
                if (studentMap.ContainsKey(e.StudentID))
                    enrollments.Add(e);
            }

            // ===============================
            // 4️⃣ تحميل Attempts
            // ===============================
            var allAttempts = await _context.HomeworkSetAttempts
                .ToListAsync();

            var attempts = new List<HomeworkSetAttempt>();

            foreach (var a in allAttempts)
            {
                if (studentMap.ContainsKey(a.StudentId))
                    attempts.Add(a);
            }

            // ===============================
            // 5️⃣ الحساب الصحيح
            // ===============================
            int totalExpected = 0;
            int totalCompleted = 0;

            foreach (var hw in homeworkSets)
            {
                int studentsInBatch = 0;

                foreach (var e in enrollments)
                {
                    if (e.BatchId == hw.BatchId)
                        studentsInBatch++;
                }

                totalExpected += studentsInBatch;

                int solved = 0;

                foreach (var a in attempts)
                {
                    if (a.HomeworkSetId == hw.Id)
                        solved++;
                }

                totalCompleted += solved;
            }

            var pending = totalExpected - totalCompleted;

            // ===============================
            // 6️⃣ Related Attempts
            // ===============================
            var relatedAttempts = new List<HomeworkSetAttempt>();

            foreach (var a in attempts)
            {
                foreach (var hw in homeworkSets)
                {
                    if (a.HomeworkSetId == hw.Id)
                    {
                        relatedAttempts.Add(a);
                        break;
                    }
                }
            }

            // ===============================
            // 7️⃣ Chart
            // ===============================
            var labels = new List<string> { "تم الحل", "لم يتم الحل" };
            var data = new List<int> { totalCompleted, pending };

            // ===============================
            // 8️⃣ Alerts
            // ===============================
            var alerts = new List<HomeworkAlertVM>();

            foreach (var student in students)
            {
                bool hasAttempt = false;

                foreach (var a in relatedAttempts)
                {
                    if (a.StudentId == student.StudentID)
                    {
                        hasAttempt = true;
                        break;
                    }
                }

                if (!hasAttempt)
                {
                    alerts.Add(new HomeworkAlertVM
                    {
                        StudentName = student.FullName ?? "",
                        Message = "لم يبدأ أي واجب"
                    });
                }
            }

            // ===============================
            // 9️⃣ Return
            // ===============================
            return new HomeworkDashboardVM
            {
                TotalHomeworks = homeworkSets.Count,
                CompletedHomeworks = totalCompleted,
                PendingHomeworks = pending,
                CompletionRate = totalExpected == 0
                    ? 0
                    : (totalCompleted * 100.0) / totalExpected,
                Labels = labels,
                Data = data,
                Alerts = alerts.Take(10).ToList()
            };
        }
    }
}