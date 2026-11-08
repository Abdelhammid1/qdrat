using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>دفعة أونلاين يصل لها الطالب الآن (R3).</summary>
    public sealed record OnlineLearningBatchAccess(int BatchId, int CourseId);

    /// <summary>محتوى منشور يحق للطالب رؤيته (R2) مع ما يلزم لفحص القفل (R13) دون استعلام إضافي.</summary>
    public sealed record OnlineLectureViewAccess(int LectureContentId, int LectureId, int BatchId, int CurriculumId);

    /// <summary>
    /// OLC-S5.1 — بوابة وصول الطالب للمحتوى التعليمي أونلاين (R2/R3/R17). كل الشروط في استعلام Join واحد:
    /// الميزة مفعّلة + تسجيل نشط (<c>Status = "Active"</c> + <c>Student.IsActiveForLearning</c> — نفس تعريف APD والشريك)
    /// + الدفعة أونلاين وغير محذوفة + (دفعة منصة أو عقد شريكها يشمل البند ضمن نافذة الطلاب R17).
    /// أي شرط ناقص = null/false بلا تمييز يكشف الوجود (404 في الـ Controller).
    /// studentId يأتي دائمًا من سياق الطالب في الخادم — لا يُقبل من الطلب.
    /// </summary>
    public interface IOnlineLearningAccessService
    {
        /// <summary>كل الدفعات الأونلاين المتاحة للطالب الآن (فارغة إن كانت الميزة مغلقة).</summary>
        Task<IReadOnlyList<OnlineLearningBatchAccess>> GetAccessibleBatchesAsync(int studentId, CancellationToken ct = default);

        /// <summary>
        /// R3 على سياق الكورس النشط: الدفعة الأونلاين التي يعمل عليها القسم. courseId = null ⇒ أي كورس.
        /// تُفضَّل دفعة السياق إن كانت متاحة، وإلا أصغر معرّف (ثابت بين الطلبات).
        /// </summary>
        Task<int?> GetEligibleBatchIdAsync(int studentId, int? courseId, int? preferredBatchId = null, CancellationToken ct = default);

        /// <summary>R3: هل يظهر قسم «المحتوى التعليمي» للطالب؟</summary>
        Task<bool> IsEligibleAsync(int studentId, int? courseId, CancellationToken ct = default);

        /// <summary>R2: المحتوى منشور وغير محذوف ودفعته متاحة للطالب. null = 404.</summary>
        Task<OnlineLectureViewAccess?> CanViewAsync(int studentId, int lectureContentId, CancellationToken ct = default);
    }

    public sealed class OnlineLearningAccessService : IOnlineLearningAccessService
    {
        private const string ActiveEnrollmentStatus = "Active";

        private readonly ApplicationDbContext _db;
        private readonly IOnlineLectureFeatureService _feature;

        public OnlineLearningAccessService(ApplicationDbContext db, IOnlineLectureFeatureService feature)
        {
            _db = db;
            _feature = feature;
        }

        public async Task<IReadOnlyList<OnlineLearningBatchAccess>> GetAccessibleBatchesAsync(int studentId, CancellationToken ct = default)
        {
            if (studentId <= 0 || !await _feature.IsEnabledAsync(ct))
                return Array.Empty<OnlineLearningBatchAccess>();

            var rows = await AccessibleBatches(studentId)
                .Select(b => new { b.Id, b.CourseId })
                .Distinct()
                .ToListAsync(ct);

            return rows
                .OrderBy(r => r.Id)
                .Select(r => new OnlineLearningBatchAccess(r.Id, r.CourseId))
                .ToList();
        }

        public async Task<int?> GetEligibleBatchIdAsync(int studentId, int? courseId, int? preferredBatchId = null, CancellationToken ct = default)
        {
            var batches = await GetAccessibleBatchesAsync(studentId, ct);
            var inCourse = courseId is > 0 ? batches.Where(b => b.CourseId == courseId.Value).ToList() : batches.ToList();
            if (inCourse.Count == 0)
                return null;

            if (preferredBatchId is > 0 && inCourse.Any(b => b.BatchId == preferredBatchId.Value))
                return preferredBatchId.Value;

            return inCourse[0].BatchId;
        }

        public async Task<bool> IsEligibleAsync(int studentId, int? courseId, CancellationToken ct = default)
            => await GetEligibleBatchIdAsync(studentId, courseId, null, ct) is not null;

        public async Task<OnlineLectureViewAccess?> CanViewAsync(int studentId, int lectureContentId, CancellationToken ct = default)
        {
            if (studentId <= 0 || lectureContentId <= 0 || !await _feature.IsEnabledAsync(ct))
                return null;

            var batches = AccessibleBatches(studentId);

            return await (
                    from c in _db.LectureContents.AsNoTracking()
                    join l in _db.Lecture.AsNoTracking() on c.LectureId equals l.Id
                    join b in batches on l.BatchId equals b.Id
                    where c.Id == lectureContentId && c.Status == LectureContentStatus.Published
                    select new OnlineLectureViewAccess(c.Id, l.Id, l.BatchId, l.Section.CurriculumId))
                .FirstOrDefaultAsync(ct);
        }

        // دفعات أونلاين غير محذوفة للطالب فيها تسجيل نشط، ومتاحة وفق R17 (دفعة المنصة دائمًا) — تعبير واحد يُدمج في أي Join
        private IQueryable<Entities.Batch> AccessibleBatches(int studentId)
        {
            var onlineBatches = _db.Batches.AsNoTracking()
                .Where(b => b.IsOnlineLearning && !b.IsDeleted)
                .WhereStudentAccessible(_db.PartnerSubscriptions.AsNoTracking(), DateTime.Today);

            return onlineBatches.Where(b => _db.StudentBatchEnrollments.Any(e =>
                e.BatchId == b.Id &&
                e.StudentID == studentId &&
                e.Status == ActiveEnrollmentStatus &&
                e.Student.IsActiveForLearning));
        }
    }
}
