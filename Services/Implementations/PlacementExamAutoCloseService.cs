using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    // ⏱️ يغلق تلقائيًا اختبارات تحديد المستوى التي انتهى وقتها المحدد
    // دون أن يستدعي الطالب SubmitFinal (إغلاق المتصفح، انقطاع الشبكة، تجميد المؤقّت في الخلفية...)
    public class PlacementExamAutoCloseService : IPlacementExamAutoCloseService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PlacementExamAutoCloseService(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<int> CloseExpiredExamsAsync()
        {
            using var _context = _contextFactory.CreateDbContext();

            var now = DateTime.UtcNow;

            var expiredStatuses = await (
                from st in _context.ExamStudentStatuses
                join e in _context.Exams on st.ExamId equals e.Id
                where e.Type == ExamType.LevelAssessment
                      && st.Status != ExamStatus.Completed
                      && st.EndAt != null
                      && st.EndAt < now
                select st
            ).ToListAsync();

            if (!expiredStatuses.Any())
                return 0;

            foreach (var status in expiredStatuses)
            {
                status.IsSubmitted = true;
                status.Status = ExamStatus.Completed;
                status.SubmittedAt = status.EndAt;
            }

            await _context.SaveChangesAsync();

            return expiredStatuses.Count;
        }
    }
}
