using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Interfaces;

namespace QdratNew.Services.PartnerDispatch
{
    // APD-S5.1: إعدادات الإسناد كما في شاشتي الأدمن (أونلاين/حضوري وموعد الظهور ومدرب الإسناد)
    public sealed record MinistrySimAssignOptions(bool IsOnline, DateTime? VisibleFrom, int? CreatedByInstructorId);

    // APD-S5.1: تكليف معمل القياس لعدة مدارس: الاختبار نفسه محتوى المعهد، والتكليف ملك المدرسة (OwnerPartnerId).
    // Transaction مستقلة لكل مدرسة، وفشل مدرسة لا يلغي غيرها، وسطر سجل لكل دفعة داخل نفس الـ Transaction.
    public interface IPartnerMinistrySimDispatchService
    {
        // targets ناتج IDispatchAudienceService.ValidateAsync
        Task<PartnerDispatchOutcome> AssignToBatchesAsync(int examId, IReadOnlyList<PartnerDispatchTarget> targets, MinistrySimAssignOptions options, DispatchActor actor, CancellationToken ct = default);

        // طلاب محددون من دفعة مدرسة واحدة (target.BatchIds = تلك الدفعة)
        Task<PartnerDispatchOutcome> AssignToStudentsAsync(int examId, PartnerDispatchTarget target, IReadOnlyList<int> studentIds, MinistrySimAssignOptions options, DispatchActor actor, CancellationToken ct = default);
    }

    public class PartnerMinistrySimDispatchService : IPartnerMinistrySimDispatchService
    {
        private const string ActiveEnrollmentStatus = "Active";
        private const int LockTimeoutMilliseconds = 15000;

        private readonly ApplicationDbContext _context;
        private readonly IMinistrySimExamAssignmentService _assignments;
        private readonly IPartnerDispatchLogService _dispatchLog;
        private readonly ILogger<PartnerMinistrySimDispatchService> _logger;

        public PartnerMinistrySimDispatchService(
            ApplicationDbContext context,
            IMinistrySimExamAssignmentService assignments,
            IPartnerDispatchLogService dispatchLog,
            ILogger<PartnerMinistrySimDispatchService> logger)
        {
            _context = context;
            _assignments = assignments;
            _dispatchLog = dispatchLog;
            _logger = logger;
        }

        // خطأ عمل متوقع رسالته آمنة للعرض (يُرمى داخل ExecutionStrategy لإلغاء الـ Transaction)
        private sealed class DispatchRuleException : Exception
        {
            public DispatchRuleException(string message) : base(message) { }
        }

        private sealed record ExamSource(int Id, string Title, int CourseId);

        private sealed record PartnerWrite(int NewBatches, int Students, string? ReferenceCode);

        public async Task<PartnerDispatchOutcome> AssignToBatchesAsync(int examId, IReadOnlyList<PartnerDispatchTarget> targets, MinistrySimAssignOptions options, DispatchActor actor, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(targets);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(actor);

            var (outcome, exam) = await BeginAsync(examId, targets.Count, ct);
            if (exam == null)
                return outcome;

            foreach (var target in targets)
            {
                outcome.Results.Add(await RunForPartnerAsync(target, outcome.DispatchGroupId, ct, async () =>
                {
                    await EnsurePartnerBatchesAsync(target, exam.CourseId, ct);

                    var ids = target.BatchIds.Distinct().ToList();
                    var existingBatchIds = await _context.MinistrySimExamAssignmentsToBatches.AsNoTracking()
                        .Where(a => a.MinistrySimExamId == exam.Id && EF.Constant(ids).Contains(a.BatchId))
                        .Select(a => a.BatchId)
                        .ToListAsync(ct);

                    var assigned = await _assignments.AssignToBatchesAsync(
                        exam.Id, ids, options.CreatedByInstructorId, options.IsOnline, options.VisibleFrom, target.PartnerId);

                    if (!assigned.Success)
                        throw new DispatchRuleException("⚠️ " + assigned.Message);

                    var rows = await _context.MinistrySimExamAssignmentsToBatches.AsNoTracking()
                        .Where(a => a.MinistrySimExamId == exam.Id && a.OwnerPartnerId == target.PartnerId && EF.Constant(ids).Contains(a.BatchId))
                        .Select(a => new { a.Id, a.BatchId })
                        .ToListAsync(ct);

                    var studentsPerBatch = await CountPerBatchAsync(ids, assigned.AssignedStudentIds, ct);

                    var nowUtc = DateTime.UtcNow;
                    await _dispatchLog.WriteAsync(
                        rows
                            .Where(r => !existingBatchIds.Contains(r.BatchId) || studentsPerBatch.GetValueOrDefault(r.BatchId) > 0)
                            .Select(r => NewLog(exam, target, actor, outcome.DispatchGroupId, r.BatchId, r.Id, studentsPerBatch.GetValueOrDefault(r.BatchId), nowUtc))
                            .ToList(),
                        ct);

                    return new PartnerWrite(assigned.AssignedBatchesCount, assigned.AssignedStudentsCount, assigned.GeneratedReferenceCode);
                }, actor, exam));
            }

            return outcome;
        }

