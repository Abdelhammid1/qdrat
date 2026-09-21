using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.Exam;

namespace QdratNew.Services.Implementations
{
    public class StudentRankingService : IStudentRankingService
    {
        private readonly ApplicationDbContext _context;

        public StudentRankingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task RecordStudentRankAsync(int studentId, int batchId)
        {
            int totalStudents = await _context.StudentBatchEnrollments
                .CountAsync(e => e.BatchId == batchId && e.Status == "Active");

            // حساب متوسط درجات كل طالب في الدفعة
            var rankedStudents = await (
                from sp in _context.StudentPerformances
                join s in _context.Students on sp.StudentID equals s.StudentID
                join e in _context.StudentBatchEnrollments on s.StudentID equals e.StudentID
                where e.BatchId == batchId && e.Status == "Active"
                group sp by sp.StudentID into g
                select new
                {
                    StudentId = g.Key,
                    AvgScore = g.Average(x => x.Score)
                }
            )
            .OrderByDescending(x => x.AvgScore)
            .ToListAsync();

            // تحديد ترتيب الطالب
            int rank = rankedStudents.FindIndex(x => x.StudentId == studentId) + 1;

            if (rank > 0)
            {
                var history = new StudentRankHistory
                {
                    StudentId = studentId,
                    BatchId = batchId,
                    Rank = rank,
                    TotalStudents = totalStudents,
                    RecordedAt = DateTime.UtcNow
                };

                _context.StudentRankHistories.Add(history);
                await _context.SaveChangesAsync();
            }
        }

        // ✅ النسخة المعدلة الكاملة — دعم المراكز بعد الثالث
        public async Task<StudentRankVm?> GetCurrentRankAsync(int studentId)
        {
            var lastRank = await _context.StudentRankHistories
                .Where(r => r.StudentId == studentId)
                .OrderByDescending(r => r.RecordedAt)
                .FirstOrDefaultAsync();

            if (lastRank == null)
                return null;

            // 🔹 تحديد صورة النيشان أو شعار المنصة
            string medalPath;
            switch (lastRank.Rank)
            {
                case 1:
                    medalPath = "/images/medals/gold.png";
                    break;
                case 2:
                    medalPath = "/images/medals/silver.png";
                    break;
                case 3:
                    medalPath = "/images/medals/bronze.png";
                    break;
                default:
                    medalPath = "/images/160×100.png"; // شعار المنصة العام
                    break;
            }

            // 🔹 توليد رسالة تحفيزية ذكية
            string message;
            if (lastRank.Rank <= 3)
            {
                message = lastRank.Rank switch
                {
                    1 => "🏆 ممتاز! أنت في المركز الأول على دفعتك، استمر في التميز!",
                    2 => "🥈 أداء رائع! اقتربت من القمة، أنت على الطريق الصحيح!",
                    3 => "🥉 إنجاز عظيم! حافظ على مستواك ونافس على المراتب الأولى!",
                    _ => "استمر في التقدم نحو المراتب الأولى."
                };
            }
            else if (lastRank.Rank <= (lastRank.TotalStudents * 0.3))
            {
                message = "🔥 أنت ضمن أفضل 30% من دفعتك، خطوة صغيرة تفصلك عن القمة!";
            }
            else if (lastRank.Rank <= (lastRank.TotalStudents * 0.7))
            {
                message = "💪 أداء جيد! واصل التدريب لتتحسن أكثر في الاختبارات القادمة.";
            }
            else
            {
                message = "🚀 لا تستسلم! البداية نحو التقدم تبدأ من هنا، استمر بالمثابرة.";
            }

            // 🔹 إنشاء نموذج العرض
            return new StudentRankVm
            {
                Rank = lastRank.Rank,
                TotalStudents = lastRank.TotalStudents,
                LastUpdated = lastRank.RecordedAt,
                MedalImageUrl = medalPath,
                MotivationalMessage = message
            };
        }
    }
}
