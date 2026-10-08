using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Security.Permissions;
using QdratNew.Services.Interfaces;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>
    /// RTK-S13 (D29–D34): ملحق المحور — الإنشاء (set-based)، النطاق/IDOR، المشاهدة بزمن الخادم، الاختبار، وأهمها:
    /// تسليم محاولة ملحق لا يغيّر أي صف AxisProgress ولا حالة التسجيل (يحرس الخطر R8 في OnExamSubmittedAsync).
    /// InMemory: ExecuteUpdate/RowVersion/الفهارس المُصفّاة تُغطّى على SQL Server الحقيقي (RemedialTrackSqlServerV3Tests).
    /// </summary>
    public class RemedialTrackAddendumTests
    {
        private static readonly DateTime T0 = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        private const string Correct = "أ";
        private const string Wrong = "ب";
        private const string YouTubeUrl = "https://youtu.be/ccccccccccc";
        private const string GoodReason = "الفيديو الأصلي أُهمل عند إنشاء الخطة";

        private sealed class MutableTime : TimeProvider
        {
            public DateTime Now { get; set; } = T0;
            public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
        }

        private sealed class FakeTz : ITimeZoneService
        {
            public DateTime GetNowUtc() => T0;
            public DateTime GetNowSaudi() => T0.AddHours(3);
            public DateTime ConvertToSaudi(DateTime utcTime) => utcTime.AddHours(3);
            public DateTime ConvertToUtc(DateTime saudiTime) => saudiTime.AddHours(-3);
        }

        private static readonly RemedialTrackActor Actor = new("admin-1", "مدير");
        private static readonly RemedialTrackBatchScope All = RemedialTrackBatchScope.Unrestricted;

        private sealed class Fx
        {
            public TestDbContextFactory Factory { get; } = new(Guid.NewGuid().ToString());
            public MutableTime Clock { get; } = new();
            public Mock<INotificationService> Notifications { get; } = new();
            public Mock<IAdminActivityLogger> Activity { get; } = new();
            public RemedialTrackProgressService Progress { get; }
            public RemedialTrackAddendumStudentService Student { get; }
            public RemedialTrackExamService Exams { get; }
            public RemedialTrackAddendumService Admin { get; }

            // طلاب: 1،2 في الأمر الأول — 3 ملغى في الأول — 4 في أمر آخر
            public int S1, S2, S3, S4;
            public int E1, E2, E3, E4;
            public int Pub1, Pub2, TrackId;
            public int[] AxisIds = Array.Empty<int>();
            public int ModelOk, ModelEmpty, Model101, Model102;

            public Fx()
            {
                Progress = new RemedialTrackProgressService(Factory, Clock, new FakeTz(), NullLogger<RemedialTrackProgressService>.Instance);
                Student = new RemedialTrackAddendumStudentService(Factory, Clock, new FakeTz(), NullLogger<RemedialTrackAddendumStudentService>.Instance);
                Exams = new RemedialTrackExamService(Factory, Clock, Progress, Student, NullLogger<RemedialTrackExamService>.Instance);
                Admin = new RemedialTrackAddendumService(Factory, Clock, Notifications.Object, Activity.Object, NullLogger<RemedialTrackAddendumService>.Instance);
            }

            public async Task SeedAsync(RemedialTrackAxisStatus[]? axisStatuses = null, int extraStudents = 0)
            {
                axisStatuses ??= new[] { RemedialTrackAxisStatus.Passed, RemedialTrackAxisStatus.AwaitingExam102, RemedialTrackAxisStatus.Locked };
                await using var db = Factory.CreateDbContext();

                var cur = new Curriculum { Title = "قدرات", Description = "d", CurriculumTypeName = "t" };
                db.Curriculums.Add(cur);
                await db.SaveChangesAsync();

                var b1 = new Batch { Name = "دفعة أ" };
                var b2 = new Batch { Name = "دفعة ب" };
                db.Batches.AddRange(b1, b2);
                await db.SaveChangesAsync();

                var stus = new List<Student>();
                for (var i = 1; i <= 4 + extraStudents; i++)
                    stus.Add(new Student { NationalID = $"10000000{i:D2}", FullName = $"طالب {i}", Gender = "ذكر", School = "م", BranchId = 1 });
                db.Students.AddRange(stus);
                await db.SaveChangesAsync();
                S1 = stus[0].StudentID; S2 = stus[1].StudentID; S3 = stus[2].StudentID; S4 = stus[3].StudentID;

                var m101 = new ProfessionalModel { Title = "M101", Description = "d", CreatedBy = "t" };
                var m102 = new ProfessionalModel { Title = "M102", Description = "d", CreatedBy = "t" };
                var mAdd = new ProfessionalModel { Title = "MAdd", Description = "d", CreatedBy = "t" };
                var mEmpty = new ProfessionalModel { Title = "MEmpty", Description = "d", CreatedBy = "t" };
                db.ProfessionalModels.AddRange(m101, m102, mAdd, mEmpty);
                await db.SaveChangesAsync();
                Model101 = m101.Id; Model102 = m102.Id; ModelOk = mAdd.Id; ModelEmpty = mEmpty.Id;
                AddQuestions(db, cur.Id, m101.Id, 4);
                AddQuestions(db, cur.Id, m102.Id, 4);
                AddQuestions(db, cur.Id, mAdd.Id, 4);
                await db.SaveChangesAsync();

                var track = new RemedialTrack
                {
                    Code = "RTK-2026-0001", Title = "خطة علاجية", CurriculumId = cur.Id, Status = RemedialTrackStatus.Ready,
                    MinWatchPercent = 90, PassPercent = 60, IsStructureLocked = true, CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-3)
                };
                db.RemedialTracks.Add(track);
                await db.SaveChangesAsync();
                TrackId = track.Id;

                var axes = new List<RemedialTrackAxis>();
                for (var i = 1; i <= 3; i++)
                {
                    var sec = new Section { Title = $"قسم {i}", CurriculumId = cur.Id };
                    db.Sections.Add(sec);
                    await db.SaveChangesAsync();
                    axes.Add(new RemedialTrackAxis
                    {
                        TrackId = track.Id, SectionId = sec.Id, Order = i, TitleOverride = $"محور {i}",
                        Exam101ModelId = m101.Id, Exam102ModelId = m102.Id, ExamDurationMinutes = 30
                    });
                }
                db.RemedialTrackAxes.AddRange(axes);
                await db.SaveChangesAsync();
                AxisIds = axes.Select(a => a.Id).ToArray();

                var pub1 = NewPub(track.Id, b1.Id);
                var pub2 = NewPub(track.Id, b2.Id);
                db.RemedialTrackPublications.AddRange(pub1, pub2);
                await db.SaveChangesAsync();
                Pub1 = pub1.Id; Pub2 = pub2.Id;

                E1 = await AddEnrollmentAsync(db, pub1.Id, track.Id, S1, axes, axisStatuses, RemedialTrackEnrollmentStatus.InProgress);
                E2 = await AddEnrollmentAsync(db, pub1.Id, track.Id, S2, axes, axisStatuses, RemedialTrackEnrollmentStatus.InProgress);
                E3 = await AddEnrollmentAsync(db, pub1.Id, track.Id, S3, axes, axisStatuses, RemedialTrackEnrollmentStatus.Cancelled);
                E4 = await AddEnrollmentAsync(db, pub2.Id, track.Id, S4, axes, axisStatuses, RemedialTrackEnrollmentStatus.InProgress);
            }

            private static RemedialTrackPublication NewPub(int trackId, int batch) => new()
            {
                TrackId = trackId, BatchId = batch, Scope = RemedialTrackPublicationScope.WholeBatch, Mode = RemedialTrackDeliveryMode.Online,
                PublishAtUtc = T0.AddHours(-1), Status = RemedialTrackPublicationStatus.Active, CodeVersion = 1,
                CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-2), TotalStudents = 2
            };

            private static void AddQuestions(ApplicationDbContext db, int curriculumId, int modelId, int count)
            {
                for (var i = 1; i <= count; i++)
                {
                    var q = new Question
                    {
                        Id = Guid.NewGuid(), Title = $"سؤال {modelId}-{i}", ReferenceNumber = $"Q-{modelId}-{i}",
                        CurriculumId = curriculumId, LessonId = 1, CorrectAnswer = Correct,
                        Explanation = "شرح-سري", VideoUrl = "https://example.com/secret",
                        Options = new List<QuestionOption> { new() { Text = Correct }, new() { Text = Wrong }, new() { Text = "ج" }, new() { Text = "د" } }
                    };
                    db.Questions.Add(q);
                    db.ProfessionalModelQuestions.Add(new ProfessionalModelQuestion { ModelId = modelId, QuestionId = q.Id, OrderNumber = i });
                }
            }

            private static async Task<int> AddEnrollmentAsync(
                ApplicationDbContext db, int pubId, int trackId, int studentId, List<RemedialTrackAxis> axes,
                RemedialTrackAxisStatus[] statuses, RemedialTrackEnrollmentStatus status)
            {
                var e = new RemedialTrackEnrollment { PublicationId = pubId, TrackId = trackId, StudentId = studentId, CreatedAtUtc = T0.AddDays(-2), Status = status, CurrentAxisId = axes[1].Id };
                for (var i = 0; i < axes.Count; i++)
                    e.AxisProgresses.Add(new RemedialTrackAxisProgress
                    {
                        AxisId = axes[i].Id, Order = axes[i].Order, Status = statuses[i],
                        Round = statuses[i] is RemedialTrackAxisStatus.AwaitingExam102 or RemedialTrackAxisStatus.Rewatch ? 2 : 1
                    });
                db.RemedialTrackEnrollments.Add(e);
                await db.SaveChangesAsync();
                return e.Id;
            }

            // ───── اختصارات ─────

            public async Task<int> CreateAsync(
                int axisOrder = 1, int? modelId = null, bool all = true, int[]? ids = null, int pub = 0,
                string url = YouTubeUrl, int? duration = 100, string reason = GoodReason)
            {
                var r = await Admin.CreateAsync(
                    new CreateAddendumInput(pub == 0 ? Pub1 : pub, AxisIds[axisOrder - 1], "ملحق المحور", url, duration, reason,
                        modelId, modelId.HasValue ? 15 : null, all, ids),
                    Actor, All);
                Assert.True(r.Success, r.Message);
                return (int)r.Data!;
            }

            public async Task<int> ProgressIdAsync(int addendumId, int enrollmentId)
            {
                await using var db = Factory.CreateDbContext();
                return await db.RemedialTrackAddendumProgresses.Where(p => p.AddendumId == addendumId && p.EnrollmentId == enrollmentId).Select(p => p.Id).SingleAsync();
            }

            public async Task<RemedialTrackAddendumProgress> ProgressAsync(int addendumId, int enrollmentId)
            {
                await using var db = Factory.CreateDbContext();
                return await db.RemedialTrackAddendumProgresses.AsNoTracking().SingleAsync(p => p.AddendumId == addendumId && p.EnrollmentId == enrollmentId);
            }

            public async Task<RemedialTrackPingResult> PingAsync(int student, int enrollment, int progressId, string state, double duration = 100)
                => await Student.RecordPingAsync(student, new RemedialTrackAddendumPingRequest
                { EnrollmentId = enrollment, AddendumProgressId = progressId, State = state, Duration = duration });

            // مشاهدة كاملة بزمن الخادم: نبضة أولى بلا رصيد ثم 7 نبضات كل 15ث ثم ended
            public async Task WatchFullyAsync(int student, int enrollment, int addendumId)
            {
                var pid = await ProgressIdAsync(addendumId, enrollment);
                Assert.Equal(RemedialTrackPingStatus.Ok, (await PingAsync(student, enrollment, pid, "playing")).Status);
                for (var i = 0; i < 7; i++)
                {
                    Clock.Now = Clock.Now.AddSeconds(15);
                    await PingAsync(student, enrollment, pid, "playing");
                }
                Clock.Now = Clock.Now.AddSeconds(1);
                var done = await PingAsync(student, enrollment, pid, "ended");
                Assert.True(done.Body.Completed, "الفيديو لم يكتمل");
            }

            public async Task<int> StartAddendumAttemptAsync(int student, int enrollment, int addendumId)
            {
                var r = await Student.StartExamAsync(student, enrollment, addendumId);
                Assert.True(r.Status is RemedialTrackExamStartStatus.Created or RemedialTrackExamStartStatus.Existing, $"start: {r.Status} {r.Message}");
                return r.AttemptId;
            }

            public async Task SubmitAttemptAsync(int student, int attemptId, int correctCount)
            {
                await using (var db = Factory.CreateDbContext())
                {
                    var qids = await db.RemedialTrackExamAttemptQuestions.Where(x => x.AttemptId == attemptId).OrderBy(x => x.Order).Select(x => x.QuestionId).ToListAsync();
                    for (var i = 0; i < qids.Count; i++)
                    {
                        var a = await Exams.SaveAnswerAsync(student, attemptId, qids[i], i < correctCount ? Correct : Wrong);
                        Assert.Equal(RemedialTrackSaveAnswerStatus.Ok, a.Status);
                    }
                }
                var s = await Exams.SubmitAsync(student, attemptId);
                Assert.True(s.Status is RemedialTrackSubmitStatus.Submitted or RemedialTrackSubmitStatus.AlreadyClosed);
            }

            // لقطة كل ما يخص المحاور والتسجيل: يجب ألا يتغير شيء بسبب أي عمل على الملحق
            public async Task<string> AxisSnapshotAsync()
            {
                await using var db = Factory.CreateDbContext();
                var axes = await db.RemedialTrackAxisProgresses.AsNoTracking().OrderBy(a => a.Id)
                    .Select(a => $"{a.Id}|{a.Status}|{a.Round}|{a.Exam101Percent}|{a.Exam102Percent}|{a.PassedAtUtc}|{a.FailedAtUtc}").ToListAsync();
                var enr = await db.RemedialTrackEnrollments.AsNoTracking().OrderBy(e => e.Id)
                    .Select(e => $"{e.Id}|{e.Status}|{e.CurrentAxisId}|{e.CompletedAtUtc}").ToListAsync();
                var events = await db.RemedialTrackEvents.AsNoTracking().CountAsync();
                var parentReports = await db.RemedialTrackParentReports.AsNoTracking().CountAsync();
                return string.Join(";", axes) + "#" + string.Join(";", enr) + "#" + events + "#" + parentReports;
            }
        }

        private static async Task<Fx> NewAsync(RemedialTrackAxisStatus[]? statuses = null, int extra = 0)
        {
            var f = new Fx();
            await f.SeedAsync(statuses, extra);
            return f;
        }

        // ═══════════════════════════ الإنشاء ═══════════════════════════

        [Fact]
        public async Task Create_AllEnrollments_CreatesProgressForActiveOnly_AndNeverTouchesAxes()
        {
            var f = await NewAsync();
            var before = await f.AxisSnapshotAsync();

            var id = await f.CreateAsync();

            await using var db = f.Factory.CreateDbContext();
            var rows = await db.RemedialTrackAddendumProgresses.Where(p => p.AddendumId == id).Select(p => p.EnrollmentId).ToListAsync();
            Assert.Equal(new[] { f.E1, f.E2 }.OrderBy(x => x), rows.OrderBy(x => x));   // E3 ملغى، E4 أمر آخر
            Assert.Equal(before, await f.AxisSnapshotAsync());                            // D29: لا أثر على المحاور
            var a = await db.RemedialTrackAddenda.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.True(a.IsActive);
            Assert.Equal(T0, a.CreatedAtUtc);
            Assert.Equal("admin-1", a.CreatedByUserId);
            Assert.Equal(RemedialTrackVideoProvider.YouTube, a.Provider);
            Assert.Equal("ccccccccccc", a.ExternalId);
            f.Activity.Verify(x => x.LogAsync("RemedialTrackAddendumCreated", It.Is<string>(d => d.Contains($"[RTK pub:{f.Pub1}]")), "admin-1", "مدير", null, null, 1), Times.Once);
        }

        [Fact]
        public async Task Create_SelectedStudents_OnlyThose()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(all: false, ids: new[] { f.E2 });

            await using var db = f.Factory.CreateDbContext();
            var rows = await db.RemedialTrackAddendumProgresses.Where(p => p.AddendumId == id).Select(p => p.EnrollmentId).ToListAsync();
            Assert.Equal(new[] { f.E2 }, rows);
        }

        [Fact]
        public async Task Create_ForeignOrCancelledEnrollmentId_RejectsWholeRequest()
        {
            var f = await NewAsync();

            foreach (var bad in new[] { f.E4 /* أمر آخر */, f.E3 /* ملغى */, 999999 })
            {
                var r = await f.Admin.CreateAsync(
                    new CreateAddendumInput(f.Pub1, f.AxisIds[0], "ملحق", YouTubeUrl, 100, GoodReason, null, null, false, new[] { f.E1, bad }), Actor, All);
                Assert.False(r.Success);
            }

            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(0, await db.RemedialTrackAddenda.CountAsync());          // لم يُنفَّذ شيء
            Assert.Equal(0, await db.RemedialTrackAddendumProgresses.CountAsync());
        }

        [Theory]
        [InlineData("")]
        [InlineData("قصير")]                         // < 5
        public async Task Create_InvalidReason_Rejected(string reason)
        {
            var f = await NewAsync();
            var r = await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "ملحق", YouTubeUrl, 100, reason, null, null, true, null), Actor, All);
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Create_ReasonOver300_Rejected()
        {
            var f = await NewAsync();
            var r = await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "ملحق", YouTubeUrl, 100, new string('س', 301), null, null, true, null), Actor, All);
            Assert.False(r.Success);
        }

        [Theory]
        [InlineData("http://youtu.be/ccccccccccc")]    // ليس https
        [InlineData("javascript:alert(1)")]
        [InlineData("")]
        public async Task Create_BadUrl_Rejected(string url)
        {
            var f = await NewAsync();
            var r = await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "ملحق", url, 100, GoodReason, null, null, true, null), Actor, All);
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Create_OtherProvider_RequiresDuration()
        {
            var f = await NewAsync();
            var r = await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "ملحق", "https://example.com/video/1", null, GoodReason, null, null, true, null), Actor, All);
            Assert.False(r.Success);
            var ok = await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "ملحق", "https://example.com/video/1", 120, GoodReason, null, null, true, null), Actor, All);
            Assert.True(ok.Success, ok.Message);
        }

        [Fact]
        public async Task Create_ExamModel_ValidatedLikeBuilder()
        {
            var f = await NewAsync();

            // بلا أسئلة
            Assert.False((await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "م", YouTubeUrl, 100, GoodReason, f.ModelEmpty, 15, true, null), Actor, All)).Success);
            // نموذج غير موجود
            Assert.False((await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "م", YouTubeUrl, 100, GoodReason, 987654, 15, true, null), Actor, All)).Success);
            // المدة خارج 5..180 أو غائبة
            Assert.False((await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "م", YouTubeUrl, 100, GoodReason, f.ModelOk, 4, true, null), Actor, All)).Success);
            Assert.False((await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "م", YouTubeUrl, 100, GoodReason, f.ModelOk, 181, true, null), Actor, All)).Success);
            Assert.False((await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "م", YouTubeUrl, 100, GoodReason, f.ModelOk, null, true, null), Actor, All)).Success);
            // صالح
            var ok = await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "م", YouTubeUrl, 100, GoodReason, f.ModelOk, 15, true, null), Actor, All);
            Assert.True(ok.Success, ok.Message);
        }

        [Fact]
        public async Task Create_DeletedOrCancelledPublication_AndOutOfScope_AreRejected()
        {
            var f = await NewAsync();

            // خارج نطاق الدفعات ≡ غير موجود
            var scoped = new RemedialTrackBatchScope(new HashSet<int> { 99 });
            Assert.False((await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "م", YouTubeUrl, 100, GoodReason, null, null, true, null), Actor, scoped)).Success);

            await using (var db = f.Factory.CreateDbContext())
            {
                var p = await db.RemedialTrackPublications.FirstAsync(x => x.Id == f.Pub1);
                p.IsDeleted = true;
                await db.SaveChangesAsync();
            }
            Assert.False((await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, f.AxisIds[0], "م", YouTubeUrl, 100, GoodReason, null, null, true, null), Actor, All)).Success);

            await using (var db = f.Factory.CreateDbContext())
            {
                var p = await db.RemedialTrackPublications.FirstAsync(x => x.Id == f.Pub2);
                p.Status = RemedialTrackPublicationStatus.Cancelled;
                await db.SaveChangesAsync();
            }
            Assert.False((await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub2, f.AxisIds[0], "م", YouTubeUrl, 100, GoodReason, null, null, true, null), Actor, All)).Success);
        }

        [Fact]
        public async Task Create_AxisOfAnotherTrack_Rejected()
        {
            var f = await NewAsync();
            int foreignAxis;
            await using (var db = f.Factory.CreateDbContext())
            {
                var cur = await db.Curriculums.FirstAsync();
                var t2 = new RemedialTrack { Code = "RTK-2026-0002", Title = "أخرى", CurriculumId = cur.Id, CreatedByUserId = "a", CreatedAtUtc = T0 };
                db.RemedialTracks.Add(t2);
                var sec = new Section { Title = "قسم x", CurriculumId = cur.Id };
                db.Sections.Add(sec);
                await db.SaveChangesAsync();
                var ax = new RemedialTrackAxis { TrackId = t2.Id, SectionId = sec.Id, Order = 1, Exam101ModelId = f.Model101, Exam102ModelId = f.Model102 };
                db.RemedialTrackAxes.Add(ax);
                await db.SaveChangesAsync();
                foreignAxis = ax.Id;
            }
            var r = await f.Admin.CreateAsync(new CreateAddendumInput(f.Pub1, foreignAxis, "م", YouTubeUrl, 100, GoodReason, null, null, true, null), Actor, All);
            Assert.False(r.Success);
        }

        [Fact]
        public async Task Create_ForFiftyEnrollments_InsertsFiftyRowsInOneRequest()
        {
            var f = await NewAsync(extra: 50);
            // 50 تسجيلًا إضافيًا في الأمر الأول (E1,E2 قائمان)
            await using (var db = f.Factory.CreateDbContext())
            {
                var axes = await db.RemedialTrackAxes.OrderBy(a => a.Order).ToListAsync();
                var studentIds = await db.Students.OrderBy(s => s.StudentID).Skip(4).Select(s => s.StudentID).ToListAsync();
                foreach (var sid in studentIds)
                {
                    var e = new RemedialTrackEnrollment { PublicationId = f.Pub1, TrackId = f.TrackId, StudentId = sid, CreatedAtUtc = T0, Status = RemedialTrackEnrollmentStatus.InProgress };
                    foreach (var ax in axes)
                        e.AxisProgresses.Add(new RemedialTrackAxisProgress { AxisId = ax.Id, Order = ax.Order, Status = ax.Order == 1 ? RemedialTrackAxisStatus.Videos : RemedialTrackAxisStatus.Locked });
                    db.RemedialTrackEnrollments.Add(e);
                }
                await db.SaveChangesAsync();
            }

            var id = await f.CreateAsync();
            await using var db2 = f.Factory.CreateDbContext();
            Assert.Equal(52, await db2.RemedialTrackAddendumProgresses.CountAsync(p => p.AddendumId == id));
            Assert.Equal(1, await db2.RemedialTrackAddenda.CountAsync());
        }

        [Fact]
        public async Task Create_NotifiesOnlyStudentsWhoseAxisIsOpen_NotLockedOnes()
        {
            var f = await NewAsync();
            List<int>? sent = null;
            f.Notifications.Setup(n => n.SendToStudentsAsync(It.IsAny<List<int>>(), It.IsAny<string>(), It.IsAny<NotificationCategory>(), It.IsAny<string?>(), It.IsAny<int?>()))
                .Callback<List<int>, string, NotificationCategory, string?, int?>((ids, _, _, _, _) => sent = ids)
                .Returns(Task.CompletedTask);

            await f.CreateAsync(axisOrder: 3);   // المحور 3 Locked للجميع ← لا إشعار (يظهر عند فتح المحور)
            Assert.Null(sent);

            await f.CreateAsync(axisOrder: 1);   // المحور 1 Passed ← إشعار للطالبين
            Assert.NotNull(sent);
            Assert.Equal(new[] { f.S1, f.S2 }.OrderBy(x => x), sent!.OrderBy(x => x));
        }

        [Fact]
        public async Task SetActive_TogglesAndIsIdempotent_AndRespectsScope()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync();

            Assert.False((await f.Admin.SetActiveAsync(id, false, Actor, new RemedialTrackBatchScope(new HashSet<int> { 99 }))).Success);   // خارج النطاق
            Assert.False((await f.Admin.SetActiveAsync(424242, false, Actor, All)).Success);

            Assert.True((await f.Admin.SetActiveAsync(id, false, Actor, All)).Success);
            Assert.True((await f.Admin.SetActiveAsync(id, false, Actor, All)).Success);   // Idempotent
            await using (var db = f.Factory.CreateDbContext())
                Assert.False((await db.RemedialTrackAddenda.AsNoTracking().SingleAsync(a => a.Id == id)).IsActive);

            Assert.True((await f.Admin.SetActiveAsync(id, true, Actor, All)).Success);
            await using (var db = f.Factory.CreateDbContext())
                Assert.True((await db.RemedialTrackAddenda.AsNoTracking().SingleAsync(a => a.Id == id)).IsActive);
        }

        [Fact]
        public async Task Tracking_AggregatesPerAddendum_ExcludingCancelledEnrollments()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(modelId: f.ModelOk);
            await f.WatchFullyAsync(f.S1, f.E1, id);

            var rows = await f.Admin.GetTrackingAsync(f.Pub1);
            var r = Assert.Single(rows);
            Assert.Equal(2, r.Targeted);
            Assert.Equal(1, r.VideoCompleted);
            Assert.Equal(0, r.Completed);   // له اختبار لم يجتَزه بعد
            Assert.True(r.HasExam);
            Assert.NotNull(r.LastActivityUtc);

            var panel = await f.Admin.GetPanelAsync(f.Pub1, 1, canManage: true, All);
            Assert.NotNull(panel);
            Assert.Single(panel!.Rows);
            Assert.Equal(3, panel.Axes.Count);
            Assert.Contains(panel.ExamModels, m => m.Id == f.ModelOk);

            // بلا صلاحية إدارة: لا خيارات نموذج؛ خارج النطاق: null
            var readOnly = await f.Admin.GetPanelAsync(f.Pub1, 1, canManage: false, All);
            Assert.Empty(readOnly!.Axes);
            Assert.Null(await f.Admin.GetPanelAsync(f.Pub1, 1, true, new RemedialTrackBatchScope(new HashSet<int> { 99 })));
        }

        [Fact]
        public async Task Students_Drilldown_IsScopedAndPaged()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync();

            var page = await f.Admin.GetStudentsAsync(id, 1, All);
            Assert.NotNull(page);
            Assert.Equal(2, page!.Total);
            Assert.All(page.Items, i => Assert.False(i.Completed));
            Assert.Null(await f.Admin.GetStudentsAsync(id, 1, new RemedialTrackBatchScope(new HashSet<int> { 99 })));
            Assert.Null(await f.Admin.GetStudentsAsync(31337, 1, All));
        }

        // ═══════════════════════════ الطالب: الملكية والرؤية (IDOR / D31 / D34) ═══════════════════════════

        [Fact]
        public async Task Get_Owner_SeesLink_NonTargetedAndOtherPublicationGet404()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(all: false, ids: new[] { f.E1 });   // E2 غير مستهدف

            var mine = await f.Student.GetAsync(f.S1, f.E1, id);
            Assert.NotNull(mine);
            Assert.Equal("ccccccccccc", mine!.ExternalId);
            Assert.False(string.IsNullOrEmpty(mine.Url));
            Assert.Equal("طالب 1", mine.WatermarkText);

            Assert.Null(await f.Student.GetAsync(f.S2, f.E2, id));   // طالب غير مستهدف
            Assert.Null(await f.Student.GetAsync(f.S2, f.E1, id));   // IDOR: طالب يطلب تسجيل غيره
            Assert.Null(await f.Student.GetAsync(f.S1, f.E2, id));   // IDOR: تسجيل غيره بهويته
            Assert.Null(await f.Student.GetAsync(f.S4, f.E4, id));   // طالب من أمر آخر
            Assert.Null(await f.Student.GetAsync(f.S3, f.E3, id));   // ملغى/غير مستهدف
            Assert.Null(await f.Student.GetAsync(f.S1, f.E1, 777777));
        }

        [Fact]
        public async Task Get_LockedAxis_Hidden_UntilAxisOpens()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(axisOrder: 3);

            Assert.Null(await f.Student.GetAsync(f.S1, f.E1, id));   // المحور 3 Locked

            await using (var db = f.Factory.CreateDbContext())
            {
                var ap = await db.RemedialTrackAxisProgresses.FirstAsync(a => a.EnrollmentId == f.E1 && a.Order == 3);
                ap.Status = RemedialTrackAxisStatus.Videos;
                await db.SaveChangesAsync();
            }
            Assert.NotNull(await f.Student.GetAsync(f.S1, f.E1, id));   // فُتح المحور ← يظهر الملحق
        }

        [Fact]
        public async Task Get_InactiveAddendum_DeletedOrCancelledPublication_Get404()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync();
            Assert.NotNull(await f.Student.GetAsync(f.S1, f.E1, id));

            await f.Admin.SetActiveAsync(id, false, Actor, All);
            Assert.Null(await f.Student.GetAsync(f.S1, f.E1, id));            // ملحق غير نشط
            Assert.Null((await f.Progress.GetPlanAsync(f.S1, f.E1))!.Addenda.FirstOrDefault());
            await f.Admin.SetActiveAsync(id, true, Actor, All);

            await using (var db = f.Factory.CreateDbContext())
            {
                var p = await db.RemedialTrackPublications.FirstAsync(x => x.Id == f.Pub1);
                p.IsDeleted = true;
                await db.SaveChangesAsync();
            }
            Assert.Null(await f.Student.GetAsync(f.S1, f.E1, id));            // D34: محذوف ناعمًا

            await using (var db = f.Factory.CreateDbContext())
            {
                var p = await db.RemedialTrackPublications.FirstAsync(x => x.Id == f.Pub1);
                p.IsDeleted = false;
                p.Status = RemedialTrackPublicationStatus.Cancelled;
                await db.SaveChangesAsync();
            }
            Assert.Null(await f.Student.GetAsync(f.S1, f.E1, id));            // D34: ملغى
        }

        // ═══════════════════════════ المشاهدة بزمن الخادم (D32) ═══════════════════════════

        [Fact]
        public async Task Ping_ServerTime_CompletesAt90Percent_AndDeliversNoLinkAfterwards()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync();   // بلا اختبار ← يكتمل الملحق بإتمام الفيديو
            var pid = await f.ProgressIdAsync(id, f.E1);

            // لا رصيد لأول نبضة، ولا إتمام قبل ended
            var first = await f.PingAsync(f.S1, f.E1, pid, "playing");
            Assert.False(first.Body.Completed);
            Assert.Equal(0, first.Body.WatchedSeconds);

            // ended مبكرًا (لم يُشاهد 90%) ← insufficient
            f.Clock.Now = f.Clock.Now.AddSeconds(10);
            var early = await f.PingAsync(f.S1, f.E1, pid, "ended");
            Assert.False(early.Body.Completed);
            Assert.Equal("insufficient", early.Body.Reason);

            await f.WatchFullyAsync(f.S1, f.E1, id);   // يكمل (بنبضات جديدة)
            var p = await f.ProgressAsync(id, f.E1);
            Assert.True(p.VideoCompleted);
            Assert.NotNull(p.VideoCompletedAtUtc);
            Assert.NotNull(p.CompletedAtUtc);          // ملحق بلا اختبار: يكتمل بإتمام الفيديو
            Assert.True(p.WatchedSeconds >= 90);

            var vm = await f.Student.GetAsync(f.S1, f.E1, id);
            Assert.True(vm!.VideoCompleted);
            Assert.True(string.IsNullOrEmpty(vm.Url));       // لا يغادر الخادمَ رابط بعد الاكتمال
            Assert.Null(vm.ExternalId);
            Assert.Equal(StudentAddendumState.Completed, vm.State);

            // نبضة بعد الاكتمال: ردّ Idempotent
            var again = await f.PingAsync(f.S1, f.E1, pid, "playing");
            Assert.True(again.Body.Completed);
        }

        [Fact]
        public async Task Ping_ClientCannotInflateWatchedTime()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync();
            var pid = await f.ProgressIdAsync(id, f.E1);

            await f.PingAsync(f.S1, f.E1, pid, "playing");
            f.Clock.Now = f.Clock.Now.AddMinutes(30);   // انقطاع طويل: الرصيد مسقوف بـ 20ث
            var r = await f.PingAsync(f.S1, f.E1, pid, "playing");
            Assert.Equal(20, r.Body.WatchedSeconds);
        }

        [Fact]
        public async Task Ping_OtherStudentOrWrongEnrollment_NotFound_AndBadStateRejected()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(all: false, ids: new[] { f.E1 });
            var pid = await f.ProgressIdAsync(id, f.E1);

            Assert.Equal(RemedialTrackPingStatus.NotFound, (await f.PingAsync(f.S2, f.E1, pid, "playing")).Status);   // IDOR
            Assert.Equal(RemedialTrackPingStatus.NotFound, (await f.PingAsync(f.S1, f.E2, pid, "playing")).Status);
            Assert.Equal(RemedialTrackPingStatus.NotFound, (await f.PingAsync(f.S1, f.E1, 99999, "playing")).Status);
            Assert.Equal(RemedialTrackPingStatus.BadRequest, (await f.PingAsync(f.S1, f.E1, pid, "bogus")).Status);
            Assert.Equal(RemedialTrackPingStatus.BadRequest, (await f.PingAsync(f.S1, f.E1, pid, "manual-done")).Status);   // YouTube لا manual-done
        }

        [Fact]
        public async Task Ping_InactiveAddendum_LockedAxis_DeletedPublication_NotAccepted()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync();
            var locked = await f.CreateAsync(axisOrder: 3);
            var pid = await f.ProgressIdAsync(id, f.E1);
            var lockedPid = await f.ProgressIdAsync(locked, f.E1);

            Assert.Equal(RemedialTrackPingStatus.NotFound, (await f.PingAsync(f.S1, f.E1, lockedPid, "playing")).Status);   // محور مقفل

            await f.Admin.SetActiveAsync(id, false, Actor, All);
            Assert.Equal(RemedialTrackPingStatus.NotFound, (await f.PingAsync(f.S1, f.E1, pid, "playing")).Status);         // غير نشط
            await f.Admin.SetActiveAsync(id, true, Actor, All);

            await using (var db = f.Factory.CreateDbContext())
            {
                var p = await db.RemedialTrackPublications.FirstAsync(x => x.Id == f.Pub1);
                p.IsDeleted = true;
                await db.SaveChangesAsync();
            }
            Assert.NotEqual(RemedialTrackPingStatus.Ok, (await f.PingAsync(f.S1, f.E1, pid, "playing")).Status);            // D34
        }

        // ═══════════════════════════ الاختبار + حارس الخطر R8 ═══════════════════════════

        [Fact]
        public async Task Exam_GatedByVideo_OnePerRun_UnlimitedUntilPass_Idempotent()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(modelId: f.ModelOk);

            // قبل اكتمال الفيديو
            var early = await f.Student.StartExamAsync(f.S1, f.E1, id);
            Assert.Equal(RemedialTrackExamStartStatus.Conflict, early.Status);

            await f.WatchFullyAsync(f.S1, f.E1, id);
            var p0 = await f.ProgressAsync(id, f.E1);
            Assert.True(p0.VideoCompleted);
            Assert.Null(p0.CompletedAtUtc);   // له اختبار: لا اكتمال قبل النجاح

            // محاولة واحدة جارية في آن واحد
            var a1 = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);
            var same = await f.Student.StartExamAsync(f.S1, f.E1, id);
            Assert.Equal(RemedialTrackExamStartStatus.Existing, same.Status);
            Assert.Equal(a1, same.AttemptId);

            // رسوب ← محاولات غير محدودة
            await f.SubmitAttemptAsync(f.S1, a1, correctCount: 1);
            var p1 = await f.ProgressAsync(id, f.E1);
            Assert.False(p1.ExamPassed);
            Assert.Equal(1, p1.AttemptsCount);
            Assert.Null(p1.CompletedAtUtc);
            Assert.Equal(25, p1.BestScorePercent);

            var a2 = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);
            Assert.NotEqual(a1, a2);
            await f.SubmitAttemptAsync(f.S1, a2, correctCount: 2);
            Assert.Equal(2, (await f.ProgressAsync(id, f.E1)).AttemptsCount);

            var vmRetake = await f.Student.GetAsync(f.S1, f.E1, id);
            Assert.Equal(StudentAddendumState.RetakeExam, vmRetake!.State);

            // نجاح (نسبة الاجتياز 60 من الخطة نفسها)
            var a3 = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);
            await f.SubmitAttemptAsync(f.S1, a3, correctCount: 3);
            var p3 = await f.ProgressAsync(id, f.E1);
            Assert.True(p3.ExamPassed);
            Assert.Equal(3, p3.AttemptsCount);
            Assert.Equal(75, p3.BestScorePercent);
            Assert.NotNull(p3.CompletedAtUtc);

            // تسليم مزدوج وإعادة الاستدعاء: لا تضخيم للعدّاد
            await f.Exams.SubmitAsync(f.S1, a3);
            await f.Student.OnAttemptSubmittedAsync(a3);
            Assert.Equal(3, (await f.ProgressAsync(id, f.E1)).AttemptsCount);

            // بعد النجاح لا اختبار جديد
            Assert.Equal(RemedialTrackExamStartStatus.Conflict, (await f.Student.StartExamAsync(f.S1, f.E1, id)).Status);
        }

        [Fact]
        public async Task Exam_AddendumWithoutExam_CannotStartExam()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync();
            await f.WatchFullyAsync(f.S1, f.E1, id);
            Assert.Equal(RemedialTrackExamStartStatus.Conflict, (await f.Student.StartExamAsync(f.S1, f.E1, id)).Status);
        }

        [Fact]
        public async Task Exam_OtherStudentOrOtherOrder_CannotStartOrReadAttempt()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(modelId: f.ModelOk, all: false, ids: new[] { f.E1 });
            await f.WatchFullyAsync(f.S1, f.E1, id);
            var attempt = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);

            Assert.Equal(RemedialTrackExamStartStatus.NotFound, (await f.Student.StartExamAsync(f.S2, f.E2, id)).Status);    // غير مستهدف
            Assert.Equal(RemedialTrackExamStartStatus.NotFound, (await f.Student.StartExamAsync(f.S2, f.E1, id)).Status);    // تسجيل غيره
            Assert.Equal(RemedialTrackExamStartStatus.NotFound, (await f.Student.StartExamAsync(f.S4, f.E4, id)).Status);    // أمر آخر

            // محاولة الغير: لا حل ولا حفظ ولا تسليم ولا نتيجة
            Assert.Equal(RemedialTrackSolveStatus.NotFound, (await f.Exams.GetSolveAsync(f.S2, attempt)).Status);
            Assert.Equal(RemedialTrackSubmitStatus.NotFound, (await f.Exams.SubmitAsync(f.S2, attempt)).Status);
            Assert.Equal(RemedialTrackResultStatus.NotFound, (await f.Exams.GetResultAsync(f.S4, attempt)).Status);
        }

        [Fact]
        public async Task Solve_AddendumAttempt_NeverLeaksCorrectAnswerOrExplanation()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(modelId: f.ModelOk);
            await f.WatchFullyAsync(f.S1, f.E1, id);
            var attempt = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);

            var solve = await f.Exams.GetSolveAsync(f.S1, attempt);
            Assert.Equal(RemedialTrackSolveStatus.Ok, solve.Status);
            Assert.Equal(RemedialTrackExamNumber.Addendum, solve.Model!.ExamNumber);
            Assert.All(solve.Model.Questions, q =>
            {
                Assert.Null(q.Question.CorrectAnswer);
                Assert.Null(q.Question.Explanation);
                Assert.Null(q.Question.VideoUrl);
            });
        }

        [Theory]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam102, 4)]   // الخطر R8: كان يُعامل كأنه 102 ← يجتاز المحور
        [InlineData(RemedialTrackAxisStatus.AwaitingExam102, 0)]   // ... أو يرسب فيه FailedBlocked
        [InlineData(RemedialTrackAxisStatus.AwaitingExam101, 4)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam101, 0)]
        public async Task SubmittingAddendumAttempt_NeverChangesAnyAxisOrEnrollmentState(RemedialTrackAxisStatus axis2Status, int correct)
        {
            var f = await NewAsync(new[] { RemedialTrackAxisStatus.Passed, axis2Status, RemedialTrackAxisStatus.Locked });
            var id = await f.CreateAsync(axisOrder: 2, modelId: f.ModelOk);   // ملحق على المحور المنتظر لاختبار 101/102
            await f.WatchFullyAsync(f.S1, f.E1, id);

            var before = await f.AxisSnapshotAsync();
            var attempt = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);
            await f.SubmitAttemptAsync(f.S1, attempt, correct);

            Assert.Equal(before, await f.AxisSnapshotAsync());   // لا أثر على أي AxisProgress/Enrollment/أحداث/تقارير ولي أمر

            // استدعاء الانتقال مباشرة بمحاولة ملحق: حارس صريح، بلا تغيير
            var t = await f.Progress.OnExamSubmittedAsync(attempt);
            Assert.False(t.Applied);
            Assert.Equal(before, await f.AxisSnapshotAsync());

            // الجلب العادي للمحور لا يحمل محاولة الملحق
            var axisVm = await f.Progress.GetAxisAsync(f.S1, f.E1, await ApIdAsync(f, f.E1, 2));
            Assert.NotNull(axisVm);
            Assert.DoesNotContain(axisVm!.Attempts, a => a.ExamNumber == RemedialTrackExamNumber.Addendum);
        }

        [Fact]
        public async Task Result_AddendumAttempt_ReturnsAddendumNextStep_NotAxisFlow()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(modelId: f.ModelOk);
            await f.WatchFullyAsync(f.S1, f.E1, id);

            var fail = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);
            await f.SubmitAttemptAsync(f.S1, fail, 0);
            var r1 = await f.Exams.GetResultAsync(f.S1, fail);
            Assert.Equal(RemedialTrackResultStatus.Ok, r1.Status);
            Assert.Equal(RemedialTrackResultNext.AddendumRetry, r1.Model!.Next);
            Assert.Equal(id, r1.Model.AddendumId);
            Assert.Null(r1.Model.NextAxisProgressId);
            Assert.Equal("اختبار الملحق", r1.Model.ExamNumber.DisplayName());

            var pass = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);
            await f.SubmitAttemptAsync(f.S1, pass, 4);
            var r2 = await f.Exams.GetResultAsync(f.S1, pass);
            Assert.Equal(RemedialTrackResultNext.AddendumPassed, r2.Model!.Next);
        }

        [Fact]
        public async Task ExpiredAddendumAttempt_IsClosedAndCounted_WithoutAxisEffect()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(modelId: f.ModelOk);
            await f.WatchFullyAsync(f.S1, f.E1, id);
            var attempt = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);
            var before = await f.AxisSnapshotAsync();

            f.Clock.Now = f.Clock.Now.AddMinutes(16);                    // انتهى الوقت وسماحيته
            var solve = await f.Exams.GetSolveAsync(f.S1, attempt);      // يُصحَّح تلقائيًا
            Assert.Equal(RemedialTrackSolveStatus.Closed, solve.Status);

            var p = await f.ProgressAsync(id, f.E1);
            Assert.Equal(1, p.AttemptsCount);
            Assert.False(p.ExamPassed);
            Assert.Equal(before, await f.AxisSnapshotAsync());
        }

        // ═══════════════════════════ D29: لا يمنع المحور التالي ═══════════════════════════

        [Fact]
        public async Task PassingAxis_WithPendingAddendum_StillOpensNextAxis()
        {
            var f = await NewAsync(new[] { RemedialTrackAxisStatus.AwaitingExam101, RemedialTrackAxisStatus.Locked, RemedialTrackAxisStatus.Locked });
            var id = await f.CreateAsync(axisOrder: 1, modelId: f.ModelOk);   // ملحق معلّق (لم يُشاهد) على المحور 1

            // اجتياز اختبار 101 للمحور 1 عبر المحرك العادي
            var apId = await ApIdAsync(f, f.E1, 1);
            var start = await f.Exams.StartExamAsync(f.S1, f.E1, apId);
            Assert.Equal(RemedialTrackExamStartStatus.Created, start.Status);
            await f.SubmitAttemptAsync(f.S1, start.AttemptId, 4);

            await using var db = f.Factory.CreateDbContext();
            var axes = await db.RemedialTrackAxisProgresses.AsNoTracking().Where(a => a.EnrollmentId == f.E1).OrderBy(a => a.Order).ToListAsync();
            Assert.Equal(RemedialTrackAxisStatus.Passed, axes[0].Status);
            Assert.Equal(RemedialTrackAxisStatus.Videos, axes[1].Status);   // التالي مفتوح قبل الملحق
            var p = await f.ProgressAsync(id, f.E1);
            Assert.False(p.VideoCompleted);                                   // والملحق ما زال معلّقًا

            // ... وبعد إتمام الملحق: حالة المحور كما هي Passed
            await f.WatchFullyAsync(f.S1, f.E1, id);
            await using var db2 = f.Factory.CreateDbContext();
            Assert.Equal(RemedialTrackAxisStatus.Passed, (await db2.RemedialTrackAxisProgresses.AsNoTracking().FirstAsync(a => a.EnrollmentId == f.E1 && a.Order == 1)).Status);
        }

        // بوابة S13.5 (بند 3): طالب اجتاز محورًا قبل النشر وتاليه Locked بسبب الجدولة القديمة ← يُفتح عند أول فتح لصفحة الخطة
        [Fact]
        public async Task LegacyLockedNextAxis_OpensOnFirstPlanOpen_AndAddendumThenShowsOnIt()
        {
            var f = await NewAsync(new[] { RemedialTrackAxisStatus.Passed, RemedialTrackAxisStatus.Locked, RemedialTrackAxisStatus.Locked });
            var onSecond = await f.CreateAsync(axisOrder: 2);
            Assert.Null(await f.Student.GetAsync(f.S1, f.E1, onSecond));   // المحور 2 ما زال Locked ← لا يظهر الملحق

            var plan = await f.Progress.GetPlanAsync(f.S1, f.E1);          // أول فتح لصفحة الخطة بعد النشر
            Assert.Equal(RemedialTrackAxisStatus.Videos, plan!.Axes.First(a => a.Order == 2).Status);
            Assert.Equal(RemedialTrackAxisStatus.Locked, plan.Axes.First(a => a.Order == 3).Status);   // الثالث يبقى مقفلًا (الثاني لم يُنهَ)

            var card = Assert.Single(plan.Addenda);                         // وبعد الفتح تظهر بطاقة ملحقه
            Assert.Equal(onSecond, card.AddendumId);
            Assert.NotNull(await f.Student.GetAsync(f.S1, f.E1, onSecond));
        }

        [Fact]
        public async Task MyPlans_ShowsPendingAddendumAlert_ForOpenAxisOnly_AndClearsWhenCompleted()
        {
            var f = await NewAsync();
            var open = await f.CreateAsync(axisOrder: 1);
            await f.CreateAsync(axisOrder: 3);   // محور مقفل ← لا تنبيه

            var mine = (await f.Progress.GetMyPlansAsync(f.S1)).Items.Single();
            var alert = Assert.Single(mine.PendingAddenda);
            Assert.Equal(open, alert.AddendumId);
            Assert.Equal("محور 1", alert.AxisTitle);
            Assert.Empty((await f.Progress.GetMyPlansAsync(f.S4)).Items.Single().PendingAddenda);   // أمر آخر: لا تسرّب

            await f.WatchFullyAsync(f.S1, f.E1, open);   // بلا اختبار ← يكتمل
            Assert.Empty((await f.Progress.GetMyPlansAsync(f.S1)).Items.Single().PendingAddenda);
        }

        // ═══════════════════════════ بطاقة الخطة والتقارير (D33) ═══════════════════════════

        [Fact]
        public async Task Plan_ShowsCardUnderPassedAxis_WithStates_NotUnderLocked()
        {
            var f = await NewAsync();
            var onPassed = await f.CreateAsync(axisOrder: 1, modelId: f.ModelOk);
            await f.CreateAsync(axisOrder: 3);   // محور مقفل ← لا بطاقة

            var plan = await f.Progress.GetPlanAsync(f.S1, f.E1);
            var card = Assert.Single(plan!.Addenda);
            Assert.Equal(onPassed, card.AddendumId);
            Assert.Equal(StudentAddendumState.NotStarted, card.State);
            Assert.True(card.HasExam);
            Assert.Equal(plan.Axes.First(a => a.Order == 1).AxisId, card.AxisId);
            Assert.Equal(RemedialTrackAxisStatus.Passed, plan.Axes.First(a => a.Order == 1).Status);   // يظهر حتى لو المحور Passed

            var pid = await f.ProgressIdAsync(onPassed, f.E1);
            await f.PingAsync(f.S1, f.E1, pid, "playing");
            f.Clock.Now = f.Clock.Now.AddSeconds(15);
            await f.PingAsync(f.S1, f.E1, pid, "playing");   // الرصيد الزمني يبدأ من النبضة الثانية (لا رصيد لأول نبضة)
            Assert.Equal(StudentAddendumState.Watching, (await f.Progress.GetPlanAsync(f.S1, f.E1))!.Addenda.Single().State);

            await f.WatchFullyAsync(f.S1, f.E1, onPassed);
            Assert.Equal(StudentAddendumState.Watched, (await f.Progress.GetPlanAsync(f.S1, f.E1))!.Addenda.Single().State);

            // طالب آخر لا يرى بطاقة غيره ولا تتسرّب بين الأوامر
            Assert.Empty((await f.Progress.GetPlanAsync(f.S4, f.E4))!.Addenda);
        }

        [Fact]
        public async Task Reports_ShowAddendaLine_ButAddendumAttemptsNeverEnterAxisNumbers()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync(modelId: f.ModelOk);
            await f.WatchFullyAsync(f.S1, f.E1, id);
            var attempt = await f.StartAddendumAttemptAsync(f.S1, f.E1, id);
            await f.SubmitAttemptAsync(f.S1, attempt, 4);

            await using var db = f.Factory.CreateDbContext();
            var vm = await RemedialTrackReportBuilder.BuildAsync(db, f.E1, f.S1, null, false, T0, default);
            Assert.NotNull(vm);
            Assert.Equal(1, vm!.AddendaTotal);
            Assert.Equal(1, vm.AddendaCompleted);
            Assert.All(vm.Axes, a => Assert.Empty(a.Attempts));        // محاولة الملحق لا تظهر ضمن محاولات المحاور
            Assert.Null(vm.AverageScorePercent);                        // ولا تدخل في متوسط النتائج
            Assert.Equal(1, vm.PassedAxes);                             // نسب المحاور كما هي (المحور 1 Passed فقط)

            // E2 لم يُكمل: 0 من 1
            var vm2 = await RemedialTrackReportBuilder.BuildAsync(db, f.E2, f.S2, null, false, T0, default);
            Assert.Equal(1, vm2!.AddendaTotal);
            Assert.Equal(0, vm2.AddendaCompleted);

            // ملحق على محور مقفل لا يظهر في التقرير حتى يُفتح المحور (D31)
            await f.CreateAsync(axisOrder: 3);
            var vmLocked = await RemedialTrackReportBuilder.BuildAsync(db, f.E1, f.S1, null, false, T0, default);
            Assert.Equal(1, vmLocked!.AddendaTotal);

            // ملحق موقوف يختفي من التقرير
            await f.Admin.SetActiveAsync(id, false, Actor, All);
            var vm3 = await RemedialTrackReportBuilder.BuildAsync(db, f.E1, f.S1, null, false, T0, default);
            Assert.Equal(0, vm3!.AddendaTotal);
        }

        [Fact]
        public async Task ParentSnapshot_CarriesAddendaCountsAtCreationTime_AndOldSnapshotsStillDeserialize()
        {
            var f = await NewAsync();
            var id = await f.CreateAsync();
            await f.WatchFullyAsync(f.S1, f.E1, id);

            await using var db = f.Factory.CreateDbContext();
            var vm = await RemedialTrackReportBuilder.BuildAsync(db, f.E1, null, null, false, T0, default);
            var snap = RemedialTrackParentReportWriter.ToSnapshot(vm!, null);
            Assert.Equal(1, snap.AddendaTotal);
            Assert.Equal(1, snap.AddendaCompleted);

            var json = RemedialTrackParentReportWriter.Serialize(snap);
            var back = RemedialTrackParentReportWriter.TryDeserialize(json);
            Assert.Equal(1, back!.AddendaTotal);

            // لقطة قديمة (قبل S13) بلا الحقلين ← تُقرأ بصفر
            var old = RemedialTrackParentReportWriter.TryDeserialize("{\"Version\":1,\"StudentName\":\"ن\",\"Axes\":[]}");
            Assert.NotNull(old);
            Assert.Equal(0, old!.AddendaTotal);
        }

        // ═══════════════════════════ أمان: الصلاحية وAntiforgery والتسجيل ═══════════════════════════

        private const string PubController = "RemedialTrackPublications";

        [Theory]
        [InlineData("CreateAddendum")]
        [InlineData("SetAddendumActive")]
        public void AdminPosts_AreAntiForgery_PostOnly_AndGuardedByManageAddendum(string action)
        {
            var m = typeof(Areas.Admin.Controllers.RemedialTrackPublicationsController).GetMethod(action);
            Assert.NotNull(m);
            Assert.NotNull(m!.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            var auth = Assert.Single(m.GetCustomAttributes<AdminPermissionAttribute>());
            Assert.Equal($"{PubController}:ManageAddendum", auth.Policy);
        }

        [Fact]
        public void AddendumStudents_IsGet_AndRequiresReadPermission()
        {
            var m = typeof(Areas.Admin.Controllers.RemedialTrackPublicationsController).GetMethod("AddendumStudents");
            Assert.NotNull(m);
            Assert.NotNull(m!.GetCustomAttribute<HttpGetAttribute>());
            Assert.Equal($"{PubController}:Read", Assert.Single(m.GetCustomAttributes<AdminPermissionAttribute>()).Policy);
        }

        [Theory]
        [InlineData("AddendumPing", "rtk-ping")]
        [InlineData("StartAddendumExam", "rtk-exam")]
        public void StudentPosts_AreAntiForgery_PostOnly_AndRateLimited(string action, string policy)
        {
            var m = typeof(Areas.Students.Controllers.RemedialTrackController).GetMethod(action);
            Assert.NotNull(m);
            Assert.NotNull(m!.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.Equal(policy, m.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        }

        [Fact]
        public void StudentAddendumGet_IsGet_And_ControllerIsStudentOnly()
        {
            var t = typeof(Areas.Students.Controllers.RemedialTrackController);
            Assert.NotNull(t.GetMethod("Addendum")!.GetCustomAttribute<HttpGetAttribute>());
            var auth = t.GetCustomAttribute<AuthorizeAttribute>();
            Assert.Equal("Student", auth!.Roles);
        }

        [Fact]
        public void ManageAddendum_IsInCatalog_PolicyConstant_AndProgramRegistration()
        {
            Assert.Contains(AdminControllerActionCatalog.GetActions(PubController), a => a.Key == "ManageAddendum" && !string.IsNullOrWhiteSpace(a.DisplayNameAr));
            Assert.Equal($"{PubController}:ManageAddendum", AdminPermissionPolicies.RemedialTrackPublications_ManageAddendum);

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "QdratNew.csproj"))) dir = dir.Parent;
            var program = File.ReadAllText(Path.Combine(dir!.FullName, "Program.cs"));
            Assert.Contains("AdminPermissionPolicies.RemedialTrackPublications_ManageAddendum", program);
            Assert.Contains("IRemedialTrackAddendumService", program);
            Assert.Contains("IRemedialTrackAddendumStudentService", program);
        }

        [Fact]
        public async Task Staff_WithoutManageAddendum_IsDenied_ButWithItIsAllowed()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            var req = new AdminPermissionAuthorizationRequirement(AdminPermissionPolicies.RemedialTrackPublications_ManageAddendum);

            async Task<bool> Can(string userId)
            {
                var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId),
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
                }, "test"));
                var ctx = new AuthorizationHandlerContext(new[] { req }, user, null);
                await new AdminPermissionAuthorizationHandler(db).HandleAsync(ctx);
                return ctx.HasSucceeded;
            }

            async Task Seed(string userId, params (string Action, bool Allowed)[] custom)
            {
                var profile = new AdminProfile { Name = "موظف " + userId };
                db.AdminProfiles.Add(profile);
                await db.SaveChangesAsync();
                var perm = new AdminProfileControllerPermission { AdminProfileId = profile.Id, ControllerName = PubController, AccessLevel = AdminControllerAccessLevel.Read, HasCustomPermissions = true };
                foreach (var (a, allowed) in custom) perm.CustomPermissions.Add(new AdminProfileCustomPermission { ActionName = a, IsAllowed = allowed });
                db.AdminProfileControllerPermissions.Add(perm);
                db.AdminUserProfiles.Add(new AdminUserProfile { UserId = userId, AdminProfileId = profile.Id });
                await db.SaveChangesAsync();
            }

            await Seed("u-none");                                                     // بلا أي صلاحية مخصّصة
            await Seed("u-review", ("ManageReview", true));                           // لديه ManageReview فقط
            await Seed("u-add", ("ManageAddendum", true));

            Assert.False(await Can("u-none"));
            Assert.False(await Can("u-review"));
            Assert.True(await Can("u-add"));
        }

        [Fact]
        public void CreateInput_HasNoOverpostableFields_AndValidatesReason()
        {
            var props = typeof(CreateRemedialTrackAddendumInput).GetProperties().Select(p => p.Name).ToHashSet();
            // لا حقل لحالة المحور/التقدّم/المنشئ/الفعّالية: تُحدَّد كلها في الخادم
            foreach (var forbidden in new[] { "IsActive", "CreatedByUserId", "CreatedAtUtc", "Provider", "ExternalId", "Status", "Progresses" })
                Assert.DoesNotContain(forbidden, props);

            var reason = typeof(CreateRemedialTrackAddendumInput).GetProperty("Reason")!;
            Assert.NotNull(reason.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>());
            Assert.Equal(5, reason.GetCustomAttribute<System.ComponentModel.DataAnnotations.StringLengthAttribute>()!.MinimumLength);
        }

        // ═══════════════════════════ اختبارات نقية ═══════════════════════════

        [Theory]
        [InlineData(0, false, 0, false, StudentAddendumState.NotStarted)]
        [InlineData(5, false, 0, false, StudentAddendumState.Watching)]
        [InlineData(100, true, 0, false, StudentAddendumState.Watched)]
        [InlineData(100, true, 2, false, StudentAddendumState.RetakeExam)]
        [InlineData(100, true, 2, true, StudentAddendumState.Completed)]
        [InlineData(0, false, 0, true, StudentAddendumState.Completed)]
        public void StateOf_MapsProgressToStudentState(double watched, bool video, int attempts, bool completed, StudentAddendumState expected)
            => Assert.Equal(expected, RemedialTrackAddendumRules.StateOf(watched, video, attempts, completed));

        [Fact]
        public void ExamNumber_Addendum_DoesNotChangeExistingValues_AndHasOwnDisplayName()
        {
            Assert.Equal(101, (int)RemedialTrackExamNumber.Exam101);
            Assert.Equal(102, (int)RemedialTrackExamNumber.Exam102);
            Assert.Equal("الاختبار الأول", RemedialTrackExamNumber.Exam101.DisplayName());
            Assert.Equal("الاختبار الثاني", RemedialTrackExamNumber.Exam102.DisplayName());
            Assert.Equal("اختبار الملحق", RemedialTrackExamNumber.Addendum.DisplayName());
        }

        [Fact]
        public void StateMachine_RejectsAddendumExamNumber_Explicitly()
        {
            foreach (var status in new[] { RemedialTrackAxisStatus.AwaitingExam101, RemedialTrackAxisStatus.AwaitingExam102 })
                Assert.Throws<InvalidOperationException>(() => RemedialTrackStateMachine.OnExamSubmitted(status, RemedialTrackExamNumber.Addendum, true));
        }

        private static async Task<int> ApIdAsync(Fx f, int enrollmentId, int order)
        {
            await using var db = f.Factory.CreateDbContext();
            return await db.RemedialTrackAxisProgresses.Where(a => a.EnrollmentId == enrollmentId && a.Order == order).Select(a => a.Id).SingleAsync();
        }
    }
}
