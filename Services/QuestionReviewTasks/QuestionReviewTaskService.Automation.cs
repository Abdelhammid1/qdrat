using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.ViewModels.QuestionReviewTasks;

namespace QdratNew.Services.QuestionReviewTasks
{
    /// <summary>QRT-S7.2: التوزيع التلقائي لأول N سؤال على عدة مدربين (كل مدرب = مهمة مستقلة).</summary>
    public sealed partial class QuestionReviewTaskService
    {
        private const int MaxDistributionInstructors = 20;

        // مدرب مختار ومؤهل مع عبئه وعدد أسئلته في هذا التوزيع
        private sealed record DistributionPlan(
            List<CandidateQuestion> Questions,
            List<(int Id, string Name, string UserId, int Load, int Assigned)> Lines,
            List<string> ExcludedNames);

        private sealed record DistributionTask(string Code, int TaskId, int InstructorId, string UserId, int Count);

        public async Task<OperationResult> PreviewAutoDistributionAsync(AutoDistributeInput input, CancellationToken ct = default)
        {
            var validation = ValidateDistributionInput(input, out _);
            if (validation is not null)
                return validation;

            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var (plan, error) = await BuildDistributionPlanAsync(db, input, ct);
            if (plan is null)
                return OperationResult.Fail(error!);

            return OperationResult.Ok(string.Empty, new AutoDistributionPreviewDto(
                plan.Questions.Count,
                plan.ExcludedNames.Count,
                plan.ExcludedNames,
                plan.Lines.Select(l => new AutoDistributionLineDto(l.Id, l.Name, l.Load, l.Assigned)).ToList()));
        }

        public async Task<OperationResult> AutoDistributeAsync(AutoDistributeInput input, ReviewActor actor, CancellationToken ct = default)
        {
            var validation = ValidateDistributionInput(input, out var dueUtc);
            if (validation is not null)
                return validation;

            var nowUtc = _time.GetUtcNow().UtcDateTime;
            if (dueUtc.HasValue && dueUtc.Value <= nowUtc)
                return OperationResult.Fail("⚠️ موعد التسليم يجب أن يكون في المستقبل.");

            await using var readDb = await _dbFactory.CreateDbContextAsync(ct);
            var (plan, error) = await BuildDistributionPlanAsync(readDb, input, ct);
            if (plan is null)
                return OperationResult.Fail(error!);

            var title = input.Title.Trim();
            var adminNote = string.IsNullOrWhiteSpace(input.AdminNote) ? null : input.AdminNote.Trim();
            var curriculumIds = plan.Questions.Select(q => q.CurriculumId).Distinct().ToList();

            // Transaction واحدة لكل المهام: فشل أي جزء = Rollback كامل (إعادة محاولة واحدة عند تكرار الكود)
            List<DistributionTask>? created = null;
            for (var attempt = 0; attempt < 2 && created is null; attempt++)
            {
                try
                {
                    created = await InsertDistributionAsync(title, adminNote, curriculumIds, plan, input.Priority, dueUtc, actor, nowUtc, ct);
                }
                catch (DbUpdateException ex) when (IsConstraintViolation(ex, CodeIndexName) && attempt == 0)
                {
                    _logger.LogWarning(ex, "QRT: task code collision during auto-distribution, retrying once");
                }
                catch (DbUpdateException ex) when (IsConstraintViolation(ex, ActiveLockIndexName))
                {
                    _logger.LogWarning(ex, "QRT: active-lock violation during auto-distribution");
                    return OperationResult.Fail("⚠️ بعض الأسئلة أُسندت لمهمة أخرى للتو، أعد المعاينة وحاول مجددًا.");
                }
            }

            if (created is null)
                return OperationResult.Fail("⚠️ تعذّر إنشاء المهام، حاول مرة أخرى.");

            // الإشعارات خارج الـ Transaction — فشلها لا يُفشل العملية
            foreach (var t in created)
                await NotifyInstructorAsync(t.UserId, new CreatedTaskInfo(t.TaskId, t.Code, t.Count), dueUtc, ct);

            var message = $"✅ تم توزيع {plan.Questions.Count} سؤال على {created.Count} مدرب في {created.Count} مهمة مستقلة.";
            if (plan.ExcludedNames.Count > 0)
                message += $" (استُبعد {plan.ExcludedNames.Count} مدرب غير مؤهل.)";

            return OperationResult.Ok(message, new
            {
                tasks = created.Select(t => new { taskId = t.TaskId, code = t.Code, instructorId = t.InstructorId, count = t.Count }).ToList(),
                total = plan.Questions.Count
            });
        }

