using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
    /// RTK-S5: اختبارا 101/102 + الانتقالات + التصحيح + IDOR على المحاولات.
    /// على InMemory: ExecuteUpdate/RowVersion/الفهارس المُصفّاة لا تُفرض هنا — تُغطّى على SQL Server الحقيقي في RTK-S7.3.
    /// </summary>
    public class RemedialTrackExamTests
    {
        private static readonly DateTime T0 = new(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        private const string Correct = "أ";
        private const string Wrong = "ب";
        private const string SecretExplanation = "شرح-سري-لا-يظهر-للطالب";

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

        private sealed class Fx
        {
            public TestDbContextFactory Factory { get; } = new(Guid.NewGuid().ToString());
            public MutableTime Clock { get; } = new();
            public RemedialTrackProgressService Progress { get; }
            public RemedialTrackExamService Exams { get; }

            public const int Student = 101;
            public const int Other = 202;
            public int Enrollment { get; private set; }
            public int OtherEnrollment { get; private set; }
            public int[] Ap { get; private set; } = Array.Empty<int>();
            public int TrackId { get; private set; }
            public int Model101 { get; private set; }
            public int Model102 { get; private set; }
            public int PublicationId { get; private set; }

            public Fx()
            {
                Progress = new RemedialTrackProgressService(Factory, Clock, new FakeTz(), NullLogger<RemedialTrackProgressService>.Instance);
                Exams = new RemedialTrackExamService(Factory, Clock, Progress, NullLogger<RemedialTrackExamService>.Instance);
            }

            public async Task SeedAsync(
                int axes = 3,
                RemedialTrackAxisStatus firstStatus = RemedialTrackAxisStatus.AwaitingExam101,
                RemedialTrackDeliveryMode mode = RemedialTrackDeliveryMode.Online,
                RemedialTrackPublicationStatus pubStatus = RemedialTrackPublicationStatus.Active,
                int questionsPerModel = 4,
                bool emptyModel102 = false)
            {
                await using var db = Factory.CreateDbContext();

                var cur = new Curriculum { Title = "قدرات", Description = "d", CurriculumTypeName = "t" };
                db.Curriculums.Add(cur);
                await db.SaveChangesAsync();

                var m101 = new ProfessionalModel { Title = "M101", Description = "d", CreatedBy = "t" };
                var m102 = new ProfessionalModel { Title = "M102", Description = "d", CreatedBy = "t" };
                db.ProfessionalModels.AddRange(m101, m102);
                await db.SaveChangesAsync();
                Model101 = m101.Id; Model102 = m102.Id;

                AddModelQuestions(db, cur.Id, m101.Id, questionsPerModel);
                if (!emptyModel102) AddModelQuestions(db, cur.Id, m102.Id, questionsPerModel);
                await db.SaveChangesAsync();

                var track = new RemedialTrack
                {
                    Code = "RTK-2026-0001", Title = "خطة علاجية", CurriculumId = cur.Id, Status = RemedialTrackStatus.Ready,
                    MinWatchPercent = 90, PassPercent = 60, IsStructureLocked = true, CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-3)
                };
                db.RemedialTracks.Add(track);
                await db.SaveChangesAsync();
                TrackId = track.Id;

                var axisEntities = new List<RemedialTrackAxis>();
                for (var i = 1; i <= axes; i++)
                {
                    var sec = new Section { Title = $"قسم {i}", CurriculumId = cur.Id };
                    db.Sections.Add(sec);
                    await db.SaveChangesAsync();
                    axisEntities.Add(new RemedialTrackAxis
                    {
                        TrackId = track.Id, SectionId = sec.Id, Order = i, TitleOverride = $"محور {i}",
                        Exam101ModelId = m101.Id, Exam102ModelId = m102.Id, ExamDurationMinutes = 30
                    });
                }
                db.RemedialTrackAxes.AddRange(axisEntities);
                await db.SaveChangesAsync();

                foreach (var a in axisEntities)
                {
                    db.RemedialTrackVideos.AddRange(
                        new RemedialTrackVideo { AxisId = a.Id, Order = 1, Title = $"فيديو {a.Order}-1", Url = "https://youtu.be/aaaaaaaaaaa", Provider = RemedialTrackVideoProvider.YouTube, ExternalId = "aaaaaaaaaaa", DurationSeconds = 100 },
                        new RemedialTrackVideo { AxisId = a.Id, Order = 2, Title = $"فيديو {a.Order}-2", Url = "https://youtu.be/bbbbbbbbbbb", Provider = RemedialTrackVideoProvider.YouTube, ExternalId = "bbbbbbbbbbb", DurationSeconds = 100 });
                }

                var pub = new RemedialTrackPublication
                {
                    TrackId = track.Id, BatchId = 1, Scope = RemedialTrackPublicationScope.WholeBatch, Mode = mode,
                    PublishAtUtc = T0.AddHours(-1), Status = pubStatus,
                    AccessCode = mode == RemedialTrackDeliveryMode.InPerson ? "123456" : null,
                    CodeVersion = 1, CreatedByUserId = "admin", CreatedAtUtc = T0.AddDays(-2), TotalStudents = 2
                };
                db.RemedialTrackPublications.Add(pub);
                await db.SaveChangesAsync();
                PublicationId = pub.Id;

                Enrollment = await AddEnrollmentAsync(db, pub.Id, track.Id, Student, axisEntities, firstStatus);
                OtherEnrollment = await AddEnrollmentAsync(db, pub.Id, track.Id, Other, axisEntities, firstStatus);

                Ap = await db.RemedialTrackAxisProgresses.Where(a => a.EnrollmentId == Enrollment)
                    .OrderBy(a => a.Order).Select(a => a.Id).ToArrayAsync();
            }

            private static void AddModelQuestions(ApplicationDbContext db, int curriculumId, int modelId, int count)
            {
                for (var i = 1; i <= count; i++)
                {
                    var q = new Question
                    {
                        Id = Guid.NewGuid(), Title = $"سؤال {modelId}-{i}", ReferenceNumber = $"Q-{modelId}-{i}",
                        CurriculumId = curriculumId, LessonId = 1, CorrectAnswer = Correct,
                        Explanation = SecretExplanation, VideoUrl = "https://example.com/secret-video",
                        Options = new List<QuestionOption>
                        {
                            new() { Text = Correct }, new() { Text = Wrong }, new() { Text = "ج" }, new() { Text = "د" }
                        }
                    };
                    db.Questions.Add(q);
                    db.ProfessionalModelQuestions.Add(new ProfessionalModelQuestion { ModelId = modelId, QuestionId = q.Id, OrderNumber = i });
                }
            }

            private static async Task<int> AddEnrollmentAsync(
                ApplicationDbContext db, int pubId, int trackId, int studentId, List<RemedialTrackAxis> axes, RemedialTrackAxisStatus firstStatus)
            {
                var e = new RemedialTrackEnrollment { PublicationId = pubId, TrackId = trackId, StudentId = studentId, CreatedAtUtc = T0.AddDays(-2) };
                foreach (var a in axes)
                {
                    var first = a.Order == 1;
                    e.AxisProgresses.Add(new RemedialTrackAxisProgress
                    {
                        AxisId = a.Id, Order = a.Order,
                        Status = first ? firstStatus : RemedialTrackAxisStatus.Locked,
                        Round = firstStatus is RemedialTrackAxisStatus.Rewatch or RemedialTrackAxisStatus.AwaitingExam102 && first ? 2 : 1
                    });
                }
                e.CurrentAxisId = axes[0].Id;
                e.Status = RemedialTrackEnrollmentStatus.InProgress;
                db.RemedialTrackEnrollments.Add(e);
                await db.SaveChangesAsync();
                return e.Id;
            }

            public async Task SetAxisAsync(int apId, RemedialTrackAxisStatus status, int round = 1)
            {
                await using var db = Factory.CreateDbContext();
                var ap = await db.RemedialTrackAxisProgresses.FirstAsync(a => a.Id == apId);
                ap.Status = status; ap.Round = round;
                await db.SaveChangesAsync();
            }

            public async Task<int> StartAttemptAsync(int axisIndex = 0, int? student = null, int? enrollment = null)
            {
                var r = await Exams.StartExamAsync(student ?? Student, enrollment ?? Enrollment, Ap[axisIndex]);
                Assert.True(r.Status is RemedialTrackExamStartStatus.Created or RemedialTrackExamStartStatus.Existing, $"start: {r.Status}");
                return r.AttemptId;
            }

            // يجيب أول correctCount سؤالًا إجابة صحيحة والباقي خاطئة (أو يترك unanswered ... بلا إجابة)
            public async Task AnswerAsync(int attemptId, int correctCount, int unanswered = 0)
            {
                await using var db = Factory.CreateDbContext();
                var qids = await db.RemedialTrackExamAttemptQuestions.Where(x => x.AttemptId == attemptId)
                    .OrderBy(x => x.Order).Select(x => x.QuestionId).ToListAsync();
                for (var i = 0; i < qids.Count - unanswered; i++)
                {
                    var r = await Exams.SaveAnswerAsync(Student, attemptId, qids[i], i < correctCount ? Correct : Wrong);
                    Assert.Equal(RemedialTrackSaveAnswerStatus.Ok, r.Status);
                }
            }

            public async Task<RemedialTrackAxisProgress> GetApAsync(int index)
            {
                await using var db = Factory.CreateDbContext();
                return await db.RemedialTrackAxisProgresses.AsNoTracking().SingleAsync(a => a.Id == Ap[index]);
            }

            public async Task<RemedialTrackEnrollment> GetEnrollmentAsync()
            {
                await using var db = Factory.CreateDbContext();
                return await db.RemedialTrackEnrollments.AsNoTracking().SingleAsync(e => e.Id == Enrollment);
            }

            public async Task<int> EventCountAsync(RemedialTrackEventType type)
            {
                await using var db = Factory.CreateDbContext();
                return await db.RemedialTrackEvents.CountAsync(e => e.EnrollmentId == Enrollment && e.Type == type);
            }

            // محاولة كاملة: بدء + إجابة + تسليم
            public async Task<int> RunExamAsync(int axisIndex, int correct)
            {
                var id = await StartAttemptAsync(axisIndex);
                await AnswerAsync(id, correct);
                var s = await Exams.SubmitAsync(Student, id);
                Assert.Equal(RemedialTrackSubmitStatus.Submitted, s.Status);
                return id;
            }

            // يُكمل فيديوهات الجولة مباشرة في القاعدة ثم ينفّذ الانتقال عبر الخدمة
            public async Task<RemedialTrackTransitionResult> CompleteVideosAsync(int axisIndex)
            {
                var vm = await Progress.GetAxisAsync(Student, Enrollment, Ap[axisIndex]);
                Assert.NotNull(vm);
                await using (var db = Factory.CreateDbContext())
                {
                    var ids = vm!.Videos.Select(v => v.VideoProgressId).ToList();
                    var rows = await db.RemedialTrackVideoProgresses.Where(v => v.AxisProgressId == Ap[axisIndex] && v.Round == vm.Round).ToListAsync();
                    Assert.Equal(ids.Count, rows.Count);
                    foreach (var r in rows) { r.IsCompleted = true; r.CompletedAtUtc = T0; }
                    await db.SaveChangesAsync();
                }
                return await Progress.OnAllVideosCompletedAsync(Ap[axisIndex]);
            }
        }

        private static async Task<Fx> NewAsync(
            int axes = 3,
            RemedialTrackAxisStatus firstStatus = RemedialTrackAxisStatus.AwaitingExam101,
            RemedialTrackDeliveryMode mode = RemedialTrackDeliveryMode.Online,
            RemedialTrackPublicationStatus pubStatus = RemedialTrackPublicationStatus.Active,
            int questionsPerModel = 4,
            bool emptyModel102 = false)
        {
            var f = new Fx();
            await f.SeedAsync(axes, firstStatus, mode, pubStatus, questionsPerModel, emptyModel102);
            return f;
        }

        // ═══════════ التصحيح (دوال نقية) ═══════════

        [Theory]
        [InlineData("أ", "أ", true)]
        [InlineData("  أ  ", "أ", true)]            // مسافات
        [InlineData("أ", "  أ", true)]
        [InlineData("Abc", "aBC", true)]           // OrdinalIgnoreCase كما في الأصل
        [InlineData("ب", "أ", false)]
        [InlineData(null, "أ", false)]             // لا إجابة
        [InlineData("", "أ", false)]
        [InlineData("   ", "أ", false)]
        [InlineData("أ", null, false)]             // سؤال بلا إجابة صحيحة
        [InlineData("أ", "", false)]
        [InlineData("<b>أ</b>", "أ", false)]       // الأصل لا يطبّع HTML — نطابقه حرفيًا
        [InlineData("غير موجودة", "أ", false)]     // إجابة ليست ضمن الخيارات
        public void IsCorrect_MatchesExistingExamRule(string? selected, string? correct, bool expected) =>
            Assert.Equal(expected, RemedialTrackScoring.IsCorrect(selected, correct));

        [Theory]
        [InlineData(3, 4, 75.0)]
        [InlineData(1, 3, 33.3)]
        [InlineData(2, 3, 66.7)]
        [InlineData(0, 4, 0.0)]
        [InlineData(0, 0, 0.0)]
        public void ScorePercent_RoundsToOneDecimal(int correct, int total, double expected) =>
            Assert.Equal(expected, RemedialTrackScoring.ScorePercent(correct, total));

        [Theory]
        [InlineData(60.0, 60, true)]    // الحد نفسه ينجح (>=)
        [InlineData(59.9, 60, false)]
        [InlineData(100.0, 60, true)]
        [InlineData(0.0, 60, false)]
        public void IsPassed_UsesGreaterOrEqual(double score, int pass, bool expected) =>
            Assert.Equal(expected, RemedialTrackScoring.IsPassed(score, pass));

        // ═══════════ بدء الاختبار ═══════════

        [Fact]
        public async Task Start_Creates_AttemptWithFrozenOrderedQuestions_AndExpiry_AndEvent()
        {
            var f = await NewAsync();
            var r = await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[0]);

            Assert.Equal(RemedialTrackExamStartStatus.Created, r.Status);
            await using var db = f.Factory.CreateDbContext();
            var attempt = await db.RemedialTrackExamAttempts.Include(a => a.Questions).SingleAsync(a => a.Id == r.AttemptId);
            Assert.Equal(RemedialTrackExamNumber.Exam101, attempt.ExamNumber);
            Assert.Equal(f.Model101, attempt.ModelId);
            Assert.Equal(RemedialTrackAttemptStatus.InProgress, attempt.Status);
            Assert.Equal(T0, attempt.StartedAtUtc);
            Assert.Equal(T0.AddMinutes(30), attempt.ExpiresAtUtc);
            Assert.Equal(4, attempt.TotalQuestions);
            Assert.Equal(new[] { 1, 2, 3, 4 }, attempt.Questions.OrderBy(q => q.Order).Select(q => q.Order).ToArray());
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.ExamStarted));
        }

        [Fact]
        public async Task Start_On102_UsesModel102()
        {
            var f = await NewAsync(firstStatus: RemedialTrackAxisStatus.AwaitingExam102);
            var r = await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[0]);

            Assert.Equal(RemedialTrackExamStartStatus.Created, r.Status);
            await using var db = f.Factory.CreateDbContext();
            var a = await db.RemedialTrackExamAttempts.SingleAsync(x => x.Id == r.AttemptId);
            Assert.Equal(RemedialTrackExamNumber.Exam102, a.ExamNumber);
            Assert.Equal(f.Model102, a.ModelId);
        }

        [Theory]
        [InlineData(RemedialTrackAxisStatus.Videos)]
        [InlineData(RemedialTrackAxisStatus.Rewatch)]
        [InlineData(RemedialTrackAxisStatus.Passed)]
        [InlineData(RemedialTrackAxisStatus.FailedBlocked)]
        public async Task Start_BeforeVideosDoneOrAfterFinish_IsConflict_AndCreatesNothing(RemedialTrackAxisStatus status)
        {
            var f = await NewAsync(firstStatus: status);
            var r = await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[0]);

            Assert.Equal(RemedialTrackExamStartStatus.Conflict, r.Status);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(0, await db.RemedialTrackExamAttempts.CountAsync());
        }

        [Fact]
        public async Task Start_LockedAxis_IsConflict()
        {
            var f = await NewAsync();
            Assert.Equal(RemedialTrackExamStartStatus.Conflict, (await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[1])).Status);
        }

        [Fact]
        public async Task Start_Twice_ReturnsSameAttempt_NoDuplicate()
        {
            var f = await NewAsync();
            var first = await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[0]);
            var second = await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[0]);

            Assert.Equal(RemedialTrackExamStartStatus.Existing, second.Status);
            Assert.Equal(first.AttemptId, second.AttemptId);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(1, await db.RemedialTrackExamAttempts.CountAsync());
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.ExamStarted));
        }

        [Fact]
        public async Task Start_OtherStudentsEnrollment_IsNotFound_IDOR()
        {
            var f = await NewAsync();
            Assert.Equal(RemedialTrackExamStartStatus.NotFound, (await f.Exams.StartExamAsync(Fx.Other, f.Enrollment, f.Ap[0])).Status);
            // معرّف محور من تسجيل آخر بتسجيلي أنا
            Assert.Equal(RemedialTrackExamStartStatus.NotFound, (await f.Exams.StartExamAsync(Fx.Student, f.OtherEnrollment, f.Ap[0])).Status);
        }

        [Fact]
        public async Task Start_InPerson_WithoutVerifiedCode_NeedsCode()
        {
            var f = await NewAsync(mode: RemedialTrackDeliveryMode.InPerson);
            Assert.Equal(RemedialTrackExamStartStatus.NeedsCode, (await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[0])).Status);
        }

        [Theory]
        [InlineData(RemedialTrackPublicationStatus.Cancelled)]
        [InlineData(RemedialTrackPublicationStatus.Closed)]
        public async Task Start_NonActivePublication_IsForbidden(RemedialTrackPublicationStatus status)
        {
            var f = await NewAsync(pubStatus: status);
            Assert.Equal(RemedialTrackExamStartStatus.Forbidden, (await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[0])).Status);
        }

        [Fact]
        public async Task Start_ModelWithoutQuestions_IsNoQuestions()
        {
            var f = await NewAsync(firstStatus: RemedialTrackAxisStatus.AwaitingExam102, emptyModel102: true);
            Assert.Equal(RemedialTrackExamStartStatus.NoQuestions, (await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[0])).Status);
        }

        // ═══════════ واجهة الحل (D15: لا تسريب للإجابة) ═══════════

        [Fact]
        public async Task Solve_NeverExposesCorrectAnswerExplanationOrVideo()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();

            var r = await f.Exams.GetSolveAsync(Fx.Student, id);

            Assert.Equal(RemedialTrackSolveStatus.Ok, r.Status);
            var vm = r.Model!;
            Assert.Equal(4, vm.Questions.Count);
            Assert.All(vm.Questions, q =>
            {
                Assert.Null(q.Question.CorrectAnswer);
                Assert.Null(q.Question.Explanation);
                Assert.Null(q.Question.VideoUrl);
                Assert.Null(q.Question.Hint);
                Assert.False(q.Question.IsAnswerConfirmed);
                Assert.Null(q.Question.SelectedCorrectIndex);
                Assert.Equal(4, q.Question.Options.Count);   // الخيارات تُعرض دون تمييز الصحيح
            });

            // كل ما يصل للـ View يُسلسَل: لا أثر لنص الشرح أو رابط الفيديو السري
            var json = JsonSerializer.Serialize(vm);
            Assert.DoesNotContain(SecretExplanation, json);
            Assert.DoesNotContain("secret-video", json);
        }

        [Fact]
        public async Task Solve_ReturnsSavedAnswers_AndServerRemainingTime()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            await f.AnswerAsync(id, 2, unanswered: 1);   // 3 مُجابة، 1 بلا إجابة

            f.Clock.Now = T0.AddMinutes(10);
            var vm = (await f.Exams.GetSolveAsync(Fx.Student, id)).Model!;

            Assert.Equal(3, vm.AnsweredCount);
            Assert.Equal(20 * 60, vm.RemainingSeconds);
            Assert.Equal(RemedialTrackExamNumber.Exam101, vm.ExamNumber);
            Assert.Equal("محور 1", vm.AxisTitle);
        }

        [Fact]
        public async Task Solve_OtherStudentsAttempt_IsNotFound_IDOR()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            Assert.Equal(RemedialTrackSolveStatus.NotFound, (await f.Exams.GetSolveAsync(Fx.Other, id)).Status);
        }

        [Fact]
        public async Task Solve_SubmittedAttempt_IsClosed()
        {
            var f = await NewAsync();
            var id = await f.RunExamAsync(0, 4);
            Assert.Equal(RemedialTrackSolveStatus.Closed, (await f.Exams.GetSolveAsync(Fx.Student, id)).Status);
        }

        [Fact]
        public async Task Solve_AfterExpiryAndGrace_AutoSubmitsAsExpired_AndTransitions()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            await f.AnswerAsync(id, 4);

            f.Clock.Now = T0.AddMinutes(30).AddSeconds(RemedialTrackExamService.SubmitGraceSeconds + 1);
            Assert.Equal(RemedialTrackSolveStatus.Closed, (await f.Exams.GetSolveAsync(Fx.Student, id)).Status);

            await using var db = f.Factory.CreateDbContext();
            var a = await db.RemedialTrackExamAttempts.SingleAsync(x => x.Id == id);
            Assert.Equal(RemedialTrackAttemptStatus.Expired, a.Status);
            Assert.Equal(100.0, a.ScorePercent);
            Assert.Equal(RemedialTrackAxisStatus.Passed, (await f.GetApAsync(0)).Status);
        }

        [Fact]
        public async Task Solve_InsideGraceAfterExpiry_StillShowsWithZeroRemaining()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();

            f.Clock.Now = T0.AddMinutes(30).AddSeconds(10);
            var r = await f.Exams.GetSolveAsync(Fx.Student, id);

            Assert.Equal(RemedialTrackSolveStatus.Ok, r.Status);
            Assert.Equal(0, r.Model!.RemainingSeconds);
        }

        // ═══════════ حفظ الإجابة ═══════════

        private static async Task<List<Guid>> QuestionIdsAsync(Fx f, int attemptId)
        {
            await using var db = f.Factory.CreateDbContext();
            return await db.RemedialTrackExamAttemptQuestions.Where(x => x.AttemptId == attemptId)
                .OrderBy(x => x.Order).Select(x => x.QuestionId).ToListAsync();
        }

        [Fact]
        public async Task SaveAnswer_Valid_StoresOptionText_AndCanBeChanged()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            var q = (await QuestionIdsAsync(f, id))[0];

            Assert.Equal(RemedialTrackSaveAnswerStatus.Ok, (await f.Exams.SaveAnswerAsync(Fx.Student, id, q, "  " + Wrong + " ")).Status);
            Assert.Equal(RemedialTrackSaveAnswerStatus.Ok, (await f.Exams.SaveAnswerAsync(Fx.Student, id, q, Correct)).Status);

            await using var db = f.Factory.CreateDbContext();
            var row = await db.RemedialTrackExamAttemptQuestions.SingleAsync(x => x.AttemptId == id && x.QuestionId == q);
            Assert.Equal(Correct, row.SelectedAnswer);
            Assert.Equal(T0, row.AnsweredAtUtc);
        }

        [Fact]
        public async Task SaveAnswer_NotAnOption_IsBadRequest_AndNothingStored()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            var q = (await QuestionIdsAsync(f, id))[0];

            Assert.Equal(RemedialTrackSaveAnswerStatus.BadRequest, (await f.Exams.SaveAnswerAsync(Fx.Student, id, q, "خيار مخترع")).Status);
            Assert.Equal(RemedialTrackSaveAnswerStatus.BadRequest, (await f.Exams.SaveAnswerAsync(Fx.Student, id, q, null)).Status);
            Assert.Equal(RemedialTrackSaveAnswerStatus.BadRequest, (await f.Exams.SaveAnswerAsync(Fx.Student, id, q, "  ")).Status);

            await using var db = f.Factory.CreateDbContext();
            Assert.Null((await db.RemedialTrackExamAttemptQuestions.SingleAsync(x => x.AttemptId == id && x.QuestionId == q)).SelectedAnswer);
        }

        [Fact]
        public async Task SaveAnswer_QuestionNotInAttempt_IsNotFound()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            Assert.Equal(RemedialTrackSaveAnswerStatus.NotFound, (await f.Exams.SaveAnswerAsync(Fx.Student, id, Guid.NewGuid(), Correct)).Status);
        }

        [Fact]
        public async Task SaveAnswer_OtherStudent_IsNotFound_IDOR()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            var q = (await QuestionIdsAsync(f, id))[0];

            Assert.Equal(RemedialTrackSaveAnswerStatus.NotFound, (await f.Exams.SaveAnswerAsync(Fx.Other, id, q, Correct)).Status);
            await using var db = f.Factory.CreateDbContext();
            Assert.Null((await db.RemedialTrackExamAttemptQuestions.SingleAsync(x => x.AttemptId == id && x.QuestionId == q)).SelectedAnswer);
        }

        [Fact]
        public async Task SaveAnswer_AfterExpiry_IsExpired()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            var q = (await QuestionIdsAsync(f, id))[0];

            f.Clock.Now = T0.AddMinutes(30).AddSeconds(1);
            Assert.Equal(RemedialTrackSaveAnswerStatus.Expired, (await f.Exams.SaveAnswerAsync(Fx.Student, id, q, Correct)).Status);
        }

        [Fact]
        public async Task SaveAnswer_AfterSubmit_IsExpired()
        {
            var f = await NewAsync();
            var id = await f.RunExamAsync(0, 4);
            var q = (await QuestionIdsAsync(f, id))[0];
            Assert.Equal(RemedialTrackSaveAnswerStatus.Expired, (await f.Exams.SaveAnswerAsync(Fx.Student, id, q, Wrong)).Status);
        }

        [Fact]
        public async Task SaveAnswer_CancelledPublicationMidExam_IsForbidden()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            var q = (await QuestionIdsAsync(f, id))[0];

            await using (var db = f.Factory.CreateDbContext())
            {
                var pub = await db.RemedialTrackPublications.FirstAsync();
                pub.Status = RemedialTrackPublicationStatus.Cancelled;
                await db.SaveChangesAsync();
            }
            Assert.Equal(RemedialTrackSaveAnswerStatus.Forbidden, (await f.Exams.SaveAnswerAsync(Fx.Student, id, q, Correct)).Status);
        }

        [Fact]
        public async Task SaveAnswer_InPerson_RenewedCodeMidExam_StillContinues_D7()
        {
            var f = await NewAsync(mode: RemedialTrackDeliveryMode.InPerson);
            await using (var db = f.Factory.CreateDbContext())
            {
                var e = await db.RemedialTrackEnrollments.FirstAsync(x => x.Id == f.Enrollment);
                e.VerifiedCodeVersion = 1;
                await db.SaveChangesAsync();
            }
            var id = await f.StartAttemptAsync();
            var q = (await QuestionIdsAsync(f, id))[0];

            // تجديد الرقم: النسخة 2
            await using (var db = f.Factory.CreateDbContext())
            {
                var pub = await db.RemedialTrackPublications.FirstAsync();
                pub.CodeVersion = 2;
                await db.SaveChangesAsync();
            }

            Assert.Equal(RemedialTrackSaveAnswerStatus.Ok, (await f.Exams.SaveAnswerAsync(Fx.Student, id, q, Correct)).Status);
            Assert.Equal(RemedialTrackSolveStatus.Ok, (await f.Exams.GetSolveAsync(Fx.Student, id)).Status);
            // لكن بدء اختبار جديد يتطلب الرقم الجديد
            Assert.Equal(RemedialTrackSubmitStatus.Submitted, (await f.Exams.SubmitAsync(Fx.Student, id)).Status);
        }

        // ═══════════ التسليم والتصحيح والانتقالات ═══════════

        [Fact]
        public async Task Submit_Pass101_ScoresAndOpensNextAxis_WithEvents()   // (أ)
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            await f.AnswerAsync(id, 3);   // 3 صحيحة + 1 خاطئة = 75%
            f.Clock.Now = T0.AddMinutes(5);

            var r = await f.Exams.SubmitAsync(Fx.Student, id);
            Assert.Equal(RemedialTrackSubmitStatus.Submitted, r.Status);

            await using var db = f.Factory.CreateDbContext();
            var a = await db.RemedialTrackExamAttempts.Include(x => x.Questions).SingleAsync(x => x.Id == id);
            Assert.Equal(RemedialTrackAttemptStatus.Submitted, a.Status);
            Assert.Equal(T0.AddMinutes(5), a.SubmittedAtUtc);
            Assert.Equal(3, a.CorrectCount);
            Assert.Equal(4, a.TotalQuestions);
            Assert.Equal(75.0, a.ScorePercent);
            Assert.True(a.IsPassed);
            Assert.Equal(3, a.Questions.Count(q => q.IsCorrect == true));
            Assert.Equal(1, a.Questions.Count(q => q.IsCorrect == false));

            var ap1 = await f.GetApAsync(0);
            Assert.Equal(RemedialTrackAxisStatus.Passed, ap1.Status);
            Assert.Equal(75.0, ap1.Exam101Percent);
            Assert.Equal(T0.AddMinutes(5), ap1.PassedAtUtc);

            var ap2 = await f.GetApAsync(1);
            Assert.Equal(RemedialTrackAxisStatus.Videos, ap2.Status);
            Assert.Equal(1, ap2.Round);
            Assert.Equal(T0.AddMinutes(5), ap2.OpenedAtUtc);
            Assert.Equal(RemedialTrackAxisStatus.Locked, (await f.GetApAsync(2)).Status);   // لا فتح لمحورين

            var enr = await f.GetEnrollmentAsync();
            Assert.Equal(RemedialTrackEnrollmentStatus.InProgress, enr.Status);
            Assert.NotEqual(0, enr.CurrentAxisId);
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.ExamPassed));
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.AxisOpened));
        }

        [Fact]
        public async Task Submit_ExactlyAtPassPercent_Passes()
        {
            // 5 أسئلة، 3 صحيحة = 60% = الحد
            var f = await NewAsync(questionsPerModel: 5);
            var id = await f.RunExamAsync(0, 3);

            await using var db = f.Factory.CreateDbContext();
            var a = await db.RemedialTrackExamAttempts.SingleAsync(x => x.Id == id);
            Assert.Equal(60.0, a.ScorePercent);
            Assert.True(a.IsPassed);
        }

        [Fact]
        public async Task Submit_Fail101_OpensRewatchRound2_WithFreshVideoRows()   // (ب)
        {
            var f = await NewAsync();
            // جولة 1: أنشئ صفوف الفيديو كما يفعل الطالب عند فتح الصفحة، وأكمل ما سبق فعلًا
            await f.SetAxisAsync(f.Ap[0], RemedialTrackAxisStatus.Videos);
            await f.CompleteVideosAsync(0);
            Assert.Equal(RemedialTrackAxisStatus.AwaitingExam101, (await f.GetApAsync(0)).Status);

            var id = await f.RunExamAsync(0, 1);   // 25%

            var ap = await f.GetApAsync(0);
            Assert.Equal(RemedialTrackAxisStatus.Rewatch, ap.Status);
            Assert.Equal(2, ap.Round);
            Assert.Equal(25.0, ap.Exam101Percent);
            Assert.Null(ap.PassedAtUtc);
            Assert.Equal(RemedialTrackAxisStatus.Locked, (await f.GetApAsync(1)).Status);

            await using var db = f.Factory.CreateDbContext();
            var round2 = await db.RemedialTrackVideoProgresses.Where(v => v.AxisProgressId == f.Ap[0] && v.Round == 2).ToListAsync();
            Assert.Equal(2, round2.Count);
            Assert.All(round2, v => { Assert.False(v.IsCompleted); Assert.Equal(0, v.WatchedSeconds); });
            // فيديوهات الجولة 1 تبقى للسجل
            Assert.Equal(2, await db.RemedialTrackVideoProgresses.CountAsync(v => v.AxisProgressId == f.Ap[0] && v.Round == 1 && v.IsCompleted));
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.ExamFailed));
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.AxisRewatchOpened));
            Assert.Equal(0, await f.EventCountAsync(RemedialTrackEventType.AxisNotPassed));

            // صفحة المحور تعرض فيديوهات الجولة 2 قابلة للمشاهدة وبلا زر اختبار قبل إتمامها
            var vm = (await f.Progress.GetAxisAsync(Fx.Student, f.Enrollment, f.Ap[0]))!;
            Assert.True(vm.CanWatch);
            Assert.Equal(2, vm.Round);
            Assert.False(vm.ExamReady);
            Assert.Equal(2, vm.Videos.Count);
            Assert.All(vm.Videos, v => Assert.False(v.IsCompleted));
            Assert.Contains(vm.Attempts, x => x.AttemptId == id && x.IsClosed);
        }

        [Fact]
        public async Task FullPath_Fail101_Rewatch_Then102Pass_MovesToNextAxis()   // (ب) كاملة
        {
            var f = await NewAsync();
            await f.RunExamAsync(0, 1);                                   // 101 دون الحد
            var done = await f.CompleteVideosAsync(0);                    // إعادة الفيديوهات
            Assert.True(done.Applied);
            Assert.Equal(RemedialTrackAxisStatus.AwaitingExam102, done.Status);

            await f.RunExamAsync(0, 4);                                   // 102 ناجح

            var ap1 = await f.GetApAsync(0);
            Assert.Equal(RemedialTrackAxisStatus.Passed, ap1.Status);
            Assert.Equal(2, ap1.Round);
            Assert.Equal(25.0, ap1.Exam101Percent);
            Assert.Equal(100.0, ap1.Exam102Percent);
            Assert.Equal(RemedialTrackAxisStatus.Videos, (await f.GetApAsync(1)).Status);
        }

        [Fact]
        public async Task FullPath_Fail101And102_BlocksAxis_RecordsNotPassed_NoNextAxis()   // (ج)
        {
            var f = await NewAsync();
            await f.RunExamAsync(0, 1);
            await f.CompleteVideosAsync(0);
            await f.RunExamAsync(0, 2);          // 102 = 50% < 60

            var ap1 = await f.GetApAsync(0);
            Assert.Equal(RemedialTrackAxisStatus.FailedBlocked, ap1.Status);
            Assert.Equal(25.0, ap1.Exam101Percent);
            Assert.Equal(50.0, ap1.Exam102Percent);
            Assert.Equal(T0, ap1.FailedAtUtc);
            Assert.Equal(RemedialTrackAxisStatus.Locked, (await f.GetApAsync(1)).Status);   // لا فتح تلقائي
            Assert.Equal(RemedialTrackEnrollmentStatus.InProgress, (await f.GetEnrollmentAsync()).Status);

            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.AxisNotPassed));
            await using var db = f.Factory.CreateDbContext();
            var msg = (await db.RemedialTrackEvents.SingleAsync(e => e.Type == RemedialTrackEventType.AxisNotPassed && e.EnrollmentId == f.Enrollment)).Message;
            Assert.Contains("لم يجتز الطالب الخطة العلاجية للمحور «محور 1»", msg);
            Assert.Contains("الأول: 25%", msg);
            Assert.Contains("الثاني: 50%", msg);

            // لا اختبار ثالث، ولا بدء اختبار في المحور التالي المغلق
            Assert.Equal(RemedialTrackExamStartStatus.Conflict, (await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[0])).Status);
            Assert.Equal(RemedialTrackExamStartStatus.Conflict, (await f.Exams.StartExamAsync(Fx.Student, f.Enrollment, f.Ap[1])).Status);
            Assert.Equal(2, await db.RemedialTrackExamAttempts.CountAsync(a => a.AxisProgressId == f.Ap[0]));
        }

        [Fact]
        public async Task LastAxis_Pass_CompletesEnrollment()   // (د) اجتياز
        {
            var f = await NewAsync(axes: 1);
            await f.RunExamAsync(0, 4);

            var enr = await f.GetEnrollmentAsync();
            Assert.Equal(RemedialTrackEnrollmentStatus.Completed, enr.Status);
            Assert.Equal(T0, enr.CompletedAtUtc);
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.TrackCompleted));
            Assert.Equal(0, await f.EventCountAsync(RemedialTrackEventType.AxisOpened));
        }

        [Fact]
        public async Task LastAxis_Fail102_CompletesWithFailures()   // (د) رسوب في 102
        {
            var f = await NewAsync(axes: 1);
            await f.RunExamAsync(0, 0);
            await f.CompleteVideosAsync(0);
            await f.RunExamAsync(0, 0);

            var enr = await f.GetEnrollmentAsync();
            Assert.Equal(RemedialTrackEnrollmentStatus.CompletedWithFailures, enr.Status);
            Assert.Equal(T0, enr.CompletedAtUtc);
            Assert.Equal(RemedialTrackAxisStatus.FailedBlocked, (await f.GetApAsync(0)).Status);
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.TrackCompleted));
        }

        [Fact]
        public async Task LastAxis_Pass_AfterEarlierAxisOpenedByAdmin_IsCompletedWithFailures()
        {
            var f = await NewAsync(axes: 2);
            await f.RunExamAsync(0, 0);
            await f.CompleteVideosAsync(0);
            await f.RunExamAsync(0, 0);                                            // محور 1 محجوب
            var open = await f.Progress.AdminOpenNextAsync(f.Ap[0], "قرار الإدارة", new RemedialTrackActor("u1", "المشرف"));
            Assert.True(open.Applied);

            // المحور الأخير: يكمل فيديوهاته ثم 101 ناجح
            await f.CompleteVideosAsync(1);
            await f.RunExamAsync(1, 4);

            Assert.Equal(RemedialTrackEnrollmentStatus.CompletedWithFailures, (await f.GetEnrollmentAsync()).Status);
        }

        // ═══════════ تسليم: حدود الوقت والازدواج ═══════════

        [Fact]
        public async Task Submit_Twice_IsIdempotent_SameResult_NoDuplicateEffects()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            await f.AnswerAsync(id, 4);

            var first = await f.Exams.SubmitAsync(Fx.Student, id);
            f.Clock.Now = T0.AddMinutes(1);
            var second = await f.Exams.SubmitAsync(Fx.Student, id);

            Assert.Equal(RemedialTrackSubmitStatus.Submitted, first.Status);
            Assert.Equal(RemedialTrackSubmitStatus.AlreadyClosed, second.Status);
            await using var db = f.Factory.CreateDbContext();
            var a = await db.RemedialTrackExamAttempts.SingleAsync(x => x.Id == id);
            Assert.Equal(T0, a.SubmittedAtUtc);                                      // لم يتغيّر
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.ExamPassed));
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.AxisOpened));
            Assert.Equal(RemedialTrackAxisStatus.Videos, (await f.GetApAsync(1)).Status);
        }

        [Fact]
        public async Task OnExamSubmitted_CalledTwice_SecondIsNoOp()
        {
            var f = await NewAsync();
            var id = await f.RunExamAsync(0, 4);

            var again = await f.Progress.OnExamSubmittedAsync(id);

            Assert.False(again.Applied);
            Assert.Null(again.Error);
            Assert.Equal(RemedialTrackAxisStatus.Passed, again.Status);
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.ExamPassed));
        }

        [Fact]
        public async Task OnExamSubmitted_BeforeSubmit_IsRejected()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            var r = await f.Progress.OnExamSubmittedAsync(id);
            Assert.False(r.Applied);
            Assert.NotNull(r.Error);
            Assert.Equal(RemedialTrackAxisStatus.AwaitingExam101, (await f.GetApAsync(0)).Status);
        }

        [Fact]
        public async Task Submit_WithinGrace_IsSubmitted_NotExpired()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            await f.AnswerAsync(id, 4);

            f.Clock.Now = T0.AddMinutes(30).AddSeconds(RemedialTrackExamService.SubmitGraceSeconds);
            Assert.Equal(RemedialTrackSubmitStatus.Submitted, (await f.Exams.SubmitAsync(Fx.Student, id)).Status);

            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(RemedialTrackAttemptStatus.Submitted, (await db.RemedialTrackExamAttempts.SingleAsync(x => x.Id == id)).Status);
        }

        [Fact]
        public async Task Submit_AfterGrace_IsExpired_ScoredOnAnsweredOnly()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            await f.AnswerAsync(id, 2, unanswered: 2);   // سؤالان صحيحان فقط، والباقي بلا إجابة

            f.Clock.Now = T0.AddMinutes(30).AddSeconds(RemedialTrackExamService.SubmitGraceSeconds + 1);
            await f.Exams.SubmitAsync(Fx.Student, id);

            await using var db = f.Factory.CreateDbContext();
            var a = await db.RemedialTrackExamAttempts.SingleAsync(x => x.Id == id);
            Assert.Equal(RemedialTrackAttemptStatus.Expired, a.Status);
            Assert.Equal(2, a.CorrectCount);
            Assert.Equal(50.0, a.ScorePercent);
            Assert.False(a.IsPassed);
            Assert.Equal(RemedialTrackAxisStatus.Rewatch, (await f.GetApAsync(0)).Status);
        }

        [Fact]
        public async Task Submit_Unanswered_CountedIncorrect()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            await f.Exams.SubmitAsync(Fx.Student, id);

            await using var db = f.Factory.CreateDbContext();
            var a = await db.RemedialTrackExamAttempts.Include(x => x.Questions).SingleAsync(x => x.Id == id);
            Assert.Equal(0, a.CorrectCount);
            Assert.Equal(0.0, a.ScorePercent);
            Assert.All(a.Questions, q => Assert.False(q.IsCorrect));
        }

        [Fact]
        public async Task Submit_OtherStudent_IsNotFound_AndAttemptUntouched_IDOR()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();

            Assert.Equal(RemedialTrackSubmitStatus.NotFound, (await f.Exams.SubmitAsync(Fx.Other, id)).Status);
            await using var db = f.Factory.CreateDbContext();
            Assert.Equal(RemedialTrackAttemptStatus.InProgress, (await db.RemedialTrackExamAttempts.SingleAsync(x => x.Id == id)).Status);
        }

        // ═══════════ النتيجة ═══════════

        [Fact]
        public async Task Result_Pass_WithNextAxis_PointsToNextAxis()
        {
            var f = await NewAsync();
            var id = await f.RunExamAsync(0, 4);

            var r = await f.Exams.GetResultAsync(Fx.Student, id);
            Assert.Equal(RemedialTrackResultStatus.Ok, r.Status);
            var vm = r.Model!;
            Assert.True(vm.IsPassed);
            Assert.Equal(100.0, vm.ScorePercent);
            Assert.Equal(4, vm.CorrectCount);
            Assert.Equal(4, vm.TotalQuestions);
            Assert.Equal(60, vm.PassPercent);
            Assert.Equal(RemedialTrackResultNext.NextAxis, vm.Next);
            Assert.Equal(f.Ap[1], vm.NextAxisProgressId);
            Assert.Equal("محور 2", vm.NextAxisTitle);
        }

        [Fact]
        public async Task Result_Pass_OnLastAxis_PointsToFinalReport()
        {
            var f = await NewAsync(axes: 1);
            var id = await f.RunExamAsync(0, 4);
            Assert.Equal(RemedialTrackResultNext.FinalReport, (await f.Exams.GetResultAsync(Fx.Student, id)).Model!.Next);
        }

        [Fact]
        public async Task Result_Fail101_PointsToRewatch()
        {
            var f = await NewAsync();
            var id = await f.RunExamAsync(0, 0);
            var vm = (await f.Exams.GetResultAsync(Fx.Student, id)).Model!;
            Assert.False(vm.IsPassed);
            Assert.Equal(RemedialTrackResultNext.RewatchThenExam102, vm.Next);
        }

        [Fact]
        public async Task Result_Fail102_PointsToAdmin_OrFinalWhenLast()
        {
            var f = await NewAsync();
            await f.RunExamAsync(0, 0);
            await f.CompleteVideosAsync(0);
            var id = await f.RunExamAsync(0, 0);
            Assert.Equal(RemedialTrackResultNext.AwaitAdmin, (await f.Exams.GetResultAsync(Fx.Student, id)).Model!.Next);

            var g = await NewAsync(axes: 1);
            await g.RunExamAsync(0, 0);
            await g.CompleteVideosAsync(0);
            var id2 = await g.RunExamAsync(0, 0);
            Assert.Equal(RemedialTrackResultNext.AwaitAdminFinal, (await g.Exams.GetResultAsync(Fx.Student, id2)).Model!.Next);
        }

        [Fact]
        public async Task Result_UsesNoFailureLanguage()
        {
            var f = await NewAsync();
            var id = await f.RunExamAsync(0, 0);
            var vm = (await f.Exams.GetResultAsync(Fx.Student, id)).Model!;

            var text = string.Join(" ", vm.Tone.Title, vm.Tone.Subtitle, vm.Tone.BadgeText, vm.Tone.MotivationHeading, vm.Tone.MotivationMsg);
            foreach (var banned in new[] { "فشل", "راسب", "لم تنجح", "لم ينجح", "ضعيف" })
                Assert.DoesNotContain(banned, text);
        }

        [Fact]
        public async Task Result_InProgress_RedirectsToSolve_AndOtherStudentIsNotFound()
        {
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();

            Assert.Equal(RemedialTrackResultStatus.InProgress, (await f.Exams.GetResultAsync(Fx.Student, id)).Status);
            Assert.Equal(RemedialTrackResultStatus.NotFound, (await f.Exams.GetResultAsync(Fx.Other, id)).Status);
        }

        [Fact]
        public async Task Result_HealsInterruptedTransition()
        {
            // محاكاة انقطاع بين حفظ التصحيح والانتقال: المحاولة مُسلَّمة والمحور ما زال ينتظر 101
            var f = await NewAsync();
            var id = await f.StartAttemptAsync();
            await using (var db = f.Factory.CreateDbContext())
            {
                var a = await db.RemedialTrackExamAttempts.SingleAsync(x => x.Id == id);
                a.Status = RemedialTrackAttemptStatus.Submitted;
                a.SubmittedAtUtc = T0;
                a.TotalQuestions = 4; a.CorrectCount = 4; a.ScorePercent = 100; a.IsPassed = true;
                await db.SaveChangesAsync();
            }
            Assert.Equal(RemedialTrackAxisStatus.AwaitingExam101, (await f.GetApAsync(0)).Status);

            var r = await f.Exams.GetResultAsync(Fx.Student, id);

            Assert.Equal(RemedialTrackResultStatus.Ok, r.Status);
            Assert.Equal(RemedialTrackAxisStatus.Passed, (await f.GetApAsync(0)).Status);
            Assert.Equal(RemedialTrackAxisStatus.Videos, (await f.GetApAsync(1)).Status);
        }

        // ═══════════ OnAllVideosCompleted ═══════════

        [Fact]
        public async Task OnAllVideosCompleted_WithPendingVideos_IsRejected()
        {
            var f = await NewAsync(firstStatus: RemedialTrackAxisStatus.Videos);
            await f.Progress.GetAxisAsync(Fx.Student, f.Enrollment, f.Ap[0]);   // ينشئ صفوف الجولة 1

            var r = await f.Progress.OnAllVideosCompletedAsync(f.Ap[0]);

            Assert.False(r.Applied);
            Assert.NotNull(r.Error);
            Assert.Equal(RemedialTrackAxisStatus.Videos, (await f.GetApAsync(0)).Status);
        }

        [Fact]
        public async Task OnAllVideosCompleted_AllDone_Transitions_AndIsIdempotent()
        {
            var f = await NewAsync(firstStatus: RemedialTrackAxisStatus.Videos);
            var first = await f.CompleteVideosAsync(0);
            var second = await f.Progress.OnAllVideosCompletedAsync(f.Ap[0]);

            Assert.True(first.Applied);
            Assert.Equal(RemedialTrackAxisStatus.AwaitingExam101, first.Status);
            Assert.False(second.Applied);
            Assert.Null(second.Error);
            Assert.Equal(RemedialTrackAxisStatus.AwaitingExam101, (await f.GetApAsync(0)).Status);
        }

        [Fact]
        public async Task OnAllVideosCompleted_LockedAxis_IsRejected()
        {
            var f = await NewAsync();
            var r = await f.Progress.OnAllVideosCompletedAsync(f.Ap[1]);
            Assert.False(r.Applied);
            Assert.NotNull(r.Error);
        }

        // ═══════════ AdminOpenNext ═══════════

        private static async Task<Fx> BlockedAxisAsync(int axes = 3)
        {
            var f = await NewAsync(axes);
            await f.RunExamAsync(0, 0);
            await f.CompleteVideosAsync(0);
            await f.RunExamAsync(0, 0);
            Assert.Equal(RemedialTrackAxisStatus.FailedBlocked, (await f.GetApAsync(0)).Status);
            return f;
        }

        [Fact]
        public async Task AdminOpenNext_OpensNextAxis_RecordsActorAndReason()
        {
            var f = await BlockedAxisAsync();
            f.Clock.Now = T0.AddDays(1);

            var r = await f.Progress.AdminOpenNextAsync(f.Ap[0], "  موافقة ولي الأمر  ", new RemedialTrackActor("admin-1", "أ. سعد"));

            Assert.True(r.Applied);
            Assert.Equal(RemedialTrackAxisStatus.FailedOpenedByAdmin, r.Status);
            var ap1 = await f.GetApAsync(0);
            Assert.Equal(RemedialTrackAxisStatus.FailedOpenedByAdmin, ap1.Status);
            Assert.Equal("admin-1", ap1.AdminOpenedByUserId);
            Assert.Equal("أ. سعد", ap1.AdminOpenedByName);
            Assert.Equal("موافقة ولي الأمر", ap1.AdminOpenReason);
            Assert.Equal(T0.AddDays(1), ap1.AdminOpenedAtUtc);
            Assert.Equal(RemedialTrackAxisStatus.Videos, (await f.GetApAsync(1)).Status);
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.AdminOpenedNext));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AdminOpenNext_RequiresReason(string? reason)
        {
            var f = await BlockedAxisAsync();
            var r = await f.Progress.AdminOpenNextAsync(f.Ap[0], reason, new RemedialTrackActor("a", "b"));

            Assert.False(r.Applied);
            Assert.NotNull(r.Error);
            Assert.Equal(RemedialTrackAxisStatus.FailedBlocked, (await f.GetApAsync(0)).Status);
            Assert.Equal(RemedialTrackAxisStatus.Locked, (await f.GetApAsync(1)).Status);
        }

        [Fact]
        public async Task AdminOpenNext_IsIdempotent_AndRejectedForNonBlockedAxis()
        {
            var f = await BlockedAxisAsync();
            var actor = new RemedialTrackActor("a", "b");
            await f.Progress.AdminOpenNextAsync(f.Ap[0], "سبب", actor);
            var again = await f.Progress.AdminOpenNextAsync(f.Ap[0], "سبب آخر", actor);

            Assert.False(again.Applied);
            Assert.Null(again.Error);
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.AdminOpenedNext));
            Assert.Equal("سبب", (await f.GetApAsync(0)).AdminOpenReason);

            var other = await NewAsync();   // محور في AwaitingExam101: غير محجوب
            var bad = await other.Progress.AdminOpenNextAsync(other.Ap[0], "سبب", actor);
            Assert.False(bad.Applied);
            Assert.NotNull(bad.Error);
            Assert.Equal(RemedialTrackAxisStatus.Locked, (await other.GetApAsync(1)).Status);
        }

        [Fact]
        public async Task AdminOpenNext_OnLastAxis_CompletesWithFailures()
        {
            var f = await BlockedAxisAsync(axes: 1);
            Assert.Equal(RemedialTrackEnrollmentStatus.CompletedWithFailures, (await f.GetEnrollmentAsync()).Status);
            var r = await f.Progress.AdminOpenNextAsync(f.Ap[0], "إغلاق", new RemedialTrackActor("a", "b"));
            Assert.True(r.Applied);
            Assert.Equal(RemedialTrackEnrollmentStatus.CompletedWithFailures, (await f.GetEnrollmentAsync()).Status);
            Assert.Equal(1, await f.EventCountAsync(RemedialTrackEventType.TrackCompleted));   // لا تكرار
        }

        // ═══════════ صفحة المحور (Axis VM) ═══════════

        [Fact]
        public async Task AxisVm_AwaitingExam_ShowsStartThenResume()
        {
            var f = await NewAsync();

            var before = (await f.Progress.GetAxisAsync(Fx.Student, f.Enrollment, f.Ap[0]))!;
            Assert.True(before.ExamReady);
            Assert.Equal(RemedialTrackExamNumber.Exam101, before.PendingExam);
            Assert.Equal(30, before.ExamDurationMinutes);
            Assert.Null(before.InProgressAttemptId);

            var id = await f.StartAttemptAsync();
            var during = (await f.Progress.GetAxisAsync(Fx.Student, f.Enrollment, f.Ap[0]))!;
            Assert.Equal(id, during.InProgressAttemptId);
        }

        // ═══════════ RTK-S8.1: المسار الجديد (D19) ═══════════

        [Fact]
        public async Task GetPlan_OpensLockedAxis_WhosePreviousWasPassed_LegacyScheduleLeftover()
        {
            var f = await NewAsync();
            await f.RunExamAsync(0, 4);                                   // اجتياز 101 ← يُفتح التالي
            await using (var db = f.Factory.CreateDbContext())            // محاكاة محور بقي Locked بسبب الجدولة القديمة
            {
                var next = await db.RemedialTrackAxisProgresses.SingleAsync(a => a.Id == f.Ap[1]);
                next.Status = RemedialTrackAxisStatus.Locked;
                await db.SaveChangesAsync();
            }

            var plan = await f.Progress.GetPlanAsync(Fx.Student, f.Enrollment);

            Assert.NotNull(plan);
            Assert.Equal(RemedialTrackAxisStatus.Videos, (await f.GetApAsync(1)).Status);
            Assert.Equal(RemedialTrackAxisStatus.Locked, (await f.GetApAsync(2)).Status);   // لا قفز لمحورين
        }

        [Fact]
        public async Task GetPlan_DoesNotOpenAxis_WhenPreviousNotPassed()
        {
            var f = await NewAsync();
            await f.RunExamAsync(0, 1);                                   // رسوب 101 ← جولة 2

            await f.Progress.GetPlanAsync(Fx.Student, f.Enrollment);

            Assert.Equal(RemedialTrackAxisStatus.Locked, (await f.GetApAsync(1)).Status);
        }
    }
}
