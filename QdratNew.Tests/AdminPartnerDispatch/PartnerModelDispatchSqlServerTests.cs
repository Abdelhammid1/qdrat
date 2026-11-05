using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Implementations;
using QdratNew.Services.PartnerDispatch;
using QdratNew.Services.ProfessionalModels;
using QdratNew.Tests.Integration;
using Xunit;
using HomeworkEntity = QdratNew.Entities.Homework;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S2.1 — الإرسال المتعدد على قاعدة حقيقية: نسخة لكل شريك، عزل الفشل، السجل داخل الـ Transaction، وتجميع التحذيرات.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class PartnerModelDispatchSqlServerTests : IAsyncLifetime
    {
        private static readonly DispatchActor Actor = new(ApdTestData.SenderId, "مرسل الاختبار");

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured)
                return;

            await using var db = RtkRealDb.CreateDb();
            await ApdTestData.CleanupAsync(db);
        }

        public async Task DisposeAsync()
        {
            if (!RtkRealDb.IsConfigured)
                return;

            await using var db = RtkRealDb.CreateDb();
            await ApdTestData.CleanupAsync(db);
        }

        private static async Task<(ApdSeed Seed, int ExamModelId)> SeedAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            var seed = await ApdTestData.SeedAsync(db);
            return (seed, await ApdTestData.PrepareSendableModelsAsync(db, seed));
        }

        private static ProfessionalModelSendService NewSend(ApplicationDbContext db)
            => new(db, new ProfessionalModelAvailabilityService(db), new TimeZoneService(), new AdvancedNotificationService(db), new PartnerDispatchLogService(db), NullLogger<ProfessionalModelSendService>.Instance);

        private static PartnerModelDispatchService NewDispatch(ApplicationDbContext db, IProfessionalModelSendService? send = null)
            => new(send ?? NewSend(db), db, NullLogger<PartnerModelDispatchService>.Instance);

        // يفشل في منتصف الكتابة للشريك صاحب الطالب المحدد
        private sealed class FailingForStudentSend : ProfessionalModelSendService
        {
            private readonly int _studentId;

            public FailingForStudentSend(ApplicationDbContext db, int studentId)
                : base(db, new ProfessionalModelAvailabilityService(db), new TimeZoneService(), new AdvancedNotificationService(db), new PartnerDispatchLogService(db), NullLogger<ProfessionalModelSendService>.Instance)
            {
                _studentId = studentId;
            }

            protected override Task InsertHomeworksAsync(List<HomeworkEntity> homeworks, CancellationToken ct)
                => homeworks.Any(h => h.StudentId == _studentId)
                    ? throw new InvalidOperationException("فشل مصطنع")
                    : base.InsertHomeworksAsync(homeworks, ct);

            protected override Task InsertExamStatusesAsync(List<ExamStudentStatus> statuses, CancellationToken ct)
                => statuses.Any(s => s.StudentId == _studentId)
                    ? throw new InvalidOperationException("فشل مصطنع")
                    : base.InsertExamStatusesAsync(statuses, ct);
        }

        private sealed class ThrowingLog : IPartnerDispatchLogService
        {
            public Task WriteAsync(IReadOnlyCollection<PartnerDispatchLog> entries, CancellationToken ct = default)
                => throw new InvalidOperationException("فشل السجل");

            public IQueryable<PartnerDispatchLog> Query() => throw new NotSupportedException();
        }

        private static DateTime LocalInFuture(int days)
        {
            var saudi = DateTime.UtcNow.AddHours(3).AddDays(days);
            return DateTime.SpecifyKind(new DateTime(saudi.Year, saudi.Month, saudi.Day, saudi.Hour, saudi.Minute, 0), DateTimeKind.Unspecified);
        }

        private static PartnerDispatchTarget Target(int partnerId, string name, params int[] batchIds)
            => new() { PartnerId = partnerId, PartnerName = name, BatchIds = batchIds.ToList() };

        private static List<PartnerDispatchTarget> TwoSchools(ApdSeed seed)
            => new() { Target(seed.PartnerA, "مدرسة A", seed.BatchA1), Target(seed.PartnerB, "مدرسة B", seed.BatchB1) };

        private static SendModelHomeworkRequest Homework(ApdSeed seed, bool confirm = false) => new()
        {
            ModelId = seed.ModelLinkedToA,
            Title = $"{ApdTestData.Marker} واجب",
            StartAtLocal = LocalInFuture(0),
            EndAtLocal = LocalInFuture(3),
            ConfirmWarnings = confirm
        };

        private static SendModelExamRequest Exam(int modelId, bool inLab = false) => new()
        {
            ModelId = modelId,
            Title = $"{ApdTestData.Marker} اختبار",
            StartAtLocal = LocalInFuture(0),
            EndAtLocal = LocalInFuture(3),
            DurationMinutes = 40,
            IsInLab = inLab,
            RandomizeQuestions = false
        };

        // =====================================================
        // نسخة مستقلة لكل شريك
        // =====================================================
        [SqlServerFact]
        public async Task Homework_TwoSchools_EachOwnsItsCopy_ActiveStudentsOnly_AndLogged()
        {
            var (seed, _) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).SendHomeworkAsync(TwoSchools(seed), Homework(seed), Actor);

            Assert.Null(outcome.ValidationError);
            Assert.Equal(2, outcome.SucceededCount);

            await using var read = RtkRealDb.CreateDb();
            var setA = await read.HomeworkSets.AsNoTracking().SingleAsync(h => h.BatchId == seed.BatchA1);
            var setB = await read.HomeworkSets.AsNoTracking().SingleAsync(h => h.BatchId == seed.BatchB1);

            Assert.NotEqual(setA.Id, setB.Id);
            Assert.Equal(seed.PartnerA, setA.OwnerPartnerId);
            Assert.Equal(seed.PartnerB, setB.OwnerPartnerId);
            Assert.Equal(ApdTestData.SenderId, setA.AssignedByUserId);

            // دفعة A1 فيها طالب غير نشط وطالب شريك آخر ومنسحب: لا يستلم إلا النشط من طلاب A
            var recipients = await read.Homeworks.AsNoTracking().Where(h => h.HomeworkSetId == setA.Id).Select(h => h.StudentId).Distinct().ToListAsync();
            Assert.Equal(new[] { seed.ActiveStudentA }, recipients);

            var logs = await read.PartnerDispatchLogs.AsNoTracking().Where(l => l.DispatchGroupId == outcome.DispatchGroupId).OrderBy(l => l.PartnerId).ToListAsync();
            Assert.Equal(2, logs.Count);

            var logA = logs.Single(l => l.PartnerId == seed.PartnerA);
            Assert.Equal(PartnerDispatchKind.Homework, logA.Kind);
            Assert.Equal(seed.BatchA1, logA.BatchId);
            Assert.Equal(setA.Id, logA.TargetEntityId);
            Assert.Equal(1, logA.StudentsCount);
            Assert.Equal(seed.ModelLinkedToA, logA.SourceProfessionalModelId);
            Assert.Equal(ApdTestData.SenderId, logA.SentByUserId);
            Assert.False(logA.ContractOverridden);
            Assert.Null(logA.OverrideReason);
        }

        [SqlServerFact]
        public async Task Exam_TwoSchools_SeparateExamPerBatch_OwnedAndVisibleToPartnerFlow()
        {
            var (seed, examModelId) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).SendExamAsync(TwoSchools(seed), Exam(examModelId), Actor);

            Assert.Equal(2, outcome.SucceededCount);

            await using var read = RtkRealDb.CreateDb();
            var exams = await read.Exams.AsNoTracking().Where(e => e.SourceProfessionalModelId == examModelId).ToListAsync();
            Assert.Equal(2, exams.Count);
            Assert.Contains(exams, e => e.OwnerPartnerId == seed.PartnerA);
            Assert.Contains(exams, e => e.OwnerPartnerId == seed.PartnerB);
            Assert.All(exams, e => Assert.Equal(seed.CurriculumId, e.CurriculumId));
            Assert.All(exams, e => Assert.False(e.RandomizeQuestions));

            var examA = exams.Single(e => e.OwnerPartnerId == seed.PartnerA);

            // ExamAssignments كمسار الشريك، بخلاف إرسال الأدمن للمنصة
            Assert.Equal(new[] { seed.ActiveStudentA }, await read.ExamAssignments.AsNoTracking().Where(a => a.ExamId == examA.Id).Select(a => a.StudentId).ToListAsync());
            Assert.Equal(new[] { seed.ActiveStudentA }, await read.ExamStudentStatuses.AsNoTracking().Where(s => s.ExamId == examA.Id).Select(s => s.StudentId).ToListAsync());

            var log = await read.PartnerDispatchLogs.AsNoTracking().SingleAsync(l => l.PartnerId == seed.PartnerA && l.DispatchGroupId == outcome.DispatchGroupId);
            Assert.Equal(PartnerDispatchKind.Exam, log.Kind);
            Assert.Equal(examA.Id, log.TargetEntityId);
        }

        [SqlServerFact]
        public async Task Exam_InLab_EachOwnedExamHasItsOwnReferenceCode()
        {
            var (seed, examModelId) = await SeedAsync();

            // الدفعة الفارغة تأخذ طالبًا حتى يكون للشريك اختباران
            await using (var setup = RtkRealDb.CreateDb())
            {
                setup.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = seed.ActiveStudentA, BatchId = seed.BatchAEmpty });
                await setup.SaveChangesAsync();
            }

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).SendExamAsync(
                new List<PartnerDispatchTarget> { Target(seed.PartnerA, "مدرسة A", seed.BatchA1, seed.BatchAEmpty) },
                Exam(examModelId, inLab: true),
                Actor);

            Assert.Equal(1, outcome.SucceededCount);

            await using var read = RtkRealDb.CreateDb();
            var codes = await read.Exams.AsNoTracking().Where(e => e.SourceProfessionalModelId == examModelId).Select(e => e.ReferenceCode).ToListAsync();
            Assert.Equal(2, codes.Count);
            Assert.All(codes, code => Assert.False(string.IsNullOrEmpty(code)));
            Assert.Equal(2, codes.Distinct().Count());
        }

        // =====================================================
        // عزل الفشل
        // =====================================================
        [SqlServerFact]
        public async Task Homework_OneSchoolFails_OtherIsSaved_FailedHasNoRowsNoLog()
        {
            var (seed, _) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db, new FailingForStudentSend(db, seed.ActiveStudentA))
                .SendHomeworkAsync(TwoSchools(seed), Homework(seed), Actor);

            Assert.Equal(1, outcome.SucceededCount);
            Assert.Equal(1, outcome.FailedCount);
            Assert.False(outcome.Results.Single(r => r.PartnerId == seed.PartnerA).Succeeded);
            Assert.True(outcome.Results.Single(r => r.PartnerId == seed.PartnerB).Succeeded);

            await using var read = RtkRealDb.CreateDb();
            Assert.False(await read.HomeworkSets.AsNoTracking().AnyAsync(h => h.BatchId == seed.BatchA1));
            Assert.True(await read.HomeworkSets.AsNoTracking().AnyAsync(h => h.BatchId == seed.BatchB1));

            var log = await read.PartnerDispatchLogs.AsNoTracking().SingleAsync(l => l.DispatchGroupId == outcome.DispatchGroupId);
            Assert.Equal(seed.PartnerB, log.PartnerId);
        }

        [SqlServerFact]
        public async Task Exam_FirstSchoolFails_SecondStillSent_NoLeftoverExam()
        {
            var (seed, examModelId) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db, new FailingForStudentSend(db, seed.ActiveStudentA))
                .SendExamAsync(TwoSchools(seed), Exam(examModelId), Actor);

            Assert.Equal(1, outcome.SucceededCount);

            await using var read = RtkRealDb.CreateDb();
            Assert.False(await read.Exams.AsNoTracking().AnyAsync(e => e.OwnerPartnerId == seed.PartnerA));
            Assert.True(await read.Exams.AsNoTracking().AnyAsync(e => e.OwnerPartnerId == seed.PartnerB));
        }

        [SqlServerFact]
        public async Task BatchOfAnotherPartner_FailsOnlyThatTarget()
        {
            var (seed, _) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).SendHomeworkAsync(
                new List<PartnerDispatchTarget> { Target(seed.PartnerA, "مدرسة A", seed.BatchB1), Target(seed.PartnerB, "مدرسة B", seed.BatchB1) },
                Homework(seed),
                Actor);

            Assert.False(outcome.Results.Single(r => r.PartnerId == seed.PartnerA).Succeeded);
            Assert.True(outcome.Results.Single(r => r.PartnerId == seed.PartnerB).Succeeded);

            await using var read = RtkRealDb.CreateDb();
            Assert.Equal(seed.PartnerB, (await read.HomeworkSets.AsNoTracking().SingleAsync(h => h.BatchId == seed.BatchB1)).OwnerPartnerId);
        }

        // السجل في نفس الـ Transaction: فشله يلغي إرسال هذا الشريك كاملًا
        [SqlServerFact]
        public async Task LogFailure_RollsBackThatSchoolsSend()
        {
            var (seed, _) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var send = new ProfessionalModelSendService(db, new ProfessionalModelAvailabilityService(db), new TimeZoneService(), new AdvancedNotificationService(db), new ThrowingLog(), NullLogger<ProfessionalModelSendService>.Instance);

            var outcome = await NewDispatch(db, send).SendHomeworkAsync(TwoSchools(seed), Homework(seed), Actor);

            Assert.Equal(2, outcome.FailedCount);

            await using var read = RtkRealDb.CreateDb();
            Assert.False(await read.HomeworkSets.AsNoTracking().AnyAsync(h => h.BatchId == seed.BatchA1 || h.BatchId == seed.BatchB1));
            Assert.False(await read.Homeworks.AsNoTracking().AnyAsync(h => h.StudentId == seed.ActiveStudentA));
        }

        // =====================================================
        // التجاوز
        // =====================================================
        [SqlServerFact]
        public async Task OverriddenSchool_IsLoggedWithReason_OtherSchoolIsNot()
        {
            var (seed, _) = await SeedAsync();
            const string reason = "اتفاق خاص مع إدارة المدرسة";

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).SendHomeworkAsync(
                new List<PartnerDispatchTarget>
                {
                    Target(seed.PartnerA, "مدرسة A", seed.BatchA1),
                    new() { PartnerId = seed.PartnerB, PartnerName = "مدرسة B", BatchIds = new() { seed.BatchB1 }, ContractOverridden = true, OverrideReason = reason }
                },
                Homework(seed),
                Actor);

            Assert.Equal(2, outcome.SucceededCount);
            Assert.True(outcome.Results.Single(r => r.PartnerId == seed.PartnerB).ContractOverridden);

            await using var read = RtkRealDb.CreateDb();
            var logs = await read.PartnerDispatchLogs.AsNoTracking().Where(l => l.DispatchGroupId == outcome.DispatchGroupId).ToListAsync();
            var overridden = logs.Single(l => l.PartnerId == seed.PartnerB);
            Assert.True(overridden.ContractOverridden);
            Assert.Equal(reason, overridden.OverrideReason);
            Assert.False(logs.Single(l => l.PartnerId == seed.PartnerA).ContractOverridden);
        }

        // =====================================================
        // التحذيرات والتحقق المسبق
        // =====================================================
        [SqlServerFact]
        public async Task RepeatSend_AggregatesWarningsAcrossSchools_WritesNothingUntilConfirmed()
        {
            var (seed, _) = await SeedAsync();

            await using (var first = RtkRealDb.CreateDb())
            {
                await NewDispatch(first).SendHomeworkAsync(TwoSchools(seed), Homework(seed), Actor);
            }

            // خارج نافذة النقرة المزدوجة
            await using (var shift = RtkRealDb.CreateDb())
            {
                await shift.HomeworkSets.Where(h => h.BatchId == seed.BatchA1 || h.BatchId == seed.BatchB1)
                    .ExecuteUpdateAsync(x => x.SetProperty(h => h.CreatedAt, DateTime.UtcNow.AddDays(-1)));
            }

            await using var db = RtkRealDb.CreateDb();
            var warned = await NewDispatch(db).SendHomeworkAsync(TwoSchools(seed), Homework(seed), Actor);

            Assert.True(warned.RequiresConfirmation);
            Assert.Empty(warned.Results);
            Assert.Equal(2, warned.Preview!.Batches.Count);
            Assert.Contains(warned.Preview.Batches, b => b.Name.StartsWith("مدرسة A") && b.AlreadySentFromModel);
            Assert.Contains(warned.Preview.Batches, b => b.Name.StartsWith("مدرسة B") && b.AlreadySentFromModel);

            await using (var read = RtkRealDb.CreateDb())
            {
                Assert.Equal(2, await read.HomeworkSets.AsNoTracking().CountAsync(h => h.BatchId == seed.BatchA1 || h.BatchId == seed.BatchB1));
            }

            await using var again = RtkRealDb.CreateDb();
            var confirmed = await NewDispatch(again).SendHomeworkAsync(TwoSchools(seed), Homework(seed, confirm: true), Actor);
            Assert.Equal(2, confirmed.SucceededCount);

            await using var after = RtkRealDb.CreateDb();
            Assert.Equal(4, await after.HomeworkSets.AsNoTracking().CountAsync(h => h.BatchId == seed.BatchA1 || h.BatchId == seed.BatchB1));
        }

        [SqlServerFact]
        public async Task MissingDates_IsOneRequestError_NoWrites()
        {
            var (seed, _) = await SeedAsync();
            var request = Homework(seed);
            request.EndAtLocal = null;

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).SendHomeworkAsync(TwoSchools(seed), request, Actor);

            Assert.Contains("موعد", outcome.ValidationError);
            Assert.Empty(outcome.Results);
        }

        [SqlServerFact]
        public async Task WrongModelType_IsNotFound_NoWrites()
        {
            var (seed, examModelId) = await SeedAsync();
            var request = Homework(seed);
            request.ModelId = examModelId;

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).SendHomeworkAsync(TwoSchools(seed), request, Actor);

            Assert.True(outcome.IsNotFound);
            Assert.Empty(outcome.Results);
        }

        // =====================================================
        // الحدود بين النطاقات
        // =====================================================
        [SqlServerFact]
        public async Task AdminScope_RejectsPartnerBatch_AcceptsPlatformBatch()
        {
            var (seed, _) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var send = NewSend(db);

            var rejected = await send.SendHomeworkAsync(ModelSendScope.Admin(ApdTestData.SenderId), new SendModelHomeworkRequest
            {
                ModelId = seed.ModelLinkedToA,
                BatchIds = new() { seed.BatchA1 },
                Title = $"{ApdTestData.Marker} مرفوض"
            });
            Assert.False(rejected.Succeeded);

            var accepted = await send.SendHomeworkAsync(ModelSendScope.Admin(ApdTestData.SenderId), new SendModelHomeworkRequest
            {
                ModelId = seed.ModelLinkedToA,
                BatchIds = new() { seed.BatchI1 },
                Title = $"{ApdTestData.Marker} منصة"
            });
            Assert.True(accepted.Succeeded, accepted.Message);

            await using var read = RtkRealDb.CreateDb();
            Assert.Null((await read.HomeworkSets.AsNoTracking().SingleAsync(h => h.BatchId == seed.BatchI1)).OwnerPartnerId);
            Assert.False(await read.PartnerDispatchLogs.AsNoTracking().AnyAsync(l => l.BatchId == seed.BatchI1));
        }

        [SqlServerFact]
        public async Task AdminForPartner_RejectsStudentSelection_AndLectureLink()
        {
            var (seed, examModelId) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var send = NewSend(db);
            var scope = ModelSendScope.AdminForPartner(seed.PartnerA, ApdTestData.SenderId, null, false, null, Guid.NewGuid());

            var exam = Exam(examModelId);
            exam.BatchIds = new() { seed.BatchA1 };
            exam.StudentIds = new() { seed.ActiveStudentA };
            Assert.Contains("طلاب محددين", (await send.SendExamAsync(scope, exam)).Message);

            var homework = Homework(seed);
            homework.BatchIds = new() { seed.BatchA1 };
            homework.LectureId = 1;
            Assert.Contains("محاضرة", (await send.SendHomeworkAsync(scope, homework)).Message);
        }

        [Fact]
        public void AdminForPartner_RequiresPartnerSenderAndGroup()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ModelSendScope.AdminForPartner(0, "u", null, false, null, Guid.NewGuid()));
            Assert.Throws<ArgumentException>(() => ModelSendScope.AdminForPartner(1, " ", null, false, null, Guid.NewGuid()));
            Assert.Throws<ArgumentException>(() => ModelSendScope.AdminForPartner(1, "u", null, false, null, Guid.Empty));

            var scope = ModelSendScope.AdminForPartner(5, "u", null, false, null, Guid.NewGuid());
            Assert.Equal(5, scope.OwnerPartnerId);
            Assert.False(scope.IsPartner);
            Assert.Null(ModelSendScope.Admin("u").OwnerPartnerId);
            Assert.Equal(7, ModelSendScope.Partner(7, "u").OwnerPartnerId);
        }
    }
}
