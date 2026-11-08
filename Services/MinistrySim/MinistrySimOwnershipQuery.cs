using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;

namespace QdratNew.Services.MinistrySim
{
    // APD-S5.1: قاعدة ملكية واحدة لمحاولات وطلاب معمل القياس.
    // المحاولة لا تحمل رقم التكليف (مفتاحها الاختبار والطالب)، فتُنسب لصاحب تكليف الطالب في هذا الاختبار:
    //   1) صف التكليف الفردي (MinistrySimExamAssignmentToStudent) هو المرجع إن وُجد، وإسناد الدفعة يكتبه لكل طالب أيضًا.
    //   2) بلا صف فردي (طالب انضم لدفعة بعد إسنادها): تكليف دفعة يكون الطالب مسجّلًا فيها تسجيلًا نشطًا.
    // وتُنسب للمدرسة P فقط إن كان الطالب من فروعها الآن (Student.Branch.PartnerId == P).
    // كل ما لا يخص مدرسة بهذه القاعدة يخص المعهد، فكل محاولة تظهر في جهة واحدة فقط.
    public interface IMinistrySimOwnershipQuery
    {
        IQueryable<MinistrySimExamStudentAttempt> PartnerAttempts(int partnerId);

        IQueryable<MinistrySimExamStudentAttempt> PlatformAttempts();

        // طلاب الاختبار المملوكون للمدرسة (لهم محاولة أم لا)
        IQueryable<Student> PartnerStudents(int examId, int partnerId);

        // نفس قاعدة PartnerStudents لكل اختبارات المدرسة في استعلام واحد (للعدّ في قائمتها)
        IQueryable<MinistrySimStudentPair> PartnerStudentPairs(int partnerId);

        // طلاب الاختبار المُسنَد لهم بأي طريق ولا تملكهم مدرسة
        IQueryable<Student> PlatformStudents(int examId);

        Task<bool> IsPartnerStudentAsync(int examId, int studentId, int partnerId, CancellationToken ct = default);

        // نتيجة الطالب في هذا الاختبار تخص مدرسة شريكة، فتُخفى عن الأدمن
        Task<bool> IsOwnedByAnyPartnerAsync(int examId, int studentId, CancellationToken ct = default);
    }

    public sealed class MinistrySimStudentPair
    {
        public int ExamId { get; init; }
        public int StudentId { get; init; }
    }

    public class MinistrySimOwnershipQuery : IMinistrySimOwnershipQuery
    {
        private const string ActiveEnrollmentStatus = "Active";

        private readonly ApplicationDbContext _context;

        public MinistrySimOwnershipQuery(ApplicationDbContext context)
        {
            _context = context;
        }

