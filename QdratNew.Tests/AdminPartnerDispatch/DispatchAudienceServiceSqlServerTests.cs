using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Enums;
using QdratNew.Services.PartnerDispatch;
using QdratNew.Services.ProfessionalModels;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.AdminPartnerDispatch
{
    // APD-S1.2 — الأهلية والتجاوز ودفعات الجمهور على SQL Server حقيقي (EF.Constant + عدّ الطلاب داخل الاستعلام).
    // التشغيل: QDRAT_TEST_SQL على قاعدة الاختبار المعزولة. بدونه تُتخطّى.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class DispatchAudienceServiceSqlServerTests : IAsyncLifetime
    {
        private const string ValidReason = "طلب إدارة المدرسة بخطاب رسمي";

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

        private static DispatchAudienceService NewService(ApplicationDbContext db)
            => new(db);

        private static DispatchSource HomeworkFrom(int modelId) => new(PartnerDispatchKind.Homework, modelId, null, null);

        private static AudienceSelection Partners(params (int PartnerId, int[] Batches)[] items) => new()
        {
            Audience = DispatchAudience.Partners,
            PartnerBatchIds = items.ToDictionary(i => i.PartnerId, i => i.Batches.ToList())
        };

        private static async Task<(ApdSeed Seed, ApplicationDbContext Db, DispatchAudienceService Service)> ArrangeAsync()
        {
            var seedDb = RtkRealDb.CreateDb();
            var seed = await ApdTestData.SeedAsync(seedDb);
            await seedDb.DisposeAsync();

            var db = RtkRealDb.CreateDb();
            return (seed, db, NewService(db));
        }

        // =====================================================
        // القوائم
        // =====================================================
        [SqlServerFact]
        public async Task PlatformBatches_AreInstituteOnly_ActiveAndNotArchived_NotFilteredByCurriculum()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var batches = await service.GetPlatformBatchesAsync(HomeworkFrom(seed.ModelLinkedToA));
            var ids = batches.Select(b => b.BatchId).ToHashSet();

            Assert.Contains(seed.BatchI1, ids);
            Assert.Contains(seed.BatchI2OtherCourse, ids); // منهج مختلف: الشاشات اليوم تعرض كل دفعات المعهد
            foreach (var partnerBatch in new[] { seed.BatchA1, seed.BatchAEmpty, seed.BatchA3OtherCurriculum, seed.BatchB1, seed.BatchC1, seed.BatchA2Archived })
                Assert.DoesNotContain(partnerBatch, ids);

            Assert.Equal(1, batches.Single(b => b.BatchId == seed.BatchI1).ActiveStudents);
        }

        [SqlServerFact]
        public async Task PlatformBatches_RespectSourceCourse()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var ids = (await service.GetPlatformBatchesAsync(new DispatchSource(PartnerDispatchKind.MinistrySim, null, null, seed.CourseId)))
                .Select(b => b.BatchId).ToHashSet();

            Assert.Contains(seed.BatchI1, ids);
            Assert.DoesNotContain(seed.BatchI2OtherCourse, ids);
        }

        [SqlServerFact]
        public async Task Partners_EligibilityMatrix_ForHomeworkFromModel()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var partners = (await service.GetPartnersAsync(HomeworkFrom(seed.ModelLinkedToA))).ToDictionary(p => p.PartnerId);

            var a = partners[seed.PartnerA];
            Assert.True(a.Eligible);
            Assert.Null(a.BlockReason);

            var b = partners[seed.PartnerB];
            Assert.False(b.Eligible);
            Assert.Equal(DispatchRules.FeatureMissing(PartnerDispatchKind.Homework), b.BlockReason);
            Assert.True(b.Overridable);

            // عقد منتهٍ = لا عقد نشط، ولا يُتجاوز
            var c = partners[seed.PartnerC];
            Assert.False(c.Eligible);
            Assert.Equal(DispatchRules.NoActiveContract, c.BlockReason);
            Assert.False(c.Overridable);

            // شراكة منتهية بلا عقد نشط: لا تُعرض
            Assert.False(partners.ContainsKey(seed.PartnerD));
        }

        [SqlServerFact]
        public async Task Partners_ContractWithoutProfessionalModels_IsOverridable()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;
            await RemoveProfessionalModelsFromContractAsync(db, seed.PartnerA);

            var a = (await service.GetPartnersAsync(HomeworkFrom(seed.ModelLinkedToA))).Single(p => p.PartnerId == seed.PartnerA);

            Assert.False(a.Eligible);
            Assert.Equal(DispatchRules.ProfessionalModelsMissing, a.BlockReason);
            Assert.True(a.Overridable);
        }

        // الأدمن هو المرسل: إتاحة النموذج في مكتبة الشريك لا تحجب الإرسال، العقد وحده يحكم
        [SqlServerFact]
        public async Task Partners_InternalModel_IsEligible_WhenContractIncludesProfessionalModels()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var a = (await service.GetPartnersAsync(HomeworkFrom(seed.ModelNotForAnyone))).Single(p => p.PartnerId == seed.PartnerA);

            Assert.True(a.Eligible);
            Assert.Null(a.BlockReason);
        }

        private static async Task RemoveProfessionalModelsFromContractAsync(ApplicationDbContext db, int partnerId)
        {
            var contracts = await db.PartnerSubscriptions.Where(s => s.PartnerId == partnerId).ToListAsync();
            contracts.ForEach(c => c.CanUseProfessionalModels = false);
            await db.SaveChangesAsync();
        }

        [SqlServerFact]
        public async Task PartnerBatches_MatchModelCurriculum_ExcludeArchived_AndCountOwnActiveStudentsOnly()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var a = (await service.GetPartnersAsync(HomeworkFrom(seed.ModelLinkedToA))).Single(p => p.PartnerId == seed.PartnerA);
            var ids = a.Batches.Select(b => b.BatchId).ToHashSet();

            Assert.Equal(new[] { seed.BatchA1, seed.BatchAEmpty }.OrderBy(x => x), ids.OrderBy(x => x));

            // نشط واحد: غير النشط للتعلم، وطالب الشريك الآخر، والمنسحب لا يُحسبون
            Assert.Equal(1, a.Batches.Single(b => b.BatchId == seed.BatchA1).ActiveStudents);
            Assert.Equal(0, a.Batches.Single(b => b.BatchId == seed.BatchAEmpty).ActiveStudents);
        }

        [SqlServerFact]
        public async Task PerformanceIndicator_UsesSourceCurriculum_AndItsContractFlag()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var partners = (await service.GetPartnersAsync(new DispatchSource(PartnerDispatchKind.PerformanceIndicator, null, seed.OtherCurriculumId, null)))
                .ToDictionary(p => p.PartnerId);

            Assert.True(partners[seed.PartnerA].Eligible);
            Assert.Equal(new[] { seed.BatchA3OtherCurriculum }, partners[seed.PartnerA].Batches.Select(b => b.BatchId));
            Assert.Equal(DispatchRules.FeatureMissing(PartnerDispatchKind.PerformanceIndicator), partners[seed.PartnerB].BlockReason);
        }

        [SqlServerFact]
        public async Task MinistrySim_UsesNewContractFlag_AndSourceCourse()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var partners = (await service.GetPartnersAsync(new DispatchSource(PartnerDispatchKind.MinistrySim, null, null, seed.CourseId)))
                .ToDictionary(p => p.PartnerId);

            Assert.True(partners[seed.PartnerB].Eligible);
            Assert.Equal(new[] { seed.BatchB1 }, partners[seed.PartnerB].Batches.Select(b => b.BatchId));
            Assert.Equal(DispatchRules.FeatureMissing(PartnerDispatchKind.MinistrySim), partners[seed.PartnerA].BlockReason);
            Assert.DoesNotContain(seed.BatchA3OtherCurriculum, partners[seed.PartnerA].Batches.Select(b => b.BatchId));
        }

        [SqlServerFact]
        public async Task Placement_WithoutContractFlag_IsBlocked()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var a = (await service.GetPartnersAsync(new DispatchSource(PartnerDispatchKind.Placement, seed.ModelLinkedToA, null, null)))
                .Single(p => p.PartnerId == seed.PartnerA);

            Assert.Equal(DispatchRules.FeatureMissing(PartnerDispatchKind.Placement), a.BlockReason);
            Assert.True(a.Overridable);
        }

        // =====================================================
        // التحقق
        // =====================================================
        [SqlServerFact]
        public async Task Validate_Platform_AcceptsInstituteBatches()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var result = await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA),
                new AudienceSelection { PlatformBatchIds = new() { seed.BatchI1, seed.BatchI2OtherCourse, seed.BatchI1 } }, canOverride: false);

            Assert.True(result.IsValid, string.Join(" | ", result.Errors));
            Assert.Equal(DispatchAudience.Platform, result.Audience);
            Assert.Equal(new[] { seed.BatchI1, seed.BatchI2OtherCourse }, result.PlatformBatchIds);
            Assert.Empty(result.Partners);
        }

        [SqlServerFact]
        public async Task Validate_Platform_RejectsPartnerBatch()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var result = await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA),
                new AudienceSelection { PlatformBatchIds = new() { seed.BatchI1, seed.BatchA1 } }, canOverride: true);

            Assert.False(result.IsValid);
            Assert.Empty(result.PlatformBatchIds);
        }

        [SqlServerFact]
        public async Task Validate_Platform_RejectsPartnerSelectionInSameRequest()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var selection = Partners((seed.PartnerA, new[] { seed.BatchA1 }));
            selection.Audience = DispatchAudience.Platform;
            selection.PlatformBatchIds = new() { seed.BatchI1 };

            Assert.False((await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA), selection, canOverride: true)).IsValid);
        }

        [SqlServerFact]
        public async Task Validate_Partners_EligiblePartner_IsAccepted()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var result = await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA), Partners((seed.PartnerA, new[] { seed.BatchA1, seed.BatchA1 })), canOverride: false);

            Assert.True(result.IsValid, string.Join(" | ", result.Errors));
            var target = Assert.Single(result.Partners);
            Assert.Equal(seed.PartnerA, target.PartnerId);
            Assert.Equal(new[] { seed.BatchA1 }, target.BatchIds);
            Assert.False(target.ContractOverridden);
            Assert.Null(target.OverrideReason);
        }

        [SqlServerFact]
        public async Task Validate_Partners_RejectsPlatformBatch_UnderPartner()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var result = await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA), Partners((seed.PartnerA, new[] { seed.BatchI1 })), canOverride: true);

            Assert.False(result.IsValid);
            Assert.Empty(result.Partners);
        }

        [SqlServerFact]
        public async Task Validate_Partners_RejectsPlatformIdsInSameRequest()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var selection = Partners((seed.PartnerA, new[] { seed.BatchA1 }));
            selection.PlatformBatchIds = new() { seed.BatchI1 };

            Assert.False((await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA), selection, canOverride: true)).IsValid);
        }

        [SqlServerFact]
        public async Task Validate_Partners_RejectsAnotherPartnersBatch_ArchivedBatch_AndOtherCurriculum()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            foreach (var batch in new[] { seed.BatchB1, seed.BatchA2Archived, seed.BatchA3OtherCurriculum })
            {
                var result = await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA), Partners((seed.PartnerA, new[] { seed.BatchA1, batch })), canOverride: true);
                Assert.False(result.IsValid);
                Assert.Empty(result.Partners);
            }
        }

        [SqlServerFact]
        public async Task Validate_Partners_EmptySelection_OrUnknownPartner_IsRejected()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            Assert.False((await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA), Partners((seed.PartnerA, Array.Empty<int>())), canOverride: true)).IsValid);
            Assert.False((await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA), Partners((int.MaxValue, new[] { seed.BatchA1 })), canOverride: true)).IsValid);
        }

        [SqlServerFact]
        public async Task Validate_IneligiblePartner_NeedsOverrideFlag_Permission_AndValidReason()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;
            var source = HomeworkFrom(seed.ModelLinkedToA);

            // بلا طلب تجاوز
            var noFlag = await service.ValidateAsync(source, Partners((seed.PartnerB, new[] { seed.BatchB1 })), canOverride: true);
            Assert.False(noFlag.IsValid);
            Assert.Contains(noFlag.Errors, e => e.Contains(DispatchRules.FeatureMissing(PartnerDispatchKind.Homework)));

            // بطلب تجاوز بلا صلاحية
            var withFlag = Partners((seed.PartnerB, new[] { seed.BatchB1 }));
            withFlag.OverrideContract = true;
            withFlag.OverrideReason = ValidReason;
            Assert.False((await service.ValidateAsync(source, withFlag, canOverride: false)).IsValid);

            // سبب قصير
            withFlag.OverrideReason = "  قصير  ";
            Assert.False((await service.ValidateAsync(source, withFlag, canOverride: true)).IsValid);

            // سبب أطول من 500
            withFlag.OverrideReason = new string('س', 501);
            Assert.False((await service.ValidateAsync(source, withFlag, canOverride: true)).IsValid);
        }

        [SqlServerFact]
        public async Task Validate_Override_MarksOnlyIneligiblePartners_WithTrimmedReason()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var selection = Partners((seed.PartnerA, new[] { seed.BatchA1 }), (seed.PartnerB, new[] { seed.BatchB1 }));
            selection.OverrideContract = true;
            selection.OverrideReason = "  " + ValidReason + "  ";

            var result = await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA), selection, canOverride: true);

            Assert.True(result.IsValid, string.Join(" | ", result.Errors));
            var a = result.Partners.Single(p => p.PartnerId == seed.PartnerA);
            var b = result.Partners.Single(p => p.PartnerId == seed.PartnerB);
            Assert.False(a.ContractOverridden);
            Assert.Null(a.OverrideReason);
            Assert.True(b.ContractOverridden);
            Assert.Equal(ValidReason, b.OverrideReason);
        }

        [SqlServerFact]
        public async Task Validate_ContractWithoutProfessionalModels_CanBeOverridden()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;
            await RemoveProfessionalModelsFromContractAsync(db, seed.PartnerA);

            var selection = Partners((seed.PartnerA, new[] { seed.BatchA1 }));
            selection.OverrideContract = true;
            selection.OverrideReason = ValidReason;

            var result = await service.ValidateAsync(HomeworkFrom(seed.ModelNotForAnyone), selection, canOverride: true);

            Assert.True(result.IsValid, string.Join(" | ", result.Errors));
            Assert.True(Assert.Single(result.Partners).ContractOverridden);
        }

        [SqlServerFact]
        public async Task Validate_PartnerWithoutActiveContract_IsNeverOverridden()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var selection = Partners((seed.PartnerC, new[] { seed.BatchC1 }));
            selection.OverrideContract = true;
            selection.OverrideReason = ValidReason;

            var result = await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA), selection, canOverride: true);

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains(DispatchRules.NoActiveContract));
        }

        [SqlServerFact]
        public async Task Validate_OneInvalidPartner_ReturnsNoTargetsAtAll()
        {
            var (seed, db, service) = await ArrangeAsync();
            await using var _ = db;

            var result = await service.ValidateAsync(HomeworkFrom(seed.ModelLinkedToA),
                Partners((seed.PartnerA, new[] { seed.BatchA1 }), (seed.PartnerC, new[] { seed.BatchC1 })), canOverride: false);

            Assert.False(result.IsValid);
            Assert.Empty(result.Partners);
        }

        // =====================================================
        // إتاحة النموذج لعدة شركاء = نفس القاعدة لكل شريك
        // =====================================================
        [SqlServerFact]
        public async Task PartnerIdsWithAccess_MatchesPerPartnerRule()
        {
            var (seed, db, _) = await ArrangeAsync();
            await using var __ = db;
            var availability = new ProfessionalModelAvailabilityService(db);

            foreach (var modelId in new[] { seed.ModelLinkedToA, seed.ModelNotForAnyone })
            {
                var bulk = await availability.GetPartnerIdsWithAccessAsync(modelId, null);
                foreach (var partnerId in new[] { seed.PartnerA, seed.PartnerB, seed.PartnerC, seed.PartnerD })
                    Assert.Equal(await availability.IsAvailableForPartnerAsync(modelId, partnerId, null), bulk.Contains(partnerId));
            }

            Assert.Contains(seed.PartnerA, await availability.GetPartnerIdsWithAccessAsync(seed.ModelLinkedToA, null));
            Assert.Empty((await availability.GetPartnerIdsWithAccessAsync(seed.ModelLinkedToA, ProfessionalModelType.Exam))
                .Intersect(new[] { seed.PartnerA, seed.PartnerB, seed.PartnerC, seed.PartnerD }));
        }
    }
}
