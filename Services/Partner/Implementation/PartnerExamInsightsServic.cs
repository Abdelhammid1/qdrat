using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Partner.Interfaces;
using QdratNew.ViewModels.Partner.Exam;

namespace QdratNew.Services.Partner.Implementation
{
    public class PartnerExamInsightsService : IPartnerExamInsightsService
    {
        private readonly ApplicationDbContext _context;

        public PartnerExamInsightsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ExamDashboardVM> GetDashboardAsync(int partnerId)
        {
            // ===============================
            // 1️⃣ تحميل الطلاب (خاص بالشريك فقط)
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
            // 2️⃣ تحميل Assignments المرسلة فقط
            // ===============================
            var assignments = await _context.ExamAssignmentsToBatches
                .Include(x => x.Batch)
                    .ThenInclude(b => b.Branch)
                .Where(x =>
                    x.IsSentToStudents &&
                    x.Batch != null &&
                    x.Batch.Branch.PartnerId == partnerId)
                .ToListAsync();

            if (!assignments.Any())
                return new ExamDashboardVM();

            // ===============================
            // 3️⃣ تحميل Enrollments (فلترة في الذاكرة)
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
            // 4️⃣ تحميل Statuses (فلترة في الذاكرة)
            // ===============================
            var allStatuses = await _context.ExamStudentStatuses
                .ToListAsync();

            var statuses = new List<ExamStudentStatus>();

            foreach (var s in allStatuses)
            {
                if (studentMap.ContainsKey(s.StudentId))
                    statuses.Add(s);
            }

            // ===============================
            // 5️⃣ الحساب الصحيح (مطابق الكنترولر)
            // ===============================
            int totalExpectedAttempts = 0;
            int totalCompleted = 0;

            foreach (var a in assignments)
            {
                int studentsInBatch = 0;

                foreach (var e in enrollments)
                {
                    if (e.BatchId == a.BatchId)
                        studentsInBatch++;
                }

                totalExpectedAttempts += studentsInBatch;

                int attempted = 0;

                foreach (var s in statuses)
                {
                    if (
                        s.ExamAssignmentId == a.Id &&
                        (s.IsSubmitted || s.Status == ExamStatus.Completed)
                    )
                    {
                        attempted++;
                    }
                }

                totalCompleted += attempted;
            }

            var pending = totalExpectedAttempts - totalCompleted;

            // ===============================
            // 6️⃣ Statuses المرتبطة فقط
            // ===============================
            var relatedStatuses = new List<ExamStudentStatus>();

            foreach (var s in statuses)
            {
                foreach (var a in assignments)
                {
                    if (s.ExamAssignmentId == a.Id)
                    {
                        relatedStatuses.Add(s);
                        break;
                    }
                }
            }

            // ===============================
            // 7️⃣ متوسط الدرجات
            // ===============================
            double avgScore = 0;

            var scores = new List<double>();

            foreach (var s in relatedStatuses)
            {
                if (s.Score.HasValue)
                    scores.Add(s.Score.Value);
            }

            if (scores.Count > 0)
                avgScore = scores.Average();

            // ===============================
            // 8️⃣ توزيع الأداء
            // ===============================
            int excellent = 0;
            int good = 0;
            int weak = 0;

            foreach (var s in relatedStatuses)
            {
                if (!s.Score.HasValue)
                    continue;

                var score = s.Score.Value;

                if (score >= 85) excellent++;
                else if (score >= 60) good++;
                else weak++;
            }

            var labels = new List<string> { "ممتاز", "جيد", "ضعيف" };
            var data = new List<int> { excellent, good, weak };

            // ===============================
            // 9️⃣ Alerts
            // ===============================
            var alerts = new List<ExamAlertVM>();

            foreach (var student in students)
            {
                var studentExams = new List<ExamStudentStatus>();

                foreach (var s in relatedStatuses)
                {
                    if (s.StudentId == student.StudentID)
                        studentExams.Add(s);
                }

                if (studentExams.Count == 0)
                {
                    alerts.Add(new ExamAlertVM
                    {
                        StudentName = student.FullName ?? "",
                        Message = "لم يدخل أي اختبار"
                    });
                }
                else
                {
                    bool allWeak = true;

                    foreach (var s in studentExams)
                    {
                        if (s.Score.HasValue && s.Score.Value >= 50)
                        {
                            allWeak = false;
                            break;
                        }
                    }

                    if (allWeak)
                    {
                        alerts.Add(new ExamAlertVM
                        {
                            StudentName = student.FullName ?? "",
                            Message = "نتائجه ضعيفة في جميع الاختبارات"
                        });
                    }
                }
            }

            // ===============================
            // 🔟 Return
            // ===============================
            return new ExamDashboardVM
            {
                TotalExams = assignments.Count,
                CompletedExams = totalCompleted,
                PendingExams = pending,
                AverageScore = avgScore,
                Labels = labels,
                Data = data,
                Alerts = alerts.Take(10).ToList()
            };
        }
    }
}