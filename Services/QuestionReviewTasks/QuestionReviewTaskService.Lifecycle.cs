using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;

namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>
    /// QRT-S6: إدارة دورة الحياة من جهة الأدمن — معالجة المرتجعات، إزالة العناصر، إعادة الإسناد،
    /// الإلغاء، الإغلاق، تمديد الموعد. قواعد الانتقال في <see cref="QuestionReviewTaskStateRules"/>.
    /// كل عملية: Transaction + ExecutionStrategy، عدادات بـ GROUP BY واحد (D10)، Audit لكل سؤال متأثر، والإشعار بعد الإنهاء.
    /// </summary>
    public sealed partial class QuestionReviewTaskService
    {
        private const string AuditActionReturnResolved = "معالجة مرتجع مهمة مراجعة";
        private const string AuditActionRemovedFromTask = "إزالة من مهمة مراجعة";
        private const string AuditActionCancelledTask = "إلغاء مهمة مراجعة";
        private const string AuditActionReassigned = "إعادة إسناد مهمة مراجعة";
        private const int CancelReasonMin = 5;
        private const int CancelReasonMax = 500;
        private const int ResolutionNoteMax = 500;

        private const string ConcurrencyMessage = "⚠️ تم تعديل هذه المهمة من مستخدم آخر، أعد تحميل الصفحة وحاول مجددًا.";

        /// <summary>Transaction واحدة داخل ExecutionStrategy (EnableRetryOnFailure) — مزوّد InMemory بلا Transaction.</summary>
        private async Task<T> RunInTransactionAsync<T>(Func<ApplicationDbContext, Task<T>> work, CancellationToken ct)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var strategy = db.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear(); // أمان عند إعادة تشغيل الـ Strategy
                await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;

                var result = await work(db);

                if (tx is not null)
                    await tx.CommitAsync(ct);
                return result;
            });
        }

        /// <summary>
        /// بعد تغيير عناصر من جهة الأدمن: تثبيت التغييرات، إعادة حساب العدادات، وإكمال المهمة النشطة إن لم يبقَ معلّق.
        /// بخلاف مسار المدرب لا يحوّل Assigned إلى InProgress (الأدمن ليس المدرب).
        /// </summary>
        private async Task RefreshAfterAdminChangeAsync(ApplicationDbContext db, QuestionReviewTask task, DateTime nowUtc, CancellationToken ct)
        {
            await db.SaveChangesAsync(ct);
            await RecalculateCountersAsync(db, task.Id, ct); // يحدّث نفس الكائن المتتبَّع

            if (QuestionReviewTaskMetrics.IsActive(task.Status) && task.PendingItems == 0)
            {
                task.StartedAtUtc ??= nowUtc;
                task.Status = QuestionReviewTaskStatus.Completed;
                task.CompletedAtUtc = nowUtc;
                await db.SaveChangesAsync(ct);
            }
        }

        private static void StampAction(QuestionReviewTaskItem item, QuestionReviewTaskItemStatus status, ReviewActor actor, DateTime nowUtc, string? note)
        {
            item.Status = status;
            item.IsLockActive = false;
            item.ActionAtUtc = nowUtc;
            item.ActionByUserId = actor.UserId;
            item.ActionByName = actor.Name;
            item.AdminResolutionNote = note;
        }

        private async Task SafeNotifyAsync(string? userId, string message, string? targetUrl, string taskCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            try
            {
                await _notifications.SendToUserAsync(userId, message, NotificationCategory.Important, targetUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "QRT: failed to send lifecycle notification for task {Code}", taskCode);
            }
        }

        // ===== QRT-S6.1: معالجة المرتجع =====
        public async Task<OperationResult> ResolveReturnedAsync(long itemId, ReturnResolution resolution, string? note, ReviewActor actor, CancellationToken ct = default)
        {
            if (!Enum.IsDefined(resolution))
                return OperationResult.Fail("⚠️ إجراء المعالجة غير معروف.");

            var cleanNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (cleanNote is { Length: > ResolutionNoteMax })
                return OperationResult.Fail($"⚠️ الملاحظة لا تتجاوز {ResolutionNoteMax} حرف.");

            var nowUtc = _time.GetUtcNow().UtcDateTime;

            try
            {
                return await RunInTransactionAsync(async db =>
                {
                    var item = await db.QuestionReviewTaskItems
                        .Include(i => i.Task)
                        .Include(i => i.Question!).ThenInclude(q => q.Options)
                        .FirstOrDefaultAsync(i => i.Id == itemId, ct);
                    if (item is null)
                        return OperationResult.Fail("⚠️ العنصر غير موجود.");
                    if (item.Status != QuestionReviewTaskItemStatus.Returned)
                        return OperationResult.Fail("ℹ️ هذا السؤال لا يحتاج معالجة (عولج مسبقًا أو لم يُرجَع).");

                    var task = item.Task!;
                    var q = item.Question!;
                    string message;
                    string resolutionNote;
                    string auditSummary;

                    switch (resolution)
                    {
                        case ReturnResolution.ApproveAsIs:
                            if (q.IsReviewed)
                            {
                                // اعتُمد من مسار آخر قبل المعالجة — نسجّل المعالجة دون المساس بالسؤال
                                resolutionNote = cleanNote ?? "كان السؤال معتمدًا مسبقًا";
                            }
                            else
                            {
                                if (!IsApprovable(q))
                                    return OperationResult.Fail("⚠️ تعذّر اعتماد السؤال لأنه غير مكتمل أو مرفوض أو إجابته غير مرتبطة بخيار — عدّله أولًا، أو اختر إعادته للمسبح أو رفضه.");

                                q.IsReviewed = true;
                                q.ReviewedByUserId = actor.UserId;
                                q.ReviewedAt = DateTime.Now; // يبقى بالتوقيت المحلي كما في بقية البنك (D9)
                                resolutionNote = cleanNote ?? "اعتمدها الأدمن كما هي بعد الإرجاع";
                            }
                            message = "✅ اعتُمد السؤال وعولج الإرجاع.";
                            auditSummary = $"المهمة {task.Code} — اعتماد بعد الإرجاع";
                            break;

                        case ReturnResolution.ReleaseToPool:
                            resolutionNote = cleanNote ?? "أُعيد للمسبح العام";
                            message = "✅ أُعيد السؤال للمسبح العام ويمكن إسناده من جديد.";
                            auditSummary = $"المهمة {task.Code} — إعادة للمسبح";
                            break;

                        default: // Reject
                            if (q.IsReviewed)
                                return OperationResult.Fail("⚠️ السؤال معتمد حاليًا؛ ألغِ اعتماده أولًا ثم ارفضه.");

                            q.IsRejected = true;
                            resolutionNote = cleanNote ?? "رُفض نهائيًا بعد الإرجاع";
                            message = "✅ رُفض السؤال نهائيًا وعولج الإرجاع.";
                            auditSummary = $"المهمة {task.Code} — رفض نهائي بعد الإرجاع";
                            break;
                    }

                    StampAction(item, QuestionReviewTaskItemStatus.ReturnResolved, actor, nowUtc, resolutionNote);

                    var fullSummary = auditSummary + " — ملاحظة المدرب: " + item.ReturnNote;
                    db.QuestionAuditLogs.Add(NewAudit(
                        item.QuestionId, AuditActionReturnResolved,
                        fullSummary.Length > 1000 ? fullSummary[..1000] : fullSummary, actor, DateTime.Now));

                    await RefreshAfterAdminChangeAsync(db, task, nowUtc, ct);

                    return OperationResult.Ok(message, new
                    {
                        returned = task.ReturnedItems,
                        taskStatus = (int)task.Status,
                        canClose = QuestionReviewTaskStateRules.CanClose(task.Status, task.ReturnedItems)
                    });
                }, ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "QRT: concurrency conflict while resolving returned item {ItemId}", itemId);
                return OperationResult.Fail(ConcurrencyMessage);
            }
        }

        // ===== QRT-S6.2: إزالة عناصر معلّقة =====
        public async Task<OperationResult> RemoveItemsAsync(int taskId, IReadOnlyCollection<long> itemIds, ReviewActor actor, CancellationToken ct = default)
        {
            if (itemIds is null || itemIds.Count == 0)
                return OperationResult.Fail("⚠️ حدّد سؤالًا واحدًا على الأقل.");
            if (itemIds.Count > MaxQuestionsPerTask)
                return OperationResult.Fail($"⚠️ الحد الأقصى {MaxQuestionsPerTask} سؤال في العملية الواحدة.");

            var wanted = itemIds.ToHashSet();
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            try
            {
                return await RunInTransactionAsync(async db =>
                {
                    var task = await db.QuestionReviewTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
                    if (task is null)
                        return OperationResult.Fail("⚠️ المهمة غير موجودة.");
                    if (!QuestionReviewTaskStateRules.CanRemoveItems(task.Status, task.PendingItems))
                        return OperationResult.Fail("⚠️ لا يمكن إزالة أسئلة من هذه المهمة في حالتها الحالية.");

                    // المعلّق فقط (≤500 للمهمة) والتصفية بالمعرّفات في الذاكرة — بلا Contains داخل SQL
                    var pending = await db.QuestionReviewTaskItems
                        .Where(i => i.TaskId == taskId && i.Status == QuestionReviewTaskItemStatus.Pending)
                        .ToListAsync(ct);
                    var targets = pending.Where(i => wanted.Contains(i.Id)).ToList();
                    if (targets.Count == 0)
                        return OperationResult.Fail("ℹ️ لا توجد أسئلة معلّقة ضمن التحديد (ربما تم التصرف فيها).");

                    if (QuestionReviewTaskMetrics.Effective(task.TotalItems, task.RemovedItems) - targets.Count <= 0)
                        return OperationResult.Fail("⚠️ لا يمكن إزالة كل أسئلة المهمة؛ استخدم «إلغاء المهمة» بدلًا من ذلك.");

                    foreach (var item in targets)
                        StampAction(item, QuestionReviewTaskItemStatus.Removed, actor, nowUtc, "أزالها الأدمن من المهمة");

                    var auditAt = DateTime.Now;
                    db.QuestionAuditLogs.AddRange(targets.Select(i => NewAudit(
                        i.QuestionId, AuditActionRemovedFromTask, $"المهمة {task.Code} — أُزيل السؤال وعاد للمسبح", actor, auditAt)));

                    await RefreshAfterAdminChangeAsync(db, task, nowUtc, ct);

                    var skipped = wanted.Count - targets.Count;
                    var message = $"✅ أُزيل {targets.Count} سؤال من المهمة وعادت للمسبح العام.";
                    if (skipped > 0)
                        message += $" (تُجوهل {skipped} لأنها لم تعد معلّقة.)";
                    if (task.Status == QuestionReviewTaskStatus.Completed)
                        message += " اكتملت المراجعة لعدم بقاء أسئلة معلّقة.";

                    return OperationResult.Ok(message, new
                    {
                        removed = targets.Count,
                        pending = task.PendingItems,
                        taskStatus = (int)task.Status
                    });
                }, ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "QRT: concurrency conflict while removing items of task {TaskId}", taskId);
                return OperationResult.Fail(ConcurrencyMessage);
            }
        }

        // ===== QRT-S6.2: المرشّحون لإعادة الإسناد =====
        public async Task<OperationResult> GetReassignCandidatesAsync(int taskId, CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var task = await db.QuestionReviewTasks.AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new { t.Id, t.InstructorId, t.Status })
                .FirstOrDefaultAsync(ct);
            if (task is null)
                return OperationResult.Fail("⚠️ المهمة غير موجودة.");

            var pending = await LoadPendingQuestionInfoAsync(db, taskId, ct);
            if (!QuestionReviewTaskStateRules.CanReassign(task.Status, pending.Count))
                return OperationResult.Fail("⚠️ لا توجد أسئلة معلّقة قابلة لإعادة الإسناد في هذه المهمة.");

            var partnerIds = pending.Select(p => p.PartnerId).Distinct().ToList();
            if (partnerIds.Count > 1)
                return OperationResult.Fail("⚠️ أسئلة المهمة تتبع جهات (شركاء) مختلفة؛ لا يمكن إسنادها لمدرب واحد.");

            var curriculumIds = pending.Select(p => p.CurriculumId).Distinct().ToList();
            var eligible = (await FindEligibleInstructorsAsync(db, curriculumIds, partnerIds[0], ct))
                .Where(i => i.Id != task.InstructorId)
                .ToList();

            return OperationResult.Ok(
                eligible.Count == 0 ? "لا يوجد مدرب آخر مؤهل: يجب أن يملك المدرب كل المناهج المعنية ونفس الجهة." : string.Empty,
                new { pendingCount = pending.Count, instructors = eligible });
        }

        private sealed record PendingQuestionInfo(long ItemId, Guid QuestionId, int CurriculumId, int? PartnerId);

        // استعلام واحد: المعلّق بسؤاله (منهج + شريك) دون تحميل نص الأسئلة
        private static Task<List<PendingQuestionInfo>> LoadPendingQuestionInfoAsync(ApplicationDbContext db, int taskId, CancellationToken ct)
            => db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.TaskId == taskId && i.Status == QuestionReviewTaskItemStatus.Pending)
                .OrderBy(i => i.SortOrder)
                .Select(i => new PendingQuestionInfo(i.Id, i.QuestionId, i.Question!.CurriculumId, i.Question.PartnerId))
                .ToListAsync(ct);

        private sealed record ReassignOutcome(
            OperationResult Result, int NewTaskId = 0, string NewCode = "", int Count = 0, DateTime? NewDueUtc = null,
            string? OldInstructorUserId = null, string OldCode = "", bool OldCancelled = false);

        // ===== QRT-S6.2: إعادة إسناد المتبقي لمدرب آخر (مهمة جديدة بـ ParentTaskId) =====
        public async Task<OperationResult> ReassignRemainingAsync(int taskId, int newInstructorId, ReviewActor actor, CancellationToken ct = default)
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            await using var readDb = await _dbFactory.CreateDbContextAsync(ct);

            var source = await readDb.QuestionReviewTasks.AsNoTracking()
                .Where(t => t.Id == taskId)
                .Select(t => new
                {
                    t.Id, t.Code, t.Title, t.AdminNote, t.InstructorId, t.Status, t.Priority, t.DueAtUtc,
                    OldUserId = t.Instructor!.UserId
                })
                .FirstOrDefaultAsync(ct);
            if (source is null)
                return OperationResult.Fail("⚠️ المهمة غير موجودة.");
            if (source.InstructorId == newInstructorId)
                return OperationResult.Fail("⚠️ المدرب الجديد هو نفسه مدرب المهمة الحالي.");

            var instructor = await readDb.Instructors.AsNoTracking()
                .Where(i => i.Id == newInstructorId)
                .Select(i => new { i.Id, i.FullName, i.IsActive, i.UserId, i.PartnerId })
                .FirstOrDefaultAsync(ct);
            if (instructor is null || !instructor.IsActive)
                return OperationResult.Fail("⚠️ المدرب غير موجود أو غير نشط.");
            if (string.IsNullOrWhiteSpace(instructor.UserId))
                return OperationResult.Fail("⚠️ المدرب غير مرتبط بحساب دخول.");

            var pending = await LoadPendingQuestionInfoAsync(readDb, taskId, ct);
            if (!QuestionReviewTaskStateRules.CanReassign(source.Status, pending.Count))
                return OperationResult.Fail("⚠️ لا توجد أسئلة معلّقة قابلة لإعادة الإسناد في هذه المهمة.");

            // D7: عزل الشركاء
            var partnerMismatch = pending.Count(p => p.PartnerId != instructor.PartnerId);
            if (partnerMismatch > 0)
                return OperationResult.Fail($"⚠️ {partnerMismatch} سؤال لا يتبع نفس جهة المدرب (الشريك)، ولا يمكن إسناده إليه.");

            // صلاحية المنهج
            var curriculumIds = pending.Select(p => p.CurriculumId).Distinct().ToList();
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

            // الموعد القديم يُنقل فقط إن كان لا يزال في المستقبل
            var dueUtc = source.DueAtUtc.HasValue && source.DueAtUtc.Value > nowUtc ? source.DueAtUtc : null;
            var expectedIds = pending.Select(p => p.ItemId).ToHashSet();

            ReassignOutcome? outcome = null;
            try
            {
                for (var attempt = 0; attempt < 2 && outcome is null; attempt++)
                {
                    try
                    {
                        outcome = await RunInTransactionAsync(db => ReassignCoreAsync(
                            db, taskId, instructor.Id, instructor.FullName, curriculumIds, expectedIds, dueUtc, actor, nowUtc, ct), ct);
                    }
                    catch (DbUpdateException ex) when (IsConstraintViolation(ex, CodeIndexName) && attempt == 0)
                    {
                        _logger.LogWarning(ex, "QRT: task code collision while reassigning, retrying once");
                    }
                    catch (DbUpdateException ex) when (IsConstraintViolation(ex, ActiveLockIndexName))
                    {
                        _logger.LogWarning(ex, "QRT: active-lock violation while reassigning task {TaskId}", taskId);
                        return OperationResult.Fail("⚠️ تعذّر نقل الحجز، أعد تحميل الصفحة وحاول مجددًا.");
                    }
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "QRT: concurrency conflict while reassigning task {TaskId}", taskId);
                return OperationResult.Fail(ConcurrencyMessage);
            }

            if (outcome is null)
                return OperationResult.Fail("⚠️ تعذّر إعادة الإسناد، حاول مرة أخرى.");
            if (!outcome.Result.Success)
                return outcome.Result;

            // الإشعارات خارج الـ Transaction — فشلها لا يُفشل العملية
            await NotifyInstructorAsync(instructor.UserId!, new CreatedTaskInfo(outcome.NewTaskId, outcome.NewCode, outcome.Count), outcome.NewDueUtc, ct);
            await SafeNotifyAsync(
                outcome.OldInstructorUserId,
                $"ℹ️ سُحب المتبقي من مهمة المراجعة {outcome.OldCode} ({outcome.Count} سؤال) وأُسند إلى مدرب آخر.",
                null, outcome.OldCode, ct);

            var message = $"✅ أُعيد إسناد {outcome.Count} سؤال إلى {instructor.FullName} ضمن المهمة الجديدة {outcome.NewCode}.";
            if (source.DueAtUtc.HasValue && dueUtc is null)
                message += " (لم يُنقل موعد التسليم لأنه انقضى — حدّد موعدًا جديدًا من المهمة الجديدة.)";

            return OperationResult.Ok(message, new { taskId = outcome.NewTaskId, code = outcome.NewCode, count = outcome.Count });
        }

        private async Task<ReassignOutcome> ReassignCoreAsync(
            ApplicationDbContext db, int taskId, int newInstructorId, string newInstructorName, IReadOnlyList<int> curriculumIds,
            HashSet<long> expectedItemIds, DateTime? dueUtc, ReviewActor actor, DateTime nowUtc, CancellationToken ct)
        {
            var task = await db.QuestionReviewTasks.FirstOrDefaultAsync(t =>
                t.Id == taskId &&
                (t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress), ct);
            if (task is null)
                return new ReassignOutcome(OperationResult.Fail("⚠️ المهمة لم تعد نشطة."));

            var oldUserId = await db.Instructors.AsNoTracking()
                .Where(i => i.Id == task.InstructorId)
                .Select(i => i.UserId)
                .FirstOrDefaultAsync(ct);

            var oldItems = await db.QuestionReviewTaskItems
                .Where(i => i.TaskId == taskId && i.Status == QuestionReviewTaskItemStatus.Pending)
                .OrderBy(i => i.SortOrder)
                .ToListAsync(ct);

            // تغيّرت المجموعة منذ التحقق (المدرب تصرّف في أسئلة للتو): لا ننقل شيئًا لم نتحقق من أهليته
            if (oldItems.Count != expectedItemIds.Count || oldItems.Any(i => !expectedItemIds.Contains(i.Id)))
                return new ReassignOutcome(OperationResult.Fail("⚠️ تغيّرت أسئلة المهمة للتو (تصرّف المدرب في بعضها)، أعد تحميل الصفحة وحاول مجددًا."));

            var newCode = await NextCodeAsync(db, ct);

            // 1) تحرير الحجز القديم وتثبيته أولًا: الفهرس الفريد على الحجز النشط يمنع حجزين لنفس السؤال ولو داخل Transaction واحدة
            foreach (var item in oldItems)
                StampAction(item, QuestionReviewTaskItemStatus.Removed, actor, nowUtc, $"نُقل إلى المهمة {newCode}");
            await db.SaveChangesAsync(ct);

            // 2) المهمة الجديدة بنفس الإعدادات مع ParentTaskId (قائمة في الذاكرة ثم حفظ واحد)
            var newTask = new QuestionReviewTask
            {
                Code = newCode,
                Title = task.Title,
                AdminNote = task.AdminNote,
                InstructorId = newInstructorId,
                CurriculumId = curriculumIds.Count == 1 ? curriculumIds[0] : null,
                ParentTaskId = task.Id,
                Priority = task.Priority,
                DueAtUtc = dueUtc,
                CreatedByUserId = actor.UserId,
                CreatedByName = actor.Name,
                CreatedAtUtc = nowUtc,
                Status = QuestionReviewTaskStatus.Assigned
            };

            var order = 0;
            foreach (var old in oldItems)
            {
                newTask.Items.Add(new QuestionReviewTaskItem
                {
                    QuestionId = old.QuestionId,
                    SortOrder = ++order,
                    ReferenceNumberSnapshot = old.ReferenceNumberSnapshot,
                    IsLockActive = true
                });
            }
            db.QuestionReviewTasks.Add(newTask);

            var auditAt = DateTime.Now;
            var summary = $"من المهمة {task.Code} إلى {newCode} — المدرب: {newInstructorName}";
            db.QuestionAuditLogs.AddRange(oldItems.Select(i => NewAudit(i.QuestionId, AuditActionReassigned, summary, actor, auditAt)));

            await db.SaveChangesAsync(ct);
            await RecalculateCountersAsync(db, newTask.Id, ct);

            // 3) المهمة القديمة: لا معلّق فيها الآن
            await RecalculateCountersAsync(db, task.Id, ct);
            var oldCancelled = false;
            if (QuestionReviewTaskMetrics.Effective(task.TotalItems, task.RemovedItems) == 0)
            {
                // لم يبقَ فيها سؤال فعلي (كلها نُقلت) → تُلغى بدل أن تُعرض مكتملة بلا أسئلة
                task.Status = QuestionReviewTaskStatus.Cancelled;
                task.CancelledAtUtc = nowUtc;
                task.CancelReason = $"أُعيد إسناد كل أسئلتها إلى المهمة {newCode}";
                oldCancelled = true;
            }
            else
            {
                task.StartedAtUtc ??= nowUtc;
                task.Status = QuestionReviewTaskStatus.Completed;
                task.CompletedAtUtc = nowUtc;
            }
            await db.SaveChangesAsync(ct);

            return new ReassignOutcome(
                OperationResult.Ok(string.Empty), newTask.Id, newCode, oldItems.Count, dueUtc, oldUserId, task.Code, oldCancelled);
        }

        // ===== QRT-S6.3: إلغاء المهمة =====
        public async Task<OperationResult> CancelTaskAsync(int taskId, string reason, ReviewActor actor, CancellationToken ct = default)
        {
            var cleanReason = (reason ?? string.Empty).Trim();
            if (cleanReason.Length < CancelReasonMin || cleanReason.Length > CancelReasonMax)
                return OperationResult.Fail($"⚠️ سبب الإلغاء إلزامي ({CancelReasonMin} – {CancelReasonMax} حرف).");

            var nowUtc = _time.GetUtcNow().UtcDateTime;

            try
            {
                var (result, notifyUserId, code) = await RunInTransactionAsync<(OperationResult, string?, string)>(async db =>
                {
                    var task = await db.QuestionReviewTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
                    if (task is null)
                        return (OperationResult.Fail("⚠️ المهمة غير موجودة."), (string?)null, string.Empty);
                    if (!QuestionReviewTaskStateRules.CanCancel(task.Status))
                        return (OperationResult.Fail("⚠️ المهمة منتهية (مغلقة أو ملغاة) ولا يمكن إلغاؤها."), null, task.Code);

                    // المعلّق يُزال ويتحرر حجزه؛ المرتجع غير المعالج يبقى للأدمن (ما زال محجوزًا عن المسار العام حتى يعالجه)
                    var pending = await db.QuestionReviewTaskItems
                        .Where(i => i.TaskId == taskId && i.Status == QuestionReviewTaskItemStatus.Pending)
                        .ToListAsync(ct);

                    foreach (var item in pending)
                        StampAction(item, QuestionReviewTaskItemStatus.Removed, actor, nowUtc, "أُلغيت المهمة");

                    var auditAt = DateTime.Now;
                    var summary = $"المهمة {task.Code} — أُلغيت: {cleanReason}";
                    if (summary.Length > 1000) summary = summary[..1000];
                    db.QuestionAuditLogs.AddRange(pending.Select(i => NewAudit(i.QuestionId, AuditActionCancelledTask, summary, actor, auditAt)));

                    task.Status = QuestionReviewTaskStatus.Cancelled;
                    task.CancelledAtUtc = nowUtc;
                    task.CancelReason = cleanReason;

                    await db.SaveChangesAsync(ct);
                    await RecalculateCountersAsync(db, task.Id, ct);

                    var instructorUserId = await db.Instructors.AsNoTracking()
                        .Where(i => i.Id == task.InstructorId)
                        .Select(i => i.UserId)
                        .FirstOrDefaultAsync(ct);

                    var message = $"✅ أُلغيت المهمة {task.Code} وعاد {pending.Count} سؤال معلّق للمسبح العام.";
                    if (task.ReturnedItems > 0)
                        message += $" ما زال {task.ReturnedItems} سؤال مرتجع بانتظار معالجتك.";

                    return (OperationResult.Ok(message, new { released = pending.Count, returned = task.ReturnedItems }), instructorUserId, task.Code);
                }, ct);

                if (result.Success)
                    await SafeNotifyAsync(notifyUserId, $"ℹ️ أُلغيت مهمة المراجعة {code}: {cleanReason}", null, code, ct);

                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "QRT: concurrency conflict while cancelling task {TaskId}", taskId);
                return OperationResult.Fail(ConcurrencyMessage);
            }
        }

        // ===== QRT-S6.3: إغلاق المهمة =====
        public async Task<OperationResult> CloseTaskAsync(int taskId, ReviewActor actor, CancellationToken ct = default)
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            try
            {
                return await RunInTransactionAsync(async db =>
                {
                    var task = await db.QuestionReviewTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
                    if (task is null)
                        return OperationResult.Fail("⚠️ المهمة غير موجودة.");

                    if (QuestionReviewTaskStateRules.IsFinal(task.Status))
                        return OperationResult.Fail("ℹ️ المهمة منتهية مسبقًا.");
                    if (task.Status != QuestionReviewTaskStatus.Completed)
                        return OperationResult.Fail("⚠️ لا تُغلق المهمة قبل اكتمال المراجعة (يوجد أسئلة معلّقة).");

                    // لا نعتمد على العدّاد المخزّن وحده عند قرار الإغلاق
                    await RecalculateCountersAsync(db, task.Id, ct);
                    if (!QuestionReviewTaskStateRules.CanClose(task.Status, task.ReturnedItems))
                        return OperationResult.Fail($"⚠️ يوجد {task.ReturnedItems} سؤال مرتجع غير معالج؛ عالجه أولًا ثم أغلق المهمة.");

                    task.Status = QuestionReviewTaskStatus.Closed;
                    task.ClosedAtUtc = nowUtc;
                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation("QRT: task {Code} closed by {User}", task.Code, actor.UserId);
                    return OperationResult.Ok($"✅ أُغلقت المهمة {task.Code}.", new { taskStatus = (int)task.Status });
                }, ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "QRT: concurrency conflict while closing task {TaskId}", taskId);
                return OperationResult.Fail(ConcurrencyMessage);
            }
        }

        // ===== QRT-S6.3: تمديد/إزالة الموعد =====
        public async Task<OperationResult> ExtendDueAsync(int taskId, DateTime? newDueUtc, ReviewActor actor, CancellationToken ct = default)
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;
            if (newDueUtc.HasValue && newDueUtc.Value <= nowUtc)
                return OperationResult.Fail("⚠️ موعد التسليم الجديد يجب أن يكون في المستقبل.");

            try
            {
                var (result, notifyUserId, code) = await RunInTransactionAsync<(OperationResult, string?, string)>(async db =>
                {
                    var task = await db.QuestionReviewTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct);
                    if (task is null)
                        return (OperationResult.Fail("⚠️ المهمة غير موجودة."), (string?)null, string.Empty);
                    if (!QuestionReviewTaskStateRules.CanExtendDue(task.Status))
                        return (OperationResult.Fail("⚠️ لا يمكن تعديل موعد مهمة غير نشطة."), null, task.Code);

                    task.DueAtUtc = newDueUtc;
                    task.LastReminderAtUtc = null; // يُعاد تقييم التذكيرات على الموعد الجديد (S7)
                    await db.SaveChangesAsync(ct);

                    var instructorUserId = await db.Instructors.AsNoTracking()
                        .Where(i => i.Id == task.InstructorId)
                        .Select(i => i.UserId)
                        .FirstOrDefaultAsync(ct);

                    var message = newDueUtc.HasValue
                        ? $"✅ حُدّد موعد التسليم الجديد: {QuestionReviewTaskMetrics.FormatLocal(newDueUtc)}."
                        : "✅ أُزيل موعد التسليم من المهمة.";
                    return (OperationResult.Ok(message, new
                    {
                        dueLocal = QuestionReviewTaskMetrics.FormatLocal(newDueUtc)
                    }), instructorUserId, task.Code);
                }, ct);

                if (result.Success)
                {
                    var text = newDueUtc.HasValue
                        ? $"🗓️ عُدّل موعد تسليم مهمة المراجعة {code} إلى {QuestionReviewTaskMetrics.FormatLocal(newDueUtc)}."
                        : $"🗓️ أُزيل موعد تسليم مهمة المراجعة {code}.";
                    await SafeNotifyAsync(notifyUserId, text, $"/Instructors/QuestionReviewTasks/Review/{taskId}", code, ct);
                }

                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "QRT: concurrency conflict while extending due of task {TaskId}", taskId);
                return OperationResult.Fail(ConcurrencyMessage);
            }
        }
    }
}
