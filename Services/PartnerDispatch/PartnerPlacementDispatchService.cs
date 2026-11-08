using System.Data;
using EFCore.BulkExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.PartnerDispatch
{
    public class PartnerPlacementDispatchService : IPartnerPlacementDispatchService
    {
        private const string ActiveEnrollmentStatus = "Active";
        private const int MinDurationMinutes = 5;
        private const int MaxDurationMinutes = 300;
        private const int LockTimeoutMilliseconds = 15000;

        // ضغطتان متتاليتان تنشئان نسختين لا يراهما الأدمن في قوائمه
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(60);

        private readonly ApplicationDbContext _context;
        private readonly IPlacementExamScopeGuard _guard;
        private readonly IPartnerDispatchLogService _dispatchLog;
        private readonly IAdvancedNotificationService _notificationService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PartnerPlacementDispatchService> _logger;

        public PartnerPlacementDispatchService(
            ApplicationDbContext context,
            IPlacementExamScopeGuard guard,
            IPartnerDispatchLogService dispatchLog,
            IAdvancedNotificationService notificationService,
            IMemoryCache cache,
            ILogger<PartnerPlacementDispatchService> logger)
        {
            _context = context;
            _guard = guard;
            _dispatchLog = dispatchLog;
            _notificationService = notificationService;
            _cache = cache;
            _logger = logger;
        }

        // خطأ عمل متوقع رسالته آمنة للعرض (يُرمى داخل ExecutionStrategy لإلغاء الـ Transaction)
        private sealed class PlacementDispatchRuleException : Exception
        {
            public PlacementDispatchRuleException(string message) : base(message) { }
        }

        private sealed record ModelSource(int Id, string Title, List<Guid> QuestionIds);

        private sealed record CreatedExam(string Title, int Batches, List<int> Students);

        public async Task<PartnerDispatchOutcome> DispatchAsync(IReadOnlyList<PartnerDispatchTarget> targets, SendPlacementToPartnersRequest request, DispatchActor actor, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(targets);
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(actor);

            var outcome = new PartnerDispatchOutcome
            {
                DispatchGroupId = Guid.NewGuid(),
                Kind = PartnerDispatchKind.Placement,
                IsDraft = request.Delivery == PlacementPartnerDelivery.Draft
            };

            outcome.ValidationError = await ValidateRequestAsync(targets, request, ct);
            if (outcome.ValidationError != null)
                return outcome;

            var model = await LoadModelAsync(request.ModelId, ct);
            if (model == null)
            {
                outcome.IsNotFound = true;
                return outcome;
            }

            if (model.QuestionIds.Count == 0)
            {
                outcome.ValidationError = "⚠️ النموذج لا يحتوي على أسئلة.";
                return outcome;
            }

            foreach (var target in targets)
                outcome.Results.Add(await SendToPartnerAsync(target, model, request, actor, outcome.DispatchGroupId, ct));

            return outcome;
        }

        private async Task<string?> ValidateRequestAsync(IReadOnlyList<PartnerDispatchTarget> targets, SendPlacementToPartnersRequest request, CancellationToken ct)
        {
            if (targets.Count == 0)
                return "⚠️ اختر مدرسة شريكة ودفعة واحدة على الأقل.";

            if (!Enum.IsDefined(request.Delivery))
                return "⚠️ طريقة الإرسال غير صالحة.";

            // نفس حدود الإنشاء من منطقة الشريك
            if (request.DurationMinutes < MinDurationMinutes || request.DurationMinutes > MaxDurationMinutes)
                return $"⚠️ مدة الاختبار يجب أن تكون بين {MinDurationMinutes} و {MaxDurationMinutes} دقيقة.";

            return request.CourseId > 0 && await _context.Courses.AsNoTracking().AnyAsync(c => c.Id == request.CourseId, ct)
                ? null
                : "⚠️ يجب اختيار دورة.";
        }

        // نفس قائمة نماذج شاشة الأدمن: غير مؤرشف وليس واجبًا. إتاحته للشريك فُحصت في ValidateAsync مع التجاوز.
        private async Task<ModelSource?> LoadModelAsync(int modelId, CancellationToken ct)
        {
            var model = await _context.ProfessionalModels.AsNoTracking()
                .Where(m => m.Id == modelId && !m.IsArchived && m.ModelType != ProfessionalModelType.Homework)
                .Select(m => new { m.Id, m.Title })
                .FirstOrDefaultAsync(ct);

            if (model == null)
                return null;

            var rows = await _context.ProfessionalModelQuestions.AsNoTracking()
                .Where(q => q.ModelId == modelId && q.QuestionId != null)
                .OrderBy(q => q.OrderNumber)
                .ThenBy(q => q.Id)
                .Select(q => q.QuestionId!.Value)
                .ToListAsync(ct);

            // في البيانات أزواج مكررة (نموذج + سؤال)، والسؤال لا يتكرر داخل الاختبار (كخدمة الإرسال من نموذج)
            return new ModelSource(model.Id, model.Title, rows.Distinct().ToList());
        }

        private async Task<PartnerDispatchPartnerResult> SendToPartnerAsync(
            PartnerDispatchTarget target,
            ModelSource model,
            SendPlacementToPartnersRequest request,
            DispatchActor actor,
            Guid groupId,
            CancellationToken ct)
        {
            var result = new PartnerDispatchPartnerResult
            {
                PartnerId = target.PartnerId,
                PartnerName = target.PartnerName,
                ContractOverridden = target.ContractOverridden
            };

            var scope = PlacementScope.AdminForPartner(target.PartnerId, actor.UserId, actor.UserName, target.ContractOverridden, target.OverrideReason, groupId);
            CreatedExam created;

            try
            {
                created = await CreateAsync(scope, target, model, request, ct);
            }
            catch (PlacementDispatchRuleException ex)
            {
                _context.ChangeTracker.Clear();
                result.Message = ex.Message;
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // ما بقي متتبعًا من محاولة فاشلة قد يُحفظ مع إرسال المدرسة التالية على نفس الـ DbContext
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "APD placement dispatch failed for partner {PartnerId}, group {DispatchGroupId}", target.PartnerId, groupId);
                result.Message = "⚠️ تعذر الإرسال لهذه المدرسة، ولم يُحفظ لها أي شيء.";
                return result;
            }

            result.Succeeded = true;
            result.BatchesSent = created.Batches;
            result.StudentsSent = created.Students.Count;
            result.Message = request.Delivery == PlacementPartnerDelivery.Draft
                ? "✅ أُنشئت مسودة لدى المدرسة، ولم تصل لأي طالب."
                : "✅ تم إنشاء الاختبار وإرساله للطلاب.";

            // نجح فعليًا: تعثر الإشعار يُسجَّل ولا يقلب النتيجة إلى فشل
            await NotifyStudentsAsync(scope, created, ct);

            return result;
        }

        private async Task<CreatedExam> CreateAsync(
            PlacementScope scope,
            PartnerDispatchTarget target,
            ModelSource model,
            SendPlacementToPartnersRequest request,
            CancellationToken ct)
        {
            var partnerId = scope.PartnerId!.Value;
            var sendNow = request.Delivery == PlacementPartnerDelivery.SendNow;

            var recipients = await LoadRecipientsAsync(scope, target.BatchIds, request.CourseId, ct);
            if (recipients.Count == 0)
                throw new PlacementDispatchRuleException("⚠️ لا يوجد طلاب نشطون في الدفعات المختارة.");

            // المسودة يرسلها الشريك من صفحته، وصفحته لا تُفتح لعقد لا يشمل تحديد المستوى
            if (!sendNow && !await HasPlacementContractAsync(partnerId, ct))
                throw new PlacementDispatchRuleException("⚠️ عقد المدرسة لا يشمل تحديد المستوى، فلن تتمكن من فتح المسودة. استخدم الإرسال الفوري.");

            var batchIds = recipients.Keys.ToList();
            var students = recipients.Values.SelectMany(s => s).Distinct().ToList();
            var title = $"اختبار تحديد مستوى - {model.Title}";
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                // إعادة المحاولة عبر ExecutionStrategy تبدأ بلا صفوف معلّقة من المحاولة السابقة
                _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync(ct);

                await AcquireLockAsync(model.Id, partnerId, ct);
                await EnsureNotDuplicateAsync(model.Id, partnerId, batchIds, ct);

                var nowUtc = DateTime.UtcNow;
                var exam = new Exam
                {
                    Title = title,
                    Type = ExamType.LevelAssessment,
                    CourseId = request.CourseId,
                    CreatedAt = nowUtc,
                    DurationMinutes = request.DurationMinutes,
                    TotalQuestions = model.QuestionIds.Count,
                    IsActive = true,
                    IsFromProfessionalModel = true,
                    SourceProfessionalModelId = model.Id,
                    OwnerPartnerId = partnerId
                };

                _context.Exams.Add(exam);
                await _context.SaveChangesAsync(ct);

                await _context.BulkInsertAsync(
                    model.QuestionIds
                        .Select((questionId, i) => new ExamQuestion { ExamId = exam.Id, QuestionId = questionId, Order = i + 1 })
                        .ToList(),
                    cancellationToken: ct);

                _context.ExamAssignmentsToBatches.AddRange(batchIds.Select(batchId => new ExamAssignmentToBatch
                {
                    ExamId = exam.Id,
                    BatchId = batchId,
                    Title = exam.Title,
                    TotalQuestions = exam.TotalQuestions,
                    DurationMinutes = exam.DurationMinutes,
                    CreatedAt = nowUtc,
                    IsSentToStudents = sendNow
                }));
                await _context.SaveChangesAsync(ct);

                if (sendNow)
                    await InsertDeliveryAsync(exam, students, nowUtc, ct);

                var uniqueCounts = DispatchRules.UniqueStudentsPerBatch(recipients);
                await _dispatchLog.WriteAsync(
                    batchIds
                        .Select(batchId => new PartnerDispatchLog
                        {
                            DispatchGroupId = scope.DispatchGroupId!.Value,
                            Kind = PartnerDispatchKind.Placement,
                            Title = exam.Title,
                            SourceProfessionalModelId = model.Id,
                            PartnerId = partnerId,
                            BatchId = batchId,
                            TargetEntityId = exam.Id,
                            StudentsCount = sendNow ? uniqueCounts[batchId] : 0, // 0 = مسودة لم تصل لطالب
                            ContractOverridden = scope.ContractOverridden,
                            OverrideReason = scope.OverrideReason,
                            SentByUserId = scope.UserId!,
                            SentAtUtc = nowUtc
                        })
                        .ToList(),
                    ct);

                await transaction.CommitAsync(ct);
            });

            return new CreatedExam(title, batchIds.Count, sendNow ? students : new List<int>());
        }

        private async Task InsertDeliveryAsync(Exam exam, List<int> students, DateTime nowUtc, CancellationToken ct)
        {
            await _context.BulkInsertAsync(
                students
                    .Select(studentId => new ExamAssignment
                    {
                        ExamId = exam.Id,
                        StudentId = studentId,
                        AssignedAt = nowUtc,
                        DueDate = nowUtc.AddMinutes(exam.DurationMinutes)
                    })
                    .ToList(),
                cancellationToken: ct);

            await _context.BulkInsertAsync(
                students
                    .Select(studentId => new ExamStudentStatus
                    {
                        StudentId = studentId,
                        ExamId = exam.Id,
                        AssignedAt = nowUtc,
                        Status = ExamStatus.Pending
                    })
                    .ToList(),
                cancellationToken: ct);
        }

        // BatchId → الطلاب النشطون في فروع المدرسة. الدفعة بلا طلاب لا تُربط (الواجهة تعطّلها أصلًا).
        private async Task<Dictionary<int, List<int>>> LoadRecipientsAsync(PlacementScope scope, List<int> requestedBatchIds, int courseId, CancellationToken ct)
        {
            var partnerId = scope.PartnerId!.Value;
            var ids = requestedBatchIds.Distinct().ToList();

            var validIds = await _guard.Batches(_context, scope).AsNoTracking()
                .Where(b => EF.Constant(ids).Contains(b.Id) && !b.IsDeleted && !b.IsArchived && b.CourseId == courseId)
                .Select(b => b.Id)
                .ToListAsync(ct);

            if (ids.Count == 0 || validIds.Count != ids.Count)
                throw new PlacementDispatchRuleException("⚠️ دفعة أو أكثر لا تخص هذه المدرسة أو لم تعد متاحة لهذا الإرسال.");

            var rows = await _context.StudentBatchEnrollments.AsNoTracking()
                .Where(e =>
                    EF.Constant(ids).Contains(e.BatchId) &&
                    e.Status == ActiveEnrollmentStatus &&
                    e.Student.IsActiveForLearning &&
                    e.Student.Branch.PartnerId == partnerId)
                .Select(e => new { e.BatchId, e.StudentID })
                .Distinct()
                .ToListAsync(ct);

            return rows
                .GroupBy(r => r.BatchId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.StudentID).ToList());
        }

        private Task<bool> HasPlacementContractAsync(int partnerId, CancellationToken ct)
        {
            var today = DateTime.Today;
            return _context.PartnerSubscriptions.AsNoTracking().AnyAsync(s =>
                s.PartnerId == partnerId &&
                s.StartDate <= today &&
                s.EndDate >= today &&
                s.CanUsePlacementExams, ct);
        }

        // يتحرر مع الـ Transaction، فالطلب المتزامن الثاني ينتظر ثم يجد ما كُتب ويتوقف
        private async Task AcquireLockAsync(int modelId, int partnerId, CancellationToken ct)
        {
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = $"APD_Placement_{modelId}_{partnerId}" };
            var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = LockTimeoutMilliseconds };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout",
                new object[] { result, resource, timeout },
                ct);

            if (result.Value is not int code || code < 0)
                throw new PlacementDispatchRuleException("⚠️ يجري الإرسال لهذه المدرسة الآن، حاول بعد قليل.");
        }

        private async Task EnsureNotDuplicateAsync(int modelId, int partnerId, List<int> batchIds, CancellationToken ct)
        {
            var since = DateTime.UtcNow - DuplicateWindow;

            var duplicated = await _context.ExamAssignmentsToBatches.AsNoTracking()
                .AnyAsync(l =>
                    EF.Constant(batchIds).Contains(l.BatchId) &&
                    l.CreatedAt >= since &&
                    l.Exam.SourceProfessionalModelId == modelId &&
                    l.Exam.OwnerPartnerId == partnerId &&
                    l.Exam.Type == ExamType.LevelAssessment, ct);

            if (duplicated)
                throw new PlacementDispatchRuleException("⚠️ أُرسل هذا النموذج لهذه المدرسة قبل لحظات. تحقق من منطقتها قبل إعادة الإرسال.");
        }

        private async Task NotifyStudentsAsync(PlacementScope scope, CreatedExam created, CancellationToken ct)
        {
            if (created.Students.Count == 0)
                return;

            foreach (var studentId in created.Students)
                _cache.Remove($"PlacementExamDashboard_{studentId}");

            try
            {
                await _notificationService.SendToStudentsAsync(
                    created.Students,
                    $"📘 تم إرسال اختبار تحديد المستوى «{created.Title}» إليك. يمكنك الدخول وبدء الحل.",
                    NotificationCategory.Exam,
                    PlacementExamManagementService.StudentPlacementDashboardUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "APD placement notification failed for partner {PartnerId}, {Count} students", scope.PartnerId, created.Students.Count);
            }
        }
    }
}
