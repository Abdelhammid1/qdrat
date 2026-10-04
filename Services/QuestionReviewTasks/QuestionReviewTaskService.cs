using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Instructors;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>
    /// QRT-S1: هيكل الخدمة + مولّد الكود + إعادة حساب العدادات (D10).
    /// QRT-S2: إنشاء المهمة (CreateTaskAsync) + المدربون المؤهلون.
    /// QRT-S6: إدارة دورة الحياة في الملف الجزئي QuestionReviewTaskService.Lifecycle.cs.
    /// ما تبقى غير منفّذ يُرجع فشلًا صريحًا.
    /// </summary>
    public sealed partial class QuestionReviewTaskService : IQuestionReviewTaskService
    {
        private const int MaxQuestionsPerTask = 500; // D8
        private const string ActiveLockIndexName = "UX_QuestionReviewTaskItems_ActiveLock";
        private const string CodeIndexName = "IX_QuestionReviewTasks_Code";
        private const string AuditActionAssigned = "إسناد لمهمة مراجعة";
        private const string AuditActionApprovedInTask = "اعتماد ضمن مهمة مراجعة";
        private const string AuditActionReturnedFromTask = "إرجاع من مهمة مراجعة";
        private const string AuditActionSyncedFromAdmin = "مزامنة مع مهمة مراجعة";
        private const int ReturnNoteMin = 10;
        private const int ReturnNoteMax = 500;

        // D9: التخزين UTC، والعرض/الإدخال بتوقيت Arab Standard Time (مع بديل IANA لبيئات Linux).
        private static readonly Lazy<TimeZoneInfo> DisplayZone = new(() =>
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Riyadh"); }
        });

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly IInstructorScopeService _scope;
        private readonly IAdvancedNotificationService _notifications;
        private readonly ILogger<QuestionReviewTaskService> _logger;

        public QuestionReviewTaskService(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            IInstructorScopeService scope,
            IAdvancedNotificationService notifications,
            ILogger<QuestionReviewTaskService> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _scope = scope;
            _notifications = notifications;
            _logger = logger;
        }

        // سؤال مرشّح للإسناد (قراءة فقط)
        private sealed record CandidateQuestion(Guid Id, int CurriculumId, int? PartnerId, string? ReferenceNumber);

        private sealed record QuestionSelection(List<CandidateQuestion> Eligible, int Excluded, string? Error);

        private sealed record CreatedTaskInfo(int TaskId, string Code, int ItemsCount);

        // ===== الأدمن: إنشاء مهمة (QRT-S2.1 + S2.4) =====
        public async Task<OperationResult> CreateTaskAsync(CreateQuestionReviewTaskInput input, ReviewActor actor, CancellationToken ct = default)
        {
            if (input is null)
                return OperationResult.Fail("⚠️ بيانات المهمة غير مكتملة.");

            var title = (input.Title ?? string.Empty).Trim();
            if (title.Length < 3 || title.Length > 200)
                return OperationResult.Fail("⚠️ عنوان المهمة يجب أن يكون بين 3 و200 حرف.");

            var adminNote = string.IsNullOrWhiteSpace(input.AdminNote) ? null : input.AdminNote.Trim();
            if (adminNote is { Length: > 1000 })
                return OperationResult.Fail("⚠️ ملاحظة الإدارة لا تتجاوز 1000 حرف.");

            var nowUtc = _time.GetUtcNow().UtcDateTime;

            DateTime? dueUtc = null;
            if (input.DueAtLocal.HasValue)
            {
                dueUtc = ToUtc(input.DueAtLocal.Value);
                if (dueUtc.Value <= nowUtc)
                    return OperationResult.Fail("⚠️ موعد التسليم يجب أن يكون في المستقبل.");
            }

            await using var readDb = await _dbFactory.CreateDbContextAsync(ct);

            // 2) المدرب
            var instructor = await readDb.Instructors.AsNoTracking()
                .Where(i => i.Id == input.InstructorId)
                .Select(i => new { i.Id, i.FullName, i.IsActive, i.UserId, i.PartnerId })
                .FirstOrDefaultAsync(ct);

            if (instructor is null || !instructor.IsActive)
                return OperationResult.Fail("⚠️ المدرب غير موجود أو غير نشط.");
            if (string.IsNullOrWhiteSpace(instructor.UserId))
                return OperationResult.Fail("⚠️ المدرب غير مرتبط بحساب دخول.");

            // 3) + 4) تحديد الأسئلة وأهليتها (استعلام واحد)
            var selection = await ResolveQuestionsAsync(
                readDb, input.SelectedQuestionIds, input.CurriculumId, input.SectionId, input.LessonId, input.TakeCount, ct);
            if (selection.Error is not null)
                return OperationResult.Fail(selection.Error);

            var eligible = selection.Eligible;
            if (eligible.Count == 0)
                return OperationResult.Fail("⚠️ لا توجد أسئلة مؤهلة للإسناد (يجب أن تكون مكتملة، غير معتمدة، غير مرفوضة، لها إجابة صحيحة، وغير محجوزة).");

            // 6) D7: عزل الشركاء
            var partnerMismatch = eligible.Count(q => q.PartnerId != instructor.PartnerId);
            if (partnerMismatch > 0)
                return OperationResult.Fail($"⚠️ {partnerMismatch} سؤال لا يتبع نفس جهة المدرب (الشريك)، ولا يمكن إسناده إليه.");

            // 5) صلاحية المنهج
            var curriculumIds = eligible.Select(q => q.CurriculumId).Distinct().ToList();
            var allowed = (await _scope.GetDirectCurriculumIdsAsync(instructor.Id)).ToHashSet();
            var deniedIds = curriculumIds.Where(id => !allowed.Contains(id)).ToList();
            if (deniedIds.Count > 0)
            {
                var deniedNames = await readDb.Curriculums.AsNoTracking()
                    .Where(c => deniedIds.Contains(c.Id))
                    .Select(c => c.Title)
                    .ToListAsync(ct);
                return OperationResult.Fail($"⚠️ المدرب لا يملك صلاحية على المناهج التالية: {string.Join("، ", deniedNames)}.");
            }

            // 7) + 8) Transaction واحدة: مهمة + عناصر + Audit + عدادات (إعادة المحاولة مرة واحدة عند تكرار الكود)
            CreatedTaskInfo? created = null;
            for (var attempt = 0; attempt < 2 && created is null; attempt++)
            {
                try
                {
                    created = await InsertTaskAsync(
                        title, adminNote, instructor.Id, instructor.FullName, curriculumIds, eligible,
                        input.Priority, dueUtc, actor, nowUtc, ct);
                }
                catch (DbUpdateException ex) when (IsConstraintViolation(ex, CodeIndexName) && attempt == 0)
                {
                    _logger.LogWarning(ex, "QRT: task code collision, retrying once");
                }
                catch (DbUpdateException ex) when (IsConstraintViolation(ex, ActiveLockIndexName))
                {
                    _logger.LogWarning(ex, "QRT: active-lock violation while creating a task");
                    return OperationResult.Fail("⚠️ بعض الأسئلة أُسندت لمهمة أخرى للتو، أعد تحميل الصفحة وحاول مجددًا.");
                }
            }

            if (created is null)
                return OperationResult.Fail("⚠️ تعذّر إنشاء المهمة، حاول مرة أخرى.");

            // 9) الإشعار خارج الـ Transaction — فشله لا يُفشل العملية
            await NotifyInstructorAsync(instructor.UserId!, created, dueUtc, ct);

            var message = $"✅ تم إنشاء المهمة {created.Code} وإسناد {created.ItemsCount} سؤال إلى {instructor.FullName}.";
            if (selection.Excluded > 0)
                message += $" (استُبعد {selection.Excluded} سؤال غير مؤهل: معتمد أو مرفوض أو غير مكتمل أو محجوز أو غير موجود.)";

            return OperationResult.Ok(message, new { taskId = created.TaskId, code = created.Code, count = created.ItemsCount, excluded = selection.Excluded });
        }

        private async Task<CreatedTaskInfo> InsertTaskAsync(
            string title,
            string? adminNote,
            int instructorId,
            string instructorName,
            IReadOnlyList<int> curriculumIds,
            IReadOnlyList<CandidateQuestion> eligible,
            QuestionReviewTaskPriority priority,
            DateTime? dueUtc,
            ReviewActor actor,
            DateTime nowUtc,
            CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var strategy = db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear(); // أمان عند إعادة تشغيل الـ Strategy

                // مزوّد InMemory (الاختبارات) لا يدعم Transactions
                await using var tx = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(ct)
                    : null;

                var task = new QuestionReviewTask
                {
                    Code = await NextCodeAsync(db, ct),
                    Title = title,
                    AdminNote = adminNote,
                    InstructorId = instructorId,
                    CurriculumId = curriculumIds.Count == 1 ? curriculumIds[0] : null,
                    Priority = priority,
                    DueAtUtc = dueUtc,
                    CreatedByUserId = actor.UserId,
                    CreatedByName = actor.Name,
                    CreatedAtUtc = nowUtc,
                    Status = QuestionReviewTaskStatus.Assigned
                };

                // قائمة في الذاكرة من استعلام واحد — بلا Query أو SaveChanges داخل Loop
                var order = 0;
                foreach (var q in eligible)
                {
                    task.Items.Add(new QuestionReviewTaskItem
                    {
                        QuestionId = q.Id,
                        SortOrder = ++order,
                        ReferenceNumberSnapshot = q.ReferenceNumber,
                        IsLockActive = true
                    });
                }

                db.QuestionReviewTasks.Add(task);

                var auditAt = DateTime.Now; // QuestionAuditLog.PerformedAt يبقى بالتوقيت المحلي كما في بقية البنك
                var summary = $"المهمة {task.Code} — المدرب: {instructorName}";
                db.QuestionAuditLogs.AddRange(eligible.Select(q => new QuestionAuditLog
                {
                    QuestionId = q.Id,
                    Action = AuditActionAssigned,
                    PerformedByUserId = actor.UserId,
                    PerformedByName = actor.Name,
                    PerformedByRole = actor.Role,
                    PerformedAt = auditAt,
                    ChangedFieldsSummary = summary
                }));

                await db.SaveChangesAsync(ct);
                await RecalculateCountersAsync(db, task.Id, ct);

                if (tx is not null)
                    await tx.CommitAsync(ct);

                return new CreatedTaskInfo(task.Id, task.Code, task.TotalItems);
            });
        }

        private async Task NotifyInstructorAsync(string instructorUserId, CreatedTaskInfo created, DateTime? dueUtc, CancellationToken ct)
        {
            try
            {
                var dueText = dueUtc.HasValue
                    ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dueUtc.Value, DateTimeKind.Utc), DisplayZone.Value)
                        .ToString("yyyy/MM/dd HH:mm")
                    : null;

                await _notifications.SendToUserAsync(
                    instructorUserId,
                    $"📋 أُسندت إليك مهمة مراجعة جديدة ({created.Code}) تضم {created.ItemsCount} سؤالًا"
                        + (dueText is null ? string.Empty : $" — التسليم {dueText}"),
                    NotificationCategory.Important,
                    $"/Instructors/QuestionReviewTasks/Review/{created.TaskId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QRT: failed to notify instructor for task {Code}", created.Code);
            }
        }

        // ===== الأدمن: المدربون المؤهلون (QRT-S2.3) =====
        public async Task<OperationResult> GetEligibleInstructorsAsync(EligibleInstructorsInput input, CancellationToken ct = default)
        {
            if (input is null)
                return OperationResult.Fail("⚠️ بيانات غير مكتملة.");

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var selection = await ResolveQuestionsAsync(
                db, input.SelectedQuestionIds, input.CurriculumId, input.SectionId, input.LessonId, input.TakeCount, ct);
            if (selection.Error is not null)
                return OperationResult.Fail(selection.Error);
            if (selection.Eligible.Count == 0)
                return OperationResult.Fail("⚠️ لا توجد أسئلة مؤهلة للإسناد ضمن هذا التحديد.");

            var curriculumIds = selection.Eligible.Select(q => q.CurriculumId).Distinct().ToList();
            var partnerIds = selection.Eligible.Select(q => q.PartnerId).Distinct().ToList();
            if (partnerIds.Count > 1)
                return OperationResult.Fail("⚠️ الأسئلة المحددة تتبع جهات (شركاء) مختلفة؛ حدّد أسئلة جهة واحدة.");

            var result = await FindEligibleInstructorsAsync(db, curriculumIds, partnerIds[0], ct);

            var message = result.Count == 0
                ? "لا يوجد مدرب مؤهل: يجب أن يملك المدرب كل المناهج المعنية ونفس الجهة."
                : string.Empty;

            return OperationResult.Ok(message, new
            {
                questionCount = selection.Eligible.Count,
                excluded = selection.Excluded,
                instructors = result
            });
        }

        // مدربون نشطون لهم حساب دخول ومن نفس الشريك (D7) ويملكون كل المناهج المعنية، مرتبون حسب العبء ثم الاسم
        private async Task<List<EligibleInstructorDto>> FindEligibleInstructorsAsync(
            ApplicationDbContext db, IReadOnlyCollection<int> curriculumIds, int? partnerId, CancellationToken ct)
        {
            var candidates = await db.Instructors.AsNoTracking()
                .Where(i => i.IsActive && i.UserId != null && i.PartnerId == partnerId)
                .Select(i => new { i.Id, i.FullName })
                .ToListAsync(ct);

            if (candidates.Count == 0)
                return new List<EligibleInstructorDto>();

            // نفس شرط GetDirectCurriculumIdsAsync عبر مصدر واحد مشترك
            var pairs = await db.InstructorCurriculumBatches.AsNoTracking()
                .Where(InstructorBatchScope.IsDirectActive(DateTime.Today))
                .Where(x => curriculumIds.Contains(x.CurriculumId))
                .Select(x => new { x.InstructorId, x.CurriculumId })
                .Distinct()
                .ToListAsync(ct);

            var coverage = pairs
                .GroupBy(p => p.InstructorId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.CurriculumId).Distinct().Count());

            // عبء العمل: الأسئلة المحجوزة بانتظار المراجعة لكل مدرب (GROUP BY واحد)
            var loadRows = await db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.IsLockActive)
                .GroupBy(i => i.Task!.InstructorId)
                .Select(g => new { InstructorId = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            var load = loadRows.ToDictionary(r => r.InstructorId, r => r.Count);

            return candidates
                .Where(c => coverage.TryGetValue(c.Id, out var covered) && covered == curriculumIds.Count)
                .Select(c => new EligibleInstructorDto(c.Id, c.FullName, load.GetValueOrDefault(c.Id)))
                .OrderBy(c => c.ActiveLockedItems)
                .ThenBy(c => c.FullName)
                .ToList();
        }

        // ===== تحديد الأسئلة + الأهلية (استعلام واحد، بلا Loop) =====
        private async Task<QuestionSelection> ResolveQuestionsAsync(
            ApplicationDbContext db,
            IReadOnlyCollection<Guid>? selectedIds,
            int? curriculumId,
            int? sectionId,
            int? lessonId,
            int? takeCount,
            CancellationToken ct)
        {
            if (selectedIds is { Count: > 0 })
            {
                // وضع 1: تحديد يدوي
                var ids = selectedIds.Where(id => id != Guid.Empty).Distinct().ToList();
                if (ids.Count == 0)
                    return new QuestionSelection(new List<CandidateQuestion>(), 0, "⚠️ لم يتم تحديد أسئلة صالحة.");
                if (ids.Count > MaxQuestionsPerTask)
                    return new QuestionSelection(new List<CandidateQuestion>(), 0, $"⚠️ الحد الأقصى {MaxQuestionsPerTask} سؤال للمهمة الواحدة.");

                var rows = await db.Questions.AsNoTracking()
                    .Where(q => ids.Contains(q.Id))
                    .Select(q => new
                    {
                        q.Id,
                        q.CurriculumId,
                        q.PartnerId,
                        q.ReferenceNumber,
                        Valid = q.IsComplete && !q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer),
                        Locked = db.QuestionReviewTaskItems.Any(i => i.QuestionId == q.Id && i.IsLockActive)
                    })
                    .ToListAsync(ct);

                var eligible = rows
                    .Where(r => r.Valid && !r.Locked)
                    .Select(r => new CandidateQuestion(r.Id, r.CurriculumId, r.PartnerId, r.ReferenceNumber))
                    .ToList();

                return new QuestionSelection(eligible, ids.Count - eligible.Count, null);
            }

            // وضع 2: أول N حسب الفلتر (الأقدم أولًا) مع استبعاد المحجوز
            if (!takeCount.HasValue || takeCount.Value < 1)
                return new QuestionSelection(new List<CandidateQuestion>(), 0, "⚠️ حدّد أسئلة يدويًا أو أدخل عدد الأسئلة المطلوب (1 – 500).");
            if (takeCount.Value > MaxQuestionsPerTask)
                return new QuestionSelection(new List<CandidateQuestion>(), 0, $"⚠️ الحد الأقصى {MaxQuestionsPerTask} سؤال للمهمة الواحدة.");

            var query = db.Questions.AsNoTracking()
                .Where(q => q.IsComplete && !q.IsReviewed && !q.IsRejected && !string.IsNullOrWhiteSpace(q.CorrectAnswer))
                .Where(q => !db.QuestionReviewTaskItems.Any(i => i.QuestionId == q.Id && i.IsLockActive));

            if (curriculumId.HasValue) query = query.Where(q => q.CurriculumId == curriculumId.Value);
            if (sectionId.HasValue) query = query.Where(q => q.SectionId == sectionId.Value);
            if (lessonId.HasValue) query = query.Where(q => q.LessonId == lessonId.Value);

            var picked = await query
                .OrderBy(q => q.CreatedAt)
                .ThenBy(q => q.Id)
                .Take(takeCount.Value)
                .Select(q => new CandidateQuestion(q.Id, q.CurriculumId, q.PartnerId, q.ReferenceNumber))
                .ToListAsync(ct);

            return new QuestionSelection(picked, 0, null);
        }

        private static bool IsConstraintViolation(DbUpdateException ex, string indexName)
        {
            for (Exception? e = ex; e is not null; e = e.InnerException)
            {
                if (e.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static DateTime ToUtc(DateTime local) => QuestionReviewTaskMetrics.LocalToUtc(local);

        // ===== المدرب (Sprint 3) =====
        // نتيجة إجراء المدرب داخل الـ Transaction (الإشعار يُرسل بعد الإنهاء)
        private sealed record ReviewOutcome(
            OperationResult Result, string? NotifyUserId = null, string? TaskCode = null, int ApprovalPercent = 0, bool Completed = false);

        // QRT-S3.2 + S3.4: اعتماد جماعي/فردي لعناصر مهمة المدرب
        public async Task<OperationResult> ApproveItemsAsync(int instructorId, int taskId, IReadOnlyCollection<long> itemIds, ReviewActor actor, CancellationToken ct = default)
        {
            if (itemIds is null || itemIds.Count == 0)
                return OperationResult.Fail("⚠️ حدّد سؤالًا واحدًا على الأقل.");
            if (itemIds.Count > MaxQuestionsPerTask)
                return OperationResult.Fail($"⚠️ الحد الأقصى {MaxQuestionsPerTask} سؤال في العملية الواحدة.");

            var wanted = itemIds.ToHashSet();
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            ReviewOutcome outcome;
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                var strategy = db.Database.CreateExecutionStrategy();

                outcome = await strategy.ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;

                    // D6: الملكية والحالة في شرط الاستعلام نفسه
                    var task = await db.QuestionReviewTasks.FirstOrDefaultAsync(t =>
                        t.Id == taskId && t.InstructorId == instructorId &&
                        (t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress), ct);
                    if (task is null)
                        return new ReviewOutcome(OperationResult.Fail("⚠️ المهمة غير متاحة."));

                    // عناصر المهمة المعلّقة (≤500) بسؤالها وخياراته في استعلام واحد، والتصفية بالمعرّفات في الذاكرة
                    var pending = await db.QuestionReviewTaskItems
                        .Where(i => i.TaskId == taskId && i.Status == QuestionReviewTaskItemStatus.Pending)
                        .Include(i => i.Question!).ThenInclude(q => q.Options)
                        .ToListAsync(ct);
                    var targets = pending.Where(i => wanted.Contains(i.Id)).ToList();

                    var notPending = wanted.Count - targets.Count;
                    var skippedInvalid = 0;
                    var approvedNow = new List<QuestionReviewTaskItem>();
                    var alreadyApproved = new List<QuestionReviewTaskItem>();

                    foreach (var item in targets)
                    {
                        var q = item.Question!;
                        if (q.IsReviewed)
                        {
                            // QRT-S4.3: اعتُمد السؤال من خارج المهمة (الأدمن) — نُحدّث العنصر بدل تركه معلّقًا
                            MarkApprovedByAdmin(item, q.ReviewedByUserId, null, nowUtc, "اعتُمد السؤال من الإدارة قبل مراجعتك");
                            alreadyApproved.Add(item);
                            continue;
                        }
                        if (!IsApprovable(q)) { skippedInvalid++; continue; }

                        q.IsReviewed = true;
                        q.ReviewedByUserId = actor.UserId;
                        q.ReviewedAt = DateTime.Now; // يبقى بالتوقيت المحلي كما في بقية البنك (D9)

                        item.Status = QuestionReviewTaskItemStatus.Approved;
                        item.IsLockActive = false;
                        item.ActionAtUtc = nowUtc;
                        item.ActionByUserId = actor.UserId;
                        item.ActionByName = actor.Name;
                        approvedNow.Add(item);
                    }

                    var skippedTotal = notPending + skippedInvalid + alreadyApproved.Count;
                    if (approvedNow.Count == 0 && alreadyApproved.Count == 0)
                        return new ReviewOutcome(OperationResult.Fail(BuildSkipMessage(0, notPending, skippedInvalid, 0)));

                    var auditAt = DateTime.Now;
                    db.QuestionAuditLogs.AddRange(approvedNow.Select(i => NewAudit(
                        i.QuestionId, AuditActionApprovedInTask, $"المهمة {task.Code}", actor, auditAt)));
                    db.QuestionAuditLogs.AddRange(alreadyApproved.Select(i => NewAudit(
                        i.QuestionId, AuditActionSyncedFromAdmin, $"المهمة {task.Code} — السؤال معتمد مسبقًا من الإدارة", actor, auditAt)));

                    var completed = await ApplyTransitionAsync(db, task, nowUtc, ct);
                    if (tx is not null) await tx.CommitAsync(ct);

                    var message = BuildSkipMessage(approvedNow.Count, notPending, skippedInvalid, alreadyApproved.Count);
                    return new ReviewOutcome(
                        OperationResult.Ok(message, ProgressData(task, approvedNow.Count, skippedTotal)),
                        task.CreatedByUserId, task.Code,
                        QuestionReviewTaskMetrics.ApprovalPercent(task.ApprovedItems, QuestionReviewTaskMetrics.Effective(task.TotalItems, task.RemovedItems)),
                        completed);
                });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "QRT: concurrency conflict while approving items of task {TaskId}", taskId);
                return OperationResult.Fail("⚠️ تم تعديل هذه العناصر من مستخدم آخر، أعد تحميل الصفحة وحاول مجددًا.");
            }

            await NotifyCompletionAsync(outcome, actor, ct);
            return outcome.Result;
        }

        // QRT-S3.3 + S3.4: إرجاع سؤال للإدارة بملاحظة إلزامية (D5)
        public async Task<OperationResult> ReturnItemAsync(int instructorId, long itemId, string note, ReviewActor actor, CancellationToken ct = default)
        {
            var cleanNote = (note ?? string.Empty).Trim();
            if (cleanNote.Length < ReturnNoteMin || cleanNote.Length > ReturnNoteMax)
                return OperationResult.Fail($"⚠️ ملاحظة الإرجاع إلزامية ({ReturnNoteMin} – {ReturnNoteMax} حرف).");

            var nowUtc = _time.GetUtcNow().UtcDateTime;

            ReviewOutcome outcome;
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                var strategy = db.Database.CreateExecutionStrategy();

                outcome = await strategy.ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;

                    var item = await db.QuestionReviewTaskItems
                        .Include(i => i.Task)
                        .FirstOrDefaultAsync(i => i.Id == itemId && i.Task!.InstructorId == instructorId, ct);
                    if (item is null)
                        return new ReviewOutcome(OperationResult.Fail("⚠️ العنصر غير متاح."));

                    var task = item.Task!;
                    if (!QuestionReviewTaskMetrics.IsActive(task.Status))
                        return new ReviewOutcome(OperationResult.Fail("⚠️ المهمة لم تعد متاحة للمراجعة."));
                    if (item.Status != QuestionReviewTaskItemStatus.Pending)
                        return new ReviewOutcome(OperationResult.Fail("ℹ️ تم التصرف في هذا السؤال مسبقًا."));

                    item.Status = QuestionReviewTaskItemStatus.Returned;
                    item.IsLockActive = false;
                    item.ReturnNote = cleanNote;
                    item.ActionAtUtc = nowUtc;
                    item.ActionByUserId = actor.UserId;
                    item.ActionByName = actor.Name;

                    var summary = $"المهمة {task.Code} — الملاحظة: {cleanNote}";
                    db.QuestionAuditLogs.Add(NewAudit(
                        item.QuestionId, AuditActionReturnedFromTask, summary.Length > 1000 ? summary[..1000] : summary, actor, DateTime.Now));

                    var completed = await ApplyTransitionAsync(db, task, nowUtc, ct);
                    if (tx is not null) await tx.CommitAsync(ct);

                    return new ReviewOutcome(
                        OperationResult.Ok("✅ أُرجع السؤال للإدارة مع ملاحظتك.", ProgressData(task, 0, 0)),
                        task.CreatedByUserId, task.Code,
                        QuestionReviewTaskMetrics.ApprovalPercent(task.ApprovedItems, QuestionReviewTaskMetrics.Effective(task.TotalItems, task.RemovedItems)),
                        completed);
                });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "QRT: concurrency conflict while returning item {ItemId}", itemId);
                return OperationResult.Fail("⚠️ تم تعديل هذا العنصر من مستخدم آخر، أعد تحميل الصفحة وحاول مجددًا.");
            }

            await NotifyCompletionAsync(outcome, actor, ct);
            return outcome.Result;
        }

        // QRT-S4.1: يُستدعى بعد أن يحفظ مسار التعديل السؤال ويعتمده (IsReviewed) بيد المدرب نفسه
        public async Task<OperationResult> MarkEditedAndApprovedAsync(int instructorId, long itemId, ReviewActor actor, CancellationToken ct = default)
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            ReviewOutcome outcome;
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                var strategy = db.Database.CreateExecutionStrategy();

                outcome = await strategy.ExecuteAsync(async () =>
                {
                    db.ChangeTracker.Clear();
                    await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;

                    // D6: الملكية في شرط الاستعلام نفسه
                    var item = await db.QuestionReviewTaskItems
                        .Include(i => i.Task)
                        .Include(i => i.Question)
                        .FirstOrDefaultAsync(i => i.Id == itemId && i.Task!.InstructorId == instructorId, ct);
                    if (item is null)
                        return new ReviewOutcome(OperationResult.Fail("⚠️ العنصر غير متاح."));

                    var task = item.Task!;
                    if (!QuestionReviewTaskMetrics.IsActive(task.Status))
                        return new ReviewOutcome(OperationResult.Fail("⚠️ المهمة لم تعد متاحة للمراجعة."));
                    if (item.Status != QuestionReviewTaskItemStatus.Pending)
                        return new ReviewOutcome(OperationResult.Fail("ℹ️ تم التصرف في هذا السؤال مسبقًا."));

                    var q = item.Question!;
                    if (!q.IsReviewed || q.ReviewedByUserId != actor.UserId)
                        return new ReviewOutcome(OperationResult.Fail("⚠️ لم يُعتمد السؤال بعد، لا يمكن تسجيله «عُدِّل واعتُمد»."));

                    item.Status = QuestionReviewTaskItemStatus.EditedAndApproved;
                    item.IsLockActive = false;
                    item.ActionAtUtc = nowUtc;
                    item.ActionByUserId = actor.UserId;
                    item.ActionByName = actor.Name;

                    db.QuestionAuditLogs.Add(NewAudit(
                        item.QuestionId, AuditActionApprovedInTask, $"المهمة {task.Code} — بعد التعديل", actor, DateTime.Now));

                    var completed = await ApplyTransitionAsync(db, task, nowUtc, ct);
                    if (tx is not null) await tx.CommitAsync(ct);

                    return new ReviewOutcome(
                        OperationResult.Ok("✅ تم حفظ التعديل واعتماد السؤال ضمن المهمة.", ProgressData(task, 1, 0)),
                        task.CreatedByUserId, task.Code,
                        QuestionReviewTaskMetrics.ApprovalPercent(task.ApprovedItems, QuestionReviewTaskMetrics.Effective(task.TotalItems, task.RemovedItems)),
                        completed);
                });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "QRT: concurrency conflict while marking item {ItemId} edited-and-approved", itemId);
                return OperationResult.Fail("⚠️ تم تعديل هذا العنصر من مستخدم آخر، أعد تحميل الصفحة وحاول مجددًا.");
            }

            await NotifyCompletionAsync(outcome, actor, ct);
            return outcome.Result;
        }

        // ===== QRT-S4.3: مزامنة قرارات الأدمن مع عناصر المهام =====
        public async Task<int> SyncAdminDecisionAsync(AdminQuestionDecision decision, ReviewActor actor, CancellationToken ct = default)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                var nowUtc = _time.GetUtcNow().UtcDateTime;

                // الاستعلام بحالة السؤال نفسها (بلا قائمة معرّفات) → لا Contains ولا OPENJSON، ويُصلح ما فات سابقًا
                var open = db.QuestionReviewTaskItems
                    .Where(i => i.Status == QuestionReviewTaskItemStatus.Pending || i.Status == QuestionReviewTaskItemStatus.Returned);

                List<QuestionReviewTaskItem> items;
                switch (decision)
                {
                    case AdminQuestionDecision.Approved:
                        items = await open.Where(i => i.Question!.IsReviewed).ToListAsync(ct);
                        break;

                    case AdminQuestionDecision.Rejected:
                        items = await open.Where(i => i.Question!.IsRejected && !i.Question.IsReviewed).ToListAsync(ct);
                        break;

                    case AdminQuestionDecision.Unapproved:
                        items = await db.QuestionReviewTaskItems
                            .Where(i => (i.Status == QuestionReviewTaskItemStatus.Approved
                                         || i.Status == QuestionReviewTaskItemStatus.EditedAndApproved
                                         || i.Status == QuestionReviewTaskItemStatus.ApprovedByAdmin)
                                        && !i.Question!.IsReviewed
                                        && i.Task!.Status != QuestionReviewTaskStatus.Closed
                                        && i.Task.Status != QuestionReviewTaskStatus.Cancelled)
                            .ToListAsync(ct);
                        break;

                    default:
                        return 0;
                }

                if (items.Count == 0)
                    return 0;

                var taskIds = items.Select(i => i.TaskId).Distinct().ToList();
                var tasks = await db.QuestionReviewTasks.Where(t => taskIds.Contains(t.Id)).ToListAsync(ct);
                var codeByTask = tasks.ToDictionary(t => t.Id, t => t.Code);

                var auditAt = DateTime.Now;
                var audits = new List<QuestionAuditLog>(items.Count);
                foreach (var item in items)
                {
                    var code = codeByTask.GetValueOrDefault(item.TaskId, "—");
                    string summary;
                    switch (decision)
                    {
                        case AdminQuestionDecision.Approved:
                            MarkApprovedByAdmin(item, actor.UserId, actor.Name, nowUtc, "اعتمدها الأدمن مباشرة");
                            summary = $"المهمة {code} — اعتمدها الأدمن مباشرة";
                            break;

                        case AdminQuestionDecision.Rejected:
                            // معلّق ← أُزيل من المهمة، ومرتجع ← عولج بالرفض النهائي
                            item.Status = item.Status == QuestionReviewTaskItemStatus.Pending
                                ? QuestionReviewTaskItemStatus.Removed
                                : QuestionReviewTaskItemStatus.ReturnResolved;
                            item.IsLockActive = false;
                            item.ActionAtUtc = nowUtc;
                            item.ActionByUserId = actor.UserId;
                            item.ActionByName = actor.Name;
                            item.AdminResolutionNote = "رفضها الأدمن";
                            summary = $"المهمة {code} — رفضها الأدمن";
                            break;

                        default: // Unapproved ← يخرج من احتساب المهمة ويعود السؤال للمسار العام
                            item.Status = QuestionReviewTaskItemStatus.Removed;
                            item.IsLockActive = false;
                            item.ActionAtUtc = nowUtc;
                            item.ActionByUserId = actor.UserId;
                            item.ActionByName = actor.Name;
                            item.AdminResolutionNote = "ألغى الأدمن اعتماده";
                            summary = $"المهمة {code} — ألغى الأدمن اعتماد السؤال فأُزيل من احتساب المهمة";
                            break;
                    }

                    audits.Add(NewAudit(item.QuestionId, AuditActionSyncedFromAdmin, summary, actor, auditAt));
                }

                db.QuestionAuditLogs.AddRange(audits);
                await db.SaveChangesAsync(ct);

                // العدادات: GROUP BY واحد لكل المهام المتأثرة (بلا استعلام داخل Loop)
                var groups = await db.QuestionReviewTaskItems.AsNoTracking()
                    .Where(i => taskIds.Contains(i.TaskId))
                    .GroupBy(i => new { i.TaskId, i.Status })
                    .Select(g => new { g.Key.TaskId, g.Key.Status, Count = g.Count() })
                    .ToListAsync(ct);

                foreach (var task in tasks)
                {
                    ApplyCounters(task, groups.Where(g => g.TaskId == task.Id).Select(g => (g.Status, g.Count)).ToList());

                    if (task.PendingItems == 0 && QuestionReviewTaskMetrics.IsActive(task.Status))
                    {
                        task.StartedAtUtc ??= nowUtc;
                        task.Status = QuestionReviewTaskStatus.Completed;
                        task.CompletedAtUtc = nowUtc;
                    }
                }

                await db.SaveChangesAsync(ct);
                return items.Count;
            }
            catch (Exception ex)
            {
                // قرار الأدمن على السؤال محفوظ بالفعل؛ المزامنة تُصلح نفسها في أول قرار لاحق
                _logger.LogError(ex, "QRT: failed to sync admin decision {Decision} with review tasks", decision);
                return 0;
            }
        }

        private static void MarkApprovedByAdmin(QuestionReviewTaskItem item, string? byUserId, string? byName, DateTime nowUtc, string note)
        {
            item.Status = QuestionReviewTaskItemStatus.ApprovedByAdmin;
            item.IsLockActive = false;
            item.ActionAtUtc = nowUtc;
            item.ActionByUserId = byUserId;
            item.ActionByName = byName;
            item.AdminResolutionNote = note;
        }

        // نفس تحقق ApproveSingleQuestion الحالي: مكتمل، غير مرفوض، إجابة صحيحة مرتبطة بخيار فعلي
        private static bool IsApprovable(Question q)
        {
            if (!q.IsComplete || q.IsRejected || string.IsNullOrWhiteSpace(q.CorrectAnswer))
                return false;

            return q.Options.Any(o =>
                (!string.IsNullOrWhiteSpace(o.Text) && o.Text == q.CorrectAnswer) ||
                (!string.IsNullOrWhiteSpace(o.ImageUrl) && o.ImageUrl == q.CorrectAnswer));
        }

        private static string BuildSkipMessage(int approved, int notPending, int invalid, int reviewed)
        {
            var parts = new List<string>();
            if (approved > 0) parts.Add($"✅ تم اعتماد {approved} سؤال.");
            if (invalid > 0) parts.Add($"⚠️ تعذّر اعتماد {invalid} سؤال لأنها غير مكتملة أو مرفوضة أو إجابتها غير مرتبطة بخيار — استخدم الإرجاع أو التعديل.");
            if (reviewed > 0) parts.Add($"ℹ️ {reviewed} سؤال كانت الإدارة قد اعتمدته مسبقًا، وتم تحديثه في المهمة.");
            if (notPending > 0) parts.Add($"ℹ️ {notPending} عنصر لم يعد بانتظار المراجعة.");
            return string.Join(" ", parts);
        }

        private static object ProgressData(QuestionReviewTask task, int approved, int skipped)
        {
            var effective = QuestionReviewTaskMetrics.Effective(task.TotalItems, task.RemovedItems);
            return new
            {
                approved,
                skipped,
                pending = task.PendingItems,
                taskStatus = (int)task.Status,
                completed = task.Status == QuestionReviewTaskStatus.Completed,
                approvalPercent = QuestionReviewTaskMetrics.ApprovalPercent(task.ApprovedItems, effective),
                handledPercent = QuestionReviewTaskMetrics.HandledPercent(task.PendingItems, effective)
            };
        }

        /// <summary>
        /// بعد تغيير العناصر: تثبيت التغييرات، إعادة حساب العدادات (D10)، وانتقال الحالة
        /// (Assigned → InProgress عند أول إجراء، وإلى Completed عند انتهاء كل المعلّق). يرجع true عند الإكمال.
        /// </summary>
        private async Task<bool> ApplyTransitionAsync(ApplicationDbContext db, QuestionReviewTask task, DateTime nowUtc, CancellationToken ct)
        {
            if (task.Status == QuestionReviewTaskStatus.Assigned)
            {
                task.Status = QuestionReviewTaskStatus.InProgress;
                task.StartedAtUtc ??= nowUtc;
            }

            await db.SaveChangesAsync(ct);                      // تثبيت العناصر والأسئلة والـ Audit أولًا
            await RecalculateCountersAsync(db, task.Id, ct);    // GROUP BY واحد

            if (task.PendingItems == 0 && task.Status == QuestionReviewTaskStatus.InProgress)
            {
                task.Status = QuestionReviewTaskStatus.Completed;
                task.CompletedAtUtc = nowUtc;
                await db.SaveChangesAsync(ct);
                return true;
            }

            return false;
        }

        private async Task NotifyCompletionAsync(ReviewOutcome outcome, ReviewActor actor, CancellationToken ct)
        {
            if (!outcome.Completed || string.IsNullOrWhiteSpace(outcome.NotifyUserId))
                return;

            try
            {
                await _notifications.SendToUserAsync(
                    outcome.NotifyUserId!,
                    $"✅ أنهى المدرب {actor.Name} مهمة المراجعة {outcome.TaskCode}: نسبة الاعتماد {outcome.ApprovalPercent}%",
                    NotificationCategory.Important,
                    null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QRT: failed to notify admin about completion of task {Code}", outcome.TaskCode);
            }
        }

        private static QuestionAuditLog NewAudit(Guid questionId, string action, string? summary, ReviewActor actor, DateTime at) => new()
        {
            QuestionId = questionId,
            Action = action,
            PerformedByUserId = actor.UserId,
            PerformedByName = actor.Name,
            PerformedByRole = actor.Role,
            PerformedAt = at,
            ChangedFieldsSummary = summary
        };

        // ===== مولّد الكود QRT-{yyyy}-{0000} (QRT-S1.5) =====
        // الفهرس الفريد على Code يحمي من التكرار؛ CreateTaskAsync يعيد المحاولة مرة واحدة عند التصادم.
        internal async Task<string> NextCodeAsync(ApplicationDbContext db, CancellationToken ct)
        {
            var year = _time.GetUtcNow().Year;
            var prefix = $"QRT-{year}-";

            var last = await db.QuestionReviewTasks.AsNoTracking()
                .Where(t => t.Code.StartsWith(prefix))
                .OrderByDescending(t => t.Code)
                .Select(t => t.Code)
                .FirstOrDefaultAsync(ct);

            var n = 1;
            if (last is not null && int.TryParse(last.AsSpan(prefix.Length), out var lastNumber))
                n = lastNumber + 1;

            return $"{prefix}{n:0000}";
        }

        // ===== إعادة حساب العدادات بـ GROUP BY واحد (D10) =====
        public async Task RecalculateCountersAsync(ApplicationDbContext db, int taskId, CancellationToken ct = default)
        {
            var groups = await db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.TaskId == taskId)
                .GroupBy(i => i.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var task = await db.QuestionReviewTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
            if (task is null)
            {
                _logger.LogWarning("RecalculateCounters: task {TaskId} not found", taskId);
                return;
            }

            ApplyCounters(task, groups.Select(g => (g.Status, g.Count)).ToList());

            await db.SaveChangesAsync(ct);
        }

        // تعريف العدادات في مكان واحد (D10) — يستخدمه RecalculateCountersAsync والمزامنة
        private static void ApplyCounters(QuestionReviewTask task, IReadOnlyCollection<(QuestionReviewTaskItemStatus Status, int Count)> groups)
        {
            int CountOf(params QuestionReviewTaskItemStatus[] statuses)
                => groups.Where(g => statuses.Contains(g.Status)).Sum(g => g.Count);

            task.TotalItems = groups.Sum(g => g.Count);
            task.PendingItems = CountOf(QuestionReviewTaskItemStatus.Pending);
            task.ApprovedItems = CountOf(
                QuestionReviewTaskItemStatus.Approved,
                QuestionReviewTaskItemStatus.EditedAndApproved,
                QuestionReviewTaskItemStatus.ApprovedByAdmin);
            task.ReturnedItems = CountOf(QuestionReviewTaskItemStatus.Returned);
            task.RemovedItems = CountOf(QuestionReviewTaskItemStatus.Removed);
        }
    }
}