        public IQueryable<MinistrySimExamStudentAttempt> PartnerAttempts(int partnerId)
            => _context.MinistrySimExamStudentAttempts.AsNoTracking()
                .Where(a =>
                    a.Student.Branch.PartnerId == partnerId &&
                    (_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                         x.MinistrySimExamId == a.MinistrySimExamId &&
                         x.StudentId == a.StudentId &&
                         x.OwnerPartnerId == partnerId) ||
                     (!_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                          x.MinistrySimExamId == a.MinistrySimExamId &&
                          x.StudentId == a.StudentId) &&
                      _context.MinistrySimExamAssignmentsToBatches.Any(b =>
                          b.MinistrySimExamId == a.MinistrySimExamId &&
                          b.OwnerPartnerId == partnerId &&
                          _context.StudentBatchEnrollments.Any(e =>
                              e.BatchId == b.BatchId &&
                              e.StudentID == a.StudentId &&
                              e.Status == ActiveEnrollmentStatus)))));

        public IQueryable<MinistrySimExamStudentAttempt> PlatformAttempts()
            => _context.MinistrySimExamStudentAttempts.AsNoTracking()
                .Where(a => !(
                    a.Student.Branch.PartnerId != null &&
                    (_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                         x.MinistrySimExamId == a.MinistrySimExamId &&
                         x.StudentId == a.StudentId &&
                         x.OwnerPartnerId == a.Student.Branch.PartnerId) ||
                     (!_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                          x.MinistrySimExamId == a.MinistrySimExamId &&
                          x.StudentId == a.StudentId) &&
                      _context.MinistrySimExamAssignmentsToBatches.Any(b =>
                          b.MinistrySimExamId == a.MinistrySimExamId &&
                          b.OwnerPartnerId == a.Student.Branch.PartnerId &&
                          _context.StudentBatchEnrollments.Any(e =>
                              e.BatchId == b.BatchId &&
                              e.StudentID == a.StudentId &&
                              e.Status == ActiveEnrollmentStatus))))));

        public IQueryable<Student> PartnerStudents(int examId, int partnerId)
            => _context.Students.AsNoTracking()
                .Where(s =>
                    s.Branch.PartnerId == partnerId &&
                    (_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                         x.MinistrySimExamId == examId &&
                         x.StudentId == s.StudentID &&
                         x.OwnerPartnerId == partnerId) ||
                     (!_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                          x.MinistrySimExamId == examId &&
                          x.StudentId == s.StudentID) &&
                      _context.MinistrySimExamAssignmentsToBatches.Any(b =>
                          b.MinistrySimExamId == examId &&
                          b.OwnerPartnerId == partnerId &&
                          _context.StudentBatchEnrollments.Any(e =>
                              e.BatchId == b.BatchId &&
                              e.StudentID == s.StudentID &&
                              e.Status == ActiveEnrollmentStatus)))));

        public IQueryable<MinistrySimStudentPair> PartnerStudentPairs(int partnerId)
        {
            var direct = _context.MinistrySimExamAssignmentsToStudents.AsNoTracking()
                .Where(x => x.OwnerPartnerId == partnerId && x.Student.Branch.PartnerId == partnerId)
                .Select(x => new MinistrySimStudentPair { ExamId = x.MinistrySimExamId, StudentId = x.StudentId });

            var viaBatch =
                from b in _context.MinistrySimExamAssignmentsToBatches.AsNoTracking()
                join e in _context.StudentBatchEnrollments.AsNoTracking() on b.BatchId equals e.BatchId
                where b.OwnerPartnerId == partnerId &&
                      e.Status == ActiveEnrollmentStatus &&
                      e.Student.Branch.PartnerId == partnerId &&
                      !_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                          x.MinistrySimExamId == b.MinistrySimExamId &&
                          x.StudentId == e.StudentID)
                select new MinistrySimStudentPair { ExamId = b.MinistrySimExamId, StudentId = e.StudentID };

            return direct.Union(viaBatch);
        }

        public IQueryable<Student> PlatformStudents(int examId)
            => _context.Students.AsNoTracking()
                .Where(s =>
                    (_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                         x.MinistrySimExamId == examId &&
                         x.StudentId == s.StudentID) ||
                     _context.MinistrySimExamAssignmentsToBatches.Any(b =>
                         b.MinistrySimExamId == examId &&
                         _context.StudentBatchEnrollments.Any(e =>
                             e.BatchId == b.BatchId &&
                             e.StudentID == s.StudentID &&
                             e.Status == ActiveEnrollmentStatus))) &&
                    !(s.Branch.PartnerId != null &&
                      (_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                           x.MinistrySimExamId == examId &&
                           x.StudentId == s.StudentID &&
                           x.OwnerPartnerId == s.Branch.PartnerId) ||
                       (!_context.MinistrySimExamAssignmentsToStudents.Any(x =>
                            x.MinistrySimExamId == examId &&
                            x.StudentId == s.StudentID) &&
                        _context.MinistrySimExamAssignmentsToBatches.Any(b =>
                            b.MinistrySimExamId == examId &&
                            b.OwnerPartnerId == s.Branch.PartnerId &&
                            _context.StudentBatchEnrollments.Any(e =>
                                e.BatchId == b.BatchId &&
                                e.StudentID == s.StudentID &&
                                e.Status == ActiveEnrollmentStatus))))));

        public Task<bool> IsPartnerStudentAsync(int examId, int studentId, int partnerId, CancellationToken ct = default)
            => PartnerStudents(examId, partnerId).AnyAsync(s => s.StudentID == studentId, ct);

        public async Task<bool> IsOwnedByAnyPartnerAsync(int examId, int studentId, CancellationToken ct = default)
        {
            var partnerId = await _context.Students.AsNoTracking()
                .Where(s => s.StudentID == studentId)
                .Select(s => s.Branch.PartnerId)
                .FirstOrDefaultAsync(ct);

            return partnerId.HasValue && await IsPartnerStudentAsync(examId, studentId, partnerId.Value, ct);
        }
    }
}
