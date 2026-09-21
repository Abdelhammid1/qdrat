using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Partner.Interfaces;
using QdratNew.ViewModels.Partner.Risk;

namespace QdratNew.Services.Partner.Implementation
{
    public class PartnerRiskService : IPartnerRiskService
    {
        private readonly ApplicationDbContext _context;

        public PartnerRiskService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<StudentRiskVM>> CalculateRiskAsync(int partnerId)
        {
            // ===============================
            // 1️⃣ الطلاب
            // ===============================
            var students = await _context.Students
                .Where(s => s.Branch.PartnerId == partnerId)
                .ToListAsync();

            var studentIds = students.Select(s => s.StudentID).ToList();

            // ===============================
            // 2️⃣ تحميل البيانات
            // ===============================
            var examStatuses = await _context.ExamStudentStatuses.ToListAsync();
            var homeworkAttempts = await _context.HomeworkSetAttempts.ToListAsync();

            var results = new List<StudentRiskVM>();

            // ===============================
            // 3️⃣ حساب الريسك لكل طالب
            // ===============================
            foreach (var student in students)
            {
                int risk = 0;
                string reason = "";

                // ===============================
                // Exams
                // ===============================
                var studentExams = examStatuses
                    .Where(x => x.StudentId == student.StudentID)
                    .ToList();

                if (!studentExams.Any())
                {
                    risk += 40;
                    reason += "لم يدخل اختبارات - ";
                }
                else
                {
                    var avg = studentExams
                        .Where(x => x.Score.HasValue)
                        .Select(x => x.Score.Value)
                        .DefaultIfEmpty(0)
                        .Average();

                    if (avg < 50)
                    {
                        risk += 30;
                        reason += "درجات ضعيفة - ";
                    }
                }

                // ===============================
                // Homework
                // ===============================
                var studentHw = homeworkAttempts
                    .Where(x => x.StudentId == student.StudentID)
                    .ToList();

                if (!studentHw.Any())
                {
                    risk += 30;
                    reason += "لم يحل واجبات - ";
                }

                // ===============================
                // تحديد المستوى
                // ===============================
                string level;

                if (risk >= 60) level = "Risk";
                else if (risk >= 30) level = "Warning";
                else level = "Safe";

                results.Add(new StudentRiskVM
                {
                    StudentId = student.StudentID,
                    Name = student.FullName ?? "",
                    RiskScore = risk,
                    RiskLevel = level,
                    Reason = reason
                });
            }

            return results
                .OrderByDescending(x => x.RiskScore)
                .ToList();
        }
    }
}