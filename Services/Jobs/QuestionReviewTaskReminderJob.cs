using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.QuestionReviewTasks;

namespace QdratNew.Jobs
{
    public sealed record ReminderRunResult(int InstructorReminders, int CreatorAlerts, int DailySummaries);

    /// <summary>
    /// QRT-S7.1 + S7.4: تذكيرات مهام المراجعة (القريبة والمتأخرة) + الملخص اليومي لمنشئي المهام.
    /// يعمل يوميًا 08:00 بتوقيت الرياض عبر Hangfire.
    /// </summary>
    public sealed class QuestionReviewTaskReminderJob
    {
        private const int DueSoonHours = 24;
        private const int MinHoursBetweenReminders = 20;
        private const int MaxTasksPerRun = 500;

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;
        private readonly IAdvancedNotificationService _notifications;
        private readonly ILogger<QuestionReviewTaskReminderJob> _logger;

        public QuestionReviewTaskReminderJob(
            IDbContextFactory<ApplicationDbContext> dbFactory,
            TimeProvider time,
            IAdvancedNotificationService notifications,
            ILogger<QuestionReviewTaskReminderJob> logger)
        {
            _dbFactory = dbFactory;
            _time = time;
            _notifications = notifications;
            _logger = logger;
        }

        // Hangfire لا يقبل معاملات اختيارية داخل Expression
        public Task<ReminderRunResult> RunAsync() => RunAsync(CancellationToken.None);

        public async Task<ReminderRunResult> RunAsync(CancellationToken ct)
        {
            var nowUtc = _time.GetUtcNow().UtcDateTime;

            var reminders = 0;
            var alerts = 0;
            var summaries = 0;

            try { (reminders, alerts) = await SendRemindersAsync(nowUtc, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "QRT reminder job: reminders step failed");
            }

            // فشل التذكيرات لا يمنع الملخص اليومي
            try { summaries = await SendDailySummariesAsync(nowUtc, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "QRT reminder job: daily summary step failed");
            }

            _logger.LogInformation(
                "QRT reminder job done: {Reminders} instructor reminders, {Alerts} creator alerts, {Summaries} summaries",
                reminders, alerts, summaries);

            return new ReminderRunResult(reminders, alerts, summaries);
        }

        // ---- 1) + 2) + 3): تذكير المدرب، تنبيه المنشئ للمتأخر، ثم تحديث LastReminderAtUtc بحفظ واحد ----
        private async Task<(int Reminders, int Alerts)> SendRemindersAsync(DateTime nowUtc, CancellationToken ct)
        {
            var dueLimit = nowUtc.AddHours(DueSoonHours);
            var staleLimit = nowUtc.AddHours(-MinHoursBetweenReminders);

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            // مهام نشطة بها معلّق، موعدها خلال 24 ساعة أو فات، ولم يُذكَّر بها خلال 20 ساعة (المتأخرة تدخل تلقائيًا: Due < Limit)
            var rows = await db.QuestionReviewTasks
                .Where(t => (t.Status == QuestionReviewTaskStatus.Assigned || t.Status == QuestionReviewTaskStatus.InProgress)
                            && t.PendingItems > 0
                            && t.DueAtUtc != null && t.DueAtUtc <= dueLimit
                            && (t.LastReminderAtUtc == null || t.LastReminderAtUtc < staleLimit))
                .OrderBy(t => t.DueAtUtc)
                .Take(MaxTasksPerRun)
                .Select(t => new { Task = t, InstructorUserId = t.Instructor!.UserId })
                .ToListAsync(ct);

            if (rows.Count == 0)
                return (0, 0);

            var reminders = 0;
            foreach (var r in rows)
            {
                if (string.IsNullOrWhiteSpace(r.InstructorUserId))
                    continue;

                var overdue = r.Task.DueAtUtc!.Value < nowUtc;
                var due = QuestionReviewTaskMetrics.FormatLocal(r.Task.DueAtUtc);
                var text = overdue
                    ? $"⏰ مهمة المراجعة {r.Task.Code} متأخرة منذ {due} وبقي فيها {r.Task.PendingItems} سؤال بانتظار مراجعتك"
                    : $"⏰ مهمة المراجعة {r.Task.Code} موعد تسليمها {due} وبقي فيها {r.Task.PendingItems} سؤال بانتظار مراجعتك";

                if (await TrySendAsync(r.InstructorUserId!, text, NotificationCategory.Reminder,
                        $"/Instructors/QuestionReviewTasks/Review/{r.Task.Id}", ct))
                    reminders++;
            }

            // منشئ المهمة: إشعار واحد لكل منشئ يجمع المتأخر من مهامه
            var alerts = 0;
            var overdueByCreator = rows
                .Where(r => r.Task.DueAtUtc!.Value < nowUtc && !string.IsNullOrWhiteSpace(r.Task.CreatedByUserId))
                .GroupBy(r => r.Task.CreatedByUserId);

            foreach (var g in overdueByCreator)
            {
                var codes = string.Join("، ", g.Select(x => x.Task.Code).Take(5));
                var more = g.Count() > 5 ? $" و{g.Count() - 5} أخرى" : string.Empty;
                if (await TrySendAsync(g.Key,
                        $"⚠️ {g.Count()} مهمة مراجعة متأخرة عن موعدها: {codes}{more}",
                        NotificationCategory.Important, "/Admin/QuestionReviewTasks", ct))
                    alerts++;
            }

            // حفظ واحد لكل التحديثات (حتى لو لم يكن للمدرب حساب، لا نعيد المحاولة كل تشغيل)
            foreach (var r in rows)
                r.Task.LastReminderAtUtc = nowUtc;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // عدّل أحدهم المهمة أثناء التشغيل — الإشعارات أُرسلت أصلًا، فنكتفي بالتسجيل
                _logger.LogWarning(ex, "QRT reminder job: concurrency conflict while stamping LastReminderAtUtc");
            }