        // ---- تحقق المدخلات (مشترك بين المعاينة والتنفيذ) ----
        private OperationResult? ValidateDistributionInput(AutoDistributeInput? input, out DateTime? dueUtc)
        {
            dueUtc = null;
            if (input is null)
                return OperationResult.Fail("⚠️ بيانات التوزيع غير مكتملة.");

            var title = (input.Title ?? string.Empty).Trim();
            if (title.Length < 3 || title.Length > 150)
                return OperationResult.Fail("⚠️ عنوان المهام يجب أن يكون بين 3 و150 حرفًا.");

            if (input.AdminNote is { Length: > 1000 })
                return OperationResult.Fail("⚠️ ملاحظة الإدارة لا تتجاوز 1000 حرف.");

            if (!input.TakeCount.HasValue || input.TakeCount.Value < 1 || input.TakeCount.Value > MaxQuestionsPerTask)
                return OperationResult.Fail($"⚠️ عدد الأسئلة يجب أن يكون بين 1 و{MaxQuestionsPerTask}.");

            var ids = input.InstructorIds?.Where(i => i > 0).Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0)
                return OperationResult.Fail("⚠️ اختر مدربًا واحدًا على الأقل.");
            if (ids.Count > MaxDistributionInstructors)
                return OperationResult.Fail($"⚠️ الحد الأقصى {MaxDistributionInstructors} مدربًا في التوزيع الواحد.");

            if (input.DueAtLocal.HasValue)
                dueUtc = ToUtc(input.DueAtLocal.Value);

            return null;
        }

        // ---- بناء الخطة: الأسئلة ← المدربون المؤهلون المختارون ← التوزيع حسب العبء ----
        private async Task<(DistributionPlan? Plan, string? Error)> BuildDistributionPlanAsync(
            ApplicationDbContext db, AutoDistributeInput input, CancellationToken ct)
        {
            var selection = await ResolveQuestionsAsync(
                db, null, input.CurriculumId, input.SectionId, input.LessonId, input.TakeCount, ct);
            if (selection.Error is not null)
                return (null, selection.Error);
            if (selection.Eligible.Count == 0)
                return (null, "⚠️ لا توجد أسئلة مؤهلة للتوزيع ضمن هذا الفلتر.");

            var partnerIds = selection.Eligible.Select(q => q.PartnerId).Distinct().ToList();
            if (partnerIds.Count > 1)
                return (null, "⚠️ الأسئلة تتبع جهات (شركاء) مختلفة؛ ضيّق الفلتر على جهة واحدة.");

            var curriculumIds = selection.Eligible.Select(q => q.CurriculumId).Distinct().ToList();

            // المؤهلون (D7 + صلاحية المنهج) مرتبون حسب العبء ثم الاسم — نفس مصدر قائمة الإسناد اليدوي
            var eligible = await FindEligibleInstructorsAsync(db, curriculumIds, partnerIds[0], ct);

            var wanted = input.InstructorIds.Where(i => i > 0).Distinct().ToList();
            var chosen = eligible.Where(e => wanted.Contains(e.Id)).ToList();

            var excludedIds = wanted.Where(id => chosen.All(c => c.Id != id)).ToList();
            var excludedNames = excludedIds.Count == 0
                ? new List<string>()
                : await db.Instructors.AsNoTracking()
                    .Where(i => excludedIds.Contains(i.Id))
                    .Select(i => i.FullName)
                    .ToListAsync(ct);

            if (chosen.Count == 0)
                return (null, "⚠️ لا يوجد بين المدربين المختارين من هو مؤهل (يجب أن يملك كل المناهج المعنية ونفس الجهة).");

            // UserId لازم للإشعار — استعلام واحد
            var chosenIds = chosen.Select(c => c.Id).ToList();
            var users = await db.Instructors.AsNoTracking()
                .Where(i => chosenIds.Contains(i.Id))
                .Select(i => new { i.Id, i.UserId })
                .ToListAsync(ct);
            var userById = users.ToDictionary(u => u.Id, u => u.UserId ?? string.Empty);

            var counts = QuestionReviewTaskDistributor.Allocate(
                selection.Eligible.Count, chosen.Select(c => c.ActiveLockedItems).ToList());

            var lines = new List<(int, string, string, int, int)>();
            for (var i = 0; i < chosen.Count; i++)
            {
                if (counts[i] > 0)
                    lines.Add((chosen[i].Id, chosen[i].FullName, userById[chosen[i].Id], chosen[i].ActiveLockedItems, counts[i]));
            }

            return (new DistributionPlan(selection.Eligible, lines, excludedNames), null);
        }

