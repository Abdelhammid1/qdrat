using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.Services.PartnerDispatch;
using QdratNew.Services.ProfessionalModels;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S3.3 — تحديد المستوى من نموذج للمدارس الشريكة على قاعدة حقيقية:
    // اختبار لكل مدرسة، الإخفاء عن الأدمن، المسودة، الطلاب النشطون، عزل الفشل، السجل، والضغطة المزدوجة.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    public sealed class PartnerPlacementDispatchSqlServerTests : IAsyncLifetime
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

        private static async Task<(ApdSeed Seed, ApdPlacementSeed Placement)> SeedAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            var seed = await ApdTestData.SeedAsync(db);
            return (seed, await ApdTestData.PreparePlacementAsync(db, seed));
        }

        private static PartnerPlacementDispatchService NewDispatch(ApplicationDbContext db, IPartnerDispatchLogService? log = null)
            => new(
                db,
                new PlacementExamScopeGuard(),
                log ?? new PartnerDispatchLogService(db),
                new AdvancedNotificationService(db),
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<PartnerPlacementDispatchService>.Instance);

        private static PlacementExamManagementService NewManagement(ApplicationDbContext db)
        {
            var tz = new Mock<ITimeZoneService>();
            tz.Setup(t => t.GetNowSaudi()).Returns(DateTime.UtcNow.AddHours(3));

            return new PlacementExamManagementService(
                db,
                new Mock<IPlacementExamGeneratorService>().Object,
                new PlacementExamScopeGuard(),
                tz.Object,
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<PlacementExamManagementService>.Instance,
                new AdvancedNotificationService(db),
                new ProfessionalModelAvailabilityService(db));
        }

        private sealed class ThrowingLog : IPartnerDispatchLogService
        {
            public Task WriteAsync(IReadOnlyCollection<PartnerDispatchLog> entries, CancellationToken ct = default)
                => throw new InvalidOperationException("فشل السجل");

            public IQueryable<PartnerDispatchLog> Query() => throw new NotSupportedException();
        }

        private static PartnerDispatchTarget Target(int partnerId, string name, params int[] batchIds)
            => new() { PartnerId = partnerId, PartnerName = name, BatchIds = batchIds.ToList() };

        private static SendPlacementToPartnersRequest Request(int modelId, int courseId, PlacementPartnerDelivery delivery = PlacementPartnerDelivery.SendNow, int duration = 45)
            => new() { ModelId = modelId, CourseId = courseId, DurationMinutes = duration, Delivery = delivery };

        private static Task<List<Exam>> OwnedExamsAsync(ApplicationDbContext db, int partnerId)
            => db.Exams.AsNoTracking().Where(e => e.OwnerPartnerId == partnerId).ToListAsync();

        // =====================================================
        // اختبار لكل مدرسة
        // =====================================================
        [SqlServerFact]
        public async Task SendNow_CreatesOneIndependentExamPerSchool_OnItsSelectedBatches()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var outcome = await NewDispatch(db).DispatchAsync(
                new[]
                {
                    Target(seed.PartnerA, "A", seed.BatchA1, placement.BatchA4),
                    Target(seed.PartnerB, "B", seed.BatchB1)
                },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            Assert.Equal(2, outcome.SucceededCount);
            Assert.False(outcome.IsDraft);
            Assert.Equal(PartnerDispatchKind.Placement, outcome.Kind);

            var examA = Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
            var examB = Assert.Single(await OwnedExamsAsync(db, seed.PartnerB));
            Assert.NotEqual(examA.Id, examB.Id);

            Assert.Equal(ExamType.LevelAssessment, examA.Type);
            Assert.Equal(placement.ModelId, examA.SourceProfessionalModelId);
            Assert.Equal(seed.CourseId, examA.CourseId);
            Assert.Equal(45, examA.DurationMinutes);
            Assert.True(examA.IsActive);

            var linksA = await db.ExamAssignmentsToBatches.AsNoTracking().Where(l => l.ExamId == examA.Id).ToListAsync();
            Assert.Equal(new[] { seed.BatchA1, placement.BatchA4 }.OrderBy(x => x), linksA.Select(l => l.BatchId).OrderBy(x => x));
            Assert.All(linksA, l => Assert.True(l.IsSentToStudents));
            Assert.Equal(new[] { seed.BatchB1 }, await db.ExamAssignmentsToBatches.AsNoTracking().Where(l => l.ExamId == examB.Id).Select(l => l.BatchId).ToListAsync());

            Assert.Equal(1, await db.ExamQuestions.AsNoTracking().CountAsync(q => q.ExamId == examA.Id));
            Assert.Equal(1, await db.ExamQuestions.AsNoTracking().CountAsync(q => q.ExamId == examB.Id));
            Assert.Equal(1, examA.TotalQuestions);
        }

        [SqlServerFact]
        public async Task SendNow_ReachesOnlyActiveStudentsOfTheSchool_AndNotifiesThem()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var outcome = await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchA1) },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            // في A1: طالب نشط + غير نشط + طالب شريك آخر + منسحب
            var result = Assert.Single(outcome.Results);
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(1, result.StudentsSent);

            var exam = Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
            Assert.Equal(new[] { seed.ActiveStudentA }, await db.ExamAssignments.AsNoTracking().Where(a => a.ExamId == exam.Id).Select(a => a.StudentId).ToListAsync());

            var status = Assert.Single(await db.ExamStudentStatuses.AsNoTracking().Where(s => s.ExamId == exam.Id).ToListAsync());
            Assert.Equal(seed.ActiveStudentA, status.StudentId);
            Assert.Equal(ExamStatus.Pending, status.Status);

            Assert.Equal(1, await db.Notifications.AsNoTracking().CountAsync(n => n.StudentID == seed.ActiveStudentA));
        }

        [SqlServerFact]
        public async Task SendNow_StudentInTwoSelectedBatches_GetsOneAssignment()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = seed.ActiveStudentA, BatchId = placement.BatchA4 });
            await db.SaveChangesAsync();

            var outcome = await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchA1, placement.BatchA4) },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            var result = Assert.Single(outcome.Results);
            Assert.Equal(2, result.StudentsSent);

            var exam = Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
            Assert.Equal(1, await db.ExamAssignments.AsNoTracking().CountAsync(a => a.ExamId == exam.Id && a.StudentId == seed.ActiveStudentA));
            Assert.Equal(2, await db.ExamAssignments.AsNoTracking().CountAsync(a => a.ExamId == exam.Id));
        }

        // =====================================================
        // الإخفاء والعزل
        // =====================================================
        [SqlServerFact]
        public async Task SentExam_HiddenFromAdmin_VisibleToOwnerOnly_WithInstituteBadge()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchA1), Target(seed.PartnerB, "B", seed.BatchB1) },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            var examA = Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
            var examB = Assert.Single(await OwnedExamsAsync(db, seed.PartnerB));

            var management = NewManagement(db);
            var admin = PlacementScope.Admin("admin");
            var scopeA = PlacementScope.Partner(seed.PartnerA, "u");
            var scopeB = PlacementScope.Partner(seed.PartnerB, "u");

            // الأدمن
            Assert.Empty((await management.GetBatchExamsAsync(admin, seed.BatchA1))!);
            Assert.DoesNotContain((await management.GetIndexAsync(admin)).ActiveBatches, b => b.BatchId == seed.BatchA1 || b.BatchId == seed.BatchB1);
            Assert.Null(await management.GetReviewAsync(admin, examA.Id));

            // صاحبها فقط
            var listA = (await management.GetBatchExamsAsync(scopeA, seed.BatchA1))!;
            Assert.Equal(examA.Id, Assert.Single(listA).ExamId);
            Assert.True(listA[0].IsFromInstitute);
            Assert.True(listA[0].IsSentToStudents);
            Assert.NotNull(await management.GetReviewAsync(scopeA, examA.Id));
            Assert.Null(await management.GetReviewAsync(scopeA, examB.Id));
            Assert.Null(await management.GetBatchExamsAsync(scopeB, seed.BatchA1));
            Assert.Equal(examB.Id, Assert.Single((await management.GetBatchExamsAsync(scopeB, seed.BatchB1))!).ExamId);
        }

        [SqlServerFact]
        public async Task PartnerOwnCreatedExam_HasNoInstituteBadge()
        {
            var (seed, _) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var own = new Exam
            {
                Title = $"{ApdTestData.Marker} اختبار الشريك نفسه",
                Type = ExamType.LevelAssessment,
                CourseId = seed.CourseId,
                DurationMinutes = 30,
                TotalQuestions = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                OwnerPartnerId = seed.PartnerA
            };
            db.Exams.Add(own);
            await db.SaveChangesAsync();
            db.ExamAssignmentsToBatches.Add(new ExamAssignmentToBatch { ExamId = own.Id, BatchId = seed.BatchA1, Title = own.Title, TotalQuestions = 1, DurationMinutes = 30, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            var row = Assert.Single((await NewManagement(db).GetBatchExamsAsync(PlacementScope.Partner(seed.PartnerA, "u"), seed.BatchA1))!);
            Assert.False(row.IsFromInstitute);
            Assert.Equal(1, row.LinkedBatchCount);
        }

        // =====================================================
        // المسودة
        // =====================================================
        [SqlServerFact]
        public async Task Draft_AppearsToSchool_ReachesNoStudent_UntilSchoolSendsIt()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var outcome = await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchA1, placement.BatchA4) },
                Request(placement.ModelId, seed.CourseId, PlacementPartnerDelivery.Draft),
                Actor);

            Assert.True(outcome.IsDraft);
            var result = Assert.Single(outcome.Results);
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(0, result.StudentsSent);
            Assert.Equal(2, result.BatchesSent);

            var exam = Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
            Assert.False(await db.ExamAssignments.AsNoTracking().AnyAsync(a => a.ExamId == exam.Id));
            Assert.False(await db.ExamStudentStatuses.AsNoTracking().AnyAsync(s => s.ExamId == exam.Id));
            Assert.False(await db.Notifications.AsNoTracking().AnyAsync(n => n.StudentID == seed.ActiveStudentA));
            Assert.All(await db.ExamAssignmentsToBatches.AsNoTracking().Where(l => l.ExamId == exam.Id).ToListAsync(), l => Assert.False(l.IsSentToStudents));

            // الشريك يراها مسودة قابلة للإرسال
            var management = NewManagement(db);
            var scope = PlacementScope.Partner(seed.PartnerA, "u");
            var row = Assert.Single((await management.GetBatchExamsAsync(scope, seed.BatchA1))!);
            Assert.False(row.IsSentToStudents);
            Assert.True(row.IsFromInstitute);
            Assert.Equal(2, row.LinkedBatchCount);

            var review = await management.GetReviewAsync(scope, exam.Id);
            Assert.True(review!.CanSend);

            // الإرسال من منطقة الشريك يوصلها
            var send = await management.SendToStudentsAsync(scope, exam.Id);
            Assert.True(send.Succeeded, send.Message);
            Assert.Contains(seed.ActiveStudentA, await db.ExamAssignments.AsNoTracking().Where(a => a.ExamId == exam.Id).Select(a => a.StudentId).ToListAsync());
            Assert.Contains(placement.StudentA4, await db.ExamAssignments.AsNoTracking().Where(a => a.ExamId == exam.Id).Select(a => a.StudentId).ToListAsync());
        }

        [SqlServerFact]
        public async Task Draft_ForSchoolWhoseContractLacksPlacement_IsRejected_SendNowAllowed()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var contract = await db.PartnerSubscriptions.FirstAsync(s => s.PartnerId == seed.PartnerA);
            contract.CanUsePlacementExams = false;
            await db.SaveChangesAsync();

            var target = new PartnerDispatchTarget
            {
                PartnerId = seed.PartnerA,
                PartnerName = "A",
                BatchIds = new List<int> { seed.BatchA1 },
                ContractOverridden = true,
                OverrideReason = "موافقة الإدارة على هذا الإرسال"
            };

            var draft = await NewDispatch(db).DispatchAsync(new[] { target }, Request(placement.ModelId, seed.CourseId, PlacementPartnerDelivery.Draft), Actor);
            var rejected = Assert.Single(draft.Results);
            Assert.False(rejected.Succeeded);
            Assert.Contains("الإرسال الفوري", rejected.Message);
            Assert.Empty(await OwnedExamsAsync(db, seed.PartnerA));

            var send = await NewDispatch(db).DispatchAsync(new[] { target }, Request(placement.ModelId, seed.CourseId), Actor);
            Assert.True(Assert.Single(send.Results).Succeeded);
            Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
        }

        // =====================================================
        // السجل
        // =====================================================
        [SqlServerFact]
        public async Task Log_OneRowPerBatch_SameGroup_OverrideRecorded_DraftHasNoStudents()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var overridden = new PartnerDispatchTarget
            {
                PartnerId = seed.PartnerB,
                PartnerName = "B",
                BatchIds = new List<int> { seed.BatchB1 },
                ContractOverridden = true,
                OverrideReason = "موافقة الإدارة على هذا الإرسال"
            };

            var outcome = await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchA1, placement.BatchA4), overridden },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            var logs = await db.PartnerDispatchLogs.AsNoTracking().Where(l => l.DispatchGroupId == outcome.DispatchGroupId).ToListAsync();
            Assert.Equal(3, logs.Count);
            Assert.All(logs, l =>
            {
                Assert.Equal(PartnerDispatchKind.Placement, l.Kind);
                Assert.Equal(placement.ModelId, l.SourceProfessionalModelId);
                Assert.Equal(ApdTestData.SenderId, l.SentByUserId);
            });

            var examA = Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
            var logA1 = logs.Single(l => l.BatchId == seed.BatchA1);
            Assert.Equal(examA.Id, logA1.TargetEntityId);
            Assert.Equal(1, logA1.StudentsCount);
            Assert.False(logA1.ContractOverridden);

            var logB = logs.Single(l => l.PartnerId == seed.PartnerB);
            Assert.True(logB.ContractOverridden);
            Assert.Equal("موافقة الإدارة على هذا الإرسال", logB.OverrideReason);
        }

        [SqlServerFact]
        public async Task Log_Draft_RecordsZeroStudents()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var outcome = await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchA1) },
                Request(placement.ModelId, seed.CourseId, PlacementPartnerDelivery.Draft),
                Actor);

            var log = Assert.Single(await db.PartnerDispatchLogs.AsNoTracking().Where(l => l.DispatchGroupId == outcome.DispatchGroupId).ToListAsync());
            Assert.Equal(0, log.StudentsCount);
        }

        // =====================================================
        // عزل الفشل
        // =====================================================
        [SqlServerFact]
        public async Task OneSchoolFails_OtherSchoolStillCreated()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            // دفعة B ضمن هدف A لا تجتاز حارس الدفعات حتى لو تجاوزت ValidateAsync
            var outcome = await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchB1), Target(seed.PartnerB, "B", seed.BatchB1) },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            Assert.False(outcome.Results.Single(r => r.PartnerId == seed.PartnerA).Succeeded);
            Assert.True(outcome.Results.Single(r => r.PartnerId == seed.PartnerB).Succeeded);
            Assert.Empty(await OwnedExamsAsync(db, seed.PartnerA));
            Assert.Single(await OwnedExamsAsync(db, seed.PartnerB));
        }

        [SqlServerFact]
        public async Task LogFailure_RollsBackEverythingForThatSchool()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var outcome = await NewDispatch(db, new ThrowingLog()).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchA1) },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            var result = Assert.Single(outcome.Results);
            Assert.False(result.Succeeded);
            Assert.Contains("لم يُحفظ", result.Message);

            Assert.Empty(await OwnedExamsAsync(db, seed.PartnerA));
            Assert.False(await db.ExamAssignments.AsNoTracking().AnyAsync(a => a.StudentId == seed.ActiveStudentA));
            Assert.False(await db.ExamStudentStatuses.AsNoTracking().AnyAsync(s => s.StudentId == seed.ActiveStudentA));
            Assert.False(await db.ExamAssignmentsToBatches.AsNoTracking().AnyAsync(l => l.BatchId == seed.BatchA1));
        }

        [SqlServerFact]
        public async Task EmptyBatch_FailsThatSchool_OtherBatchWithoutStudentsIsNotLinked()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var onlyEmpty = await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchAEmpty) },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            var failed = Assert.Single(onlyEmpty.Results);
            Assert.False(failed.Succeeded);
            Assert.Contains("لا يوجد طلاب", failed.Message);

            var mixed = await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchA1, seed.BatchAEmpty) },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            Assert.Equal(1, Assert.Single(mixed.Results).BatchesSent);
            var exam = Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
            Assert.Equal(new[] { seed.BatchA1 }, await db.ExamAssignmentsToBatches.AsNoTracking().Where(l => l.ExamId == exam.Id).Select(l => l.BatchId).ToListAsync());
        }

        [SqlServerFact]
        public async Task BatchOfAnotherCourseOrArchived_IsRejected()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            foreach (var batchId in new[] { seed.BatchA3OtherCurriculum, seed.BatchA2Archived })
            {
                var outcome = await NewDispatch(db).DispatchAsync(
                    new[] { Target(seed.PartnerA, "A", batchId) },
                    Request(placement.ModelId, seed.CourseId),
                    Actor);

                Assert.False(Assert.Single(outcome.Results).Succeeded);
            }

            Assert.Empty(await OwnedExamsAsync(db, seed.PartnerA));
        }

        // =====================================================
        // الضغطة المزدوجة
        // =====================================================
        [SqlServerFact]
        public async Task RepeatWithinWindow_IsRejected_ForSameSchool_NotForAnother()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var first = await NewDispatch(db).DispatchAsync(new[] { Target(seed.PartnerA, "A", seed.BatchA1) }, Request(placement.ModelId, seed.CourseId), Actor);
            Assert.True(Assert.Single(first.Results).Succeeded);

            var repeat = await NewDispatch(db).DispatchAsync(
                new[] { Target(seed.PartnerA, "A", seed.BatchA1), Target(seed.PartnerB, "B", seed.BatchB1) },
                Request(placement.ModelId, seed.CourseId),
                Actor);

            var repeated = repeat.Results.Single(r => r.PartnerId == seed.PartnerA);
            Assert.False(repeated.Succeeded);
            Assert.Contains("قبل لحظات", repeated.Message);
            Assert.True(repeat.Results.Single(r => r.PartnerId == seed.PartnerB).Succeeded);

            Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
        }

        [SqlServerFact]
        public async Task ConcurrentDoubleSubmit_CreatesOneExam()
        {
            var (seed, placement) = await SeedAsync();

            async Task<PartnerDispatchOutcome> SendAsync()
            {
                await using var db = RtkRealDb.CreateDb();
                return await NewDispatch(db).DispatchAsync(new[] { Target(seed.PartnerA, "A", seed.BatchA1) }, Request(placement.ModelId, seed.CourseId), Actor);
            }

            var outcomes = await Task.WhenAll(SendAsync(), SendAsync());

            Assert.Equal(1, outcomes.Count(o => o.Results.Single().Succeeded));

            await using var check = RtkRealDb.CreateDb();
            Assert.Single(await OwnedExamsAsync(check, seed.PartnerA));
        }

        // =====================================================
        // أخطاء الطلب كله: لا يُكتب شيء
        // =====================================================
        [SqlServerFact]
        public async Task HomeworkModel_MissingModel_AreNotFound()
        {
            var (seed, _) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            foreach (var modelId in new[] { seed.ModelLinkedToA, -1 })
            {
                var outcome = await NewDispatch(db).DispatchAsync(new[] { Target(seed.PartnerA, "A", seed.BatchA1) }, Request(modelId, seed.CourseId), Actor);
                Assert.True(outcome.IsNotFound);
                Assert.Empty(outcome.Results);
            }

        }

        [SqlServerFact]
        public async Task ModelWithoutQuestions_InvalidDuration_MissingCourse_NoTargets_AreValidationErrors()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var targets = new[] { Target(seed.PartnerA, "A", seed.BatchA1) };

            var empty = new ProfessionalModel
            {
                Title = $"{ApdTestData.Marker} نموذج فارغ", Description = "-", CreatedBy = "-", CreatedAt = DateTime.UtcNow,
                ModelType = ProfessionalModelType.Exam, CurriculumId = seed.CurriculumId
            };
            db.ProfessionalModels.Add(empty);
            await db.SaveChangesAsync();

            var dispatch = NewDispatch(db);

            Assert.Contains("لا يحتوي على أسئلة", (await dispatch.DispatchAsync(targets, Request(empty.Id, seed.CourseId), Actor)).ValidationError);
            Assert.Contains("مدة الاختبار", (await dispatch.DispatchAsync(targets, Request(placement.ModelId, seed.CourseId, duration: 4), Actor)).ValidationError);
            Assert.Contains("مدة الاختبار", (await dispatch.DispatchAsync(targets, Request(placement.ModelId, seed.CourseId, duration: 301), Actor)).ValidationError);
            Assert.Contains("دورة", (await dispatch.DispatchAsync(targets, Request(placement.ModelId, 0), Actor)).ValidationError);
            Assert.Contains("طريقة الإرسال", (await dispatch.DispatchAsync(targets, Request(placement.ModelId, seed.CourseId, (PlacementPartnerDelivery)9), Actor)).ValidationError);
            Assert.Contains("اختر مدرسة", (await dispatch.DispatchAsync(Array.Empty<PartnerDispatchTarget>(), Request(placement.ModelId, seed.CourseId), Actor)).ValidationError);

            Assert.Empty(await OwnedExamsAsync(db, seed.PartnerA));
        }

        // =====================================================
        // نموذج مكرر الأسئلة
        // =====================================================
        [SqlServerFact]
        public async Task ModelWithDuplicateQuestion_ExamHasItOnce()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            db.ProfessionalModelQuestions.Add(new ProfessionalModelQuestion { ModelId = placement.ModelId, QuestionId = seed.QuestionId, OrderNumber = 2 });
            await db.SaveChangesAsync();

            await NewDispatch(db).DispatchAsync(new[] { Target(seed.PartnerA, "A", seed.BatchA1) }, Request(placement.ModelId, seed.CourseId), Actor);

            var exam = Assert.Single(await OwnedExamsAsync(db, seed.PartnerA));
            Assert.Equal(1, exam.TotalQuestions);
            Assert.Equal(1, await db.ExamQuestions.AsNoTracking().CountAsync(q => q.ExamId == exam.Id));
        }

        // =====================================================
        // «المنصة»: دفعات المعهد فقط عند الإنشاء
        // =====================================================
        [SqlServerFact]
        public async Task PlatformCreate_RejectsPartnerBatch_AcceptsInstituteBatch_LookupsArePlatformOnly()
        {
            var (seed, placement) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var management = NewManagement(db);
            var admin = PlacementScope.Admin(ApdTestData.SenderId);

            var rejected = await management.CreateFromProfessionalModelAsync(admin, new QdratNew.ViewModels.Exam.PlacementExamFromModelVm
            {
                SelectedModelId = placement.ModelId,
                SelectedBatchId = seed.BatchA1,
                SelectedCourseId = seed.CourseId,
                DurationMinutes = 40
            });

            Assert.False(rejected.Succeeded);
            Assert.Contains("ليست من دفعات المنصة", rejected.Message);
            Assert.False(await db.ExamAssignmentsToBatches.AsNoTracking().AnyAsync(l => l.BatchId == seed.BatchA1));

            var accepted = await management.CreateFromProfessionalModelAsync(admin, new QdratNew.ViewModels.Exam.PlacementExamFromModelVm
            {
                SelectedModelId = placement.ModelId,
                SelectedBatchId = seed.BatchI1,
                SelectedCourseId = seed.CourseId,
                DurationMinutes = 40
            });

            Assert.True(accepted.Succeeded, accepted.Message);
            var link = await db.ExamAssignmentsToBatches.AsNoTracking().Include(l => l.Exam).SingleAsync(l => l.BatchId == seed.BatchI1);
            Assert.Null(link.Exam.OwnerPartnerId);
            Assert.True(link.IsSentToStudents);

            var lookups = await management.GetCreateFromModelAsync(admin);
            Assert.Contains(lookups.AvailableBatches, b => b.Value == seed.BatchI1.ToString());
            Assert.DoesNotContain(lookups.AvailableBatches, b => b.Value == seed.BatchA1.ToString());

            var advanced = await management.GetCreateAdvancedAsync(admin);
            Assert.Contains(advanced.Batches, b => b.Value == seed.BatchI1.ToString());
            Assert.DoesNotContain(advanced.Batches, b => b.Value == seed.BatchA1.ToString());

            // قراءة الأدمن تبقى كما هي: دفعة شريك قديمة تُعرف باسمها
            Assert.NotNull(await management.GetBatchNameAsync(admin, seed.BatchA1));
        }

        [SqlServerFact]
        public async Task PlatformCreateAdvanced_RejectsPartnerBatch()
        {
            var (seed, _) = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();

            var result = await NewManagement(db).CreateAdvancedAsync(PlacementScope.Admin(ApdTestData.SenderId), new QdratNew.ViewModels.Exam.PlacementExamCreateAdvancedViewModel
            {
                CourseId = seed.CourseId,
                BatchId = seed.BatchA1,
                TotalQuestions = 1,
                DurationMinutes = 30,
                SectionSelections = new List<QdratNew.ViewModels.Exam.PlacementExamSectionSelectionVm>
                {
                    new() { SectionId = 1, QuestionCount = 1 }
                }
            });

            Assert.False(result.Succeeded);
            Assert.Contains("ليست من دفعات المنصة", result.Message);
        }

        [Fact]
        public void AdminForPartnerScope_ActsAsPartner_AndRequiresItsLogData()
        {
            var group = Guid.NewGuid();
            var scope = PlacementScope.AdminForPartner(7, "admin", "مدير", true, "سبب", group);

            Assert.True(scope.IsPartner);
            Assert.True(scope.IsAdminForPartner);
            Assert.Equal(7, scope.PartnerId);
            Assert.True(scope.ContractOverridden);
            Assert.Equal("سبب", scope.OverrideReason);
            Assert.Equal(group, scope.DispatchGroupId);

            Assert.False(PlacementScope.Partner(7, "u").IsAdminForPartner);
            Assert.True(PlacementScope.Partner(7, "u").IsPartner);
            Assert.False(PlacementScope.Admin("u").IsPartner);

            Assert.Throws<ArgumentOutOfRangeException>(() => PlacementScope.AdminForPartner(0, "admin", null, false, null, group));
            Assert.Throws<ArgumentException>(() => PlacementScope.AdminForPartner(7, " ", null, false, null, group));
            Assert.Throws<ArgumentException>(() => PlacementScope.AdminForPartner(7, "admin", null, false, null, Guid.Empty));
        }
    }
}
