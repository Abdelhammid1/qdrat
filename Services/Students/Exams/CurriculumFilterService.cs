using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Services.Exams;
using QdratNew.ViewModels.Exam;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QdratNew.Services.Exams
{
    public class CurriculumFilterService : ICurriculumFilterService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public CurriculumFilterService(
            IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<List<CurriculumFilterViewModel>> GetStudentCurriculumsAsync(
            int studentId,
            int batchId)
        {
            using var db = _contextFactory.CreateDbContext();

            // ======================================================
            // المناهج المرتبطة بالاختبارات المرسلة للدفعة
            // ======================================================
            var data = await (
                from a in db.ExamAssignmentsToBatches.AsNoTracking()
                join c in db.Curriculums.AsNoTracking()
                    on a.CurriculumId equals c.Id
                where a.BatchId == batchId
                      && a.CurriculumId.HasValue
                select new
                {
                    c.Id,
                    c.Title
                }
            ).Distinct().ToListAsync();

            // ======================================================
            // تحويل صريح للـ ViewModel الجديد
            // ======================================================
            return data
                .Select(x => new CurriculumFilterViewModel
                {
                    Id = x.Id,
                    Title = x.Title
                })
                .OrderBy(x => x.Title)
                .ToList();
        }
    }
}
