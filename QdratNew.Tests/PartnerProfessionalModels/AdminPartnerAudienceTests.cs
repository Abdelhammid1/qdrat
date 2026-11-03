using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using QdratNew.Areas.Admin.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Security;
using QdratNew.Security.AdminPermissions;
using QdratNew.Services.Interfaces;
using QdratNew.Services.ProfessionalModels;
using QdratNew.ViewModels.ProfessionalModel;
using Xunit;

namespace QdratNew.Tests.PartnerProfessionalModels
{
    // PPM-S1.3 + S1.4 — الحفظ وشرط صلاحية Visibility في Create/Edit/Visibility
    public class AdminPartnerAudienceTests
    {
        private const int ModelId = 50;
        private const int PartnerA = 1;
        private const int PartnerB = 2;

        private static ApplicationDbContext NewDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var db = new ApplicationDbContext(options);
            var today = DateTime.Today;
            db.Partners.AddRange(
                new Partner { Id = PartnerA, Name = "أ", Code = "A", PartnershipEnd = today.AddDays(9) },
                new Partner { Id = PartnerB, Name = "ب", Code = "B", PartnershipEnd = today.AddDays(9) });
            db.SaveChanges();
            return db;
        }

        private static ProfessionalModelsController Controller(ApplicationDbContext db, bool canManage)
        {
            var auth = new Mock<IAuthorizationService>();
            auth.Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), AdminPermissionPolicies.ProfessionalModels_Visibility))
                .ReturnsAsync(canManage ? AuthorizationResult.Success() : AuthorizationResult.Failed());

            var clock = new Mock<ITimeZoneService>();
            clock.Setup(c => c.GetNowUtc()).Returns(DateTime.UtcNow);

            var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "admin") }, "test")) };
            return new ProfessionalModelsController(db, clock.Object, new ProfessionalModelAvailabilityService(db), auth.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
            };
        }

        private static ProfessionalModelCreateVm CreateVm(bool forPartners, PartnerAudience audience, params int[] partners) => new()
        {
            Title = "M1",
            Description = "-",
            ModelType = ProfessionalModelType.Exam,
            IsForPartners = forPartners,
            PartnerAudience = audience,
            SelectedPartnerIds = partners.ToList()
        };

        private static ProfessionalModelEditVm EditVm(bool forPartners, PartnerAudience audience, params int[] partners) => new()
        {
            Title = "M1-edited",
            Description = "-",
            ModelType = ProfessionalModelType.Exam,
            IsForPartners = forPartners,
            PartnerAudience = audience,
            SelectedPartnerIds = partners.ToList()
        };

        private static void SeedModel(ApplicationDbContext db, bool forPartners, bool global, params int[] partners)
        {
            db.ProfessionalModels.Add(new ProfessionalModel { Id = ModelId, Title = "M1", Description = "-", CreatedBy = "-", IsForPartners = forPartners, IsGlobal = global });
            foreach (var p in partners)
                db.ProfessionalModelPartners.Add(new ProfessionalModelPartner { ProfessionalModelId = ModelId, PartnerId = p });
            db.SaveChanges();
            db.ChangeTracker.Clear();
        }

        private static int[] Links(ApplicationDbContext db)
            => db.ProfessionalModelPartners.AsNoTracking().Where(p => p.ProfessionalModelId == ModelId).Select(p => p.PartnerId).OrderBy(x => x).ToArray();

        private static ProfessionalModel Stored(ApplicationDbContext db)
            => db.ProfessionalModels.AsNoTracking().Single(m => m.Id == ModelId);

        // =========================================
        // Create
        // =========================================
        [Fact]
        public async Task Create_Selected_SavesFlagAndLinks()
        {
            using var db = NewDb();

            Assert.IsType<RedirectToActionResult>(await Controller(db, true).Create(CreateVm(true, PartnerAudience.Selected, PartnerA)));

            var model = await db.ProfessionalModels.AsNoTracking().SingleAsync();
            Assert.True(model.IsForPartners);
            Assert.False(model.IsGlobal);
            Assert.Equal(PartnerA, (await db.ProfessionalModelPartners.SingleAsync()).PartnerId);
        }

        [Fact]
        public async Task Create_All_SetsGlobalWithoutLinks()
        {
            using var db = NewDb();

            await Controller(db, true).Create(CreateVm(true, PartnerAudience.All, PartnerA));

            var model = await db.ProfessionalModels.AsNoTracking().SingleAsync();
            Assert.True(model.IsForPartners && model.IsGlobal);
            Assert.Empty(db.ProfessionalModelPartners);
        }

        [Fact]
        public async Task Create_WithoutVisibilityPermission_IgnoresCraftedAudience()
        {
            using var db = NewDb();

            Assert.IsType<RedirectToActionResult>(await Controller(db, false).Create(CreateVm(true, PartnerAudience.All, PartnerA)));

            var model = await db.ProfessionalModels.AsNoTracking().SingleAsync();
            Assert.False(model.IsForPartners);
            Assert.False(model.IsGlobal);
            Assert.Empty(db.ProfessionalModelPartners);
        }

        [Theory]
        [InlineData]
        [InlineData(999)]
        public async Task Create_SelectedWithoutRealPartners_FailsAndWritesNothing(params int[] partners)
        {
            using var db = NewDb();
            var controller = Controller(db, true);

            var view = Assert.IsType<ViewResult>(await controller.Create(CreateVm(true, PartnerAudience.Selected, partners)));

            Assert.True(controller.ModelState.ContainsKey(nameof(ProfessionalModelCreateVm.SelectedPartnerIds)));
            Assert.NotEmpty(Assert.IsType<ProfessionalModelCreateVm>(view.Model).PartnerOptions);
            Assert.Empty(db.ProfessionalModels);
        }

        [Fact]
        public async Task Create_UnknownAudienceValue_IsRejected()
        {
            using var db = NewDb();
            var controller = Controller(db, true);

            Assert.IsType<ViewResult>(await controller.Create(CreateVm(true, (PartnerAudience)99, PartnerA)));
            Assert.Empty(db.ProfessionalModels);
        }

        [Fact]
        public async Task Create_NonExistingPartnerIds_AreDropped()
        {
            using var db = NewDb();

            await Controller(db, true).Create(CreateVm(true, PartnerAudience.Selected, PartnerA, 999));

            Assert.Equal(PartnerA, (await db.ProfessionalModelPartners.SingleAsync()).PartnerId);
        }

        [Fact]
        public async Task Create_Get_WithoutPermission_HasNoPartnerData()
        {
            using var db = NewDb();

            var view = Assert.IsType<ViewResult>(await Controller(db, false).Create());

            var vm = Assert.IsType<ProfessionalModelCreateVm>(view.Model);
            Assert.False(vm.CanManageVisibility);
            Assert.Empty(vm.PartnerOptions);
        }

        // =========================================
        // Edit
        // =========================================
        [Fact]
        public async Task Edit_Get_LoadsSavedAudience()
        {
            using var db = NewDb();
            SeedModel(db, forPartners: true, global: false, PartnerB);

            var view = Assert.IsType<ViewResult>(await Controller(db, true).Edit(ModelId));

            var vm = Assert.IsType<ProfessionalModelEditVm>(view.Model);
            Assert.True(vm.IsForPartners);
            Assert.Equal(PartnerAudience.Selected, vm.PartnerAudience);
            Assert.Equal(new[] { PartnerB }, vm.SelectedPartnerIds);
            Assert.True(vm.CanManageVisibility);
        }

        [Fact]
        public async Task Edit_Selected_SyncsLinksOnly()
        {
            using var db = NewDb();
            SeedModel(db, forPartners: true, global: true, PartnerA);

            await Controller(db, true).Edit(ModelId, EditVm(true, PartnerAudience.Selected, PartnerB));

            Assert.False(Stored(db).IsGlobal);
            Assert.Equal(new[] { PartnerB }, Links(db));
        }

        [Fact]
        public async Task Edit_All_RemovesPartnerLinks_KeepsPeriods()
        {
            using var db = NewDb();
            SeedModel(db, forPartners: true, global: false, PartnerA);
            db.ProfessionalModelSubscriptionPeriods.Add(new ProfessionalModelSubscriptionPeriod { ProfessionalModelId = ModelId, PartnerSubscriptionPeriodId = 7 });
            db.SaveChanges();
            db.ChangeTracker.Clear();

            await Controller(db, true).Edit(ModelId, EditVm(true, PartnerAudience.All));

            Assert.True(Stored(db).IsGlobal);
            Assert.Empty(Links(db));
            Assert.Single(db.ProfessionalModelSubscriptionPeriods);
        }

        [Fact]
        public async Task Edit_TurnedOff_KeepsLinksForReactivation()
        {
            using var db = NewDb();
            SeedModel(db, forPartners: true, global: false, PartnerA);

            await Controller(db, true).Edit(ModelId, EditVm(false, PartnerAudience.All));

            Assert.False(Stored(db).IsForPartners);
            Assert.Equal(new[] { PartnerA }, Links(db));
        }

        [Fact]
        public async Task Edit_PeriodOnlyModel_SelectedWithNoPartners_StaysValid()
        {
            using var db = NewDb();
            SeedModel(db, forPartners: true, global: false);
            db.ProfessionalModelSubscriptionPeriods.Add(new ProfessionalModelSubscriptionPeriod { ProfessionalModelId = ModelId, PartnerSubscriptionPeriodId = 7 });
            db.SaveChanges();
            db.ChangeTracker.Clear();

            Assert.IsType<RedirectToActionResult>(await Controller(db, true).Edit(ModelId, EditVm(true, PartnerAudience.Selected)));
            Assert.Single(db.ProfessionalModelSubscriptionPeriods);
        }

        [Fact]
        public async Task Edit_WithoutVisibilityPermission_ChangesDataButNotAudience()
        {
            using var db = NewDb();
            SeedModel(db, forPartners: false, global: false, PartnerA);

            var result = await Controller(db, false).Edit(ModelId, EditVm(true, PartnerAudience.All, PartnerB));

            Assert.IsType<RedirectToActionResult>(result);
            var model = Stored(db);
            Assert.Equal("M1-edited", model.Title);
            Assert.False(model.IsForPartners);
            Assert.False(model.IsGlobal);
            Assert.Equal(new[] { PartnerA }, Links(db));
        }

        [Fact]
        public async Task Edit_MissingModel_ReturnsNotFound()
        {
            using var db = NewDb();
            Assert.IsType<NotFoundResult>(await Controller(db, true).Edit(ModelId, EditVm(false, PartnerAudience.All)));
        }

        // =========================================
        // Visibility
        // =========================================
        [Fact]
        public async Task Visibility_SelectedWithPeriodOnly_IsValid_AndDropsUnknownPeriods()
        {
            using var db = NewDb();
            SeedModel(db, forPartners: false, global: false);
            db.PartnerSubscriptionPeriods.Add(new PartnerSubscriptionPeriod { Id = 7, PartnerSubscriptionId = 1, StartDate = DateTime.Today, EndDate = DateTime.Today });
            db.SaveChanges();
            db.ChangeTracker.Clear();

            var result = await Controller(db, true).Visibility(new ProfessionalModelVisibilityVm
            {
                ProfessionalModelId = ModelId,
                IsForPartners = true,
                PartnerAudience = PartnerAudience.Selected,
                SelectedSubscriptionPeriodIds = new List<int> { 7, 999 }
            });

            Assert.IsType<RedirectToActionResult>(result);
            Assert.True(Stored(db).IsForPartners);
            Assert.Equal(7, (await db.ProfessionalModelSubscriptionPeriods.SingleAsync()).PartnerSubscriptionPeriodId);
        }

        [Fact]
        public async Task Visibility_SelectedWithNothing_ReturnsViewWithError()
        {
            using var db = NewDb();
            SeedModel(db, forPartners: true, global: true);
            var controller = Controller(db, true);

            var result = await controller.Visibility(new ProfessionalModelVisibilityVm
            {
                ProfessionalModelId = ModelId,
                IsForPartners = true,
                PartnerAudience = PartnerAudience.Selected
            });

            Assert.IsType<ViewResult>(result);
            Assert.False(controller.ModelState.IsValid);
            Assert.True(Stored(db).IsGlobal);
        }

        [Fact]
        public async Task Visibility_TurnedOff_KeepsAllLinks()
        {
            using var db = NewDb();
            SeedModel(db, forPartners: true, global: false, PartnerA);
            db.ProfessionalModelSubscriptionPeriods.Add(new ProfessionalModelSubscriptionPeriod { ProfessionalModelId = ModelId, PartnerSubscriptionPeriodId = 7 });
            db.SaveChanges();
            db.ChangeTracker.Clear();

            await Controller(db, true).Visibility(new ProfessionalModelVisibilityVm { ProfessionalModelId = ModelId, IsForPartners = false });

            Assert.False(Stored(db).IsForPartners);
            Assert.Equal(new[] { PartnerA }, Links(db));
            Assert.Single(db.ProfessionalModelSubscriptionPeriods);
        }

        // =========================================
        // Duplicate
        // =========================================
        [Fact]
        public async Task Duplicate_KeepsTypeAndCurriculum_ButStaysInternal()
        {
            using var db = NewDb();
            db.ProfessionalModels.Add(new ProfessionalModel
            {
                Id = ModelId,
                Title = "M1",
                Description = "-",
                CreatedBy = "-",
                ModelType = ProfessionalModelType.Homework,
                CurriculumId = 7,
                IsForPartners = true,
                IsGlobal = true
            });
            db.SaveChanges();
            db.ChangeTracker.Clear();

            Assert.IsType<RedirectToActionResult>(await Controller(db, true).Duplicate(ModelId));

            var copy = await db.ProfessionalModels.AsNoTracking().SingleAsync(m => m.Id != ModelId);
            Assert.Equal(ProfessionalModelType.Homework, copy.ModelType);
            Assert.Equal(7, copy.CurriculumId);
            Assert.False(copy.IsForPartners);
            Assert.False(copy.IsGlobal);
        }

        // =========================================
        // Attributes
        // =========================================
        private static MethodInfo PostAction(string name) => typeof(ProfessionalModelsController)
            .GetMethods()
            .Single(m => m.Name == name && m.GetCustomAttribute<HttpPostAttribute>() != null);

        [Theory]
        [InlineData(nameof(ProfessionalModelsController.Create), "ProfessionalModels:CreateModel")]
        [InlineData(nameof(ProfessionalModelsController.Edit), "ProfessionalModels:EditModel")]
        [InlineData(nameof(ProfessionalModelsController.Visibility), "ProfessionalModels:Visibility")]
        [InlineData(nameof(ProfessionalModelsController.Duplicate), "ProfessionalModels:Duplicate")]
        [InlineData(nameof(ProfessionalModelsController.AddQuestions), "ProfessionalModels:EditModel")]
        [InlineData(nameof(ProfessionalModelsController.RemoveQuestion), "ProfessionalModels:EditModel")]
        public void PostActions_HaveAntiForgeryAndPermission(string action, string policy)
        {
            var method = PostAction(action);

            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            Assert.Equal(policy, Assert.Single(method.GetCustomAttributes<AdminPermissionAttribute>()).Policy);
        }
    }
}