        public async Task<PartnerDispatchOutcome> AssignToStudentsAsync(int examId, PartnerDispatchTarget target, IReadOnlyList<int> studentIds, MinistrySimAssignOptions options, DispatchActor actor, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(studentIds);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(actor);

            var (outcome, exam) = await BeginAsync(examId, 1, ct);
            if (exam == null)
                return outcome;

            var ids = studentIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                outcome.ValidationError = "⚠️ اختر طالبًا واحدًا على الأقل.";
                return outcome;
            }

            outcome.Results.Add(await RunForPartnerAsync(target, outcome.DispatchGroupId, ct, async () =>
            {
                await EnsurePartnerBatchesAsync(target, exam.CourseId, ct);

                // الطالب يجب أن يكون نشطًا في الدفعة المختارة نفسها (لا في أي دفعة للمدرسة فقط)
                var batchId = target.BatchIds.Single();
                var inBatch = await _context.StudentBatchEnrollments.AsNoTracking()
                    .Where(e => e.BatchId == batchId && e.Status == ActiveEnrollmentStatus && EF.Constant(ids).Contains(e.StudentID))
                    .Select(e => e.StudentID)
                    .Distinct()
                    .ToListAsync(ct);

                if (inBatch.Count != ids.Count)
                    throw new DispatchRuleException("⚠️ طالب أو أكثر ليس مسجلًا تسجيلًا نشطًا في الدفعة المختارة.");

                var assigned = await _assignments.AssignToStudentsAsync(exam.Id, ids, options.IsOnline, options.VisibleFrom, target.PartnerId);
                if (!assigned.Success)
                    throw new DispatchRuleException("⚠️ " + assigned.Message);

                // لا صف تكليف واحد للطلاب الأفراد: TargetEntityId = رقم الاختبار
                if (assigned.AssignedStudentsCount > 0)
                {
                    await _dispatchLog.WriteAsync(
                        new[] { NewLog(exam, target, actor, outcome.DispatchGroupId, batchId, exam.Id, assigned.AssignedStudentsCount, DateTime.UtcNow) },
                        ct);
                }

                return new PartnerWrite(0, assigned.AssignedStudentsCount, assigned.GeneratedReferenceCode);
            }, actor, exam));

            return outcome;
        }

        // =====================================================
        // مشترك
        // =====================================================
        private async Task<(PartnerDispatchOutcome Outcome, ExamSource? Exam)> BeginAsync(int examId, int targetsCount, CancellationToken ct)
        {
            var outcome = new PartnerDispatchOutcome
            {
                DispatchGroupId = Guid.NewGuid(),
                Kind = PartnerDispatchKind.MinistrySim
            };

            if (targetsCount == 0)
            {
                outcome.ValidationError = "⚠️ اختر مدرسة شريكة ودفعة واحدة على الأقل.";
                return (outcome, null);
            }

            var exam = await _context.MinistrySimExams.AsNoTracking()
                .Where(e => e.Id == examId)
                .Select(e => new { e.Id, e.Title, e.CourseId, e.IsPublished })
                .FirstOrDefaultAsync(ct);

            if (exam == null)
            {
                outcome.IsNotFound = true;
                return (outcome, null);
            }

            if (!exam.IsPublished)
            {
                outcome.ValidationError = "⚠️ لا يمكن إسناد الاختبار إلا بعد نشره أولاً.";
                return (outcome, null);
            }

            return (outcome, new ExamSource(exam.Id, exam.Title, exam.CourseId));
        }

        private static PartnerDispatchLog NewLog(ExamSource exam, PartnerDispatchTarget target, DispatchActor actor, Guid groupId, int batchId, int targetId, int students, DateTime nowUtc)
            => new()
            {
                DispatchGroupId = groupId,
                Kind = PartnerDispatchKind.MinistrySim,
                Title = exam.Title,
                SourceMinistrySimExamId = exam.Id,
                PartnerId = target.PartnerId,
                BatchId = batchId,
                TargetEntityId = targetId,
                StudentsCount = students,
                ContractOverridden = target.ContractOverridden,
                OverrideReason = target.OverrideReason,
                SentByUserId = actor.UserId,
                SentAtUtc = nowUtc
            };

