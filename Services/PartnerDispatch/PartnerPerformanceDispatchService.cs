using System.Data;
using EFCore.BulkExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.PerformanceIndicator;

namespace QdratNew.Services.PartnerDispatch
{
    public class PartnerPerformanceDispatchService : IPartnerPerformanceDispatchService
    {
        public const string StudentDashboardUrl = "/Students/PerformanceIndicatorExams/Dashboard";

        private const string ActiveEnrollmentStatus = "Active";
        private const int LockTimeoutMilliseconds = 15000;

        // ضغطتان متتاليتان تنشئان نسختين لا يراهما الأدمن في قوائمه
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(60);

        private readonly ApplicationDbContext _context;
        private readonly IPartnerDispatchLogService _dispatchLog;
        private readonly IAdvancedNotificationService _notificationService;
        private readonly ILogger<PartnerPerformanceDispatchService> _logger;

        public PartnerPerformanceDispatchService(
            ApplicationDbContext context,
            IPartnerDispatchLogService dispatchLog,
            IAdvancedNotificationService notificationService,
            ILogger<PartnerPerformanceDispatchService> logger)
        {
            _context = context;
            _dispatchLog = dispatchLog;
            _notificationService = notificationService;
            _logger = logger;
        }

        // خطأ عمل متوقع رسالته آمنة للعرض (يُرمى داخل ExecutionStrategy لإلغاء الـ Transaction)
        private sealed class DispatchRuleException : Exception
        {
            public DispatchRuleException(string message) : base(message) { }
        }

        private sealed record TemplateSection(int SectionId, int QuestionCount, double Score);

        private sealed record TemplateQuestion(Guid QuestionId, int? SectionId, int OrderNumber);

        private sealed class TemplateSource
        {
            public int Id { get; init; }
            public int CurriculumId { get; init; }
            public string Title { get; init; } = "";
            public int QuestionsPerIndicator { get; init; }
            public int DurationMinutes { get; init; }
            public double PassPercent { get; init; }
            public bool IsOnline { get; init; }
            public DateTime StartAt { get; init; }
            public DateTime EndAt { get; init; }
            public int TotalQuestions { get; init; }
            public List<TemplateSection> Sections { get; set; } = new();
            public List<TemplateQuestion> Questions { get; set; } = new();
        }

        public async Task<PartnerDispatchOutcome> DispatchAsync(int templateExamId, IReadOnlyList<PartnerDispatchTarget> targets, DispatchActor actor, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(targets);
            ArgumentNullException.ThrowIfNull(actor);

            var outcome = new PartnerDispatchOutcome
            {
                DispatchGroupId = Guid.NewGuid(),
                Kind = PartnerDispatchKind.PerformanceIndicator
            };

            if (targets.Count == 0)
            {
                outcome.ValidationError = "⚠️ اختر مدرسة شريكة ودفعة واحدة على الأقل.";
                return outcome;
            }

            var template = await LoadTemplateAsync(templateExamId, ct);
            if (template == null)
            {
                outcome.IsNotFound = true;
                return outcome;
            }

            if (template.Questions.Count == 0)
            {
                outcome.ValidationError = "⚠️ قالب الاختبار لا يحتوي على أسئلة.";
                return outcome;
            }

            foreach (var target in targets)
                outcome.Results.Add(await SendToPartnerAsync(target, template, actor, outcome.DispatchGroupId, ct));

            return outcome;
        }

        private async Task<TemplateSource?> LoadTemplateAsync(int templateExamId, CancellationToken ct)
        {
            var template = await _context.PerformanceIndicatorExams.AsNoTracking()
                .Where(e => e.Id == templateExamId && e.IsPartnerTemplate && e.OwnerPartnerId == null)
                .Select(e => new TemplateSource
                {
                    Id = e.Id,
                    CurriculumId = e.CurriculumId,
                    Title = e.Title,
                    QuestionsPerIndicator = e.QuestionsPerIndicator,
                    DurationMinutes = e.DurationMinutes,
                    PassPercent = e.PassPercent,
                    IsOnline = e.IsOnline,
                    StartAt = e.StartAt,
                    EndAt = e.EndAt,
                    TotalQuestions = e.TotalQuestions
                })
                .FirstOrDefaultAsync(ct);

            if (template == null)
                return null;

            template.Sections = await _context.PerformanceIndicatorExamSections.AsNoTracking()
                .Where(s => s.PerformanceIndicatorExamId == templateExamId)
                .OrderBy(s => s.Id)
                .Select(s => new TemplateSection(s.SectionId, s.QuestionCount, s.Score))
                .ToListAsync(ct);

            template.Questions = await _context.PerformanceIndicatorExamQuestions.AsNoTracking()
                .Where(q => q.PerformanceIndicatorExamId == templateExamId)
                .OrderBy(q => q.Id)
                .Select(q => new TemplateQuestion(q.QuestionId, q.SectionId, q.OrderNumber))
                .ToListAsync(ct);

            return template;
        }

