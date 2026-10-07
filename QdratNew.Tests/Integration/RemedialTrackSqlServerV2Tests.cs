using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // RTK-S12.3 — اختبارات SQL Server حقيقية لحزمة v2 (S9–S12): الفهرس المُصفّى بعد الحذف الناعم،
    // الفهرس الفريد (EnrollmentId, AxisProgressId, Kind) مع NULL، التزامن الحقيقي لإنشاء/إرسال/إعادة إرسال/إقرار تقرير ولي الأمر.
    //
    // التشغيل: QDRAT_TEST_SQL على Connection String لقاعدة اختبار (اسمها يحوي "Test") — بدونه تُتخطّى.
    // يلزم أن تكون سكربتات RTK_PublicationSoftDelete/RTK_VideoReview/RTK_ParentReports مطبَّقة على قاعدة الاختبار.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class RemedialTrackSqlServerV2Tests : IAsyncLifetime
    {
        private sealed class RealTz : ITimeZoneService
        {
            public DateTime GetNowUtc() => DateTime.UtcNow;
            public DateTime GetNowSaudi() => DateTime.UtcNow.AddHours(3);
            public DateTime ConvertToSaudi(DateTime utcTime) => utcTime.AddHours(3);
            public DateTime ConvertToUtc(DateTime saudiTime) => saudiTime.AddHours(-3);
        }

        private static readonly RemedialTrackActor Admin = new("rtks7-admin", "مدير الاختبار");

        private static RemedialTrackProgressService NewProgress()
            => new(new RtkRealDb.Factory(), TimeProvider.System, new RealTz(), NullLogger<RemedialTrackProgressService>.Instance);

        private static RemedialTrackParentReportAdminService NewAdmin()
            => new(new RtkRealDb.Factory(), TimeProvider.System, new Mock<IAdminActivityLogger>().Object,
                   NullLogger<RemedialTrackParentReportAdminService>.Instance);

        private static RemedialTrackParentReportService NewParents()
            => new(new RtkRealDb.Factory(), TimeProvider.System, new Mock<IAdminActivityLogger>().Object,
                   NullLogger<RemedialTrackParentReportService>.Instance);

        public Task InitializeAsync() => RtkRealDb.IsConfigured ? RtkRealDb.CleanupAsync() : Task.CompletedTask;

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? RtkRealDb.CleanupAsync() : Task.CompletedTask;

        private static bool IsUniqueViolation(DbUpdateException ex)
            => ex.GetBaseException() is SqlException s && (s.Number == 2601 || s.Number == 2627);

        // ───────────── الحذف الناعم: الفهرس المُصفّى ─────────────

        [SqlServerFact]
        public async Task ActiveCode_SoftDeletedPublication_FreesCode_AndRestoreOverActiveDuplicateIsRejected()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1, inPerson: true);

            RemedialTrackPublication Clone() => new()
            {
                TrackId = seed.TrackId, BatchId = seed.BatchId, Scope = RemedialTrackPublicationScope.WholeBatch,
                Mode = RemedialTrackDeliveryMode.InPerson, PublishAtUtc = DateTime.UtcNow, Status = RemedialTrackPublicationStatus.Active,
                AccessCode = seed.AccessCode, CodeVersion = 1, CreatedByUserId = "rtks7-admin", CreatedAtUtc = DateTime.UtcNow, TotalStudents = 0
            };

            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackPublications.Where(p => p.Id == seed.PublicationId)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true));

            // الرقم المحذوف يُعاد استعماله في أمر نشط جديد بلا خطأ فهرس
            int newId;
            await using (var db = RtkRealDb.CreateDb())
            {
                var p = Clone();
                db.RemedialTrackPublications.Add(p);
                await db.SaveChangesAsync();
                newId = p.Id;
            }

            // استرجاع المحذوف بينما الرقم مستعمل في أمر نشط ← يرفضه الفهرس (لذا يعيد RestoreAsync توليد رقم)
            await using (var db = RtkRealDb.CreateDb())
            {
                var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                    db.RemedialTrackPublications.Where(p => p.Id == seed.PublicationId).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, false)));
                Assert.True(ex.GetBaseException() is SqlException { Number: 2601 or 2627 }, ex.GetBaseException().Message);
            }
            Assert.True(newId > 0);
        }

        // ───────────── عدد استعلامات بناء التقرير (S11.1) على SQL Server الحقيقي ─────────────

        private sealed class CountingInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
        {
            private int _count;
            public int Count => _count;

            public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
                System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
                Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _count);
                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }
        }

        private static async Task<int> CountReportQueriesAsync(int enrollmentId)
        {
            var counter = new CountingInterceptor();
            var options = new DbContextOptionsBuilder<QdratNew.Data.ApplicationDbContext>()
                .UseSqlServer(TestDatabaseBaseline.TestDatabaseConnectionString, o => o.UseCompatibilityLevel(120))
                .AddInterceptors(counter).Options;
            await using var db = new QdratNew.Data.ApplicationDbContext(options);
            var vm = await RemedialTrackReportBuilder.BuildAsync(db, enrollmentId, null, null, includeNote: false, DateTime.UtcNow, CancellationToken.None);
            Assert.NotNull(vm);
            return counter.Count;
        }

        [SqlServerFact]
        public async Task ReportBuilder_QueryCount_IsSmallAndIndependentOfAxisCount()
        {
            var two = await RtkRealDb.SeedAsync(students: 1, axes: 2);
            var withTwo = await CountReportQueriesAsync(two.Students[0].EnrollmentId);

            var six = await RtkRealDb.SeedAsync(students: 1, axes: 6);
            var withSix = await CountReportQueriesAsync(six.Students[0].EnrollmentId);

            Assert.True(withTwo <= 6, $"عدد الاستعلامات {withTwo}");
            Assert.Equal(withTwo, withSix);   // لا N+1: العدد ثابت مهما زاد عدد المحاور
        }

        // ───────────── تقارير ولي الأمر: الفهرس الفريد مع NULL ─────────────

        [SqlServerFact]
        public async Task ParentReports_UniqueIndex_RejectsDuplicates_AlsoWhenAxisProgressIsNull()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1);
            var s = seed.Students[0];

            RemedialTrackParentReport Row(int? ap, RemedialTrackParentReportKind kind) => new()
            {
                EnrollmentId = s.EnrollmentId, AxisProgressId = ap, Kind = kind, StudentId = s.StudentId,
                SnapshotJson = "{}", Status = RemedialTrackParentReportStatus.Pending, CreatedAtUtc = DateTime.UtcNow
            };

            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackParentReports.Add(Row(s.AxisProgressIds[0], RemedialTrackParentReportKind.AxisNotPassed));
                db.RemedialTrackParentReports.Add(Row(null, RemedialTrackParentReportKind.Final));
                await db.SaveChangesAsync();
            }

            // مكرر لنفس (تسجيل، محور، نوع)
            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackParentReports.Add(Row(s.AxisProgressIds[0], RemedialTrackParentReportKind.AxisNotPassed));
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.True(IsUniqueViolation(ex), ex.GetBaseException().Message);
            }

            // الختامي (AxisProgressId = NULL) مكرر لنفس التسجيل ← SQL Server يعامل NULL كقيمة واحدة في الفهرس الفريد
            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackParentReports.Add(Row(null, RemedialTrackParentReportKind.Final));
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.True(IsUniqueViolation(ex), ex.GetBaseException().Message);
            }

            // نوع مختلف لنفس المحور مسموح
            await using (var db = RtkRealDb.CreateDb())
            {
                db.RemedialTrackParentReports.Add(Row(s.AxisProgressIds[0], RemedialTrackParentReportKind.Final));
                await db.SaveChangesAsync();
            }
        }

        [SqlServerFact]
        public async Task AdminQueue_RunsOnSqlServerCompat120_WithBatchScope_AndHidesDeletedPublication()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1, firstAxisStatus: RemedialTrackAxisStatus.AwaitingExam102);
            var s = seed.Students[0];
            await RtkRealDb.LinkParentAsync(s.StudentId, 5);
            await NewProgress().OnExamSubmittedAsync(await RtkRealDb.AddFailedAttempt102Async(seed, s));
            var admin = NewAdmin();
            var any = new RemedialTrackParentReportQueueFilter();

            var all = await admin.GetQueueAsync(any, RemedialTrackBatchScope.Unrestricted);
            Assert.Equal(1, all.Total);
            Assert.Single(all.Items);
            Assert.Equal(1, all.StatusCounts[RemedialTrackParentReportStatus.Sent]);
            Assert.Contains(all.Publications, p => p.Id == seed.PublicationId);

            // نطاق الدفعة (Contains على قائمة صغيرة ← IN بثوابت تحت CompatibilityLevel 120)
            Assert.Equal(1, (await admin.GetQueueAsync(any, new RemedialTrackBatchScope(new HashSet<int> { seed.BatchId, seed.BatchId + 1 }))).Total);
            Assert.Equal(0, (await admin.GetQueueAsync(any, new RemedialTrackBatchScope(new HashSet<int> { seed.BatchId + 1 }))).Total);
            Assert.Equal(1, (await admin.GetQueueAsync(new RemedialTrackParentReportQueueFilter { PublicationId = seed.PublicationId, BatchId = seed.BatchId, Status = RemedialTrackParentReportStatus.Sent }, RemedialTrackBatchScope.Unrestricted)).Total);

            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackPublications.Where(p => p.Id == seed.PublicationId)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDeleted, true));
            Assert.Equal(0, (await admin.GetQueueAsync(any, RemedialTrackBatchScope.Unrestricted)).Total);
        }

        // ───────────── التزامن الحقيقي ─────────────

        [SqlServerFact]
        public async Task ConcurrentFailSubmit_CreatesExactlyOneParentReport_AndOneNotification()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1, firstAxisStatus: RemedialTrackAxisStatus.AwaitingExam102);
            var s = seed.Students[0];
            var (parentId, parentUserId) = await RtkRealDb.LinkParentAsync(s.StudentId, 1);
            var attemptId = await RtkRealDb.AddFailedAttempt102Async(seed, s);

            // 8 تسليمات متزامنة بخدمات مستقلة (مثل 8 تبويبات/إعادة محاولة)
            var results = await Task.WhenAll(Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => NewProgress().OnExamSubmittedAsync(attemptId))));
            Assert.Contains(results, r => r.Applied);

            await using var db = RtkRealDb.CreateDb();
            var report = await db.RemedialTrackParentReports.AsNoTracking().SingleAsync(r => r.EnrollmentId == s.EnrollmentId);
            Assert.Equal(RemedialTrackParentReportKind.AxisNotPassed, report.Kind);
            Assert.Equal(RemedialTrackParentReportStatus.Sent, report.Status);
            Assert.Equal(parentId, report.ParentId);
            Assert.NotEqual("{}", report.SnapshotJson);

            Assert.Equal(1, await db.Notifications.CountAsync(n => n.UserId == parentUserId));
        }

        [SqlServerFact]
        public async Task ConcurrentAcknowledge_NeverThrows_AndStoresOneAcknowledgement()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1, firstAxisStatus: RemedialTrackAxisStatus.AwaitingExam102);
            var s = seed.Students[0];
            var (parentId, _) = await RtkRealDb.LinkParentAsync(s.StudentId, 2);
            await NewProgress().OnExamSubmittedAsync(await RtkRealDb.AddFailedAttempt102Async(seed, s));

            int reportId;
            await using (var db = RtkRealDb.CreateDb())
                reportId = await db.RemedialTrackParentReports.Where(r => r.EnrollmentId == s.EnrollmentId).Select(r => r.Id).SingleAsync();

            var results = await Task.WhenAll(Enumerable.Range(0, 6)
                .Select(_ => Task.Run(() => NewParents().AcknowledgeAsync(parentId, reportId, "u", "ولي أمر"))));

            Assert.All(results, r => Assert.True(r.Found));
            Assert.Single(results.Select(r => r.AcknowledgedAtUtc).Distinct());   // كلهم يرون نفس وقت الإقرار الأول

            await using var check = RtkRealDb.CreateDb();
            Assert.NotNull(await check.RemedialTrackParentReports.AsNoTracking().Where(r => r.Id == reportId).Select(r => r.AcknowledgedAtUtc).SingleAsync());
        }

        [SqlServerFact]
        public async Task ConcurrentResend_SendsExactlyOneNotification()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1, firstAxisStatus: RemedialTrackAxisStatus.AwaitingExam102);
            var s = seed.Students[0];
            var (_, parentUserId) = await RtkRealDb.LinkParentAsync(s.StudentId, 3);
            await NewProgress().OnExamSubmittedAsync(await RtkRealDb.AddFailedAttempt102Async(seed, s));

            int reportId;
            await using (var db = RtkRealDb.CreateDb())
            {
                reportId = await db.RemedialTrackParentReports.Where(r => r.EnrollmentId == s.EnrollmentId).Select(r => r.Id).SingleAsync();
                // آخر إرسال قبل ساعة ← خارج حارس الدقيقة؛ يبقى RowVersion وحده يمنع التكرار المتزامن
                await db.RemedialTrackParentReports.Where(r => r.Id == reportId)
                    .ExecuteUpdateAsync(x => x.SetProperty(r => r.SentAtUtc, DateTime.UtcNow.AddHours(-1)));
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 6)
                .Select(_ => Task.Run(() => NewAdmin().ResendAsync(reportId, Admin, RemedialTrackBatchScope.Unrestricted))));

            Assert.All(results, r => Assert.True(r.Found));
            Assert.Equal(1, results.Count(r => r.Success));

            await using var check = RtkRealDb.CreateDb();
            Assert.Equal(2, await check.Notifications.CountAsync(n => n.UserId == parentUserId));   // الإنشاء التلقائي + إعادة واحدة فقط
        }

        [SqlServerFact]
        public async Task ConcurrentManualSend_OfPendingReport_NotifiesOnce()
        {
            var seed = await RtkRealDb.SeedAsync(students: 1, firstAxisStatus: RemedialTrackAxisStatus.AwaitingExam102);
            var s = seed.Students[0];
            var (_, parentUserId) = await RtkRealDb.LinkParentAsync(s.StudentId, 4);
            await using (var db = RtkRealDb.CreateDb())
                await db.RemedialTrackPublications.Where(p => p.Id == seed.PublicationId)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.AutoSendParentReports, false));   // الإنشاء ← Pending
            await NewProgress().OnExamSubmittedAsync(await RtkRealDb.AddFailedAttempt102Async(seed, s));

            int reportId;
            await using (var db = RtkRealDb.CreateDb())
            {
                var r = await db.RemedialTrackParentReports.AsNoTracking().SingleAsync(x => x.EnrollmentId == s.EnrollmentId);
                Assert.Equal(RemedialTrackParentReportStatus.Pending, r.Status);
                reportId = r.Id;
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 6)
                .Select(_ => Task.Run(() => NewAdmin().SendAsync(reportId, Admin, RemedialTrackBatchScope.Unrestricted))));

            Assert.All(results, r => Assert.True(r.Found));
            Assert.Equal(1, results.Count(r => r.Success));

            await using var check = RtkRealDb.CreateDb();
            Assert.Equal(1, await check.Notifications.CountAsync(n => n.UserId == parentUserId));
            Assert.Equal(RemedialTrackParentReportStatus.Sent,
                await check.RemedialTrackParentReports.AsNoTracking().Where(r => r.Id == reportId).Select(r => r.Status).SingleAsync());
        }
    }
}
