using Microsoft.EntityFrameworkCore;
using Moq;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>
    /// RTK-S3: النشر والاستهداف على InMemory (الفهرس الفريد المُصفّى لا يُفرض هنا — يُغطّى على SQL Server الحقيقي في RTK-S7.3).
    /// </summary>
    public class RemedialTrackPublicationServiceTests
    {
        private static readonly DateTime FixedNow = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        private static readonly RemedialTrackActor Actor = new("admin-1", "مدير");

        private sealed class FixedTime : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(FixedNow, TimeSpan.Zero);
        }

        // توقيت السعودية = UTC+3 ثابت
        private sealed class FakeTz : ITimeZoneService
        {
            public DateTime GetNowUtc() => FixedNow;
            public DateTime GetNowSaudi() => FixedNow.AddHours(3);
            public DateTime ConvertToSaudi(DateTime utcTime) => utcTime.AddHours(3);
            public DateTime ConvertToUtc(DateTime saudiTime) => DateTime.SpecifyKind(saudiTime.AddHours(-3), DateTimeKind.Utc);
        }

        private sealed class Fixture
        {
            public TestDbContextFactory Factory { get; } = new(Guid.NewGuid().ToString());
            public Mock<INotificationService> Notifications { get; } = new();
            public Mock<IAdminActivityLogger> Activity { get; } = new();
            public Mock<IRemedialTrackCodeGenerator> Codes { get; } = new();
            public RemedialTrackPublicationService Sut { get; }

            public int TrackId { get; private set; }
            public int DraftTrackId { get; private set; }
            public int BatchId { get; private set; }
            public int OtherBatchId { get; private set; }
            public int[] BatchStudents { get; private set; } = Array.Empty<int>();
            public int OutsideStudent { get; private set; }
            public int[] AxisIds { get; private set; } = Array.Empty<int>();

            private int _codeSeq = 100000;

            public Fixture()
            {
                Codes.Setup(c => c.NewAccessCode()).Returns(() => (_codeSeq++).ToString());
                Sut = new RemedialTrackPublicationService(
                    Factory, new FixedTime(), Codes.Object, new FakeTz(),
                    Notifications.Object, Activity.Object,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<RemedialTrackPublicationService>.Instance);
            }

            public async Task SeedAsync(int studentsInBatch = 4)
            {
                await using var db = Factory.CreateDbContext();

                var cur = new Curriculum { Title = "قدرات", Description = "d", CurriculumTypeName = "t" };
                db.Curriculums.Add(cur);
                await db.SaveChangesAsync();

                var track = new RemedialTrack
                {
                    Code = "RTK-2026-0001", Title = "خطة", CurriculumId = cur.Id, Status = RemedialTrackStatus.Ready,
                    CreatedByUserId = "admin-1", CreatedAtUtc = FixedNow
                };
                var draft = new RemedialTrack
                {
                    Code = "RTK-2026-0002", Title = "مسودة", CurriculumId = cur.Id, Status = RemedialTrackStatus.Draft,
                    CreatedByUserId = "admin-1", CreatedAtUtc = FixedNow
                };
                db.RemedialTracks.AddRange(track, draft);
                await db.SaveChangesAsync();
                TrackId = track.Id;
                DraftTrackId = draft.Id;

                var axes = Enumerable.Range(1, 3).Select(i => new RemedialTrackAxis
                {
                    TrackId = track.Id, SectionId = i, Order = i, Exam101ModelId = 1, Exam102ModelId = 2, ExamDurationMinutes = 30
                }).ToList();
                db.RemedialTrackAxes.AddRange(axes);

                var batch = new Batch { Name = "دفعة أ", IsDeleted = false, IsArchived = false };
                var other = new Batch { Name = "دفعة ب", IsDeleted = false, IsArchived = false };
                db.Batches.AddRange(batch, other);
                await db.SaveChangesAsync();
                BatchId = batch.Id;
                OtherBatchId = other.Id;
                AxisIds = axes.Select(a => a.Id).ToArray();

                var ids = new List<int>();
                for (var i = 0; i < studentsInBatch; i++)
                {
                    var s = NewStudent($"طالب {i + 1}");
                    db.Students.Add(s);
                    await db.SaveChangesAsync();
                    db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = s.StudentID, BatchId = batch.Id });
                    ids.Add(s.StudentID);
                }
                BatchStudents = ids.ToArray();

                var outside = NewStudent("خارج الدفعة");
                db.Students.Add(outside);
                await db.SaveChangesAsync();
                db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = outside.StudentID, BatchId = other.Id });
                OutsideStudent = outside.StudentID;
                await db.SaveChangesAsync();
            }

            private static Student NewStudent(string name) => new()
            {
                NationalID = "1234567890", FullName = name, Gender = "ذكر", School = "مدرسة", BranchId = 1
            };

            public CreateRemedialTrackPublicationInput Input(
                RemedialTrackPublicationScope scope = RemedialTrackPublicationScope.WholeBatch,
                RemedialTrackDeliveryMode mode = RemedialTrackDeliveryMode.Online,
                IEnumerable<int>? students = null,
                int? trackId = null)
                => new()
                {
                    TrackId = trackId ?? TrackId, BatchId = BatchId, Scope = scope, Mode = mode,
                    StudentIds = students?.ToList()
                };

            public Task<RemedialTrackResult> PublishAsync(CreateRemedialTrackPublicationInput input, RemedialTrackBatchScope? scope = null)
                => Sut.CreateAsync(input, Actor, scope ?? RemedialTrackBatchScope.Unrestricted);
        }

        private static async Task<Fixture> NewAsync(int students = 4)
        {
            var f = new Fixture();
            await f.SeedAsync(students);
            return f;
        }

        // ---------------- الاستهداف ----------------

        [Fact]
        public async Task Create_WholeBatch_Online_CreatesEnrollmentsAxisProgressAndEvents_AndLocksTrack()
        {
            var f = await NewAsync();

            var r = await f.PublishAsync(f.Input());

            Assert.True(r.Success, r.Message);
            var created = Assert.IsType<RemedialTrackPublicationCreated>(r.Data);
            Assert.Equal(4, created.Enrolled);
            Assert.Equal(0, created.Skipped);

            await using var db = f.Factory.CreateDbContext();
            var pub = await db.RemedialTrackPublications.SingleAsync();
            Assert.Equal(RemedialTrackPublicationStatus.Active, pub.Status);
            Assert.Equal(4, pub.TotalStudents);
            Assert.Null(pub.AccessCode);
            Assert.Equal(FixedNow, pub.PublishAtUtc);

            Assert.Equal(4, await db.RemedialTrackEnrollments.CountAsync());
            Assert.Equal(12, await db.RemedialTrackAxisProgresses.CountAsync());
            Assert.Equal(4, await db.RemedialTrackEvents.CountAsync(e => e.Type == RemedialTrackEventType.StudentEnrolled));
            Assert.Equal(0, await db.RemedialTrackVideoProgresses.CountAsync()); // تُنشأ كسولًا في S4

            var en = await db.RemedialTrackEnrollments.Include(e => e.AxisProgresses).FirstAsync();
            Assert.Equal(RemedialTrackEnrollmentStatus.NotStarted, en.Status);
            Assert.Equal(f.AxisIds[0], en.CurrentAxisId);
            var ordered = en.AxisProgresses.OrderBy(a => a.Order).ToList();
            Assert.Equal(RemedialTrackAxisStatus.Videos, ordered[0].Status);
            Assert.Equal(1, ordered[0].Round);
            Assert.Equal(FixedNow, ordered[0].OpenedAtUtc);
            Assert.All(ordered.Skip(1), a => { Assert.Equal(RemedialTrackAxisStatus.Locked, a.Status); Assert.Null(a.OpenedAtUtc); });

            Assert.True((await db.RemedialTracks.FindAsync(f.TrackId))!.IsStructureLocked);
        }

        [Fact]
        public async Task Create_SelectedStudents_EnrollsOnlyChosen_AndDeduplicatesIds()
        {
            var f = await NewAsync();
            var chosen = new[] { f.BatchStudents[0], f.BatchStudents[2], f.BatchStudents[0] };

            var r = await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents, students: chosen));

            Assert.True(r.Success, r.Message);
            await using var db = f.Factory.CreateDbContext();
            var enrolled = await db.RemedialTrackEnrollments.Select(e => e.StudentId).ToListAsync();
            Assert.Equal(new[] { f.BatchStudents[0], f.BatchStudents[2] }.OrderBy(x => x), enrolled.OrderBy(x => x));
            Assert.Equal(2, (await db.RemedialTrackPublications.SingleAsync()).TotalStudents);
        }

        [Fact]
        public async Task Create_SelectedStudents_RejectsStudentsOutsideBatch()
        {
            var f = await NewAsync();

            var r = await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                students: new[] { f.BatchStudents[0], f.OutsideStudent }));

            Assert.False(r.Success);
            await using var db = f.Factory.CreateDbContext();
            Assert.Empty(db.RemedialTrackPublications);
            Assert.Empty(db.RemedialTrackEnrollments);
        }

        [Fact]
        public async Task Create_SelectedStudents_RequiresAtLeastOne()
        {
            var f = await NewAsync();
            var r = await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents, students: Array.Empty<int>()));
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Create_SelectedStudents_RejectsMoreThanMax()
        {
            var f = await NewAsync();
            var tooMany = Enumerable.Range(1, RemedialTrackPublicationService.MaxStudentsPerPublication + 1);

            var r = await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents, students: tooMany));

            Assert.False(r.Success);
            Assert.Contains("1000", r.Message);
        }

        [Fact]
        public async Task Create_WholeBatch_RejectsWhenBatchExceedsMax()
        {
            var f = await NewAsync(RemedialTrackPublicationService.MaxStudentsPerPublication + 1);

            var r = await f.PublishAsync(f.Input());

            Assert.False(r.Success);
            await using var db = f.Factory.CreateDbContext();
            Assert.Empty(db.RemedialTrackPublications);
        }

        [Fact]
        public async Task Create_EmptyBatch_IsRejected()
        {
            var f = await NewAsync(0);
            var r = await f.PublishAsync(f.Input());
            Assert.False(r.Success);
        }

        // ---------------- التكرار ----------------

        [Fact]
        public async Task Create_SamePlanTwice_SkipsActiveDuplicates_AndRejectsWhenNothingNew()
        {
            var f = await NewAsync();
            Assert.True((await f.PublishAsync(f.Input())).Success);

            var again = await f.PublishAsync(f.Input());

            Assert.False(again.Success);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(1, await db.RemedialTrackPublications.CountAsync());
            Assert.Equal(4, await db.RemedialTrackEnrollments.CountAsync());
        }

        [Fact]
        public async Task Create_PartialDuplicates_ReportsSkippedCount()
        {
            var f = await NewAsync();
            Assert.True((await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                students: f.BatchStudents.Take(2)))).Success);

            var r = await f.PublishAsync(f.Input());

            Assert.True(r.Success, r.Message);
            var created = Assert.IsType<RemedialTrackPublicationCreated>(r.Data);
            Assert.Equal(2, created.Enrolled);
            Assert.Equal(2, created.Skipped);
            Assert.Contains("تخطي 2", r.Message);
        }

        [Fact]
        public async Task Create_CompletedOrCancelledEnrollment_IsNotTreatedAsDuplicate()
        {
            var f = await NewAsync(2);
            Assert.True((await f.PublishAsync(f.Input())).Success);

            await using (var db = f.Factory.CreateDbContext())
            {
                foreach (var e in db.RemedialTrackEnrollments)
                    e.Status = RemedialTrackEnrollmentStatus.Completed;
                await db.SaveChangesAsync();
            }

            var r = await f.PublishAsync(f.Input());

            Assert.True(r.Success, r.Message);
            Assert.Equal(2, ((RemedialTrackPublicationCreated)r.Data!).Enrolled);
        }

        // ---------------- التحقق من الخطة والدفعة والوقت ----------------

        [Fact]
        public async Task Create_RejectsTrackThatIsNotReady()
        {
            var f = await NewAsync();
            var r = await f.PublishAsync(f.Input(trackId: f.DraftTrackId));
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Create_RejectsArchivedBatch()
        {
            var f = await NewAsync();
            await using (var db = f.Factory.CreateDbContext())
            {
                (await db.Batches.FindAsync(f.BatchId))!.IsArchived = true;
                await db.SaveChangesAsync();
            }

            var r = await f.PublishAsync(f.Input());

            Assert.False(r.Success);
        }

        [Fact]
        public async Task Create_RejectsUndefinedEnums()
        {
            var f = await NewAsync();
            var input = f.Input();
            input.Mode = (RemedialTrackDeliveryMode)99;
            Assert.False((await f.PublishAsync(input)).Success);

            input = f.Input();
            input.Scope = (RemedialTrackPublicationScope)99;
            Assert.False((await f.PublishAsync(input)).Success);
        }

        [Fact]
        public async Task Create_PublishAtLocal_IsConvertedToUtc()
        {
            var f = await NewAsync();
            var input = f.Input();
            input.PublishAtLocal = new DateTime(2026, 10, 5, 18, 30, 0); // السعودية → 15:30 UTC

            var r = await f.PublishAsync(input);

            Assert.True(r.Success, r.Message);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(new DateTime(2026, 10, 5, 15, 30, 0), (await db.RemedialTrackPublications.SingleAsync()).PublishAtUtc);
        }

        [Fact]
        public async Task Create_PublishAtOlderThanOneHour_IsRejected_ButRecentPastIsAccepted()
        {
            var f = await NewAsync();

            var old = f.Input();
            old.PublishAtLocal = FixedNow.AddHours(3).AddHours(-2); // قبل ساعتين
            Assert.False((await f.PublishAsync(old)).Success);

            var recent = f.Input();
            recent.PublishAtLocal = FixedNow.AddHours(3).AddMinutes(-30);
            Assert.True((await f.PublishAsync(recent)).Success);
        }

        // ---------------- الصلاحية على الدفعة (IDOR) ----------------

        [Fact]
        public async Task Create_BatchOutsideUserScope_IsRejected_AndNothingIsWritten()
        {
            var f = await NewAsync();
            var scope = new RemedialTrackBatchScope(new HashSet<int> { f.OtherBatchId });

            var r = await f.PublishAsync(f.Input(), scope);

            Assert.False(r.Success);
            await using var db = f.Factory.CreateDbContext();
            Assert.Empty(db.RemedialTrackPublications);
            Assert.False((await db.RemedialTracks.FindAsync(f.TrackId))!.IsStructureLocked);
        }

        [Fact]
        public async Task StudentsOfBatch_OutsideScope_ReturnsNull_InsideScope_ReturnsPage()
        {
            var f = await NewAsync();

            Assert.Null(await f.Sut.GetStudentsOfBatchAsync(f.BatchId, null, 1,
                new RemedialTrackBatchScope(new HashSet<int> { f.OtherBatchId })));

            var page = await f.Sut.GetStudentsOfBatchAsync(f.BatchId, null, 1, RemedialTrackBatchScope.Unrestricted);
            Assert.NotNull(page);
            Assert.Equal(4, page!.Total);
        }

        [Fact]
        public async Task StudentsOfBatch_SearchFiltersByName()
        {
            var f = await NewAsync();
            var page = await f.Sut.GetStudentsOfBatchAsync(f.BatchId, "طالب 2", 1, RemedialTrackBatchScope.Unrestricted);
            Assert.Single(page!.Items);
        }

        [Fact]
        public async Task PublishForm_FiltersBatchesByScope_AndListsOnlyReadyTracks()
        {
            var f = await NewAsync();

            var vm = await f.Sut.GetPublishFormAsync(f.TrackId, new RemedialTrackBatchScope(new HashSet<int> { f.BatchId }));

            Assert.Single(vm.Batches);
            Assert.Equal(f.BatchId, vm.Batches[0].Id);
            Assert.Single(vm.Tracks);
            Assert.Equal(f.TrackId, vm.Input.TrackId);
        }

        [Fact]
        public async Task Index_RestrictedUser_SeesOnlyPermittedBatches()
        {
            var f = await NewAsync();
            Assert.True((await f.PublishAsync(f.Input())).Success);

            var none = await f.Sut.GetIndexAsync(1, new RemedialTrackBatchScope(new HashSet<int> { f.OtherBatchId }));
            var all = await f.Sut.GetIndexAsync(1, RemedialTrackBatchScope.Unrestricted);

            Assert.Empty(none.Items);
            Assert.Single(all.Items);
        }

        // ---------------- الرقم المرجعي ----------------

        [Fact]
        public async Task Create_InPerson_GeneratesSixDigitCode_Version1()
        {
            var f = await NewAsync();

            var r = await f.PublishAsync(f.Input(mode: RemedialTrackDeliveryMode.InPerson));

            Assert.True(r.Success, r.Message);
            await using var db = f.Factory.CreateDbContext();
            var pub = await db.RemedialTrackPublications.SingleAsync();
            Assert.Matches(@"^\d{6}$", pub.AccessCode);
            Assert.Equal(1, pub.CodeVersion);
            Assert.Equal(FixedNow, pub.CodeGeneratedAtUtc);
        }

        [Fact]
        public async Task Create_InPerson_RegeneratesWhenGeneratedCodeIsAlreadyActive()
        {
            var f = await NewAsync();
            var seq = new Queue<string>(new[] { "111111", "111111", "222222" });
            f.Codes.Reset();
            f.Codes.Setup(c => c.NewAccessCode()).Returns(() => seq.Dequeue());

            Assert.True((await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                RemedialTrackDeliveryMode.InPerson, f.BatchStudents.Take(2)))).Success);
            Assert.True((await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                RemedialTrackDeliveryMode.InPerson, f.BatchStudents.Skip(2)))).Success);

            await using var db = f.Factory.CreateDbContext();
            var codes = await db.RemedialTrackPublications.OrderBy(p => p.Id).Select(p => p.AccessCode).ToListAsync();
            Assert.Equal(new[] { "111111", "222222" }, codes);
        }

        [Fact]
        public async Task Create_InPerson_CodeFreedByCancellation_CanBeReused()
        {
            var f = await NewAsync();
            var seq = new Queue<string>(new[] { "333333", "333333" });
            f.Codes.Reset();
            f.Codes.Setup(c => c.NewAccessCode()).Returns(() => seq.Dequeue());

            var first = await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                RemedialTrackDeliveryMode.InPerson, f.BatchStudents.Take(2)));
            var firstId = ((RemedialTrackPublicationCreated)first.Data!).PublicationId;
            Assert.True((await f.Sut.CancelAsync(firstId, "إلغاء للاختبار", Actor, RemedialTrackBatchScope.Unrestricted)).Success);

            var second = await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                RemedialTrackDeliveryMode.InPerson, f.BatchStudents.Skip(2)));

            Assert.True(second.Success, second.Message);
        }

        [Fact]
        public async Task RegenerateCode_BumpsVersion_AndChangesCode()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input(mode: RemedialTrackDeliveryMode.InPerson))).Data!).PublicationId;
            string oldCode;
            await using (var db = f.Factory.CreateDbContext())
                oldCode = (await db.RemedialTrackPublications.FindAsync(id))!.AccessCode!;

            var r = await f.Sut.RegenerateCodeAsync(id, Actor, RemedialTrackBatchScope.Unrestricted);

            Assert.True(r.Success, r.Message);
            await using var db2 = f.Factory.CreateDbContext();
            var pub = await db2.RemedialTrackPublications.FindAsync(id);
            Assert.Equal(2, pub!.CodeVersion);
            Assert.NotEqual(oldCode, pub.AccessCode);
            Assert.Equal(pub.AccessCode, r.Data);
            f.Activity.Verify(a => a.LogAsync("RemedialTrack.RegenerateCode", It.IsAny<string>(), "admin-1", "مدير", null, null, f.BatchId), Times.Once);
        }

        [Fact]
        public async Task RegenerateCode_OnlineCancelledOrOutOfScope_IsRejected()
        {
            var f = await NewAsync();
            var online = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                RemedialTrackDeliveryMode.Online, f.BatchStudents.Take(2)))).Data!).PublicationId;
            var inPerson = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                RemedialTrackDeliveryMode.InPerson, f.BatchStudents.Skip(2)))).Data!).PublicationId;

            Assert.False((await f.Sut.RegenerateCodeAsync(online, Actor, RemedialTrackBatchScope.Unrestricted)).Success);
            Assert.False((await f.Sut.RegenerateCodeAsync(inPerson, Actor,
                new RemedialTrackBatchScope(new HashSet<int> { f.OtherBatchId }))).Success);

            Assert.True((await f.Sut.CancelAsync(inPerson, "إلغاء للاختبار", Actor, RemedialTrackBatchScope.Unrestricted)).Success);
            Assert.False((await f.Sut.RegenerateCodeAsync(inPerson, Actor, RemedialTrackBatchScope.Unrestricted)).Success);
        }

        [Fact]
        public async Task Details_HidesAccessCode_UnlessAllowed()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input(mode: RemedialTrackDeliveryMode.InPerson))).Data!).PublicationId;

            var hidden = await f.Sut.GetDetailsAsync(id, false, RemedialTrackBatchScope.Unrestricted);
            var shown = await f.Sut.GetDetailsAsync(id, true, RemedialTrackBatchScope.Unrestricted);
            var denied = await f.Sut.GetDetailsAsync(id, true, new RemedialTrackBatchScope(new HashSet<int> { f.OtherBatchId }));

            Assert.Null(hidden!.AccessCode);
            Assert.Matches(@"^\d{6}$", shown!.AccessCode);
            Assert.Equal(4, shown.Students.Count);
            Assert.Equal(4, shown.StatusCounts.Single().Count);
            Assert.Null(denied);
        }

        // ---------------- RTK-S9.1: الحذف الناعم (D26) ----------------

        private static readonly RemedialTrackBatchScope All = RemedialTrackBatchScope.Unrestricted;

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        [InlineData("abcd")]
        public async Task Delete_RequiresReasonOfAtLeastFiveChars(string? reason)
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input())).Data!).PublicationId;

            Assert.False((await f.Sut.DeleteAsync(id, reason, Actor, All)).Success);
            Assert.False((await f.Sut.DeleteAsync(id, new string('x', 301), Actor, All)).Success);
            await using var db = f.Factory.CreateDbContext();
            Assert.False((await db.RemedialTrackPublications.AsNoTracking().SingleAsync()).IsDeleted);
        }

        [Fact]
        public async Task Delete_HidesFromIndex_KeepsEnrollmentsAndProgress_RecordsActor_AndLogs()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input())).Data!).PublicationId;

            var r = await f.Sut.DeleteAsync(id, "  أُرسل بالخطأ  ", Actor, All);

            Assert.True(r.Success, r.Message);
            Assert.Empty((await f.Sut.GetIndexAsync(1, All)).Items);
            var deletedTab = await f.Sut.GetIndexAsync(1, All, default, deleted: true);
            var item = Assert.Single(deletedTab.Items);
            Assert.Equal("أُرسل بالخطأ", item.DeleteReason);
            Assert.Equal("مدير", item.DeletedByName);

            await using var db = f.Factory.CreateDbContext();
            var pub = await db.RemedialTrackPublications.AsNoTracking().SingleAsync();
            Assert.True(pub.IsDeleted);
            Assert.Equal("admin-1", pub.DeletedByUserId);
            Assert.Equal(FixedNow, pub.DeletedAtUtc);
            Assert.Equal(RemedialTrackPublicationStatus.Active, pub.Status);                    // لا يمس الحالة
            Assert.Equal(4, await db.RemedialTrackEnrollments.CountAsync(e => e.Status == RemedialTrackEnrollmentStatus.NotStarted));
            Assert.Equal(12, await db.RemedialTrackAxisProgresses.CountAsync());
            f.Activity.Verify(a => a.LogAsync("RemedialTrackPublicationDeleted",
                It.Is<string>(d => d.Contains($"[RTK pub:{id}]")), "admin-1", "مدير", null, null, It.IsAny<int?>()), Times.Once);
        }

        [Fact]
        public async Task Delete_IsIdempotent_AndRespectsBatchScope()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input())).Data!).PublicationId;

            Assert.False((await f.Sut.DeleteAsync(id, "سبب كافٍ", Actor, new RemedialTrackBatchScope(new HashSet<int> { f.OtherBatchId }))).Success);
            Assert.True((await f.Sut.DeleteAsync(id, "سبب كافٍ", Actor, All)).Success);
            Assert.False((await f.Sut.DeleteAsync(id, "سبب كافٍ", Actor, All)).Success);   // محذوف أصلًا
        }

        [Fact]
        public async Task Deleted_PublicationCannotBeCancelledOrCodeRegenerated()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input(mode: RemedialTrackDeliveryMode.InPerson))).Data!).PublicationId;
            await f.Sut.DeleteAsync(id, "سبب كافٍ", Actor, All);

            Assert.False((await f.Sut.CancelAsync(id, "سبب كافٍ", Actor, All)).Success);
            Assert.False((await f.Sut.RegenerateCodeAsync(id, Actor, All)).Success);
        }

        [Fact]
        public async Task Restore_ReturnsPublication_WithSameStateAndCode_AndClearsDeleteFields()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input(mode: RemedialTrackDeliveryMode.InPerson))).Data!).PublicationId;
            string codeBefore;
            await using (var db0 = f.Factory.CreateDbContext())
                codeBefore = (await db0.RemedialTrackPublications.AsNoTracking().SingleAsync()).AccessCode!;
            await f.Sut.DeleteAsync(id, "سبب كافٍ", Actor, All);

            var r = await f.Sut.RestoreAsync(id, Actor, All);

            Assert.True(r.Success, r.Message);
            Assert.Single((await f.Sut.GetIndexAsync(1, All)).Items);
            Assert.Empty((await f.Sut.GetIndexAsync(1, All, default, deleted: true)).Items);
            await using var db = f.Factory.CreateDbContext();
            var pub = await db.RemedialTrackPublications.AsNoTracking().SingleAsync();
            Assert.False(pub.IsDeleted);
            Assert.Null(pub.DeletedAtUtc);
            Assert.Null(pub.DeleteReason);
            Assert.Equal(codeBefore, pub.AccessCode);   // لا تعارض ← الرقم نفسه
            Assert.Equal(1, pub.CodeVersion);
            f.Activity.Verify(a => a.LogAsync("RemedialTrackPublicationRestored",
                It.Is<string>(d => d.Contains($"[RTK pub:{id}]")), "admin-1", "مدير", null, null, It.IsAny<int?>()), Times.Once);
        }

        [Fact]
        public async Task Restore_WhenCodeNowUsedByAnotherActivePublication_GeneratesNewCode_AndBumpsVersion()
        {
            var f = await NewAsync();
            var first = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                RemedialTrackDeliveryMode.InPerson, f.BatchStudents.Take(2)))).Data!).PublicationId;
            await f.Sut.DeleteAsync(first, "سبب كافٍ", Actor, All);

            // الرقم المحرَّر يستعمله أمر آخر نشط (محاكاة بإعطائه رقم الأمر المحذوف نفسه)
            var second = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input(RemedialTrackPublicationScope.SelectedStudents,
                RemedialTrackDeliveryMode.InPerson, f.BatchStudents.Skip(2)))).Data!).PublicationId;
            string oldCode;
            await using (var db1 = f.Factory.CreateDbContext())
            {
                oldCode = (await db1.RemedialTrackPublications.AsNoTracking().SingleAsync(p => p.Id == first)).AccessCode!;
                (await db1.RemedialTrackPublications.SingleAsync(p => p.Id == second)).AccessCode = oldCode;
                await db1.SaveChangesAsync();
            }

            var r = await f.Sut.RestoreAsync(first, Actor, All);

            Assert.True(r.Success, r.Message);
            await using var db = f.Factory.CreateDbContext();
            var restored = await db.RemedialTrackPublications.AsNoTracking().SingleAsync(p => p.Id == first);
            Assert.NotEqual(oldCode, restored.AccessCode);
            Assert.Equal(2, restored.CodeVersion);
            Assert.Equal(oldCode, (await db.RemedialTrackPublications.AsNoTracking().SingleAsync(p => p.Id == second)).AccessCode);
        }

        [Fact]
        public async Task Restore_NotDeleted_OrOutOfScope_IsRejected()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input())).Data!).PublicationId;

            Assert.False((await f.Sut.RestoreAsync(id, Actor, All)).Success);   // غير محذوف
            await f.Sut.DeleteAsync(id, "سبب كافٍ", Actor, All);
            Assert.False((await f.Sut.RestoreAsync(id, Actor, new RemedialTrackBatchScope(new HashSet<int> { f.OtherBatchId }))).Success);
        }

        [Fact]
        public async Task Details_ForDeletedPublication_ShowsDeleteInfo_AndNoLeakOfRecentChangesFromOthers()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input())).Data!).PublicationId;
            await f.Sut.DeleteAsync(id, "سبب كافٍ", Actor, All);

            var vm = await f.Sut.GetDetailsAsync(id, false, All);

            Assert.NotNull(vm);
            Assert.True(vm!.IsDeleted);
            Assert.Equal("سبب كافٍ", vm.DeleteReason);
            Assert.False(vm.IsActive);
        }

        // ---------------- RTK-S9.3: آخر التعديلات ----------------

        [Fact]
        public async Task Details_RecentChanges_FilterByPublicationOrTrackTag_Only()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input())).Data!).PublicationId;
            await using (var db = f.Factory.CreateDbContext())
            {
                db.AdminActivityLogs.AddRange(
                    new AdminActivityLog { AdminId = "a", AdminName = "مدير", ActionType = "RemedialTrackPublicationDeleted", Description = $"[RTK pub:{id}] حذف", Timestamp = FixedNow.AddMinutes(1) },
                    new AdminActivityLog { AdminId = "a", AdminName = "مدير", ActionType = "RemedialTrackVideoEdited", Description = $"[RTK track:{f.TrackId}] تعديل فيديو", Timestamp = FixedNow.AddMinutes(2) },
                    new AdminActivityLog { AdminId = "a", AdminName = "مدير", ActionType = "RemedialTrackPublicationDeleted", Description = $"[RTK pub:{id + 100}] أمر آخر", Timestamp = FixedNow.AddMinutes(3) },
                    new AdminActivityLog { AdminId = "a", AdminName = "مدير", ActionType = "RemedialTrackVideoEdited", Description = $"[RTK track:{f.TrackId + 100}] خطة أخرى", Timestamp = FixedNow.AddMinutes(4) },
                    new AdminActivityLog { AdminId = "a", AdminName = "مدير", ActionType = "OtherAction", Description = $"[RTK pub:{id}] ليس RTK", Timestamp = FixedNow.AddMinutes(5) });
                await db.SaveChangesAsync();
            }

            var vm = await f.Sut.GetDetailsAsync(id, false, All);

            Assert.Equal(2, vm!.RecentChanges.Count);
            Assert.Equal("RemedialTrackVideoEdited", vm.RecentChanges[0].ActionType);       // الأحدث أولًا
            Assert.Equal("تعديل فيديو", vm.RecentChanges[0].ActionLabel);
            Assert.DoesNotContain("[RTK", vm.RecentChanges[0].Description);
            Assert.Equal("حذف", vm.RecentChanges[1].Description);
        }

        // ---------------- الإلغاء ----------------

        [Fact]
        public async Task Cancel_StopsOpenEnrollments_KeepsFinished_WritesEvents_AndKeepsRows()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input())).Data!).PublicationId;
            await using (var db = f.Factory.CreateDbContext())
            {
                var list = await db.RemedialTrackEnrollments.OrderBy(e => e.Id).ToListAsync();
                list[0].Status = RemedialTrackEnrollmentStatus.Completed;
                list[1].Status = RemedialTrackEnrollmentStatus.InProgress;
                await db.SaveChangesAsync();
            }

            var r = await f.Sut.CancelAsync(id, "  تم النشر بالخطأ  ", Actor, RemedialTrackBatchScope.Unrestricted);

            Assert.True(r.Success, r.Message);
            await using var check = f.Factory.CreateDbContext();
            var pub = await check.RemedialTrackPublications.SingleAsync();
            Assert.Equal(RemedialTrackPublicationStatus.Cancelled, pub.Status);
            Assert.Equal(FixedNow, pub.CancelledAtUtc);
            Assert.Equal("تم النشر بالخطأ", pub.CancelReason);

            var statuses = await check.RemedialTrackEnrollments.OrderBy(e => e.Id).Select(e => e.Status).ToListAsync();
            Assert.Equal(RemedialTrackEnrollmentStatus.Completed, statuses[0]);
            Assert.All(statuses.Skip(1), s => Assert.Equal(RemedialTrackEnrollmentStatus.Cancelled, s));
            Assert.Equal(3, await check.RemedialTrackEvents.CountAsync(e => e.Type == RemedialTrackEventType.EnrollmentCancelled));
            Assert.Equal(4, await check.RemedialTrackEnrollments.CountAsync()); // لا حذف فعلي
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("قصير")]
        public async Task Cancel_RequiresReasonOfAtLeastFiveChars(string? reason)
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input())).Data!).PublicationId;

            var r = await f.Sut.CancelAsync(id, reason, Actor, RemedialTrackBatchScope.Unrestricted);

            Assert.False(r.Success);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(RemedialTrackPublicationStatus.Active, (await db.RemedialTrackPublications.SingleAsync()).Status);
        }

        [Fact]
        public async Task Cancel_RejectsReasonLongerThan300_TwiceCancel_AndOutOfScope()
        {
            var f = await NewAsync();
            var id = ((RemedialTrackPublicationCreated)(await f.PublishAsync(f.Input())).Data!).PublicationId;

            Assert.False((await f.Sut.CancelAsync(id, new string('x', 301), Actor, RemedialTrackBatchScope.Unrestricted)).Success);
            Assert.False((await f.Sut.CancelAsync(id, "سبب كافٍ", Actor, new RemedialTrackBatchScope(new HashSet<int> { f.OtherBatchId }))).Success);
            Assert.True((await f.Sut.CancelAsync(id, "سبب كافٍ", Actor, RemedialTrackBatchScope.Unrestricted)).Success);
            Assert.False((await f.Sut.CancelAsync(id, "سبب كافٍ", Actor, RemedialTrackBatchScope.Unrestricted)).Success);
        }

        // ---------------- الإشعارات وسجل النشاط ----------------

        [Fact]
        public async Task Create_SendsNotification_WithoutAccessCode_AndLogsActivity()
        {
            var f = await NewAsync();
            string? message = null;
            List<int>? ids = null;
            f.Notifications
                .Setup(n => n.SendToStudentsAsync(It.IsAny<List<int>>(), It.IsAny<string>(), NotificationCategory.Remedial, "/Students/RemedialTrack", null))
                .Callback<List<int>, string, NotificationCategory, string?, int?>((i, m, _, _, _) => { ids = i; message = m; })
                .Returns(Task.CompletedTask);

            var r = await f.PublishAsync(f.Input(mode: RemedialTrackDeliveryMode.InPerson));

            Assert.True(r.Success, r.Message);
            Assert.NotNull(message);
            Assert.Equal(4, ids!.Count);
            Assert.Contains("خطة", message);
            Assert.Contains("حضوري", message);
            Assert.Contains("2026/10/04 15:00", message); // 12:00 UTC → 15:00 السعودية
            await using var db = f.Factory.CreateDbContext();
            var code = (await db.RemedialTrackPublications.SingleAsync()).AccessCode!;
            Assert.DoesNotContain(code, message);
            f.Activity.Verify(a => a.LogAsync("RemedialTrack.Publish", It.IsAny<string>(), "admin-1", "مدير", null, null, f.BatchId), Times.Once);
        }

        [Fact]
        public async Task Create_NotificationOrActivityFailure_DoesNotInvalidatePublication()
        {
            var f = await NewAsync();
            f.Notifications
                .Setup(n => n.SendToStudentsAsync(It.IsAny<List<int>>(), It.IsAny<string>(), It.IsAny<NotificationCategory>(), It.IsAny<string?>(), It.IsAny<int?>()))
                .ThrowsAsync(new InvalidOperationException("boom"));
            f.Activity
                .Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            var r = await f.PublishAsync(f.Input());

            Assert.True(r.Success, r.Message);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(1, await db.RemedialTrackPublications.CountAsync());
        }

        [Fact]
        public async Task Create_Failure_DoesNotNotify()
        {
            var f = await NewAsync();

            var r = await f.PublishAsync(f.Input(trackId: f.DraftTrackId));

            Assert.False(r.Success);
            f.Notifications.Verify(n => n.SendToStudentsAsync(It.IsAny<List<int>>(), It.IsAny<string>(), It.IsAny<NotificationCategory>(), It.IsAny<string?>(), It.IsAny<int?>()), Times.Never);
        }
    }
}
