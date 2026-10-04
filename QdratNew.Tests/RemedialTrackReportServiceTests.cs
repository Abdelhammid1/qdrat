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
    /// RTK-S6: خدمة التقارير (تقرير الطالب، لوحة المتابعة، فتح التالي، تقرير ولي الأمر/الدفعة).
    /// InMemory: لا يفرض RowVersion/ExecuteUpdate — تغطيتها الحقيقية في RTK-S7.3.
    /// </summary>
    public class RemedialTrackReportServiceTests
    {
        private static readonly DateTime T0 = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        private static readonly RemedialTrackActor Admin = new("admin-1", "مدير");

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
            public Mock<INotificationService> Notifications { get; } = new();
            public Mock<IAdminActivityLogger> Activity { get; } = new();
            public RemedialTrackProgressService Progress { get; }
            public RemedialTrackReportService Sut { get; }

            public int BatchId { get; private set; }
            public int PublicationId { get; private set; }
            public int OtherPublicationId { get; private set; }
            public int StudentA { get; private set; }   // محور 1 FailedBlocked
            public int StudentB { get; private set; }   // محور 1 Passed، محور 2 Videos
            public int StudentC { get; private set; }   // محور 1 FailedOpenedByAdmin
            public int StudentD { get; private set; }   // محور 2 (الأخير) FailedBlocked
            public int EnrollA { get; private set; }
            public int EnrollB { get; private set; }
            public int EnrollC { get; private set; }
            public int EnrollD { get; private set; }
            public int OtherPubEnroll { get; private set; }
            public int BlockedApA { get; private set; }
            public int LastBlockedApD { get; private set; }

            public Fx()
            {
                Progress = new RemedialTrackProgressService(Factory, new FixedTime(), new FakeTz(), NullLogger<RemedialTrackProgressService>.Instance);
                Sut = new RemedialTrackReportService(Factory, new FixedTime(), Progress, Notifications.Object, Activity.Object,
                    NullLogger<RemedialTrackReportService>.Instance);
            }

            private static Student NewStudent(string name) => new()
            {
                NationalID = "1234567890", FullName = name, Gender = "ذكر", School = "مدرسة", BranchId = 1
            };

            public async Task SeedAsync()
            {
                await using var db = Factory.CreateDbContext();
                var cur = new Curriculum { Title = "قدرات", Description = "d", CurriculumTypeName = "t" };
                db.Curriculums.Add(cur);
                var batch = new Batch { Name = "دفعة أ" };
                var otherBatch = new Batch { Name = "دفعة ب" };
                db.Batches.AddRange(batch, otherBatch);
                await db.SaveChangesAsync();
                BatchId = batch.Id;

                var track = new RemedialTrack
                {
                    Code = "RTK-2026-0001", Title = "خطة علاجية", CurriculumId = cur.Id, Status = RemedialTrackStatus.Ready,
                    PassPercent = 60, MinWatchPercent = 90, IsStructureLocked = true, CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-3)
                };
                db.RemedialTracks.Add(track);
                await db.SaveChangesAsync();

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

                RemedialTrackPublication Pub(int batchId) => new()
                {
                    TrackId = track.Id, BatchId = batchId, Scope = RemedialTrackPublicationScope.WholeBatch,
                    Mode = RemedialTrackDeliveryMode.Online, PublishAtUtc = T0.AddHours(-1), CodeVersion = 1,
                    CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-2), TotalStudents = 4
                };
                var pub = Pub(batch.Id);
                var otherPub = Pub(otherBatch.Id);
                db.RemedialTrackPublications.AddRange(pub, otherPub);

                var students = new[] { NewStudent("أحمد"), NewStudent("بدر"), NewStudent("جاسم"), NewStudent("دانة"), NewStudent("خارجي") };
                db.Students.AddRange(students);
                await db.SaveChangesAsync();
                PublicationId = pub.Id;
                OtherPublicationId = otherPub.Id;
                StudentA = students[0].StudentID; StudentB = students[1].StudentID;
                StudentC = students[2].StudentID; StudentD = students[3].StudentID;

                RemedialTrackEnrollment Enr(RemedialTrackPublication p, int studentId, RemedialTrackEnrollmentStatus st, params RemedialTrackAxisStatus[] axisStatuses)
                {
                    var e = new RemedialTrackEnrollment
                    {
                        PublicationId = p.Id, TrackId = track.Id, StudentId = studentId, Status = st,
                        CurrentAxisId = axes[0].Id, CreatedAtUtc = T0.AddDays(-2)
                    };
                    for (var i = 0; i < axes.Count; i++)
                        e.AxisProgresses.Add(new RemedialTrackAxisProgress
                        {
                            AxisId = axes[i].Id, Order = axes[i].Order, Status = axisStatuses[i], Round = 2,
                            Exam101Percent = axisStatuses[i] == RemedialTrackAxisStatus.Locked ? null : 40,
                            Exam102Percent = axisStatuses[i] is RemedialTrackAxisStatus.FailedBlocked or RemedialTrackAxisStatus.FailedOpenedByAdmin ? 30 : null
                        });
                    db.RemedialTrackEnrollments.Add(e);
                    return e;
                }

                var ea = Enr(pub, StudentA, RemedialTrackEnrollmentStatus.InProgress, RemedialTrackAxisStatus.FailedBlocked, RemedialTrackAxisStatus.Locked);
                var eb = Enr(pub, StudentB, RemedialTrackEnrollmentStatus.InProgress, RemedialTrackAxisStatus.Passed, RemedialTrackAxisStatus.Videos);
                var ec = Enr(pub, StudentC, RemedialTrackEnrollmentStatus.InProgress, RemedialTrackAxisStatus.FailedOpenedByAdmin, RemedialTrackAxisStatus.Videos);
                var ed = Enr(pub, StudentD, RemedialTrackEnrollmentStatus.CompletedWithFailures, RemedialTrackAxisStatus.Passed, RemedialTrackAxisStatus.FailedBlocked);
                var eo = Enr(otherPub, students[4].StudentID, RemedialTrackEnrollmentStatus.InProgress, RemedialTrackAxisStatus.Videos, RemedialTrackAxisStatus.Locked);
                await db.SaveChangesAsync();
                EnrollA = ea.Id; EnrollB = eb.Id; EnrollC = ec.Id; EnrollD = ed.Id; OtherPubEnroll = eo.Id;
                BlockedApA = ea.AxisProgresses.First(a => a.Order == 1).Id;
                LastBlockedApD = ed.AxisProgresses.First(a => a.Order == 2).Id;
            }
        }

        private static async Task<Fx> NewAsync() { var f = new Fx(); await f.SeedAsync(); return f; }

        // ───────────── تقرير الطالب ─────────────

        [Fact]
        public async Task StudentReport_Owner_ShowsAllAxesAndOutcomes()
        {
            var f = await NewAsync();
            var r = await f.Sut.GetStudentReportAsync(f.StudentC, f.EnrollC);
            Assert.NotNull(r);
            Assert.Equal(2, r!.TotalAxes);
            Assert.Equal(RemedialTrackReportAxisOutcome.MovedByAdmin, r.Axes[0].Outcome);
            Assert.Equal(RemedialTrackReportAxisOutcome.InProgress, r.Axes[1].Outcome);
            Assert.Equal(1, r.MovedByAdminAxes);
            Assert.Null(r.AdminNote);   // لا ملاحظة أدمن في تقرير الطالب
        }

        [Fact]
        public async Task StudentReport_OtherStudent_ReturnsNull_Idor()
        {
            var f = await NewAsync();
            Assert.Null(await f.Sut.GetStudentReportAsync(f.StudentB, f.EnrollA));
        }

        [Fact]
        public async Task StudentReport_CancelledEnrollment_ReturnsNull()
        {
            var f = await NewAsync();
            await using (var db = f.Factory.CreateDbContext())
            {
                (await db.RemedialTrackEnrollments.FirstAsync(e => e.Id == f.EnrollA)).Status = RemedialTrackEnrollmentStatus.Cancelled;
                await db.SaveChangesAsync();
            }
            Assert.Null(await f.Sut.GetStudentReportAsync(f.StudentA, f.EnrollA));
        }

        // ───────────── لوحة المتابعة ─────────────

        [Fact]
        public async Task Dashboard_Kpis_AreCorrect()
        {
            var f = await NewAsync();
            var d = await f.Sut.GetDashboardAsync(f.PublicationId, new RemedialTrackDashboardFilter(), RemedialTrackBatchScope.Unrestricted);
            Assert.NotNull(d);
            Assert.Equal(4, d!.Kpis.Total);
            Assert.Equal(3, d.Kpis.InProgress);
            Assert.Equal(1, d.Kpis.CompletedWithFailures);
            Assert.Equal(1, d.Kpis.BlockedAwaitingAdmin);   // A فقط؛ D محجوب في آخر محور فلا يُحتسب
            Assert.Equal(4, d.Students.Count);
            Assert.Equal(3, d.NotPassed.Count);             // A + C + D
        }

        [Fact]
        public async Task Dashboard_BlockedOnly_ReturnsOnlyUnlockableStudent()
        {
            var f = await NewAsync();
            var d = await f.Sut.GetDashboardAsync(f.PublicationId, new RemedialTrackDashboardFilter { BlockedOnly = true }, RemedialTrackBatchScope.Unrestricted);
            var only = Assert.Single(d!.Students);
            Assert.Equal(f.EnrollA, only.EnrollmentId);
            Assert.Equal(f.BlockedApA, only.BlockedAxisProgressId);
        }

        [Fact]
        public async Task Dashboard_SearchAndStatusFilters()
        {
            var f = await NewAsync();
            var byName = await f.Sut.GetDashboardAsync(f.PublicationId, new RemedialTrackDashboardFilter { Q = "بدر" }, RemedialTrackBatchScope.Unrestricted);
            Assert.Single(byName!.Students);
            var byStatus = await f.Sut.GetDashboardAsync(f.PublicationId, new RemedialTrackDashboardFilter { Status = RemedialTrackEnrollmentStatus.CompletedWithFailures }, RemedialTrackBatchScope.Unrestricted);
            Assert.Single(byStatus!.Students);
        }

        [Fact]
        public async Task Dashboard_OutOfScopeBatch_ReturnsNull()
        {
            var f = await NewAsync();
            var scope = new RemedialTrackBatchScope(new HashSet<int> { f.BatchId + 999 });
            Assert.Null(await f.Sut.GetDashboardAsync(f.PublicationId, new RemedialTrackDashboardFilter(), scope));
        }

        [Fact]
        public async Task Dashboard_AxisChart_CountsNotPassed()
        {
            var f = await NewAsync();
            var d = await f.Sut.GetDashboardAsync(f.PublicationId, new RemedialTrackDashboardFilter(), RemedialTrackBatchScope.Unrestricted);
            var axis1 = d!.AxisChart.Single(r => r.Order == 1);
            Assert.Equal(2, axis1.NotPassed);   // A (محجوب) + C (نُقل)
            Assert.Equal(2, axis1.PassedFirstTime + axis1.PassedAfterRewatch);   // B + D
        }

        // ───────────── فتح المحور التالي ─────────────

        [Fact]
        public async Task Unlock_Success_OpensNext_Notifies_AndLogs()
        {
            var f = await NewAsync();
            var r = await f.Sut.UnlockNextAxisAsync(f.EnrollA, f.BlockedApA, "قرار إداري بعد التواصل مع ولي الأمر", Admin, RemedialTrackBatchScope.Unrestricted);
            Assert.True(r.Success, r.Message);

            await using var db = f.Factory.CreateDbContext();
            var aps = await db.RemedialTrackAxisProgresses.Where(a => a.EnrollmentId == f.EnrollA).OrderBy(a => a.Order).ToListAsync();
            Assert.Equal(RemedialTrackAxisStatus.FailedOpenedByAdmin, aps[0].Status);
            Assert.Equal(RemedialTrackAxisStatus.Videos, aps[1].Status);
            f.Notifications.Verify(n => n.SendToStudentAsync(f.StudentA, It.Is<string>(m => m.Contains("محور 2")), NotificationCategory.Remedial, It.IsAny<string?>(), It.IsAny<int?>()), Times.Once);
            f.Activity.Verify(a => a.LogAsync("RemedialTrack.UnlockNext", It.IsAny<string>(), "admin-1", "مدير", null, f.StudentA, f.BatchId), Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("قصير")]
        public async Task Unlock_ReasonTooShortOrMissing_Fails(string? reason)
        {
            var f = await NewAsync();
            var r = await f.Sut.UnlockNextAxisAsync(f.EnrollA, f.BlockedApA, reason, Admin, RemedialTrackBatchScope.Unrestricted);
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Unlock_ReasonTooLong_Fails()
        {
            var f = await NewAsync();
            var r = await f.Sut.UnlockNextAxisAsync(f.EnrollA, f.BlockedApA, new string('س', 301), Admin, RemedialTrackBatchScope.Unrestricted);
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Unlock_NotBlockedAxis_Fails()
        {
            var f = await NewAsync();
            await using var db = f.Factory.CreateDbContext();
            var passedAp = await db.RemedialTrackAxisProgresses.Where(a => a.EnrollmentId == f.EnrollB && a.Order == 1).Select(a => a.Id).FirstAsync();
            var r = await f.Sut.UnlockNextAxisAsync(f.EnrollB, passedAp, "سبب كافٍ للاختبار هنا", Admin, RemedialTrackBatchScope.Unrestricted);
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Unlock_AxisBelongsToAnotherEnrollment_Fails()
        {
            var f = await NewAsync();
            var r = await f.Sut.UnlockNextAxisAsync(f.EnrollB, f.BlockedApA, "سبب كافٍ للاختبار هنا", Admin, RemedialTrackBatchScope.Unrestricted);
            Assert.False(r.Success);
            f.Notifications.Verify(n => n.SendToStudentAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<NotificationCategory>(), It.IsAny<string?>(), It.IsAny<int?>()), Times.Never);
        }

        [Fact]
        public async Task Unlock_OutOfScope_Fails()
        {
            var f = await NewAsync();
            var scope = new RemedialTrackBatchScope(new HashSet<int> { f.BatchId + 999 });
            var r = await f.Sut.UnlockNextAxisAsync(f.EnrollA, f.BlockedApA, "سبب كافٍ للاختبار هنا", Admin, scope);
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Unlock_LastAxis_Fails_NoNextAxis()
        {
            var f = await NewAsync();
            var r = await f.Sut.UnlockNextAxisAsync(f.EnrollD, f.LastBlockedApD, "سبب كافٍ للاختبار هنا", Admin, RemedialTrackBatchScope.Unrestricted);
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Unlock_Twice_SecondIsRejected()
        {
            var f = await NewAsync();
            Assert.True((await f.Sut.UnlockNextAxisAsync(f.EnrollA, f.BlockedApA, "سبب كافٍ للاختبار هنا", Admin, RemedialTrackBatchScope.Unrestricted)).Success);
            Assert.False((await f.Sut.UnlockNextAxisAsync(f.EnrollA, f.BlockedApA, "سبب كافٍ للاختبار هنا", Admin, RemedialTrackBatchScope.Unrestricted)).Success);
        }

        [Fact]
        public async Task Unlock_CancelledPublication_Fails()
        {
            var f = await NewAsync();
            await using (var db = f.Factory.CreateDbContext())
            {
                (await db.RemedialTrackPublications.FirstAsync(p => p.Id == f.PublicationId)).Status = RemedialTrackPublicationStatus.Cancelled;
                await db.SaveChangesAsync();
            }
            Assert.False((await f.Sut.UnlockNextAxisAsync(f.EnrollA, f.BlockedApA, "سبب كافٍ للاختبار هنا", Admin, RemedialTrackBatchScope.Unrestricted)).Success);
        }

        // ───────────── تقرير ولي الأمر + الملاحظة + الدفعة ─────────────

        [Fact]
        public async Task ParentReport_IncludesNote_AndRespectsScope()
        {
            var f = await NewAsync();
            Assert.True((await f.Sut.SaveReportNoteAsync(f.EnrollA, "  ملاحظة مهمة  ", Admin, RemedialTrackBatchScope.Unrestricted)).Success);

            var r = await f.Sut.GetParentReportAsync(f.EnrollA, RemedialTrackBatchScope.Unrestricted);
            Assert.Equal("ملاحظة مهمة", r!.AdminNote);

            var outside = new RemedialTrackBatchScope(new HashSet<int> { f.BatchId + 999 });
            Assert.Null(await f.Sut.GetParentReportAsync(f.EnrollA, outside));
        }

        [Fact]
        public async Task SaveNote_TooLong_Fails_AndWritesEvent_OnSuccess()
        {
            var f = await NewAsync();
            Assert.False((await f.Sut.SaveReportNoteAsync(f.EnrollA, new string('x', 2001), Admin, RemedialTrackBatchScope.Unrestricted)).Success);
            Assert.True((await f.Sut.SaveReportNoteAsync(f.EnrollA, "ok", Admin, RemedialTrackBatchScope.Unrestricted)).Success);

            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(1, await db.RemedialTrackEvents.CountAsync(e => e.EnrollmentId == f.EnrollA && e.Type == RemedialTrackEventType.ReportNoteSaved));
        }

        [Fact]
        public async Task SaveNote_OutOfScope_Fails()
        {
            var f = await NewAsync();
            var outside = new RemedialTrackBatchScope(new HashSet<int> { f.BatchId + 999 });
            Assert.False((await f.Sut.SaveReportNoteAsync(f.EnrollA, "x", Admin, outside)).Success);
        }

        [Fact]
        public async Task BatchReport_ContainsOnlyThisPublicationStudents()
        {
            var f = await NewAsync();
            var r = await f.Sut.GetBatchReportAsync(f.PublicationId, RemedialTrackBatchScope.Unrestricted);
            Assert.Equal(4, r!.Rows.Count);
            Assert.DoesNotContain(r.Rows, x => x.StudentName == "خارجي");
            Assert.Equal(2, r.TotalAxes);

            var outside = new RemedialTrackBatchScope(new HashSet<int> { f.BatchId + 999 });
            Assert.Null(await f.Sut.GetBatchReportAsync(f.PublicationId, outside));
        }
    }
}
