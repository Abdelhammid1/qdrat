using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>
    /// RTK-S12.1: طابور تقارير أولياء الأمور للأدمن (إرسال/إعادة/إيقاف + مفتاح الإرسال التلقائي + نطاق الدفعات).
    /// يعيد استخدام مُجهِّز S11 (Fx). InMemory: التزامن الحقيقي (RowVersion) يُغطّى على SQL Server في S12.3.
    /// </summary>
    public partial class RemedialTrackParentReportTests
    {
        private sealed class MovableTime : TimeProvider
        {
            public DateTime Utc { get; set; } = T0;
            public override DateTimeOffset GetUtcNow() => new(Utc, TimeSpan.Zero);
        }

        private static readonly RemedialTrackActor Admin = new("admin-1", "مدير");
        private static readonly RemedialTrackBatchScope Open = RemedialTrackBatchScope.Unrestricted;

        private static RemedialTrackParentReportAdminService NewAdmin(Fx fx, TimeProvider? time = null)
            => new(fx.Factory, time ?? new FixedTime(), fx.Activity.Object, NullLogger<RemedialTrackParentReportAdminService>.Instance);

        /// <summary>يُسقط الطالب في اختبار 102 فيُنشأ تقرير «عدم اجتياز» (0 = طالب له ولي أمر، 1 = بلا ولي أمر).</summary>
        private static async Task FailBothAsync(Fx fx, int student)
        {
            var ap = student == 0 ? fx.ApWithParent[0] : fx.ApNoParent[0];
            var attempt = await fx.AddAttemptAsync(ap, RemedialTrackExamNumber.Exam102, 30, false);
            await fx.Progress.OnExamSubmittedAsync(attempt);
        }

        private static async Task<int> BatchIdAsync(Fx fx)
        {
            await using var db = fx.Factory.CreateDbContext();
            return await db.RemedialTrackPublications.AsNoTracking().Where(p => p.Id == fx.PublicationId).Select(p => p.BatchId).FirstAsync();
        }

        private static async Task<int> NotificationCountAsync(Fx fx)
        {
            await using var db = fx.Factory.CreateDbContext();
            return await db.Notifications.AsNoTracking().CountAsync();
        }

        // ---------------- الطابور ----------------

        [Fact]
        public async Task Queue_ListsReportsWithCountsAndFilters_AndRespectsBatchScope()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 0);   // Sent
            await FailBothAsync(fx, 1);   // NoParent
            var admin = NewAdmin(fx);

            var all = await admin.GetQueueAsync(new RemedialTrackParentReportQueueFilter(), Open);
            Assert.Equal(2, all.Total);
            Assert.Equal(RemedialTrackParentReportStatus.NoParent, all.Items[0].Status);   // غير المُرسل أولًا
            Assert.Equal(1, all.StatusCounts[RemedialTrackParentReportStatus.Sent]);
            Assert.Equal(1, all.StatusCounts[RemedialTrackParentReportStatus.NoParent]);
            Assert.Single(all.Publications);

            var sentOnly = await admin.GetQueueAsync(new RemedialTrackParentReportQueueFilter { Status = RemedialTrackParentReportStatus.Sent }, Open);
            Assert.Single(sentOnly.Items);
            Assert.Equal("أحمد", sentOnly.Items[0].StudentName);
            Assert.Equal("ولي أمر أ", sentOnly.Items[0].ParentName);

            var batchId = await BatchIdAsync(fx);
            var mine = await admin.GetQueueAsync(new RemedialTrackParentReportQueueFilter { BatchId = batchId }, new RemedialTrackBatchScope(new HashSet<int> { batchId }));
            Assert.Equal(2, mine.Total);

            var other = await admin.GetQueueAsync(new RemedialTrackParentReportQueueFilter(), new RemedialTrackBatchScope(new HashSet<int> { batchId + 99 }));
            Assert.Equal(0, other.Total);
            Assert.Empty(other.Publications);
        }

        [Fact]
        public async Task Queue_HidesReportsOfDeletedPublication()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 0);
            await using (var db = fx.Factory.CreateDbContext())
            {
                (await db.RemedialTrackPublications.FirstAsync()).IsDeleted = true;
                await db.SaveChangesAsync();
            }
            var q = await NewAdmin(fx).GetQueueAsync(new RemedialTrackParentReportQueueFilter(), Open);
            Assert.Equal(0, q.Total);
        }

        // ---------------- الإرسال ----------------

        [Fact]
        public async Task Send_NoParent_FailsWithoutNotification_ThenSucceedsAfterLinkingParent()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 1);
            var admin = NewAdmin(fx);
            var report = (await fx.ReportsAsync()).Single();
            Assert.Equal(RemedialTrackParentReportStatus.NoParent, report.Status);

            var r1 = await admin.SendAsync(report.Id, Admin, Open);
            Assert.True(r1.Found); Assert.False(r1.Success);
            Assert.Equal(0, await NotificationCountAsync(fx));
            Assert.Equal(RemedialTrackParentReportStatus.NoParent, (await fx.ReportsAsync()).Single().Status);

            await using (var db = fx.Factory.CreateDbContext())
            {
                (await db.Students.FirstAsync(s => s.StudentID == fx.StudentNoParent)).ParentId = fx.ParentB;
                await db.SaveChangesAsync();
            }

            var r2 = await admin.SendAsync(report.Id, Admin, Open);
            Assert.True(r2.Success, r2.Message);

            var after = (await fx.ReportsAsync()).Single();
            Assert.Equal(RemedialTrackParentReportStatus.Sent, after.Status);
            Assert.Equal(fx.ParentB, after.ParentId);
            Assert.Equal(T0, after.SentAtUtc);
            Assert.Equal("admin-1", after.SentByUserId);

            await using var db2 = fx.Factory.CreateDbContext();
            var n = Assert.Single(await db2.Notifications.AsNoTracking().ToListAsync());
            Assert.Equal(Fx.ParentBUser, n.UserId);
            Assert.Equal(T0, n.SentAt);
            Assert.Equal($"/Parents/RemedialTrackReports/Details?id={report.Id}", n.TargetUrl);
            fx.Activity.Verify(a => a.LogAsync("RemedialTrack.ParentReportSent",
                It.Is<string>(d => d.StartsWith($"[RTK pub:{fx.PublicationId}]")), "admin-1", "مدير", It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
        }

        [Fact]
        public async Task Send_PendingWithEmptySnapshot_RebuildsSnapshotAndSends()
        {
            var fx = new Fx(); await fx.SeedAsync(autoSend: false);
            await FailBothAsync(fx, 0);
            var report = (await fx.ReportsAsync()).Single();
            Assert.Equal(RemedialTrackParentReportStatus.Pending, report.Status);

            await using (var db = fx.Factory.CreateDbContext())
            {
                (await db.RemedialTrackParentReports.FirstAsync()).SnapshotJson = "{}";   // محاكاة فشل بناء اللقطة سابقًا
                await db.SaveChangesAsync();
            }

            var r = await NewAdmin(fx).SendAsync(report.Id, Admin, Open);
            Assert.True(r.Success, r.Message);

            var after = (await fx.ReportsAsync()).Single();
            Assert.Equal(RemedialTrackParentReportStatus.Sent, after.Status);
            var snap = RemedialTrackParentReportWriter.TryDeserialize(after.SnapshotJson);
            Assert.NotNull(snap);
            Assert.Equal("أحمد", snap!.StudentName);
            Assert.Equal(1, await NotificationCountAsync(fx));
        }

        [Fact]
        public async Task Send_AlreadySent_FailsAndDoesNotNotifyTwice()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 0);
            var id = (await fx.ReportsAsync()).Single().Id;

            var r = await NewAdmin(fx).SendAsync(id, Admin, Open);
            Assert.True(r.Found); Assert.False(r.Success);
            Assert.Equal(1, await NotificationCountAsync(fx));   // إشعار الإنشاء التلقائي فقط
        }

        [Fact]
        public async Task Send_SuppressedReport_CanBeSentAgain()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 0);
            var id = (await fx.ReportsAsync()).Single().Id;
            var admin = NewAdmin(fx);

            Assert.True((await admin.SuppressAsync(id, Admin, Open)).Success);
            Assert.True((await admin.SendAsync(id, Admin, Open)).Success);
            Assert.Equal(RemedialTrackParentReportStatus.Sent, (await fx.ReportsAsync()).Single().Status);
        }

        // ---------------- إعادة الإرسال (حارس الدقيقة) ----------------

        [Fact]
        public async Task Resend_WithinOneMinute_IsRejected_AfterwardsSendsNewNotification()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 0);   // SentAtUtc = T0
            var id = (await fx.ReportsAsync()).Single().Id;
            var time = new MovableTime { Utc = T0.AddSeconds(30) };
            var admin = NewAdmin(fx, time);

            var early = await admin.ResendAsync(id, Admin, Open);
            Assert.True(early.Found); Assert.False(early.Success);
            Assert.Equal(1, await NotificationCountAsync(fx));

            time.Utc = T0.AddSeconds(61);
            var ok = await admin.ResendAsync(id, Admin, Open);
            Assert.True(ok.Success, ok.Message);
            Assert.Equal(2, await NotificationCountAsync(fx));
            Assert.Equal(T0.AddSeconds(61), (await fx.ReportsAsync()).Single().SentAtUtc);

            var doubleClick = await admin.ResendAsync(id, Admin, Open);   // نقرة ثانية فورية
            Assert.False(doubleClick.Success);
            Assert.Equal(2, await NotificationCountAsync(fx));
        }

        [Fact]
        public async Task Resend_NonSentReport_IsRejected()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 1);   // NoParent
            var id = (await fx.ReportsAsync()).Single().Id;

            var r = await NewAdmin(fx, new MovableTime { Utc = T0.AddHours(1) }).ResendAsync(id, Admin, Open);
            Assert.False(r.Success);
            Assert.Equal(0, await NotificationCountAsync(fx));
        }

        // ---------------- الإيقاف ----------------

        [Fact]
        public async Task Suppress_HidesReportFromParent_IsIdempotent_AndRejectsNoParent()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 0);
            await FailBothAsync(fx, 1);
            var reports = await fx.ReportsAsync();
            var sent = reports.Single(r => r.Status == RemedialTrackParentReportStatus.Sent);
            var noParent = reports.Single(r => r.Status == RemedialTrackParentReportStatus.NoParent);
            var admin = NewAdmin(fx);

            Assert.NotNull(await fx.Parents.GetDetailsAsync(fx.ParentA, sent.Id));
            Assert.True((await admin.SuppressAsync(sent.Id, Admin, Open)).Success);
            Assert.Null(await fx.Parents.GetDetailsAsync(fx.ParentA, sent.Id));                 // اختفى عن ولي الأمر
            Assert.Empty((await fx.Parents.GetListAsync(fx.ParentA, 1)).Items);

            var again = await admin.SuppressAsync(sent.Id, Admin, Open);
            Assert.True(again.Success);                                                          // Idempotent
            Assert.Equal(RemedialTrackParentReportStatus.Suppressed, (await fx.ReportsAsync()).First(r => r.Id == sent.Id).Status);

            Assert.False((await admin.SuppressAsync(noParent.Id, Admin, Open)).Success);
        }

        // ---------------- نطاق الدفعات (IDOR) ----------------

        [Fact]
        public async Task AdminActions_OutsideBatchScope_AreNotFound_AndChangeNothing()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 0);
            var id = (await fx.ReportsAsync()).Single().Id;
            var batchId = await BatchIdAsync(fx);
            var foreign = new RemedialTrackBatchScope(new HashSet<int> { batchId + 99 });
            var admin = NewAdmin(fx, new MovableTime { Utc = T0.AddHours(1) });

            Assert.False((await admin.SendAsync(id, Admin, foreign)).Found);
            Assert.False((await admin.ResendAsync(id, Admin, foreign)).Found);
            Assert.False((await admin.SuppressAsync(id, Admin, foreign)).Found);
            Assert.False((await admin.ToggleAutoSendAsync(fx.PublicationId, false, Admin, foreign)).Found);
            Assert.False((await admin.SendAsync(987654, Admin, Open)).Found);                    // غير موجود

            Assert.Equal(RemedialTrackParentReportStatus.Sent, (await fx.ReportsAsync()).Single().Status);
            Assert.Equal(1, await NotificationCountAsync(fx));
            await using var db = fx.Factory.CreateDbContext();
            Assert.True((await db.RemedialTrackPublications.AsNoTracking().FirstAsync()).AutoSendParentReports);
        }

        [Fact]
        public async Task AdminActions_OnDeletedPublication_AreNotFound()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await FailBothAsync(fx, 0);
            var id = (await fx.ReportsAsync()).Single().Id;
            await using (var db = fx.Factory.CreateDbContext())
            {
                (await db.RemedialTrackPublications.FirstAsync()).IsDeleted = true;
                await db.SaveChangesAsync();
            }
            var admin = NewAdmin(fx, new MovableTime { Utc = T0.AddHours(1) });

            Assert.False((await admin.ResendAsync(id, Admin, Open)).Found);
            Assert.False((await admin.ToggleAutoSendAsync(fx.PublicationId, false, Admin, Open)).Found);
        }

        // ---------------- مفتاح الإرسال التلقائي ----------------

        [Fact]
        public async Task ToggleAutoSend_Off_MakesNextReportsPending_AndIsIdempotent()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var admin = NewAdmin(fx);

            Assert.True((await admin.ToggleAutoSendAsync(fx.PublicationId, false, Admin, Open)).Success);
            Assert.True((await admin.ToggleAutoSendAsync(fx.PublicationId, false, Admin, Open)).Success);   // لا تغيير
            fx.Activity.Verify(a => a.LogAsync("RemedialTrack.AutoSendToggled",
                It.Is<string>(d => d.Contains($"[RTK pub:{fx.PublicationId}]")), "admin-1", "مدير", It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);

            await FailBothAsync(fx, 0);
            var r = (await fx.ReportsAsync()).Single();
            Assert.Equal(RemedialTrackParentReportStatus.Pending, r.Status);
            Assert.Equal(0, await NotificationCountAsync(fx));

            Assert.True((await admin.ToggleAutoSendAsync(fx.PublicationId, true, Admin, Open)).Success);
            Assert.True((await admin.SendAsync(r.Id, Admin, Open)).Success);                                 // الإرسال اليدوي بعد التشغيل
            Assert.Equal(1, await NotificationCountAsync(fx));
        }
    }
}
