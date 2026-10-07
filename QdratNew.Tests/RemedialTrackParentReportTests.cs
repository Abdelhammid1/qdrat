using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>
    /// RTK-S11: إثراء التقرير، لقطة ولي الأمر والإنشاء التلقائي، صفحة ولي الأمر (ملكية/إقرار)، والنصوص.
    /// InMemory: الفهرس الفريد وRowVersion والتزامن الحقيقي لا تُفرض هنا — تُغطّى على SQL Server الحقيقي في RTK-S12.3.
    /// </summary>
    public class RemedialTrackParentReportTests
    {
        private static readonly DateTime T0 = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        private sealed class FixedTime : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(T0, TimeSpan.Zero);
        }

        private sealed class FakeTz : ITimeZoneService
        {
            public DateTime GetNowUtc() => T0;
            public DateTime GetNowSaudi() => T0.AddHours(3);
            public DateTime ConvertToSaudi(DateTime utcTime) => utcTime.AddHours(3);
            public DateTime ConvertToUtc(DateTime saudiTime) => saudiTime.AddHours(-3);
        }

        private sealed class Fx
        {
            public TestDbContextFactory Factory { get; } = new(Guid.NewGuid().ToString());
            public Mock<IAdminActivityLogger> Activity { get; } = new();
            public Mock<INotificationService> Notifications { get; } = new();
            public RemedialTrackProgressService Progress { get; }
            public RemedialTrackParentReportService Parents { get; }
            public RemedialTrackReportService Reports { get; }

            public int ParentA { get; private set; }
            public int ParentB { get; private set; }
            public const string ParentAUser = "parent-a-user";
            public const string ParentBUser = "parent-b-user";
            public int StudentWithParent { get; private set; }
            public int StudentNoParent { get; private set; }
            public int EnrollWithParent { get; private set; }
            public int EnrollNoParent { get; private set; }
            public int PublicationId { get; private set; }
            public int[] ApWithParent { get; private set; } = Array.Empty<int>();
            public int[] ApNoParent { get; private set; } = Array.Empty<int>();

            public Fx()
            {
                Progress = new RemedialTrackProgressService(Factory, new FixedTime(), new FakeTz(), NullLogger<RemedialTrackProgressService>.Instance);
                Parents = new RemedialTrackParentReportService(Factory, new FixedTime(), Activity.Object, NullLogger<RemedialTrackParentReportService>.Instance);
                Reports = new RemedialTrackReportService(Factory, new FixedTime(), Progress, Notifications.Object, Activity.Object,
                    NullLogger<RemedialTrackReportService>.Instance);
            }

            /// <param name="axis1">حالة المحور الأول للطالبين (الثاني مغلق افتراضيًا).</param>
            public async Task SeedAsync(
                bool autoSend = true,
                RemedialTrackAxisStatus axis1 = RemedialTrackAxisStatus.AwaitingExam102,
                RemedialTrackAxisStatus axis2 = RemedialTrackAxisStatus.Locked)
            {
                await using var db = Factory.CreateDbContext();

                var cur = new Curriculum { Title = "قدرات", Description = "d", CurriculumTypeName = "t" };
                db.Curriculums.Add(cur);
                var batch = new Batch { Name = "دفعة أ" };
                db.Batches.Add(batch);
                var pA = new Parent { FullName = "ولي أمر أ", NationalID = "1", Email = "a@x.com", PhoneNumber = "0500000001", UserId = ParentAUser };
                var pB = new Parent { FullName = "ولي أمر ب", NationalID = "2", Email = "b@x.com", PhoneNumber = "0500000002", UserId = ParentBUser };
                db.Parents.AddRange(pA, pB);
                await db.SaveChangesAsync();
                ParentA = pA.ParentID; ParentB = pB.ParentID;

                var s1 = new Student { NationalID = "1234567890", FullName = "أحمد", Gender = "ذكر", School = "م", BranchId = 1, ParentId = pA.ParentID };
                var s2 = new Student { NationalID = "1234567891", FullName = "بدر", Gender = "ذكر", School = "م", BranchId = 1 };
                db.Students.AddRange(s1, s2);

                var track = new RemedialTrack
                {
                    Code = "RTK-2026-0001", Title = "خطة علاجية", CurriculumId = cur.Id, Status = RemedialTrackStatus.Ready,
                    PassPercent = 60, MinWatchPercent = 90, IsStructureLocked = true, CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-3)
                };
                db.RemedialTracks.Add(track);
                await db.SaveChangesAsync();
                StudentWithParent = s1.StudentID; StudentNoParent = s2.StudentID;

                var axes = new List<RemedialTrackAxis>();
                for (var i = 1; i <= 2; i++)
                {
                    var sec = new Section { Title = $"قسم {i}", CurriculumId = cur.Id };
                    db.Sections.Add(sec);
                    await db.SaveChangesAsync();
                    axes.Add(new RemedialTrackAxis
                    {
                        TrackId = track.Id, SectionId = sec.Id, Order = i, TitleOverride = $"محور {i}",
                        Exam101ModelId = 1, Exam102ModelId = 2, ExamDurationMinutes = 30
                    });
                }
                db.RemedialTrackAxes.AddRange(axes);
                await db.SaveChangesAsync();
                foreach (var a in axes)
                    db.RemedialTrackVideos.AddRange(
                        new RemedialTrackVideo { AxisId = a.Id, Order = 1, Title = "v1", Url = "https://youtu.be/aaaaaaaaaaa", Provider = RemedialTrackVideoProvider.YouTube, ExternalId = "aaaaaaaaaaa", DurationSeconds = 100 },
                        new RemedialTrackVideo { AxisId = a.Id, Order = 2, Title = "v2", Url = "https://youtu.be/bbbbbbbbbbb", Provider = RemedialTrackVideoProvider.YouTube, ExternalId = "bbbbbbbbbbb", DurationSeconds = 100 });

                var pub = new RemedialTrackPublication
                {
                    TrackId = track.Id, BatchId = batch.Id, Scope = RemedialTrackPublicationScope.WholeBatch,
                    Mode = RemedialTrackDeliveryMode.Online, PublishAtUtc = T0.AddHours(-1), CodeVersion = 1,
                    CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-2), TotalStudents = 2,
                    AutoSendParentReports = autoSend
                };
                db.RemedialTrackPublications.Add(pub);
                await db.SaveChangesAsync();
                PublicationId = pub.Id;

                RemedialTrackEnrollment Enr(int studentId)
                {
                    var e = new RemedialTrackEnrollment
                    {
                        PublicationId = pub.Id, TrackId = track.Id, StudentId = studentId,
                        Status = RemedialTrackEnrollmentStatus.InProgress, CurrentAxisId = axes[0].Id, CreatedAtUtc = T0.AddDays(-2)
                    };
                    e.AxisProgresses.Add(new RemedialTrackAxisProgress
                    {
                        AxisId = axes[0].Id, Order = 1, Status = axis1, Round = 2,
                        Exam101Percent = 40, PassedAtUtc = axis1 == RemedialTrackAxisStatus.Passed ? T0 : null
                    });
                    e.AxisProgresses.Add(new RemedialTrackAxisProgress { AxisId = axes[1].Id, Order = 2, Status = axis2, Round = 1 });
                    db.RemedialTrackEnrollments.Add(e);
                    return e;
                }
                var e1 = Enr(s1.StudentID);
                var e2 = Enr(s2.StudentID);
                await db.SaveChangesAsync();
                EnrollWithParent = e1.Id; EnrollNoParent = e2.Id;
                ApWithParent = e1.AxisProgresses.OrderBy(a => a.Order).Select(a => a.Id).ToArray();
                ApNoParent = e2.AxisProgresses.OrderBy(a => a.Order).Select(a => a.Id).ToArray();
            }

            /// <summary>محاولة مُسلَّمة جاهزة لـ OnExamSubmittedAsync.</summary>
            public async Task<int> AddAttemptAsync(int apId, RemedialTrackExamNumber exam, double percent, bool passed)
            {
                await using var db = Factory.CreateDbContext();
                var att = new RemedialTrackExamAttempt
                {
                    AxisProgressId = apId, ExamNumber = exam, ModelId = 1, Status = RemedialTrackAttemptStatus.Submitted,
                    StartedAtUtc = T0.AddMinutes(-12), ExpiresAtUtc = T0.AddMinutes(18), SubmittedAtUtc = T0,
                    TotalQuestions = 10, CorrectCount = (int)Math.Round(percent / 10), ScorePercent = percent, IsPassed = passed
                };
                db.RemedialTrackExamAttempts.Add(att);
                await db.SaveChangesAsync();
                return att.Id;
            }

            public async Task AddVideoProgressAsync(int apId, int round, double watchedSeconds, bool completed)
            {
                await using var db = Factory.CreateDbContext();
                var videoIds = await db.RemedialTrackVideos.Where(v => v.Axis!.TrackId > 0 && v.Axis.Order == 1)
                    .OrderBy(v => v.Order).Select(v => v.Id).ToListAsync();
                foreach (var vid in videoIds)
                    db.RemedialTrackVideoProgresses.Add(new RemedialTrackVideoProgress
                    {
                        AxisProgressId = apId, VideoId = vid, VideoOrder = 1, Round = round,
                        WatchedSeconds = watchedSeconds, DurationSeconds = 100, IsCompleted = completed
                    });
                await db.SaveChangesAsync();
            }

            public async Task<List<RemedialTrackParentReport>> ReportsAsync()
            {
                await using var db = Factory.CreateDbContext();
                return await db.RemedialTrackParentReports.AsNoTracking().OrderBy(r => r.Id).ToListAsync();
            }
        }

        // ===================== S11.2: الإنشاء التلقائي =====================

        [Fact]
        public async Task FailBoth_CreatesOneSentReport_NotifiesParent_WithExplicitUtcSentAt()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var attempt = await fx.AddAttemptAsync(fx.ApWithParent[0], RemedialTrackExamNumber.Exam102, 30, false);

            var r = await fx.Progress.OnExamSubmittedAsync(attempt);
            Assert.True(r.Applied);

            var reports = await fx.ReportsAsync();
            var report = Assert.Single(reports);
            Assert.Equal(RemedialTrackParentReportKind.AxisNotPassed, report.Kind);
            Assert.Equal(RemedialTrackParentReportStatus.Sent, report.Status);
            Assert.Equal(fx.ParentA, report.ParentId);
            Assert.Equal(fx.ApWithParent[0], report.AxisProgressId);
            Assert.Equal(T0, report.SentAtUtc);
            Assert.Null(report.SentByUserId);

            await using var db = fx.Factory.CreateDbContext();
            var n = Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());
            Assert.Equal(Fx.ParentAUser, n.UserId);
            Assert.Equal(fx.ParentA, n.ParentID);
            Assert.Equal(T0, n.SentAt);                                   // R6: UTC صريح
            Assert.Equal(NotificationCategory.Remedial, n.Category);
            Assert.Equal($"/Parents/RemedialTrackReports/Details?id={report.Id}", n.TargetUrl);
        }

        [Fact]
        public async Task Snapshot_ContainsNoInternalIdsOrNotes_AndKeepsFocusAxis()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await using (var db = fx.Factory.CreateDbContext())
            {
                var e = await db.RemedialTrackEnrollments.FirstAsync(x => x.Id == fx.EnrollWithParent);
                e.AdminReportNote = "ملاحظة-إدارية-سرية";
                await db.SaveChangesAsync();
            }
            var attempt = await fx.AddAttemptAsync(fx.ApWithParent[0], RemedialTrackExamNumber.Exam102, 30, false);
            await fx.Progress.OnExamSubmittedAsync(attempt);

            var json = (await fx.ReportsAsync()).Single().SnapshotJson;
            Assert.DoesNotContain("EnrollmentId", json);
            Assert.DoesNotContain("AxisProgressId", json);
            Assert.DoesNotContain("StudentId", json);
            Assert.DoesNotContain("ملاحظة-إدارية-سرية", json);
            Assert.True(System.Text.Encoding.UTF8.GetByteCount(json) <= RemedialTrackParentReportWriter.MaxSnapshotBytes);

            var snap = RemedialTrackParentReportWriter.TryDeserialize(json);
            Assert.NotNull(snap);
            Assert.Equal("أحمد", snap!.StudentName);
            Assert.Equal("محور 1", snap.FocusAxisTitle);
            Assert.Equal(1, snap.FocusAxisOrder);
        }

        [Fact]
        public async Task FailBoth_StudentWithoutParent_NoParentStatus_NoNotification()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var attempt = await fx.AddAttemptAsync(fx.ApNoParent[0], RemedialTrackExamNumber.Exam102, 30, false);

            await fx.Progress.OnExamSubmittedAsync(attempt);

            var report = Assert.Single(await fx.ReportsAsync());
            Assert.Equal(RemedialTrackParentReportStatus.NoParent, report.Status);
            Assert.Null(report.ParentId);
            Assert.Null(report.SentAtUtc);
            await using var db = fx.Factory.CreateDbContext();
            Assert.Empty(await db.Notifications.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task FailBoth_AutoSendOff_StaysPending_NoNotification()
        {
            var fx = new Fx(); await fx.SeedAsync(autoSend: false);
            var attempt = await fx.AddAttemptAsync(fx.ApWithParent[0], RemedialTrackExamNumber.Exam102, 30, false);

            await fx.Progress.OnExamSubmittedAsync(attempt);

            var report = Assert.Single(await fx.ReportsAsync());
            Assert.Equal(RemedialTrackParentReportStatus.Pending, report.Status);
            Assert.Equal(fx.ParentA, report.ParentId);
            await using var db = fx.Factory.CreateDbContext();
            Assert.Empty(await db.Notifications.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task SubmittingSameAttemptTwice_DoesNotDuplicateReportOrNotification()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var attempt = await fx.AddAttemptAsync(fx.ApWithParent[0], RemedialTrackExamNumber.Exam102, 30, false);

            await fx.Progress.OnExamSubmittedAsync(attempt);
            var second = await fx.Progress.OnExamSubmittedAsync(attempt);

            Assert.False(second.Applied);
            Assert.Single(await fx.ReportsAsync());
            await using var db = fx.Factory.CreateDbContext();
            Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());
        }

        [Fact]
        public async Task WriterAddAsync_CalledTwiceForSameKey_KeepsOneRow()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await using var db = fx.Factory.CreateDbContext();
            var log = NullLogger.Instance;

            await RemedialTrackParentReportWriter.AddAsync(db, fx.EnrollWithParent, fx.ApWithParent[0], 1, RemedialTrackParentReportKind.AxisNotPassed, T0, log, default);
            await RemedialTrackParentReportWriter.AddAsync(db, fx.EnrollWithParent, fx.ApWithParent[0], 1, RemedialTrackParentReportKind.AxisNotPassed, T0, log, default);

            Assert.Single(await fx.ReportsAsync());
        }

        [Fact]
        public async Task PassFirstAttempt_CreatesNoNotPassedReport()
        {
            var fx = new Fx(); await fx.SeedAsync(axis1: RemedialTrackAxisStatus.AwaitingExam101);
            var attempt = await fx.AddAttemptAsync(fx.ApWithParent[0], RemedialTrackExamNumber.Exam101, 90, true);

            await fx.Progress.OnExamSubmittedAsync(attempt);

            Assert.Empty(await fx.ReportsAsync());
        }

        [Fact]
        public async Task CompletingLastAxis_CreatesSingleFinalReport_WithNullAxis()
        {
            var fx = new Fx();
            await fx.SeedAsync(axis1: RemedialTrackAxisStatus.Passed, axis2: RemedialTrackAxisStatus.AwaitingExam101);
            var attempt = await fx.AddAttemptAsync(fx.ApWithParent[1], RemedialTrackExamNumber.Exam101, 90, true);

            await fx.Progress.OnExamSubmittedAsync(attempt);

            var report = Assert.Single(await fx.ReportsAsync());
            Assert.Equal(RemedialTrackParentReportKind.Final, report.Kind);
            Assert.Null(report.AxisProgressId);
            Assert.Equal(RemedialTrackParentReportStatus.Sent, report.Status);
            var snap = RemedialTrackParentReportWriter.TryDeserialize(report.SnapshotJson)!;
            Assert.True(snap.IsFinished);
            Assert.Equal(2, snap.PassedAxes);
        }

        [Fact]
        public async Task AdminOpenNext_SendsParentNoticeLinkedToNotPassedReport()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var attempt = await fx.AddAttemptAsync(fx.ApWithParent[0], RemedialTrackExamNumber.Exam102, 30, false);
            await fx.Progress.OnExamSubmittedAsync(attempt);
            var report = (await fx.ReportsAsync()).Single();

            var r = await fx.Progress.AdminOpenNextAsync(fx.ApWithParent[0], "قرار إداري للتجربة", new RemedialTrackActor("admin", "مدير"));
            Assert.True(r.Applied);

            await using var db = fx.Factory.CreateDbContext();
            var notices = await db.Notifications.AsNoTracking().OrderBy(n => n.NotificationId).ToListAsync();
            Assert.Equal(2, notices.Count);
            var notice = notices[1];
            Assert.Equal(Fx.ParentAUser, notice.UserId);
            Assert.Contains("بقرار الإدارة", notice.Message);
            Assert.Contains("بحاجة لمتابعتكم", notice.Message);
            Assert.Equal($"/Parents/RemedialTrackReports/Details?id={report.Id}", notice.TargetUrl);
            Assert.Single(await fx.ReportsAsync());   // لا تقرير تابع في هذا الإصدار
        }

        // ===================== S11.1: إثراء التقرير =====================

        [Fact]
        public async Task EnrichedReport_HasMinutesAttemptsPathAndRecommendation()
        {
            var fx = new Fx(); await fx.SeedAsync();
            await fx.AddVideoProgressAsync(fx.ApWithParent[0], round: 1, watchedSeconds: 100, completed: true);   // 2 فيديو × 100ث
            await fx.AddVideoProgressAsync(fx.ApWithParent[0], round: 2, watchedSeconds: 25, completed: false);   // 2 × 25ث
            await fx.AddAttemptAsync(fx.ApWithParent[0], RemedialTrackExamNumber.Exam101, 40, false);
            var a102 = await fx.AddAttemptAsync(fx.ApWithParent[0], RemedialTrackExamNumber.Exam102, 30, false);
            await fx.Progress.OnExamSubmittedAsync(a102);

            var vm = await fx.Reports.GetParentReportAsync(fx.EnrollWithParent, RemedialTrackBatchScope.Unrestricted);
            Assert.NotNull(vm);
            var axis = vm!.Axes.First();

            Assert.Equal(3, axis.WatchedMinutesRound1);                 // 200ث ← 3.33 د ← 3
            Assert.Equal(1, axis.WatchedMinutesRound2);                 // 50ث ← 0.83 د ← 1
            Assert.Equal(3, axis.VideoMinutes);                         // 200ث
            Assert.Equal(2, axis.VideosDoneRound1);
            Assert.Equal(RemedialTrackPathKind.FailedBoth, axis.PathKind);
            Assert.Equal(2, axis.Attempts.Count);
            Assert.Equal(RemedialTrackExamNumber.Exam101, axis.Attempts[0].ExamNumber);
            Assert.Equal(12, axis.Attempts[0].DurationMinutes);         // 12 دقيقة من البدء للتسليم
            Assert.Contains("لم يجتز الطالب المحور في الاختبارين", axis.Recommendation);
            Assert.Contains("أقل من المطلوب", axis.Recommendation);     // 4 د مشاهدة من 6 د مطلوبة (جولتان)
            Assert.Equal(60, vm.PassPercent);
        }

        [Fact]
        public async Task EnrichedReport_NotReachedAxis_HasEmptyRecommendation()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var vm = await fx.Reports.GetParentReportAsync(fx.EnrollWithParent, RemedialTrackBatchScope.Unrestricted);
            var locked = vm!.Axes.Single(a => a.Order == 2);
            Assert.Equal(RemedialTrackPathKind.NotStarted, locked.PathKind);
            Assert.Equal(string.Empty, locked.Recommendation);
            Assert.Empty(locked.Attempts);
        }

        // ===================== S11.3: صفحة ولي الأمر =====================

        private static async Task<int> SeedSentReportAsync(Fx fx)
        {
            var attempt = await fx.AddAttemptAsync(fx.ApWithParent[0], RemedialTrackExamNumber.Exam102, 30, false);
            await fx.Progress.OnExamSubmittedAsync(attempt);
            return (await fx.ReportsAsync()).Single().Id;
        }

        [Fact]
        public async Task Parent_SeesOwnSentReport_InListAndDetails()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var id = await SeedSentReportAsync(fx);

            var list = await fx.Parents.GetListAsync(fx.ParentA, 1);
            var item = Assert.Single(list.Items);
            Assert.Equal(id, item.Id);
            Assert.Equal("أحمد", item.StudentName);
            Assert.Equal("محور 1", item.FocusAxisTitle);

            var details = await fx.Parents.GetDetailsAsync(fx.ParentA, id);
            Assert.NotNull(details);
            Assert.Equal("أحمد", details!.Snapshot.StudentName);
        }

        [Fact]
        public async Task Parent_Idor_OtherParentGetsNotFound_ForDetailsAndAcknowledge()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var id = await SeedSentReportAsync(fx);

            Assert.Null(await fx.Parents.GetDetailsAsync(fx.ParentB, id));
            Assert.Empty((await fx.Parents.GetListAsync(fx.ParentB, 1)).Items);
            var ack = await fx.Parents.AcknowledgeAsync(fx.ParentB, id, "u", "n");
            Assert.False(ack.Found);

            Assert.Null((await fx.ReportsAsync()).Single().AcknowledgedAtUtc);
        }

        [Fact]
        public async Task Parent_PendingAndSuppressedReports_AreHidden()
        {
            var fx = new Fx(); await fx.SeedAsync(autoSend: false);
            var id = await SeedSentReportAsync(fx);   // Pending
            Assert.Null(await fx.Parents.GetDetailsAsync(fx.ParentA, id));

            await using (var db = fx.Factory.CreateDbContext())
            {
                var r = await db.RemedialTrackParentReports.FirstAsync(x => x.Id == id);
                r.Status = RemedialTrackParentReportStatus.Suppressed;
                await db.SaveChangesAsync();
            }
            Assert.Null(await fx.Parents.GetDetailsAsync(fx.ParentA, id));
            Assert.Empty((await fx.Parents.GetListAsync(fx.ParentA, 1)).Items);
        }

        [Fact]
        public async Task Parent_DeletedPublication_HidesReport()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var id = await SeedSentReportAsync(fx);
            await using (var db = fx.Factory.CreateDbContext())
            {
                var p = await db.RemedialTrackPublications.FirstAsync(x => x.Id == fx.PublicationId);
                p.IsDeleted = true;
                await db.SaveChangesAsync();
            }
            Assert.Null(await fx.Parents.GetDetailsAsync(fx.ParentA, id));
            Assert.Empty((await fx.Parents.GetListAsync(fx.ParentA, 1)).Items);
        }

        [Fact]
        public async Task Parent_StudentReassignedToAnotherParent_OldParentLosesAccess()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var id = await SeedSentReportAsync(fx);
            await using (var db = fx.Factory.CreateDbContext())
            {
                var s = await db.Students.FirstAsync(x => x.StudentID == fx.StudentWithParent);
                s.ParentId = fx.ParentB;
                await db.SaveChangesAsync();
            }
            Assert.Null(await fx.Parents.GetDetailsAsync(fx.ParentA, id));
        }

        [Fact]
        public async Task Acknowledge_IsRecordedOnce_AndIdempotent()
        {
            var fx = new Fx(); await fx.SeedAsync();
            var id = await SeedSentReportAsync(fx);

            var first = await fx.Parents.AcknowledgeAsync(fx.ParentA, id, "parent-a-user", "ولي أمر أ");
            var second = await fx.Parents.AcknowledgeAsync(fx.ParentA, id, "parent-a-user", "ولي أمر أ");

            Assert.True(first.Found); Assert.False(first.AlreadyAcknowledged);
            Assert.True(second.Found); Assert.True(second.AlreadyAcknowledged);
            Assert.Equal(first.AcknowledgedAtUtc, second.AcknowledgedAtUtc);
            Assert.Equal(T0, (await fx.ReportsAsync()).Single().AcknowledgedAtUtc);
            fx.Activity.Verify(a => a.LogAsync("RemedialTrack.ParentAcknowledged", It.IsAny<string>(), "parent-a-user", "ولي أمر أ",
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Once);
        }

        // ===================== S11.1/S11.4: دوال نقية =====================

        [Theory]
        [InlineData(RemedialTrackPathKind.PassedFirstExam, 90, 60, 10, 10, "من الاختبار الأول")]
        [InlineData(RemedialTrackPathKind.PassedAfterRewatch, 70, 60, 10, 10, "بعد إعادة المشاهدة")]
        [InlineData(RemedialTrackPathKind.FailedBoth, 30, 60, 3, 10, "أقل من المطلوب")]
        [InlineData(RemedialTrackPathKind.FailedBoth, 30, 60, 10, 10, "جلسة مراجعة مع المعلم")]
        [InlineData(RemedialTrackPathKind.FailedBoth, 30, 60, 8, 10, "جلسة مراجعة مع المعلم")]   // 80% بالضبط = كافية
        public void Recommendations_ByPath(RemedialTrackPathKind kind, int score, int mark, int watched, int video, string expected)
            => Assert.Contains(expected, RemedialTrackRecommendations.For(kind, score, mark, watched, video));

        [Theory]
        [InlineData(RemedialTrackPathKind.NotStarted)]
        [InlineData(RemedialTrackPathKind.InProgress)]
        public void Recommendations_EmptyWhenNotFinished(RemedialTrackPathKind kind)
            => Assert.Equal(string.Empty, RemedialTrackRecommendations.For(kind, null, 60, 0, 0));

        [Fact]
        public void Recommendations_FailedBoth_ShowsScoreAgainstPassMark_AndNeverComparesStudents()
        {
            var text = RemedialTrackRecommendations.For(RemedialTrackPathKind.FailedBoth, 35, 60, 2, 10);
            Assert.Contains("35%", text);
            Assert.Contains("60%", text);
            Assert.DoesNotContain("زملاء", text);
            Assert.DoesNotContain("ضمان", text);
        }

        [Theory]
        [InlineData(RemedialTrackAxisStatus.Locked, null, null, RemedialTrackPathKind.NotStarted)]
        [InlineData(RemedialTrackAxisStatus.Videos, null, null, RemedialTrackPathKind.InProgress)]
        [InlineData(RemedialTrackAxisStatus.Rewatch, 40.0, null, RemedialTrackPathKind.InProgress)]
        [InlineData(RemedialTrackAxisStatus.Passed, 80.0, null, RemedialTrackPathKind.PassedFirstExam)]
        [InlineData(RemedialTrackAxisStatus.Passed, 40.0, 70.0, RemedialTrackPathKind.PassedAfterRewatch)]
        [InlineData(RemedialTrackAxisStatus.FailedBlocked, 40.0, 30.0, RemedialTrackPathKind.FailedBoth)]
        [InlineData(RemedialTrackAxisStatus.FailedOpenedByAdmin, 40.0, 30.0, RemedialTrackPathKind.FailedBoth)]
        public void PathOf_MapsStatusAndScores(RemedialTrackAxisStatus status, double? e101, double? e102, RemedialTrackPathKind expected)
            => Assert.Equal(expected, RemedialTrackRecommendations.PathOf(status, e101, e102));

        [Theory]
        [InlineData(200, 200, 0, 3)]     // 3.33 ← 3
        [InlineData(500, 200, 0, 3)]     // مقيّدة بمدة الفيديوهات
        [InlineData(500, 0, 120, 2)]     // لا مدة معتمدة ← مدة التقدّم المسجّلة
        [InlineData(90, 0, 0, 2)]        // بلا تقييد
        [InlineData(-5, 100, 0, 0)]      // لا قيم سالبة
        public void MinutesOf_RoundsAndCaps(double watched, int axisSeconds, int progressSeconds, int expected)
            => Assert.Equal(expected, RemedialTrackReportBuilder.MinutesOf(watched, axisSeconds, progressSeconds));

        [Fact]
        public void ParentCopy_AxisNotPassed_StatesParentResponsibilityExplicitly()
        {
            var body = RemedialTrackParentCopy.AxisNotPassedBody("المحور س");
            Assert.Contains("المحور س", body);
            Assert.Contains("في الاختبارين", body);
            Assert.Contains("بقرار من إدارة المعهد", body);
            Assert.Contains("دون اجتياز اختباره", body);
            Assert.Contains("تقع المتابعة على عاتق ولي الأمر", body);
        }

        [Fact]
        public void ParentCopy_Final_WithFollowUp_KeepsResponsibility_AndWithoutItDoesNot()
        {
            var withFollow = RemedialTrackParentCopy.FinalBody(3, 5, 2);
            Assert.Contains("3 من 5", withFollow);
            Assert.Contains("المتابعة على عاتق ولي الأمر", withFollow);

            var clean = RemedialTrackParentCopy.FinalBody(5, 5, 0);
            Assert.DoesNotContain("على عاتق ولي الأمر", clean);
        }

        [Fact]
        public void ParentCopy_AdminOpenedNotice_MentionsAdminDecisionAndPreviousAxis()
        {
            var text = RemedialTrackParentCopy.AdminOpenedNextNotice("أحمد", "محور 1");
            Assert.Contains("بقرار الإدارة", text);
            Assert.Contains("محور 1", text);
            Assert.Contains("بحاجة لمتابعتكم", text);
        }

        [Fact]
        public void ParentCopy_EveryKindHasTitle_AndFooterMentionsParent()
        {
            foreach (var k in Enum.GetValues<RemedialTrackParentReportKind>())
            {
                Assert.False(string.IsNullOrWhiteSpace(RemedialTrackParentCopy.Title(k)));
                Assert.Contains("أحمد", RemedialTrackParentCopy.NotificationMessage(k, "أحمد", "محور 1"));
            }
            Assert.Contains("ولي الأمر", RemedialTrackParentCopy.ResponsibilityFooter);
        }

        [Fact]
        public void Snapshot_Serialize_DropsAttemptsWhenTooLarge_ThenRejectsIfStillTooLarge()
        {
            var snap = new RemedialTrackParentReportSnapshot { StudentName = "أحمد" };
            snap.Axes.Add(new RemedialTrackParentReportSnapshotAxis
            {
                Title = "م",
                Attempts = Enumerable.Range(0, 3000).Select(i => new RemedialTrackParentReportSnapshotAttempt { ScorePercent = i }).ToList()
            });
            var json = RemedialTrackParentReportWriter.Serialize(snap);
            Assert.NotNull(json);
            Assert.Empty(snap.Axes[0].Attempts);

            var huge = new RemedialTrackParentReportSnapshot { StudentName = new string('أ', 40_000) };
            Assert.Null(RemedialTrackParentReportWriter.Serialize(huge));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("{}")]
        [InlineData("not json")]
        public void Snapshot_TryDeserialize_ReturnsNullForEmptyOrCorrupt(string? json)
            => Assert.Null(RemedialTrackParentReportWriter.TryDeserialize(json));

        [Fact]
        public void Snapshot_Serialize_KeepsArabicUnescaped_AndHtmlIsStoredAsPlainTextForRazorEncoding()
        {
            var snap = new RemedialTrackParentReportSnapshot { StudentName = "<script>alert(1)</script> أحمد" };
            var json = RemedialTrackParentReportWriter.Serialize(snap)!;
            Assert.Contains("أحمد", json);                     // لا \uXXXX
            var back = RemedialTrackParentReportWriter.TryDeserialize(json)!;
            Assert.Equal("<script>alert(1)</script> أحمد", back.StudentName);   // يُرمَّز عند العرض فقط (Razor)
        }
    }
}