            return (reminders, alerts);
        }

        // ---- 7.4: ملخص يومي لكل منشئ مهام (اكتمل خلال 24 ساعة، متأخر، قريب الموعد، مرتجعات غير معالجة) ----
        private async Task<int> SendDailySummariesAsync(DateTime nowUtc, CancellationToken ct)
        {
            var since = nowUtc.AddHours(-24);
            var dueLimit = nowUtc.AddHours(DueSoonHours);

            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var rows = await db.QuestionReviewTasks.AsNoTracking()
                .Where(t =>
                    t.Status == QuestionReviewTaskStatus.Assigned ||
                    t.Status == QuestionReviewTaskStatus.InProgress ||
                    (t.Status == QuestionReviewTaskStatus.Completed && (t.CompletedAtUtc >= since || t.ReturnedItems > 0)))
                .Select(t => new
                {
                    t.CreatedByUserId,
                    t.Status,
                    t.DueAtUtc,
                    t.CompletedAtUtc,
                    t.ReturnedItems
                })
                .Take(MaxTasksPerRun * 4)
                .ToListAsync(ct);

            var sent = 0;
            foreach (var g in rows.Where(r => !string.IsNullOrWhiteSpace(r.CreatedByUserId)).GroupBy(r => r.CreatedByUserId))
            {
                var completed = g.Count(r => r.Status == QuestionReviewTaskStatus.Completed && r.CompletedAtUtc >= since);
                var overdue = g.Count(r => QuestionReviewTaskMetrics.IsOverdue(r.Status, r.DueAtUtc, nowUtc));
                var dueSoon = g.Count(r => QuestionReviewTaskMetrics.IsActive(r.Status)
                                           && r.DueAtUtc.HasValue && r.DueAtUtc.Value >= nowUtc && r.DueAtUtc.Value <= dueLimit);
                var returns = g.Sum(r => r.ReturnedItems);

                if (completed + overdue + dueSoon + returns == 0)
                    continue;

                var text = $"📊 ملخص مهام المراجعة اليومي: اكتملت {completed}، متأخرة {overdue}، تستحق خلال 24 ساعة {dueSoon}، مرتجعات بانتظار معالجتك {returns}";
                if (await TrySendAsync(g.Key, text, NotificationCategory.Important, "/Admin/QuestionReviewTasks", ct))
                    sent++;
            }

            return sent;
        }

        // فشل إشعار واحد لا يوقف بقية الإشعارات
        private async Task<bool> TrySendAsync(string userId, string message, NotificationCategory category, string url, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _notifications.SendToUserAsync(userId, message, category, url);
                return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "QRT reminder job: failed to notify user {UserId}", userId);
                return false;
            }
        }
    }
}