        private async Task<List<DistributionTask>> InsertDistributionAsync(
            string title,
            string? adminNote,
            IReadOnlyList<int> curriculumIds,
            DistributionPlan plan,
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

                await using var tx = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(ct)
                    : null;

                // الأكواد تُحسب مرة واحدة ثم تُزاد محليًا (بلا SaveChanges داخل Loop)
                var firstCode = await NextCodeAsync(db, ct);
                var dash = firstCode.LastIndexOf('-');
                var prefix = firstCode[..(dash + 1)];
                var number = int.Parse(firstCode.AsSpan(dash + 1));

                var auditAt = DateTime.Now; // QuestionAuditLog.PerformedAt بالتوقيت المحلي كما في بقية البنك
                var tasks = new List<(QuestionReviewTask Task, string UserId, int InstructorId)>();
                var audits = new List<QuestionAuditLog>();
                var cursor = 0;

                foreach (var line in plan.Lines)
                {
                    var slice = plan.Questions.Skip(cursor).Take(line.Assigned).ToList();
                    cursor += slice.Count;

                    var composedTitle = $"{title} — {line.Name}";
                    var task = new QuestionReviewTask
                    {
                        Code = $"{prefix}{number++:0000}",
                        Title = composedTitle.Length > 200 ? composedTitle[..200] : composedTitle,
                        AdminNote = adminNote,
                        InstructorId = line.Id,
                        CurriculumId = curriculumIds.Count == 1 ? curriculumIds[0] : null,
                        Priority = priority,
                        DueAtUtc = dueUtc,
                        CreatedByUserId = actor.UserId,
                        CreatedByName = actor.Name,
                        CreatedAtUtc = nowUtc,
                        Status = QuestionReviewTaskStatus.Assigned,
                        // مهمة جديدة: العدادات معروفة (D10 يُطبَّق على كل تغيير لاحق)
                        TotalItems = slice.Count,
                        PendingItems = slice.Count
                    };

                    var order = 0;
                    foreach (var q in slice)
                    {
                        task.Items.Add(new QuestionReviewTaskItem
                        {
                            QuestionId = q.Id,
                            SortOrder = ++order,
                            ReferenceNumberSnapshot = q.ReferenceNumber,
                            IsLockActive = true
                        });
                    }

                    var summary = $"المهمة {task.Code} — المدرب: {line.Name} (توزيع تلقائي)";
                    audits.AddRange(slice.Select(q => new QuestionAuditLog
                    {
                        QuestionId = q.Id,
                        Action = AuditActionAssigned,
                        PerformedByUserId = actor.UserId,
                        PerformedByName = actor.Name,
                        PerformedByRole = actor.Role,
                        PerformedAt = auditAt,
                        ChangedFieldsSummary = summary
                    }));

                    tasks.Add((task, line.UserId, line.Id));
                }

                db.QuestionReviewTasks.AddRange(tasks.Select(t => t.Task));
                db.QuestionAuditLogs.AddRange(audits);
                await db.SaveChangesAsync(ct);   // حفظ واحد لكل المهام

                if (tx is not null)
                    await tx.CommitAsync(ct);

                return tasks
                    .Select(t => new DistributionTask(t.Task.Code, t.Task.Id, t.InstructorId, t.UserId, t.Task.TotalItems))
                    .ToList();
            });
        }
    }
}
