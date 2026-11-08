using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Implementations;
using QdratNew.Services.PartnerDispatch;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S6.1 — سجل الإرسال في الأدمن على قاعدة حقيقية: التجميع بضغطة الإرسال، الفلاتر، حالة مسودة تحديد المستوى، والتصدير.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class PartnerDispatchLogReportSqlServerTests : IAsyncLifetime
    {
        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;
            await using var db = RtkRealDb.CreateDb();
            await ApdTestData.CleanupAsync(db);
        }

        public async Task DisposeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;
            await using var db = RtkRealDb.CreateDb();
            await ApdTestData.CleanupAsync(db);
        }

        private static PartnerDispatchLog Line(Guid group, PartnerDispatchKind kind, string title, int partnerId, int batchId, int students, DateTime sentAtUtc, bool overridden = false, string? reason = null)
            => new()
            {
                DispatchGroupId = group,
                Kind = kind,
                Title = title,
                PartnerId = partnerId,
                BatchId = batchId,
                TargetEntityId = 1,
                StudentsCount = students,
                ContractOverridden = overridden,
                OverrideReason = reason,
                SentByUserId = ApdTestData.SenderId,
                SentAtUtc = sentAtUtc
            };

        // G1: واجب لمدرستين (A1 + B1) قبل ساعة. G2: اختبار لـ A بتجاوز الآن.
        private static async Task<(ApdSeed Seed, Guid G1, Guid G2)> SeedTwoGroupsAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            var seed = await ApdTestData.SeedAsync(db);
            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();
            var now = DateTime.UtcNow;

            db.PartnerDispatchLogs.AddRange(
                Line(g1, PartnerDispatchKind.Homework, $"{ApdTestData.Marker} واجب", seed.PartnerA, seed.BatchA1, 3, now.AddHours(-1)),
                Line(g1, PartnerDispatchKind.Homework, $"{ApdTestData.Marker} واجب", seed.PartnerB, seed.BatchB1, 2, now.AddHours(-1).AddSeconds(2)),
                Line(g2, PartnerDispatchKind.Exam, $"{ApdTestData.Marker} اختبار", seed.PartnerA, seed.BatchA1, 4, now, overridden: true, reason: "سبب تجاوز تجريبي"));
            await db.SaveChangesAsync();
            return (seed, g1, g2);
        }

        private static PartnerDispatchLogReportService NewReport(ApplicationDbContext db) => new(db);

        [SqlServerFact]
        public async Task Search_GroupsByDispatchGroup_NewestFirst_WithCountsAndNames()
        {
            var (seed, g1, g2) = await SeedTwoGroupsAsync();
            await using var db = RtkRealDb.CreateDb();

            var page = await NewReport(db).SearchAsync(new PartnerDispatchLogFilter { SentByUserId = ApdTestData.SenderId });

            Assert.Equal(2, page.TotalGroups);
            Assert.Equal(3, page.TotalLines);
            Assert.Equal(9, page.TotalStudents);
            Assert.Equal(1, page.OverriddenLines);
            Assert.Equal(new[] { g2, g1 }, page.Groups.Select(g => g.DispatchGroupId));

            var first = page.Groups[1];
            Assert.Equal(PartnerDispatchKind.Homework, first.Kind);
            Assert.Equal(2, first.PartnersCount);
            Assert.Equal(2, first.BatchesCount);
            Assert.Equal(5, first.StudentsCount);
            Assert.False(first.AnyOverridden);
            Assert.Equal($"{ApdTestData.Marker} apds1-sender", first.SentByName);
            Assert.Contains(first.Lines, l => l.PartnerName == $"{ApdTestData.Marker} مدرسة A" && l.BatchName != null && l.BranchName != null);

            var overridden = Assert.Single(page.Groups[0].Lines);
            Assert.True(overridden.ContractOverridden);
            Assert.Equal("سبب تجاوز تجريبي", overridden.OverrideReason);
            Assert.Equal(seed.PartnerA, overridden.PartnerId);
        }

        [SqlServerFact]
        public async Task Filters_Partner_Kind_Override_Sender_AndDates()
        {
            var (seed, g1, g2) = await SeedTwoGroupsAsync();
            await using var db = RtkRealDb.CreateDb();
            var report = NewReport(db);

            // مدرسة B: الإرسال الأول فقط وبسطرها وحده
            var onlyB = await report.SearchAsync(new PartnerDispatchLogFilter { PartnerId = seed.PartnerB });
            var groupB = Assert.Single(onlyB.Groups);
            Assert.Equal(g1, groupB.DispatchGroupId);
            Assert.Equal(1, groupB.PartnersCount);
            Assert.Equal(2, groupB.StudentsCount);

            var exams = await report.SearchAsync(new PartnerDispatchLogFilter { Kind = PartnerDispatchKind.Exam, SentByUserId = ApdTestData.SenderId });
            Assert.Equal(g2, Assert.Single(exams.Groups).DispatchGroupId);

            var withOverride = await report.SearchAsync(new PartnerDispatchLogFilter { Overridden = true, SentByUserId = ApdTestData.SenderId });
            Assert.Equal(g2, Assert.Single(withOverride.Groups).DispatchGroupId);

            var withoutOverride = await report.SearchAsync(new PartnerDispatchLogFilter { Overridden = false, SentByUserId = ApdTestData.SenderId });
            Assert.Equal(g1, Assert.Single(withoutOverride.Groups).DispatchGroupId);

            var otherSender = await report.SearchAsync(new PartnerDispatchLogFilter { SentByUserId = "apds1-nobody" });
            Assert.Empty(otherSender.Groups);
            Assert.Equal(0, otherSender.TotalGroups);

            var todayLocal = PartnerDispatchLogReportService.ToDisplayTime(DateTime.UtcNow).Date;
            var tomorrow = await report.SearchAsync(new PartnerDispatchLogFilter { FromDate = todayLocal.AddDays(1), SentByUserId = ApdTestData.SenderId });
            Assert.Empty(tomorrow.Groups);

            // من/إلى معكوسان يُبدَّلان، واليوم يُشمل كاملًا
            var range = await report.SearchAsync(new PartnerDispatchLogFilter { FromDate = todayLocal, ToDate = todayLocal.AddDays(-1), SentByUserId = ApdTestData.SenderId });
            Assert.Equal(2, range.TotalGroups);
        }

        [Fact]
        public void UtcRange_SwapsReversedDates_AndIncludesTheWholeLastDay()
        {
            var (from, to) = PartnerDispatchLogReportService.UtcRange(new DateTime(2026, 11, 6), new DateTime(2026, 11, 5));

            // السعودية UTC+3: بداية 5/11 محليًا = 4/11 21:00 UTC، ونهاية 6/11 محليًا = 6/11 21:00 UTC
            Assert.Equal(new DateTime(2026, 11, 4, 21, 0, 0), from);
            Assert.Equal(new DateTime(2026, 11, 6, 21, 0, 0), to);
        }

        [SqlServerFact]
        public async Task Paging_ClampsOutOfRangePage_ToLastPage()
        {
            await using (var seedDb = RtkRealDb.CreateDb())
            {
                var seed = await ApdTestData.SeedAsync(seedDb);
                var now = DateTime.UtcNow;
                seedDb.PartnerDispatchLogs.AddRange(Enumerable.Range(0, IPartnerDispatchLogReportService.PageSize + 3)
                    .Select(i => Line(Guid.NewGuid(), PartnerDispatchKind.Homework, $"{ApdTestData.Marker} {i}", seed.PartnerA, seed.BatchA1, 1, now.AddMinutes(-i))));
                await seedDb.SaveChangesAsync();
            }

            await using var db = RtkRealDb.CreateDb();
            var page = await NewReport(db).SearchAsync(new PartnerDispatchLogFilter { SentByUserId = ApdTestData.SenderId, Page = 99 });

            Assert.Equal(2, page.TotalPages);
            Assert.Equal(2, page.Page);
            Assert.Equal(3, page.Groups.Count);
            Assert.All(page.Groups, g => Assert.Single(g.Lines));
        }

        [SqlServerFact]
        public async Task PlacementDraft_ShowsPending_ThenSentByPartner_AfterTheSchoolSendsIt()
        {
            ApdSeed seed;
            await using (var seedDb = RtkRealDb.CreateDb())
            {
                seed = await ApdTestData.SeedAsync(seedDb);
                var placement = await ApdTestData.PreparePlacementAsync(seedDb, seed);

                var dispatch = new PartnerPlacementDispatchService(
                    seedDb,
                    new PlacementExamScopeGuard(),
                    new PartnerDispatchLogService(seedDb),
                    new AdvancedNotificationService(seedDb),
                    new MemoryCache(new MemoryCacheOptions()),
                    NullLogger<PartnerPlacementDispatchService>.Instance);

                var outcome = await dispatch.DispatchAsync(
                    new[] { new PartnerDispatchTarget { PartnerId = seed.PartnerA, PartnerName = "A", BatchIds = new List<int> { seed.BatchA1 } } },
                    new SendPlacementToPartnersRequest { ModelId = placement.ModelId, CourseId = seed.CourseId, DurationMinutes = 45, Delivery = PlacementPartnerDelivery.Draft },
                    new DispatchActor(ApdTestData.SenderId, "مرسل"));
                Assert.True(Assert.Single(outcome.Results).Succeeded);
            }

            var filter = new PartnerDispatchLogFilter { Kind = PartnerDispatchKind.Placement, PartnerId = seed.PartnerA };

            await using (var db = RtkRealDb.CreateDb())
            {
                var group = Assert.Single((await NewReport(db).SearchAsync(filter)).Groups);
                Assert.True(group.HasDraft);
                Assert.Equal(PartnerDispatchDraftState.DraftPending, Assert.Single(group.Lines).DraftState);
            }

            await using (var db = RtkRealDb.CreateDb())
            {
                var examId = await db.Exams.Where(e => e.OwnerPartnerId == seed.PartnerA).Select(e => e.Id).SingleAsync();
                await db.ExamAssignmentsToBatches
                    .Where(a => a.ExamId == examId)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsSentToStudents, true));

                var line = Assert.Single(Assert.Single((await NewReport(db).SearchAsync(filter)).Groups).Lines);
                Assert.Equal(PartnerDispatchDraftState.DraftSentByPartner, line.DraftState);
            }
        }

        [SqlServerFact]
        public async Task Export_WritesOneRowPerLine_RespectsFilter_AndKeepsFormulasAsText()
        {
            var (seed, _, _) = await SeedTwoGroupsAsync();
            await using (var extra = RtkRealDb.CreateDb())
            {
                extra.PartnerDispatchLogs.Add(Line(Guid.NewGuid(), PartnerDispatchKind.MinistrySim, "=HYPERLINK(\"http://x\",\"y\")", seed.PartnerB, seed.BatchB1, 1, DateTime.UtcNow));
                await extra.SaveChangesAsync();
            }

            await using var db = RtkRealDb.CreateDb();
            var export = await NewReport(db).ExportAsync(new PartnerDispatchLogFilter { PartnerId = seed.PartnerB });

            Assert.False(export.Truncated);
            Assert.EndsWith(".xlsx", export.FileName);

            using var workbook = new XLWorkbook(new MemoryStream(export.Content));
            var sheet = workbook.Worksheet(1);
            Assert.True(sheet.RightToLeft);
            Assert.Equal("النوع", sheet.Cell(1, 3).GetString());

            var rows = sheet.RowsUsed().Skip(1).ToList();
            Assert.Equal(2, rows.Count); // سطرا B فقط
            Assert.All(rows, r => Assert.Equal($"{ApdTestData.Marker} مدرسة B", r.Cell(5).GetString()));

            var injected = rows.Single(r => r.Cell(3).GetString() == "معمل القياس").Cell(4);
            Assert.False(injected.HasFormula);
            Assert.StartsWith("=HYPERLINK", injected.GetString());
        }
    }
}
