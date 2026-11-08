using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Implementations;
using QdratNew.Services.Exams.Models;
using QdratNew.Services.MinistrySim;
using QdratNew.Services.MinistrySim.PartnerScope;
using QdratNew.Services.PartnerDispatch;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S5.3 — على قاعدة حقيقية: تكليف معمل القياس لكل مدرسة بمالكها، الطلاب النشطون فقط، عزل الفشل والسجل،
    // وقاعدة ملكية المحاولة (تكليف دفعة + فردي، طالب انتقل بين الدفعات أو خرج من فروع المدرسة)
    // وأن كل محاولة تظهر في جهة واحدة فقط: لا تسرب بين المدارس ولا للأدمن.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class PartnerMinistrySimDispatchSqlServerTests : IAsyncLifetime
    {
        private static readonly DispatchActor Actor = new(ApdTestData.SenderId, "مرسل الاختبار");
        private static readonly MinistrySimAssignOptions Online = new(true, null, null);

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

        private static async Task<(ApdSeed Seed, ApdMinistrySimSeed Sim)> SeedAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            var seed = await ApdTestData.SeedAsync(db);
            return (seed, await ApdTestData.PrepareMinistrySimAsync(db, seed));
        }

        private static PartnerMinistrySimDispatchService NewDispatch(ApplicationDbContext db, IPartnerDispatchLogService? log = null)
            => new(
                db,
                new MinistrySimExamAssignmentService(db),
                log ?? new PartnerDispatchLogService(db),
                NullLogger<PartnerMinistrySimDispatchService>.Instance);

        private static PartnerDispatchTarget Target(int partnerId, string name, params int[] batchIds)
            => new() { PartnerId = partnerId, PartnerName = name, BatchIds = batchIds.ToList() };

        private sealed class ThrowingForPartnerLog : IPartnerDispatchLogService
        {
            private readonly int _partnerId;
            private readonly IPartnerDispatchLogService _inner;

            public ThrowingForPartnerLog(int partnerId, IPartnerDispatchLogService inner)
            {
                _partnerId = partnerId;
                _inner = inner;
            }

            public Task WriteAsync(IReadOnlyCollection<PartnerDispatchLog> entries, CancellationToken ct = default)
                => entries.Any(e => e.PartnerId == _partnerId)
                    ? throw new InvalidOperationException("فشل السجل")
                    : _inner.WriteAsync(entries, ct);

            public IQueryable<PartnerDispatchLog> Query() => _inner.Query();
        }

        private static async Task SendToABAsync(ApdSeed seed, ApdMinistrySimSeed sim)
        {
            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).AssignToBatchesAsync(
                sim.ExamId,
                new[] { Target(seed.PartnerA, "A", seed.BatchA1), Target(seed.PartnerB, "B", seed.BatchB1) },
                Online,
                Actor);

            Assert.Equal(2, outcome.SucceededCount);
        }

        // =====================================================
        // الإسناد
        // =====================================================
        [SqlServerFact]
        public async Task AssignToBatches_OwnsEachSchoolAssignment_ActiveSchoolStudentsOnly_AndLogsPerBatch()
        {
            var (seed, sim) = await SeedAsync();
            await SendToABAsync(seed, sim);

            await using var db = RtkRealDb.CreateDb();

            var batches = await db.MinistrySimExamAssignmentsToBatches.AsNoTracking()
                .Where(a => a.MinistrySimExamId == sim.ExamId)
                .Select(a => new { a.BatchId, a.OwnerPartnerId })
                .ToListAsync();
            Assert.Equal(2, batches.Count);
            Assert.Contains(batches, b => b.BatchId == seed.BatchA1 && b.OwnerPartnerId == seed.PartnerA);
            Assert.Contains(batches, b => b.BatchId == seed.BatchB1 && b.OwnerPartnerId == seed.PartnerB);

            var students = await db.MinistrySimExamAssignmentsToStudents.AsNoTracking()
                .Where(a => a.MinistrySimExamId == sim.ExamId)
                .Select(a => new { a.StudentId, a.OwnerPartnerId, a.SourceType })
                .ToListAsync();

            // A1 فيها: نشط، غير نشط للتعلم، طالب مدرسة B، منسحب، وA2 النشط — يصل للنشطين من فروع A فقط
            Assert.Equal(
                new[] { seed.ActiveStudentA, sim.StudentA2 }.OrderBy(x => x),
                students.Where(s => s.OwnerPartnerId == seed.PartnerA).Select(s => s.StudentId).OrderBy(x => x));
            Assert.Equal(new[] { sim.StudentB }, students.Where(s => s.OwnerPartnerId == seed.PartnerB).Select(s => s.StudentId));
            Assert.All(students, s => Assert.Equal("Batch", s.SourceType));
            Assert.DoesNotContain(students, s => s.OwnerPartnerId == null);

            var logs = await db.PartnerDispatchLogs.AsNoTracking()
                .Where(l => l.Kind == PartnerDispatchKind.MinistrySim && l.SourceMinistrySimExamId == sim.ExamId)
                .ToListAsync();
            Assert.Equal(2, logs.Count);
            Assert.Single(logs.Select(l => l.DispatchGroupId).Distinct());
            Assert.Equal(2, logs.Single(l => l.PartnerId == seed.PartnerA).StudentsCount);
            Assert.Equal(1, logs.Single(l => l.PartnerId == seed.PartnerB).StudentsCount);
        }

        [SqlServerFact]
        public async Task AssignToBatches_Again_IsIdempotent_AndWritesNoEmptyLog()
        {
            var (seed, sim) = await SeedAsync();
            await SendToABAsync(seed, sim);

            await using var db = RtkRealDb.CreateDb();
            var again = await NewDispatch(db).AssignToBatchesAsync(sim.ExamId, new[] { Target(seed.PartnerA, "A", seed.BatchA1) }, Online, Actor);

            var result = again.Results.Single();
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(0, result.BatchesSent);
            Assert.Equal(0, result.StudentsSent);

            Assert.Equal(1, await db.MinistrySimExamAssignmentsToBatches.CountAsync(a => a.MinistrySimExamId == sim.ExamId && a.BatchId == seed.BatchA1));
            Assert.Equal(2, await db.PartnerDispatchLogs.CountAsync(l => l.SourceMinistrySimExamId == sim.ExamId));
        }

        [SqlServerFact]
        public async Task AssignToBatches_BatchOfAnotherSchool_FailsOnlyThatSchool()
        {
            var (seed, sim) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var outcome = await NewDispatch(db).AssignToBatchesAsync(
                sim.ExamId,
                new[] { Target(seed.PartnerA, "A", seed.BatchB1), Target(seed.PartnerB, "B", seed.BatchB1) },
                Online,
                Actor);

            Assert.False(outcome.Results.Single(r => r.PartnerId == seed.PartnerA).Succeeded);
            Assert.True(outcome.Results.Single(r => r.PartnerId == seed.PartnerB).Succeeded);
            Assert.False(await db.MinistrySimExamAssignmentsToBatches.AnyAsync(a => a.MinistrySimExamId == sim.ExamId && a.OwnerPartnerId == seed.PartnerA));
        }

        [SqlServerFact]
        public async Task AssignToBatches_FailureOfOneSchool_RollsBackOnlyItsRows()
        {
            var (seed, sim) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var log = new ThrowingForPartnerLog(seed.PartnerA, new PartnerDispatchLogService(db));
            var outcome = await NewDispatch(db, log).AssignToBatchesAsync(
                sim.ExamId,
                new[] { Target(seed.PartnerA, "A", seed.BatchA1), Target(seed.PartnerB, "B", seed.BatchB1) },
                Online,
                Actor);

            Assert.False(outcome.Results.Single(r => r.PartnerId == seed.PartnerA).Succeeded);
            Assert.True(outcome.Results.Single(r => r.PartnerId == seed.PartnerB).Succeeded);

            await using var check = RtkRealDb.CreateDb();
            Assert.False(await check.MinistrySimExamAssignmentsToBatches.AnyAsync(a => a.MinistrySimExamId == sim.ExamId && a.OwnerPartnerId == seed.PartnerA));
            Assert.False(await check.MinistrySimExamAssignmentsToStudents.AnyAsync(a => a.MinistrySimExamId == sim.ExamId && a.OwnerPartnerId == seed.PartnerA));
            Assert.True(await check.MinistrySimExamAssignmentsToBatches.AnyAsync(a => a.MinistrySimExamId == sim.ExamId && a.OwnerPartnerId == seed.PartnerB));
        }

        [SqlServerFact]
        public async Task AssignToBatches_InPerson_GeneratesCodePerSchool_AndOverrideIsLogged()
        {
            var (seed, sim) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var target = new PartnerDispatchTarget
            {
                PartnerId = seed.PartnerA, PartnerName = "A", BatchIds = new() { seed.BatchA1 },
                ContractOverridden = true, OverrideReason = "تجربة جلسة معمل حضورية للمدرسة"
            };
            var outcome = await NewDispatch(db).AssignToBatchesAsync(sim.ExamId, new[] { target }, new MinistrySimAssignOptions(false, null, null), Actor);

            var result = outcome.Results.Single();
            Assert.True(result.Succeeded, result.Message);

            var code = await db.MinistrySimExamAssignmentsToBatches.AsNoTracking()
                .Where(a => a.MinistrySimExamId == sim.ExamId && a.OwnerPartnerId == seed.PartnerA)
                .Select(a => a.ReferenceCode)
                .SingleAsync();
            Assert.Matches("^[0-9]{6}$", code);
            Assert.Contains(code!, result.Message);

            var entry = await db.PartnerDispatchLogs.AsNoTracking().SingleAsync(l => l.SourceMinistrySimExamId == sim.ExamId);
            Assert.True(entry.ContractOverridden);
            Assert.Equal("تجربة جلسة معمل حضورية للمدرسة", entry.OverrideReason);
        }

        [SqlServerFact]
        public async Task AssignToStudents_SchoolStudentOnly_InSelectedBatch()
        {
            var (seed, sim) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var dispatch = NewDispatch(db);

            // طالب مدرسة B مسجل في A1: لا يُسند باسم A
            var foreign = await db.StudentBatchEnrollments.AsNoTracking()
                .Where(e => e.BatchId == seed.BatchA1 && e.Student.Branch.PartnerId == seed.PartnerB)
                .Select(e => e.StudentID)
                .SingleAsync();

            var rejected = await dispatch.AssignToStudentsAsync(sim.ExamId, Target(seed.PartnerA, "A", seed.BatchA1), new[] { foreign }, Online, Actor);
            Assert.False(rejected.Results.Single().Succeeded);

            var accepted = await dispatch.AssignToStudentsAsync(sim.ExamId, Target(seed.PartnerA, "A", seed.BatchA1), new[] { seed.ActiveStudentA }, Online, Actor);
            Assert.True(accepted.Results.Single().Succeeded, accepted.Results.Single().Message);

            var row = await db.MinistrySimExamAssignmentsToStudents.AsNoTracking().SingleAsync(a => a.MinistrySimExamId == sim.ExamId);
            Assert.Equal(seed.ActiveStudentA, row.StudentId);
            Assert.Equal(seed.PartnerA, row.OwnerPartnerId);
            Assert.Equal("Official", row.SourceType);
            Assert.Equal(1, await db.PartnerDispatchLogs.CountAsync(l => l.SourceMinistrySimExamId == sim.ExamId && l.StudentsCount == 1));
        }

        [SqlServerFact]
        public async Task PlatformAssignment_RejectsSchoolBatchesAndStudents()
        {
            var (seed, sim) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var service = new MinistrySimExamAssignmentService(db);

            var batches = await service.AssignToBatchesAsync(sim.ExamId, new List<int> { seed.BatchA1 }, null);
            Assert.False(batches.Success);

            var students = await service.AssignToStudentsAsync(sim.ExamId, new List<int> { seed.ActiveStudentA });
            Assert.False(students.Success);

            var institute = await service.AssignToBatchesAsync(sim.ExamId, new List<int> { seed.BatchI1 }, null);
            Assert.True(institute.Success, institute.Message);
            Assert.True(await db.MinistrySimExamAssignmentsToStudents.AnyAsync(a => a.MinistrySimExamId == sim.ExamId && a.StudentId == seed.StudentI1 && a.OwnerPartnerId == null));
            Assert.False(await db.MinistrySimExamAssignmentsToBatches.AnyAsync(a => a.MinistrySimExamId == sim.ExamId && a.OwnerPartnerId != null));
        }

        // =====================================================
        // ملكية المحاولة
        // =====================================================
        [SqlServerFact]
        public async Task Ownership_EachAttemptBelongsToExactlyOneSide()
        {
            var (seed, sim) = await SeedAsync();
            await SendToABAsync(seed, sim);

            await using (var setup = RtkRealDb.CreateDb())
                await new MinistrySimExamAssignmentService(setup).AssignToBatchesAsync(sim.ExamId, new List<int> { seed.BatchI1 }, null);

            await using var db = RtkRealDb.CreateDb();
            var attemptA = await ApdTestData.CompleteSimAttemptAsync(db, sim, seed.ActiveStudentA);
            var attemptB = await ApdTestData.CompleteSimAttemptAsync(db, sim, sim.StudentB);
            var attemptI = await ApdTestData.CompleteSimAttemptAsync(db, sim, seed.StudentI1);

            var ownership = new MinistrySimOwnershipQuery(db);

            async Task<List<int>> IdsAsync(IQueryable<MinistrySimExamStudentAttempt> q)
                => await q.Where(a => a.MinistrySimExamId == sim.ExamId).Select(a => a.Id).OrderBy(id => id).ToListAsync();

            Assert.Equal(new[] { attemptA }, await IdsAsync(ownership.PartnerAttempts(seed.PartnerA)));
            Assert.Equal(new[] { attemptB }, await IdsAsync(ownership.PartnerAttempts(seed.PartnerB)));
            Assert.Equal(new[] { attemptI }, await IdsAsync(ownership.PlatformAttempts()));

            Assert.True(await ownership.IsOwnedByAnyPartnerAsync(sim.ExamId, seed.ActiveStudentA));
            Assert.False(await ownership.IsOwnedByAnyPartnerAsync(sim.ExamId, seed.StudentI1));
            Assert.False(await ownership.IsPartnerStudentAsync(sim.ExamId, sim.StudentB, seed.PartnerA));

            // طالب مدرسة B مسجل في دفعة A لا تملكه A (ليس من فروعها) ولا B (لا تكليف لها عليه)، فيبقى للمعهد
            var platformStudents = await ownership.PlatformStudents(sim.ExamId).Select(s => s.StudentID).ToListAsync();
            Assert.Contains(seed.StudentI1, platformStudents);
            Assert.DoesNotContain(seed.ActiveStudentA, platformStudents);
            Assert.DoesNotContain(sim.StudentA2, platformStudents);
            Assert.DoesNotContain(sim.StudentB, platformStudents);

            var schoolStudents = await ownership.PartnerStudents(sim.ExamId, seed.PartnerA).Select(s => s.StudentID).ToListAsync();
            Assert.Empty(schoolStudents.Intersect(platformStudents));
        }

        [SqlServerFact]
        public async Task Ownership_StudentJoinedBatchAfterAssignment_BelongsToSchoolViaBatch()
        {
            var (seed, sim) = await SeedAsync();
            await SendToABAsync(seed, sim);

            await using var db = RtkRealDb.CreateDb();

            // طالب جديد في A1 بعد الإسناد: بلا صف تكليف فردي، ويصل للاختبار عبر دفعته
            var branchA = await db.Batches.Where(b => b.Id == seed.BatchA1).Select(b => b.BranchId).FirstAsync();
            db.Users.Add(ApdTestData.NewUser("apds1-late"));
            var late = new Student
            {
                NationalID = $"{ApdTestData.Marker}LATE", FullName = "طالب متأخر", Gender = "ذكر", Age = 17,
                School = "-", Level = "-", UserId = "apds1-late", BranchId = branchA, IsActiveForLearning = true
            };
            db.Students.Add(late);
            await db.SaveChangesAsync();
            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = late.StudentID, BatchId = seed.BatchA1 });
            await db.SaveChangesAsync();

            var attempt = await ApdTestData.CompleteSimAttemptAsync(db, sim, late.StudentID);
            var ownership = new MinistrySimOwnershipQuery(db);

            Assert.True(await ownership.PartnerAttempts(seed.PartnerA).AnyAsync(a => a.Id == attempt));
            Assert.False(await ownership.PlatformAttempts().AnyAsync(a => a.Id == attempt));
            Assert.True(await ownership.IsPartnerStudentAsync(sim.ExamId, late.StudentID, seed.PartnerA));
        }

        [SqlServerFact]
        public async Task Ownership_StudentMovedBetweenSchoolBatches_StaysWithSchool_AndMovedToInstitute_ReturnsToPlatform()
        {
            var (seed, sim) = await SeedAsync();
            await SendToABAsync(seed, sim);

            await using var db = RtkRealDb.CreateDb();
            var attempt = await ApdTestData.CompleteSimAttemptAsync(db, sim, seed.ActiveStudentA);
            var ownership = new MinistrySimOwnershipQuery(db);

            // انتقال داخل المدرسة: من A1 إلى الدفعة الفارغة لنفس المدرسة (صف التكليف الفردي يبقى المرجع)
            await db.StudentBatchEnrollments
                .Where(e => e.StudentID == seed.ActiveStudentA && e.BatchId == seed.BatchA1)
                .ExecuteUpdateAsync(u => u.SetProperty(e => e.Status, "Transferred"));
            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = seed.ActiveStudentA, BatchId = seed.BatchAEmpty });
            await db.SaveChangesAsync();

            Assert.True(await ownership.PartnerAttempts(seed.PartnerA).AnyAsync(a => a.Id == attempt));
            Assert.False(await ownership.PlatformAttempts().AnyAsync(a => a.Id == attempt));

            // خروج من فروع المدرسة إلى فرع المعهد: لم تعد المدرسة تملكه، وتعود النتيجة للمعهد
            var instituteBranch = await db.Batches.Where(b => b.Id == seed.BatchI1).Select(b => b.BranchId).FirstAsync();
            await db.Students
                .Where(s => s.StudentID == seed.ActiveStudentA)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.BranchId, instituteBranch));

            Assert.False(await ownership.PartnerAttempts(seed.PartnerA).AnyAsync(a => a.Id == attempt));
            Assert.True(await ownership.PlatformAttempts().AnyAsync(a => a.Id == attempt));
        }

        [SqlServerFact]
        public async Task Ownership_IndividualSchoolAssignment_WithoutBatch_IsOwned()
        {
            var (seed, sim) = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            await NewDispatch(db).AssignToStudentsAsync(sim.ExamId, Target(seed.PartnerA, "A", seed.BatchA1), new[] { sim.StudentA2 }, Online, Actor);
            var attempt = await ApdTestData.CompleteSimAttemptAsync(db, sim, sim.StudentA2);

            var ownership = new MinistrySimOwnershipQuery(db);
            Assert.True(await ownership.PartnerAttempts(seed.PartnerA).AnyAsync(a => a.Id == attempt));
            Assert.False(await ownership.PlatformAttempts().AnyAsync(a => a.Id == attempt));

            // A1 ليست مُسنَدة كدفعة: الطالب الآخر فيها ليس من طلاب الاختبار
            Assert.False(await ownership.IsPartnerStudentAsync(sim.ExamId, seed.ActiveStudentA, seed.PartnerA));
        }

        // =====================================================
        // صفحات المدرسة والتحليلات
        // =====================================================
        [SqlServerFact]
        public async Task PartnerScope_ListsOwnExamsOnly_AndBatchAnalyticsIsIsolated()
        {
            var (seed, sim) = await SeedAsync();
            await SendToABAsync(seed, sim);

            await using var db = RtkRealDb.CreateDb();
            await ApdTestData.CompleteSimAttemptAsync(db, sim, seed.ActiveStudentA, score: 80);
            await ApdTestData.CompleteSimAttemptAsync(db, sim, sim.StudentB, score: 20);

            var ownership = new MinistrySimOwnershipQuery(db);
            var scope = new PartnerMinistrySimExamService(db, ownership);

            var indexA = await scope.GetIndexAsync(seed.PartnerA, false, true);
            var row = Assert.Single(indexA.Exams);
            Assert.Equal(sim.ExamId, row.ExamId);

            // نفس قاعدة صفحة الطلاب: نشطا A في A1، ومعهما طالب A غير النشط للتعلم المسجل في الدفعة المُسنَدة
            var studentsOfA = await ownership.PartnerStudents(sim.ExamId, seed.PartnerA).CountAsync();
            Assert.Equal(3, studentsOfA);
            Assert.Equal(studentsOfA, row.StudentsCount);
            Assert.Equal(1, row.CompletedCount);
            Assert.Equal(new[] { seed.BatchA1 }, row.Batches.Select(b => b.BatchId));

            var studentsA = await scope.GetStudentsAsync(seed.PartnerA, sim.ExamId, null);
            Assert.NotNull(studentsA);
            Assert.DoesNotContain(studentsA!.Students, s => s.StudentId == sim.StudentB);

            Assert.Null(await scope.GetStudentsAsync(seed.PartnerA, sim.ExamId, seed.BatchB1));
            Assert.Null(await scope.GetStudentsAsync(seed.PartnerC, sim.ExamId, null));

            var analytics = new MinistrySimExamBatchAnalyticsService(db, ownership);
            var forA = await analytics.GetBatchAnalyticsAsync(sim.ExamId, seed.BatchA1, seed.PartnerA);
            Assert.Equal(MinistrySimExamBatchAnalyticsStatus.Ok, forA.Status);
            Assert.Equal(3, forA.Vm.TotalStudents);
            Assert.Equal(80, forA.Vm.AvgScorePercent);

            Assert.Equal(MinistrySimExamBatchAnalyticsStatus.AssignmentNotFound, (await analytics.GetBatchAnalyticsAsync(sim.ExamId, seed.BatchB1, seed.PartnerA)).Status);
            Assert.Equal(MinistrySimExamBatchAnalyticsStatus.AssignmentNotFound, (await analytics.GetBatchAnalyticsAsync(sim.ExamId, seed.BatchA1)).Status);
        }

        [SqlServerFact]
        public async Task PartnerScope_HideRestore_AndNoteOnOwnAttemptOnly()
        {
            var (seed, sim) = await SeedAsync();
            await SendToABAsync(seed, sim);

            await using var db = RtkRealDb.CreateDb();
            var attemptA = await ApdTestData.CompleteSimAttemptAsync(db, sim, seed.ActiveStudentA);
            var attemptB = await ApdTestData.CompleteSimAttemptAsync(db, sim, sim.StudentB);

            var scope = new PartnerMinistrySimExamService(db, new MinistrySimOwnershipQuery(db));

            Assert.True(await scope.SetHiddenAsync(seed.PartnerA, sim.ExamId, true));
            Assert.Empty((await scope.GetIndexAsync(seed.PartnerA, false, true)).Exams);
            Assert.Single((await scope.GetIndexAsync(seed.PartnerA, true, true)).Exams);
            Assert.Single((await scope.GetIndexAsync(seed.PartnerB, false, true)).Exams);

            Assert.True(await scope.SetHiddenAsync(seed.PartnerA, sim.ExamId, false));
            Assert.Single((await scope.GetIndexAsync(seed.PartnerA, false, true)).Exams);
            Assert.False(await scope.SetHiddenAsync(seed.PartnerC, sim.ExamId, true));

            Assert.True(await scope.SaveParentReportNoteAsync(seed.PartnerA, attemptA, " ملاحظة المدرسة "));
            Assert.False(await scope.SaveParentReportNoteAsync(seed.PartnerA, attemptB, "تسرب"));

            await using var check = RtkRealDb.CreateDb();
            Assert.Equal("ملاحظة المدرسة", await check.MinistrySimExamStudentAttempts.Where(a => a.Id == attemptA).Select(a => a.RecommendationsNote).SingleAsync());
            Assert.Null(await check.MinistrySimExamStudentAttempts.Where(a => a.Id == attemptB).Select(a => a.RecommendationsNote).SingleAsync());
        }
    }
}
