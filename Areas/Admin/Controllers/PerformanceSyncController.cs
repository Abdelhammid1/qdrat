using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "SuperAdmin,Owner,Developer")]
    public class PerformanceSyncController : Controller
    {
        private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

        public PerformanceSyncController(IDbContextFactory<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        [HttpPost]
        public async Task<IActionResult> SyncAllOldResults()
        {
            using var _context = _contextFactory.CreateDbContext();

            int insertedCount = 0;
            int skippedCount = 0;

            // 🧩 1️⃣ مزامنة نتائج الاختبارات العادية
            var examResults = await (
                from s in _context.ExamStudentStatuses
                join eab in _context.ExamAssignmentsToBatches on s.ExamAssignmentId equals eab.Id
                join ex in _context.Exams on eab.ExamId equals ex.Id
                where s.IsSubmitted == true
                select new
                {
                    s.StudentId,
                    s.Score,
                    s.SubmittedAt,
                    ExamId = ex.Id,
                    ex.Type,
                    ex.CurriculumId
                }
            ).ToListAsync();

            foreach (var r in examResults)
            {
                // ✅ تخطي أي اختبار بدون منهج
                if (r.CurriculumId == null)
                {
                    skippedCount++;
                    continue;
                }

                bool exists = await _context.StudentPerformances
                    .AnyAsync(sp => sp.StudentID == r.StudentId && sp.ExamId == r.ExamId && sp.ActivityType == PerformanceActivityType.FinalExam);

                if (!exists)
                {
                    var perf = new StudentPerformance
                    {
                        StudentID = r.StudentId,
                        Score = (double)(r.Score ?? 0),
                        ExamDate = r.SubmittedAt ?? DateTime.UtcNow,
                        ActivityType = PerformanceActivityType.FinalExam, // ✅ النوع الصحيح
                        ExamId = r.ExamId,
                        CurriculumId = r.CurriculumId.Value,
                        SectionId = null,
                        EngagementScore = 100
                    };
                    _context.StudentPerformances.Add(perf);
                    insertedCount++;
                }
            }

            // 🧩 2️⃣ مزامنة نتائج الواجبات
            var hwResults = await (
                from hss in _context.HomeworkSetStudents
                join hs in _context.HomeworkSets on hss.HomeworkSetId equals hs.Id
                where hss.IsSubmitted == true
                select new
                {
                    hss.StudentId,
                    hss.Score,
                    hss.SubmittedAt,
                    CurriculumId = hs.CurriculumId,
                    HomeworkSetId = hs.Id
                }
            ).ToListAsync();

            foreach (var h in hwResults)
            {
                if (h.CurriculumId == null)
                {
                    skippedCount++;
                    continue;
                }

                bool exists = await _context.StudentPerformances
                    .AnyAsync(sp =>
                        sp.StudentID == h.StudentId &&
                        sp.ExamId == h.HomeworkSetId &&
                        sp.ActivityType == PerformanceActivityType.Homework);

                if (!exists)
                {
                    var perf = new StudentPerformance
                    {
                        StudentID = h.StudentId,
                        Score = (double)(h.Score ?? 0),
                        ExamDate = h.SubmittedAt ?? DateTime.UtcNow,
                        ActivityType = PerformanceActivityType.Homework,
                        ExamId = h.HomeworkSetId,
                        CurriculumId = h.CurriculumId.Value,
                        SectionId = null,
                        EngagementScore = 100
                    };

                    _context.StudentPerformances.Add(perf);
                    insertedCount++;
                }
            }

            // 🧩 3️⃣ مزامنة نتائج اختبارات مؤشر الأداء
            var indicatorResults = await (
                from s in _context.PerformanceIndicatorExamStudents
                join e in _context.PerformanceIndicatorExams on s.PerformanceIndicatorExamId equals e.Id
                where s.IsCompleted == true
                select new
                {
                    s.StudentId,
                    s.ScorePercent,
                    s.CompletedAt,
                    ExamId = e.Id,
                    e.CurriculumId
                }
            ).ToListAsync();

            foreach (var r in indicatorResults)
            {
                if (r.CurriculumId == null)
                {
                    skippedCount++;
                    continue;
                }

                bool exists = await _context.StudentPerformances
                    .AnyAsync(sp =>
                        sp.StudentID == r.StudentId &&
                        sp.ExamId == r.ExamId &&
                        sp.ActivityType == PerformanceActivityType.PerformanceIndicatorExam);

                if (!exists)
                {
                    var perf = new StudentPerformance
                    {
                        StudentID = r.StudentId,
                        Score = (double)(r.ScorePercent ?? 0),
                        ExamDate = r.CompletedAt ?? DateTime.UtcNow,
                        ActivityType = PerformanceActivityType.PerformanceIndicatorExam,
                        ExamId = r.ExamId,
                        CurriculumId = r.CurriculumId,

                        SectionId = null,
                        EngagementScore = 100
                    };

                    _context.StudentPerformances.Add(perf);
                    insertedCount++;
                }
            }

            // 🧾 تنفيذ الحفظ النهائي
            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"✅ تم استيراد {insertedCount} سجل من النتائج القديمة بنجاح، وتم تخطي {skippedCount} سجل بدون منهج."
            });
        }
    }
}
