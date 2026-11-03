using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.ProfessionalModels;
using Xunit;

namespace QdratNew.Tests.PartnerProfessionalModels
{
    // PPM-S1.2 — مصفوفة قاعدة الإتاحة (المصدر الوحيد)
    public class AvailabilityTests
    {
        private const int PartnerA = 1;
        private const int PartnerB = 2;

        private static ProfessionalModel Model(
            int id,
            bool forPartners = true,
            bool global = false,
            bool archived = false,
            ProfessionalModelType type = ProfessionalModelType.Homework) => new()
            {
                Id = id,
                Title = "M" + id,
                Description = "-",
                CreatedBy = "-",
                IsForPartners = forPartners,
                IsGlobal = global,
                IsArchived = archived,
                ModelType = type
            };

        private static (ApplicationDbContext Db, ProfessionalModelAvailabilityService Service) Seed(
            bool contractActive = true,
            bool contractAllowsModels = true)
        {
            var db = TestDbContextFactory.CreateFreshContext(out _);
            var today = DateTime.Today;

            db.PartnerSubscriptions.Add(new PartnerSubscription
            {
                Id = 1,
                PartnerId = PartnerA,
                StartDate = contractActive ? today.AddDays(-1) : today.AddDays(-30),
                EndDate = contractActive ? today.AddDays(30) : today.AddDays(-1),
                CanUseProfessionalModels = contractAllowsModels
            });
            db.PartnerSubscriptionPeriods.AddRange(
                new PartnerSubscriptionPeriod { Id = 10, PartnerSubscriptionId = 1, StartDate = today.AddDays(-1), EndDate = today.AddDays(5) },
                new PartnerSubscriptionPeriod { Id = 11, PartnerSubscriptionId = 1, StartDate = today.AddDays(-20), EndDate = today.AddDays(-10) });
            db.SaveChanges();

            return (db, new ProfessionalModelAvailabilityService(db));
        }

        private static async Task<bool> AvailableToA(ProfessionalModel model, (ApplicationDbContext Db, ProfessionalModelAvailabilityService Service) h, ProfessionalModelType? type = null)
        {
            h.Db.ProfessionalModels.Add(model);
            await h.Db.SaveChangesAsync();

            var viaQuery = await h.Service.AvailableForPartner(PartnerA, type).AnyAsync(m => m.Id == model.Id);
            var viaCheck = await h.Service.IsAvailableForPartnerAsync(model.Id, PartnerA, type);
            Assert.Equal(viaQuery, viaCheck);
            return viaCheck;
        }

        [Fact]
        public async Task Internal_EvenWhenGlobalOrLinked_IsHidden()
        {
            var h = Seed();
            h.Db.ProfessionalModelPartners.Add(new ProfessionalModelPartner { ProfessionalModelId = 1, PartnerId = PartnerA });

            Assert.False(await AvailableToA(Model(1, forPartners: false, global: true), h));
        }

        [Fact]
        public async Task AllPartners_IsVisible()
            => Assert.True(await AvailableToA(Model(1, global: true), Seed()));

        [Fact]
        public async Task Selected_VisibleOnlyToLinkedPartner()
        {
            var h = Seed();
            h.Db.ProfessionalModelPartners.Add(new ProfessionalModelPartner { ProfessionalModelId = 1, PartnerId = PartnerA });
            h.Db.ProfessionalModelPartners.Add(new ProfessionalModelPartner { ProfessionalModelId = 2, PartnerId = PartnerB });

            Assert.True(await AvailableToA(Model(1), h));
            Assert.False(await AvailableToA(Model(2), h));
        }

        [Fact]
        public async Task SubscriptionPeriod_OnlyWhenEffectiveByDate()
        {
            var h = Seed();
            h.Db.ProfessionalModelSubscriptionPeriods.AddRange(
                new ProfessionalModelSubscriptionPeriod { ProfessionalModelId = 1, PartnerSubscriptionPeriodId = 10 },
                new ProfessionalModelSubscriptionPeriod { ProfessionalModelId = 2, PartnerSubscriptionPeriodId = 11 });

            Assert.True(await AvailableToA(Model(1), h));
            Assert.False(await AvailableToA(Model(2), h));
        }

        [Fact]
        public async Task ContractWithoutProfessionalModels_HidesAll()
            => Assert.False(await AvailableToA(Model(1, global: true), Seed(contractAllowsModels: false)));

        [Fact]
        public async Task ExpiredContract_HidesAll()
            => Assert.False(await AvailableToA(Model(1, global: true), Seed(contractActive: false)));

        [Fact]
        public async Task Archived_IsHidden()
            => Assert.False(await AvailableToA(Model(1, global: true, archived: true), Seed()));

        [Fact]
        public async Task TypeFilter_RejectsOtherType_NullAcceptsAny()
        {
            var h = Seed();
            Assert.False(await AvailableToA(Model(1, global: true, type: ProfessionalModelType.Exam), h, ProfessionalModelType.Homework));
            Assert.True(await h.Service.IsAvailableForPartnerAsync(1, PartnerA, ProfessionalModelType.Exam));
            Assert.True(await h.Service.IsAvailableForPartnerAsync(1, PartnerA, null));
        }

        [Fact]
        public async Task KillSwitch_ExplicitFalseHidesAll_OtherValuesKeepAccess()
        {
            var h = Seed();
            var setting = new SystemSetting(ProfessionalModelAvailabilityService.PartnerAccessSettingKey, "true", "-");
            h.Db.SystemSettings.Add(setting);

            Assert.True(await AvailableToA(Model(1, global: true), h));

            setting.Value = "false";
            await h.Db.SaveChangesAsync();
            Assert.False(await h.Service.IsAvailableForPartnerAsync(1, PartnerA, null));
        }

        [Fact]
        public async Task PartnerOptions_ActivePlusPreviouslySelectedEnded()
        {
            var (db, service) = Seed();
            var today = DateTime.Today;
            db.Partners.AddRange(
                new Partner { Id = 1, Name = "ب نشط", Code = "A", PartnershipEnd = today.AddDays(5) },
                new Partner { Id = 2, Name = "أ منتهي مختار", Code = "B", PartnershipEnd = today.AddDays(-5) },
                new Partner { Id = 3, Name = "منتهي غير مختار", Code = "C", PartnershipEnd = today.AddDays(-5) });
            await db.SaveChangesAsync();

            var options = await service.GetPartnerOptionsAsync(new[] { 2 });

            Assert.Equal(new[] { 2, 1 }, options.Select(o => o.Id));
            Assert.False(options[0].IsActive);
            Assert.True(options[1].IsActive);
        }
    }
}