        private async Task<PartnerDispatchPartnerResult> SendToPartnerAsync(
            PartnerDispatchTarget target,
            TemplateSource template,
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

            List<int> students;

            try
            {
                var recipients = await LoadRecipientsAsync(target, template.CurriculumId, ct);
                students = recipients.Values.SelectMany(s => s).Distinct().ToList();

                await CopyAsync(target, template, recipients, students, actor, groupId, ct);

                result.BatchesSent = recipients.Count;
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
                _logger.LogError(ex, "APD performance indicator dispatch failed for partner {PartnerId}, group {DispatchGroupId}", target.PartnerId, groupId);
                result.Message = "⚠️ تعذر الإرسال لهذه المدرسة، ولم يُحفظ لها أي شيء.";
                return result;
            }

            result.Succeeded = true;
            result.StudentsSent = students.Count;

            // بالتجاوز تصل النسخة للطلاب، لكن صفحات المتابعة تتبع عقد المدرسة
            result.Message = target.ContractOverridden
                ? "✅ تم الإرسال للطلاب. عقد المدرسة لا يشمل مؤشر الأداء، فلن تفتح صفحة المتابعة لديها حتى يشمله العقد."
                : "✅ تم إنشاء نسخة للمدرسة وإرسالها للطلاب.";

            // نجح فعليًا: تعثر الإشعار يُسجَّل ولا يقلب النتيجة إلى فشل
            await NotifyStudentsAsync(target.PartnerId, template.Title, students, ct);

            return result;
        }

        private async Task CopyAsync(
            PartnerDispatchTarget target,
            TemplateSource template,
            Dictionary<int, List<int>> recipients,
            List<int> students,
            DispatchActor actor,
            Guid groupId,
            CancellationToken ct)
        {
            var strategy = _context.Database.CreateExecutionStrategy();

            await strategy.ExecuteAsync(async () =>
            {
                // إعادة المحاولة عبر ExecutionStrategy تبدأ بلا صفوف معلّقة من المحاولة السابقة
                _context.ChangeTracker.Clear();

                await using var transaction = await _context.Database.BeginTransactionAsync(ct);

                await AcquireLockAsync(template.Id, target.PartnerId, ct);
                await EnsureNotDuplicateAsync(template.Id, target.PartnerId, ct);

                var exam = new PerformanceIndicatorExam
                {
                    Title = template.Title,
                    CurriculumId = template.CurriculumId,
                    QuestionsPerIndicator = template.QuestionsPerIndicator,
                    DurationMinutes = template.DurationMinutes,
                    PassPercent = template.PassPercent,
                    IsOnline = template.IsOnline,
                    StartAt = template.StartAt,
                    EndAt = template.EndAt,
                    TotalQuestions = template.TotalQuestions,
                    CreatedAt = DateTime.Now,
                    IsSent = true,
                    OwnerPartnerId = target.PartnerId,
                    ReferenceCode = await PerformanceExamReferenceCode.GenerateAsync(
                        code => _context.PerformanceIndicatorExams.AsNoTracking().AnyAsync(e => e.ReferenceCode == code, ct))
                };

                _context.PerformanceIndicatorExams.Add(exam);
                await _context.SaveChangesAsync(ct);

                await _context.BulkInsertAsync(
                    template.Sections
                        .Select(s => new PerformanceIndicatorExamSection
                        {
                            PerformanceIndicatorExamId = exam.Id,
                            SectionId = s.SectionId,
                            QuestionCount = s.QuestionCount,
                            Score = s.Score
                        })
                        .ToList(),
                    cancellationToken: ct);

                await _context.BulkInsertAsync(
                    template.Questions
                        .Select(q => new PerformanceIndicatorExamQuestion
                        {
                            PerformanceIndicatorExamId = exam.Id,
                            QuestionId = q.QuestionId,
                            SectionId = q.SectionId,
                            OrderNumber = q.OrderNumber
                        })
                        .ToList(),
                    cancellationToken: ct);

                _context.PerformanceIndicatorExamToBatch.AddRange(recipients.Keys.Select(batchId => new PerformanceIndicatorExamToBatch
                {
                    PerformanceIndicatorExamId = exam.Id,
                    BatchId = batchId
                }));
                await _context.SaveChangesAsync(ct);

                await _context.BulkInsertAsync(
                    students
                        .Select(studentId => new PerformanceIndicatorExamStudent
                        {
                            PerformanceIndicatorExamId = exam.Id,
                            StudentId = studentId
                        })
                        .ToList(),
                    cancellationToken: ct);

                var nowUtc = DateTime.UtcNow;
                await _dispatchLog.WriteAsync(
                    recipients
                        .Select(r => new PartnerDispatchLog
                        {
                            DispatchGroupId = groupId,
                            Kind = PartnerDispatchKind.PerformanceIndicator,
                            Title = exam.Title,
                            SourcePerformanceIndicatorExamId = template.Id,
                            PartnerId = target.PartnerId,
                            BatchId = r.Key,
                            TargetEntityId = exam.Id,
                            StudentsCount = r.Value.Count,
                            ContractOverridden = target.ContractOverridden,
                            OverrideReason = target.OverrideReason,
                            SentByUserId = actor.UserId,
                            SentAtUtc = nowUtc
                        })
                        .ToList(),
                    ct);

                await transaction.CommitAsync(ct);
            });
        }

