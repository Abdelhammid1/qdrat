using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.ViewModels.OnlineLecture;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S5.3 — يبني صفحة قسم «المحتوى التعليمي» (R12 + C.5) فوق بوابة الوصول (R3) والتسلسل (R13).
    /// studentId/courseId/batchId من سياق الطالب في الخادم (StudentBaseController) فقط.
    /// </summary>
    public interface IStudentOnlineLearningService
    {
        Task<StudentOnlineLearningIndexVm> GetIndexAsync(int studentId, int activeCourseId, int activeBatchId, CancellationToken ct = default);
    }

    public sealed class StudentOnlineLearningService : IStudentOnlineLearningService
    {
        private readonly ApplicationDbContext _db;
        private readonly IOnlineLearningAccessService _access;
        private readonly ILectureSequenceService _sequence;

        public StudentOnlineLearningService(ApplicationDbContext db, IOnlineLearningAccessService access, ILectureSequenceService sequence)
        {
            _db = db;
            _access = access;
            _sequence = sequence;
        }

        public async Task<StudentOnlineLearningIndexVm> GetIndexAsync(int studentId, int activeCourseId, int activeBatchId, CancellationToken ct = default)
        {
            var accessible = await _access.GetAccessibleBatchesAsync(studentId, ct);
            var inCourse = accessible.Where(b => b.CourseId == activeCourseId).ToList();

            if (inCourse.Count == 0)
            {
                return new StudentOnlineLearningIndexVm
                {
                    IsEligible = false,
                    HasOtherEligibleCourse = accessible.Count > 0
                };
            }

            var batchId = inCourse.Any(b => b.BatchId == activeBatchId) ? activeBatchId : inCourse[0].BatchId;

            var header = await _db.Batches.AsNoTracking()
                .Where(b => b.Id == batchId)
                .Select(b => new { BatchName = b.Name, CourseName = b.Course != null ? b.Course.Name : null })
                .FirstOrDefaultAsync(ct);

            // R12: مناهج دورة الدفعة بترتيب CourseCurriculums.Id
            var courseCurricula = await _db.CourseCurriculums.AsNoTracking()
                .Where(cc => cc.CourseId == activeCourseId)
                .OrderBy(cc => cc.Id)
                .Select(cc => new { cc.CurriculumId, Title = cc.Curriculum != null ? cc.Curriculum.Title : null })
                .ToListAsync(ct);

            var lectures = await _sequence.GetForStudentAsync(studentId, batchId, ct);

            var curricula = new List<StudentOnlineCurriculumVm>();
            var seen = new HashSet<int>();
            foreach (var cc in courseCurricula)
            {
                if (seen.Add(cc.CurriculumId))
                    curricula.Add(new StudentOnlineCurriculumVm { CurriculumId = cc.CurriculumId, Title = cc.Title ?? "منهج" });
            }

            // محاضرة محورها في منهج غير مربوط بالدورة لا تُخفى: تظهر في منهج إضافي بعد مناهج الدورة
            foreach (var extra in lectures.Where(l => !seen.Contains(l.CurriculumId)).GroupBy(l => l.CurriculumId))
            {
                seen.Add(extra.Key);
                curricula.Add(new StudentOnlineCurriculumVm { CurriculumId = extra.Key, Title = extra.First().CurriculumTitle ?? "منهج" });
            }

            var byCurriculum = lectures.GroupBy(l => l.CurriculumId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var curriculum in curricula)
            {
                if (!byCurriculum.TryGetValue(curriculum.CurriculumId, out var rows))
                    continue;

                var nextMarked = false;
                foreach (var row in rows)
                {
                    var locked = !row.CanOpen;
                    var isNext = !nextMarked && !locked && !row.IsCompleted;
                    nextMarked |= isNext;

                    curriculum.Lectures.Add(new StudentOnlineLectureVm
                    {
                        LectureContentId = row.LectureContentId,
                        LectureTitle = row.LectureTitle,
                        LectureDate = row.LectureDate,
                        SectionTitle = string.IsNullOrWhiteSpace(row.SectionTitle) ? "غير محدد" : row.SectionTitle!,
                        Mode = row.Mode,
                        Title = row.Title,
                        ItemsCount = row.ItemsCount,
                        ProgressPercent = row.ProgressPercent,
                        IsCompleted = row.IsCompleted,
                        IsLocked = locked,
                        IsNext = isNext
                    });
                }

                curriculum.TotalCount = curriculum.Lectures.Count;
                curriculum.CompletedCount = curriculum.Lectures.Count(l => l.IsCompleted);
            }

            return new StudentOnlineLearningIndexVm
            {
                IsEligible = true,
                CourseTitle = header?.CourseName ?? string.Empty,
                BatchTitle = header?.BatchName ?? string.Empty,
                Curricula = curricula
            };
        }
    }
}