        private async Task<PartnerDispatchPartnerResult> RunForPartnerAsync(
            PartnerDispatchTarget target,
            Guid groupId,
            CancellationToken ct,
            Func<Task<PartnerWrite>> write,
            DispatchActor actor,
            ExamSource exam)
        {
            var result = new PartnerDispatchPartnerResult
            {
                PartnerId = target.PartnerId,
                PartnerName = target.PartnerName,
                ContractOverridden = target.ContractOverridden
            };

            PartnerWrite? written = null;

            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    // إعادة المحاولة عبر ExecutionStrategy تبدأ بلا صفوف معلّقة من المحاولة السابقة
                    _context.ChangeTracker.Clear();

                    await using var transaction = await _context.Database.BeginTransactionAsync(ct);

                    // الضغطة المزدوجة: الطلب الثاني ينتظر ثم يجد الدفعات والطلاب مُسنَدين فلا يكرر شيئًا
                    await AcquireLockAsync(exam.Id, target.PartnerId, ct);

                    written = await write();

                    await transaction.CommitAsync(ct);
                });
            }
            catch (DispatchRuleException ex)
            {
                _context.ChangeTracker.Clear();
                result.Message = ex.Message;
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // ما بقي متتبعًا من محاولة فاشلة قد يُحفظ مع إرسال المدرسة التالية على نفس الـ DbContext
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "APD ministry sim dispatch failed for partner {PartnerId}, group {DispatchGroupId}", target.PartnerId, groupId);
                result.Message = "⚠️ تعذر الإسناد لهذه المدرسة، ولم يُحفظ لها أي شيء.";
                return result;
            }

            result.Succeeded = true;
            result.BatchesSent = written!.NewBatches;
            result.StudentsSent = written.Students;

            var parts = new List<string>
            {
                written.NewBatches == 0 && written.Students == 0
                    ? "✅ لا جديد: الدفعات والطلاب مُسنَدون لهذا الاختبار من قبل."
                    : $"✅ أُسند لـ {written.NewBatches} دفعة جديدة و{written.Students} طالب."
            };

            if (!string.IsNullOrWhiteSpace(written.ReferenceCode))
                parts.Add($"الرمز المرجعي للجلسة الحضورية: {written.ReferenceCode}.");

            // بالتجاوز يصل الاختبار للطلاب، لكن صفحات المتابعة تتبع عقد المدرسة
            if (target.ContractOverridden)
                parts.Add("عقد المدرسة لا يشمل معمل القياس، فلن تفتح صفحة المتابعة لديها حتى يشمله العقد.");

            result.Message = string.Join(" ", parts);
            return result;
        }

        private async Task EnsurePartnerBatchesAsync(PartnerDispatchTarget target, int courseId, CancellationToken ct)
        {
            var partnerId = target.PartnerId;
            var ids = target.BatchIds.Distinct().ToList();

            var validCount = ids.Count == 0
                ? 0
                : await _context.Batches.AsNoTracking()
                    .CountAsync(b =>
                        EF.Constant(ids).Contains(b.Id) &&
                        !b.IsDeleted &&
                        !b.IsArchived &&
                        b.CourseId == courseId &&
                        b.Branch.PartnerId == partnerId, ct);

            if (ids.Count == 0 || validCount != ids.Count)
                throw new DispatchRuleException("⚠️ دفعة أو أكثر لا تخص هذه المدرسة أو لم تعد متاحة لهذا الإسناد.");
        }

        private async Task<Dictionary<int, int>> CountPerBatchAsync(List<int> batchIds, List<int> studentIds, CancellationToken ct)
        {
            if (studentIds.Count == 0)
                return new Dictionary<int, int>();

            var rows = await _context.StudentBatchEnrollments.AsNoTracking()
                .Where(e =>
                    EF.Constant(batchIds).Contains(e.BatchId) &&
                    EF.Constant(studentIds).Contains(e.StudentID) &&
                    e.Status == ActiveEnrollmentStatus)
                .Select(e => new { e.BatchId, e.StudentID })
                .Distinct()
                .ToListAsync(ct);

            return rows.GroupBy(r => r.BatchId).ToDictionary(g => g.Key, g => g.Count());
        }

        // يتحرر مع الـ Transaction
        private async Task AcquireLockAsync(int examId, int partnerId, CancellationToken ct)
        {
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = $"APD_MSE_{examId}_{partnerId}" };
            var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = LockTimeoutMilliseconds };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout",
                new object[] { result, resource, timeout },
                ct);

            if (result.Value is not int code || code < 0)
                throw new DispatchRuleException("⚠️ يجري الإسناد لهذه المدرسة الآن، حاول بعد قليل.");
        }
    }
}