        // BatchId → الطلاب النشطون في فروع المدرسة. الدفعة بلا طلاب لا تُربط (الواجهة تعطّلها أصلًا).
        private async Task<Dictionary<int, List<int>>> LoadRecipientsAsync(PartnerDispatchTarget target, int curriculumId, CancellationToken ct)
        {
            var partnerId = target.PartnerId;
            var ids = target.BatchIds.Distinct().ToList();

            var validIds = await _context.Batches.AsNoTracking()
                .Where(b =>
                    EF.Constant(ids).Contains(b.Id) &&
                    !b.IsDeleted &&
                    !b.IsArchived &&
                    b.Branch.PartnerId == partnerId &&
                    _context.CourseCurriculums.Any(cc => cc.CourseId == b.CourseId && cc.CurriculumId == curriculumId))
                .Select(b => b.Id)
                .ToListAsync(ct);

            if (ids.Count == 0 || validIds.Count != ids.Count)
                throw new DispatchRuleException("⚠️ دفعة أو أكثر لا تخص هذه المدرسة أو لم تعد متاحة لهذا الإرسال.");

            var rows = await _context.StudentBatchEnrollments.AsNoTracking()
                .Where(e =>
                    EF.Constant(ids).Contains(e.BatchId) &&
                    e.Status == ActiveEnrollmentStatus &&
                    e.Student.IsActiveForLearning &&
                    e.Student.Branch.PartnerId == partnerId)
                .Select(e => new { e.BatchId, e.StudentID })
                .Distinct()
                .ToListAsync(ct);

            if (rows.Count == 0)
                throw new DispatchRuleException("⚠️ لا يوجد طلاب نشطون في الدفعات المختارة.");

            return rows
                .GroupBy(r => r.BatchId)
                .ToDictionary(g => g.Key, g => g.Select(r => r.StudentID).ToList());
        }

        // يتحرر مع الـ Transaction، فالطلب المتزامن الثاني ينتظر ثم يجد ما كُتب ويتوقف
        private async Task AcquireLockAsync(int templateId, int partnerId, CancellationToken ct)
        {
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var resource = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = $"APD_KPI_{templateId}_{partnerId}" };
            var timeout = new SqlParameter("@timeout", SqlDbType.Int) { Value = LockTimeoutMilliseconds };

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout",
                new object[] { result, resource, timeout },
                ct);

            if (result.Value is not int code || code < 0)
                throw new DispatchRuleException("⚠️ يجري الإرسال لهذه المدرسة الآن، حاول بعد قليل.");
        }

        private async Task EnsureNotDuplicateAsync(int templateId, int partnerId, CancellationToken ct)
        {
            var since = DateTime.UtcNow - DuplicateWindow;

            var duplicated = await _context.PartnerDispatchLogs.AsNoTracking()
                .AnyAsync(l =>
                    l.Kind == PartnerDispatchKind.PerformanceIndicator &&
                    l.SourcePerformanceIndicatorExamId == templateId &&
                    l.PartnerId == partnerId &&
                    l.SentAtUtc >= since, ct);

            if (duplicated)
                throw new DispatchRuleException("⚠️ أُرسل هذا الاختبار لهذه المدرسة قبل لحظات. تحقق من منطقتها قبل إعادة الإرسال.");
        }

        private async Task NotifyStudentsAsync(int partnerId, string title, List<int> students, CancellationToken ct)
        {
            try
            {
                await _notificationService.SendToStudentsAsync(
                    students,
                    $"📊 تم إرسال اختبار مؤشر الأداء «{title}» إليك. يمكنك الدخول وبدء الحل.",
                    NotificationCategory.Exam,
                    StudentDashboardUrl);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "APD performance indicator notification failed for partner {PartnerId}, {Count} students", partnerId, students.Count);
            }
        }
    }
}
