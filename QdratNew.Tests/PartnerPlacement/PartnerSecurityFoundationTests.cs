using System.Net;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using QdratNew.Areas.Partner.Controllers;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Filters;
using QdratNew.Implementations;
using QdratNew.Interfaces;
using QdratNew.Services.ProfessionalModels;
using QdratNew.Tests.Integration;
using QdratNew.ViewModels.Partner;
using Xunit;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S1.7 — الأساس الأمني لمنطقة الشريك بلا قاعدة بيانات حقيقية:
    // حارس بنية الكنترولرات + PartnerFeatureFilter + تحقق الـ Session + إتاحة النموذج الاحترافي.
    public class PartnerSecurityFoundationTests
    {
        private const string UserId = "partner-user-1";
        private const int PartnerId = 5;

        private sealed class FakeSession : ISession
        {
            private readonly Dictionary<string, byte[]> _store = new();
            public bool IsAvailable => true;
            public string Id => "test-session";
            public IEnumerable<string> Keys => _store.Keys;
            public void Clear() => _store.Clear();
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Remove(string key) => _store.Remove(key);
            public void Set(string key, byte[] value) => _store[key] = value;
            public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
        }

        private sealed class FakeSessionFeature : ISessionFeature
        {
            public ISession Session { get; set; } = new FakeSession();
        }

        private static DefaultHttpContext CreateHttpContext(int? activePartnerId)
        {
            var http = new DefaultHttpContext();
            http.Features.Set<ISessionFeature>(new FakeSessionFeature());
            if (activePartnerId.HasValue)
                http.Session.SetInt32("ActivePartnerId", activePartnerId.Value);

            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, UserId) },
                "Test"));
            return http;
        }

        private static ActionExecutingContext CreateFilterContext(
            HttpContext http,
            object controller,
            string controllerName = "PartnerExam",
            string actionName = "Generate",
            params object[] metadata)
        {
            var descriptor = new ActionDescriptor
            {
                EndpointMetadata = metadata.ToList(),
                RouteValues = new Dictionary<string, string?>
                {
                    ["controller"] = controllerName,
                    ["action"] = actionName
                }
            };
            var actionContext = new ActionContext(http, new RouteData(), descriptor, new ModelStateDictionary());
            return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller);
        }

        // =====================================================
        // حارس البنية
        // =====================================================
        private static IEnumerable<Type> PartnerControllers =>
            typeof(PartnerBaseController).Assembly.GetTypes()
                .Where(t => t.Namespace == typeof(PartnerBaseController).Namespace
                            && typeof(Controller).IsAssignableFrom(t)
                            && !t.IsAbstract);

        [Fact]
        public void EveryPartnerController_RequiresAuthorization()
        {
            var missing = PartnerControllers
                .Where(t => !t.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any())
                .Select(t => t.Name)
                .ToList();

            Assert.NotEmpty(PartnerControllers);
            Assert.True(missing.Count == 0, "Controllers بلا Authorize: " + string.Join(", ", missing));
        }

        [Fact]
        public void PartnerBaseController_UsesPartnerOnlyPolicy_ForAllInheritors()
        {
            var attribute = typeof(PartnerExamController).GetCustomAttributes<AuthorizeAttribute>(inherit: true).Single();
            Assert.Equal("PartnerOnly", attribute.Policy);
        }

        [Fact]
        public void PartnerContextSet_IsPost_WithAntiForgery()
        {
            var set = typeof(PartnerContextController).GetMethod(nameof(PartnerContextController.Set))!;

            Assert.NotNull(set.GetCustomAttribute<HttpPostAttribute>());
            Assert.NotNull(set.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }

        [Theory]
        [InlineData(typeof(PartnerExamController), PartnerFeatureType.Exams)]
        [InlineData(typeof(HomeworkDraftsController), PartnerFeatureType.Homework)]
        [InlineData(typeof(HomeworkGenerationController), PartnerFeatureType.Homework)]
        [InlineData(typeof(SentHomeworksController), PartnerFeatureType.Homework)]
        [InlineData(typeof(ProfessionalModelsController), PartnerFeatureType.ProfessionalModels)]
        public void ContractBoundControllers_DeclareTheirFeature(Type controller, PartnerFeatureType feature)
        {
            Assert.Equal(feature, controller.GetCustomAttribute<PartnerFeatureAttribute>()!.Feature);
        }

        [Theory]
        [InlineData(nameof(ReportsController.BatchHomeworks), PartnerFeatureType.Homework)]
        [InlineData(nameof(ReportsController.HomeworkStudents), PartnerFeatureType.Homework)]
        [InlineData(nameof(ReportsController.StudentHomeworkDetails), PartnerFeatureType.Homework)]
        [InlineData(nameof(ReportsController.BatchExams), PartnerFeatureType.Exams)]
        [InlineData(nameof(ReportsController.ExamStudents), PartnerFeatureType.Exams)]
        [InlineData(nameof(ReportsController.StudentExamDetails), PartnerFeatureType.Exams)]
        public void ReportActions_DeclareFeature_AndStayReadableAfterExpiry(string action, PartnerFeatureType feature)
        {
            var attribute = typeof(ReportsController).GetMethod(action)!.GetCustomAttribute<PartnerFeatureAttribute>()!;

            Assert.Equal(feature, attribute.Feature);
            Assert.True(attribute.AllowReadAfterExpiry);
        }

        // =====================================================
        // PartnerFeatureFilter
        // =====================================================
        [Fact]
        public void Filter_WithoutAttribute_DoesNothing()
        {
            var service = new Mock<IPartnerSubscriptionService>(MockBehavior.Strict);
            var context = CreateFilterContext(CreateHttpContext(null), new object());

            new PartnerFeatureFilter(service.Object).OnActionExecuting(context);

            Assert.Null(context.Result);
        }

        [Fact]
        public void Filter_FeatureNotInContract_ReturnsFeatureNotAllowedWith403()
        {
            var service = new Mock<IPartnerSubscriptionService>();
            service.Setup(s => s.CanUseExams(PartnerId)).Returns(false);
            var context = CreateFilterContext(CreateHttpContext(PartnerId), new object(), metadata: new PartnerFeatureAttribute(PartnerFeatureType.Exams));

            new PartnerFeatureFilter(service.Object).OnActionExecuting(context);

            var view = Assert.IsType<ViewResult>(context.Result);
            Assert.Equal("~/Views/Shared/FeatureNotAllowed.cshtml", view.ViewName);
            Assert.Equal(StatusCodes.Status403Forbidden, view.StatusCode);
            Assert.Equal(PartnerFeatureType.Exams, view.ViewData["FeatureName"]);
        }

        [Fact]
        public void Filter_FeatureInContract_AllowsRequest()
        {
            var service = new Mock<IPartnerSubscriptionService>();
            service.Setup(s => s.CanUseProfessionalModels(PartnerId)).Returns(true);
            var context = CreateFilterContext(CreateHttpContext(PartnerId), new object(), metadata: new PartnerFeatureAttribute(PartnerFeatureType.ProfessionalModels));

            new PartnerFeatureFilter(service.Object).OnActionExecuting(context);

            Assert.Null(context.Result);
        }

        [Fact]
        public void Filter_NoActivePartner_RedirectsToSelectPartner()
        {
            var service = new Mock<IPartnerSubscriptionService>(MockBehavior.Strict);
            var context = CreateFilterContext(CreateHttpContext(null), new object(), metadata: new PartnerFeatureAttribute(PartnerFeatureType.Exams));

            new PartnerFeatureFilter(service.Object).OnActionExecuting(context);

            var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
            Assert.Equal("SelectPartner", redirect.ActionName);
        }

        [Fact]
        public void Filter_ActionLevelAttribute_OverridesControllerLevel()
        {
            var service = new Mock<IPartnerSubscriptionService>();
            service.Setup(s => s.CanUseExams(PartnerId)).Returns(false);
            service.Setup(s => s.CanUseHomework(PartnerId)).Returns(true);
            var context = CreateFilterContext(CreateHttpContext(PartnerId), new object(), metadata: new object[]
            {
                new PartnerFeatureAttribute(PartnerFeatureType.Exams),    // Controller
                new PartnerFeatureAttribute(PartnerFeatureType.Homework)  // Action
            });

            new PartnerFeatureFilter(service.Object).OnActionExecuting(context);

            Assert.Null(context.Result);
        }

        [Fact]
        public void Filter_ExpiredContractWithinAccessWindow_AllowsReportsOnly()
        {
            var service = new Mock<IPartnerSubscriptionService>();
            service.Setup(s => s.CanReadFeatureAfterExpiry(PartnerId, PartnerFeatureType.Exams)).Returns(true);

            var report = CreateFilterContext(CreateHttpContext(PartnerId), new object(),
                metadata: new PartnerFeatureAttribute(PartnerFeatureType.Exams) { AllowReadAfterExpiry = true });
            var create = CreateFilterContext(CreateHttpContext(PartnerId), new object(),
                metadata: new PartnerFeatureAttribute(PartnerFeatureType.Exams));

            var filter = new PartnerFeatureFilter(service.Object);
            filter.OnActionExecuting(report);
            filter.OnActionExecuting(create);

            Assert.Null(report.Result);
            Assert.IsType<ViewResult>(create.Result);
        }

        [Fact]
        public void Filter_ExpiredContractPastAccessWindow_BlocksReports()
        {
            var service = new Mock<IPartnerSubscriptionService>();
            service.Setup(s => s.CanReadFeatureAfterExpiry(PartnerId, PartnerFeatureType.Exams)).Returns(false);
            var context = CreateFilterContext(CreateHttpContext(PartnerId), new object(),
                metadata: new PartnerFeatureAttribute(PartnerFeatureType.Exams) { AllowReadAfterExpiry = true });

            new PartnerFeatureFilter(service.Object).OnActionExecuting(context);

            Assert.IsType<ViewResult>(context.Result);
        }

        [Fact]
        public void Filter_ActiveContractWithoutFeature_IsNotBypassedByReadAfterExpiry()
        {
            var service = new Mock<IPartnerSubscriptionService>();
            service.Setup(s => s.GetActiveSubscription(PartnerId)).Returns(new PartnerSubscription { PartnerId = PartnerId });
            service.Setup(s => s.CanReadFeatureAfterExpiry(PartnerId, PartnerFeatureType.Exams)).Returns(true);
            var context = CreateFilterContext(CreateHttpContext(PartnerId), new object(),
                metadata: new PartnerFeatureAttribute(PartnerFeatureType.Exams) { AllowReadAfterExpiry = true });

            new PartnerFeatureFilter(service.Object).OnActionExecuting(context);

            Assert.IsType<ViewResult>(context.Result);
        }

        // PPL-S6
        [Theory]
        [InlineData(true, 5, true)]    // العقد المنتهي شمل الميزة وداخل فترة الوصول
        [InlineData(false, 5, false)]  // لم يشملها: لا قراءة رغم فترة الوصول
        [InlineData(true, -1, false)]  // شملها لكن فترة الوصول انتهت
        public void SubscriptionService_ReadAfterExpiry_RequiresFeatureOnLastContract(bool included, int accessDays, bool expected)
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            db.PartnerSubscriptions.Add(new PartnerSubscription
            {
                PartnerId = PartnerId,
                StartDate = DateTime.Today.AddDays(-60),
                EndDate = DateTime.Today.AddDays(-5),
                AccessUntilDate = DateTime.Today.AddDays(accessDays),
                CanUsePlacementExams = included
            });
            db.SaveChanges();

            var service = new QdratNew.Implementations.PartnerSubscriptionService(db);

            Assert.Equal(expected, service.CanReadFeatureAfterExpiry(PartnerId, PartnerFeatureType.PlacementExams));
            Assert.Equal(expected, service.CanReadPlacementReports(PartnerId));
        }

        [Fact]
        public void SubscriptionService_ReadAfterExpiry_IgnoresContractThatHasNotStarted()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            db.PartnerSubscriptions.AddRange(
                new PartnerSubscription
                {
                    PartnerId = PartnerId,
                    StartDate = DateTime.Today.AddDays(-60),
                    EndDate = DateTime.Today.AddDays(-5),
                    AccessUntilDate = DateTime.Today.AddDays(5),
                    CanUsePlacementExams = true
                },
                new PartnerSubscription
                {
                    PartnerId = PartnerId,
                    StartDate = DateTime.Today.AddDays(10),
                    EndDate = DateTime.Today.AddDays(100)
                });
            db.SaveChanges();

            Assert.True(new QdratNew.Implementations.PartnerSubscriptionService(db)
                .CanReadFeatureAfterExpiry(PartnerId, PartnerFeatureType.PlacementExams));
        }

        [Fact]
        public void SubscriptionService_CanUseProfessionalModels_FollowsActiveContract()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            db.PartnerSubscriptions.AddRange(
                new PartnerSubscription { PartnerId = 1, StartDate = DateTime.Today.AddDays(-1), EndDate = DateTime.Today.AddDays(1), CanUseProfessionalModels = true },
                new PartnerSubscription { PartnerId = 2, StartDate = DateTime.Today.AddDays(-1), EndDate = DateTime.Today.AddDays(1), CanUseProfessionalModels = false },
                new PartnerSubscription { PartnerId = 3, StartDate = DateTime.Today.AddDays(-9), EndDate = DateTime.Today.AddDays(-1), CanUseProfessionalModels = true });
            db.SaveChanges();

            var service = new PartnerSubscriptionService(db);

            Assert.True(service.CanUseProfessionalModels(1));
            Assert.False(service.CanUseProfessionalModels(2));
            Assert.False(service.CanUseProfessionalModels(3));
        }

        // =====================================================
        // PartnerBaseController: تحقق الـ Session + NullReference
        // =====================================================
        private static (ProfessionalModelsController Controller, Mock<IProfessionalModelSendService> Send) CreateModelsController(
            ApplicationDbContext db,
            HttpContext http,
            Mock<IPartnerSubscriptionService>? subscription = null)
        {
            // PPM-S1: قاعدة الإتاحة تشترط عقدًا نشطًا يسمح بالنماذج
            db.PartnerSubscriptions.Add(new PartnerSubscription
            {
                PartnerId = PartnerId,
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today.AddDays(30),
                CanUseProfessionalModels = true
            });
            db.SaveChanges();

            var send = new Mock<IProfessionalModelSendService>();
            var controller = new ProfessionalModelsController(db, send.Object, new ProfessionalModelAvailabilityService(db), new QdratNew.Services.Implementations.TimeZoneService(), (subscription ?? new Mock<IPartnerSubscriptionService>()).Object)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
            };
            return (controller, send);
        }

        [Fact]
        public async Task BaseController_SessionPartnerNotLinkedToUser_ClearsSessionAndRedirectsToSelect()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            db.UserPartners.Add(new UserPartner { UserId = "another-user", PartnerId = PartnerId });
            db.SaveChanges();

            var http = CreateHttpContext(PartnerId);
            var (controller, _) = CreateModelsController(db, http);
            var context = CreateFilterContext(http, controller, "ProfessionalModels", "Index");
            var nextCalled = false;

            await controller.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), controller));
            });

            var redirect = Assert.IsType<RedirectToActionResult>(context.Result);
            Assert.Equal("Select", redirect.ActionName);
            Assert.Equal("PartnerContext", redirect.ControllerName);
            Assert.Null(http.Session.GetInt32("ActivePartnerId"));
            Assert.False(nextCalled);
        }

        [Fact]
        public async Task BaseController_SessionPartnerLinkedToUser_ContinuesToAction()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            db.UserPartners.Add(new UserPartner { UserId = UserId, PartnerId = PartnerId });
            db.Branches.Add(new Branch { PartnerId = PartnerId, Name = "-", Location = "-", City = "-", State = "-", Country = "-" });
            db.SaveChanges();

            var subscription = new Mock<IPartnerSubscriptionService>();
            subscription.Setup(s => s.GetActiveContext(PartnerId))
                .Returns(new PartnerSubscriptionContext { PartnerId = PartnerId, CanUseProfessionalModels = true });

            var http = CreateHttpContext(PartnerId);
            var (controller, _) = CreateModelsController(db, http, subscription);
            var context = CreateFilterContext(http, controller, "ProfessionalModels", "Index");
            var nextCalled = false;

            await controller.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(context, new List<IFilterMetadata>(), controller));
            });

            Assert.True(nextCalled);
            Assert.Null(context.Result);
            Assert.Equal(PartnerId, http.Session.GetInt32("ActivePartnerId"));
        }

        [Fact]
        public void BaseController_NoSubscriptionOnStudentsPage_DoesNotThrow()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            db.Branches.Add(new Branch { PartnerId = PartnerId, Name = "-", Location = "-", City = "-", State = "-", Country = "-" });
            db.SaveChanges();

            var http = CreateHttpContext(PartnerId);
            var (controller, _) = CreateModelsController(db, http);
            var context = CreateFilterContext(http, controller, "Students", "Index");

            controller.OnActionExecuting(context);

            Assert.Null(context.Result);
            Assert.False((bool)controller.ViewBag.CanUseProfessionalModels);
        }

        // =====================================================
        // ProfessionalModels: إتاحة النموذج (IDOR)
        // =====================================================
        private static ProfessionalModel NewModel(int id, bool isGlobal = false) => new()
        {
            Id = id,
            Title = "M" + id,
            Description = "-",
            CreatedBy = "-",
            CreatedAt = DateTime.UtcNow,
            IsForPartners = true,
            IsGlobal = isGlobal,
            ModelType = ProfessionalModelType.Homework
        };

        // PPM-S4.2: المسار القديم لا يرسل شيئًا، والمسار الجديد يفحص الإتاحة
        [Fact]
        public void LegacySendActions_RedirectToSendHomework_WithoutSending()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            var (controller, send) = CreateModelsController(db, CreateHttpContext(PartnerId));

            foreach (var result in new[] { controller.Send(7), controller.SendCustomized(7) })
            {
                var redirect = Assert.IsType<RedirectToActionResult>(result);
                Assert.Equal(nameof(ProfessionalModelsController.SendHomework), redirect.ActionName);
                Assert.Equal(7, redirect.RouteValues!["id"]);
            }

            Assert.Empty(send.Invocations);
        }

        [Fact]
        public async Task PreviewModel_FollowsPartnerAvailability()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            db.ProfessionalModels.AddRange(NewModel(1), NewModel(2), NewModel(3, isGlobal: true));
            db.ProfessionalModelPartners.AddRange(
                new ProfessionalModelPartner { ProfessionalModelId = 1, PartnerId = PartnerId + 1 },
                new ProfessionalModelPartner { ProfessionalModelId = 2, PartnerId = PartnerId });
            db.SaveChanges();

            var (controller, _) = CreateModelsController(db, CreateHttpContext(PartnerId));

            Assert.IsType<NotFoundResult>(await controller.Preview(1));
            Assert.IsType<ViewResult>(await controller.Preview(2));
            Assert.IsType<ViewResult>(await controller.Preview(3));
        }

        // =====================================================
        // PPM-S1: HomeworkGeneration/FromProfessionalModel يتبع قاعدة الإتاحة
        // =====================================================
        private static (HomeworkGenerationController Controller, Mock<QdratNew.Services.HomeworkDraft.Interfaces.IHomeworkDraftService> Drafts) CreateGenerationController(ApplicationDbContext db)
        {
            db.PartnerSubscriptions.Add(new PartnerSubscription
            {
                PartnerId = PartnerId,
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today.AddDays(30),
                CanUseProfessionalModels = true
            });
            db.SaveChanges();

            var http = CreateHttpContext(PartnerId);
            var drafts = new Mock<QdratNew.Services.HomeworkDraft.Interfaces.IHomeworkDraftService>();
            var controller = new HomeworkGenerationController(
                drafts.Object,
                new QdratNew.Services.ProfessionalModels.ProfessionalModelAvailabilityService(db),
                new Mock<IPartnerSubscriptionService>().Object,
                db)
            {
                ControllerContext = new ControllerContext { HttpContext = http },
                TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>())
            };
            return (controller, drafts);
        }

        [Fact]
        public async Task FromProfessionalModel_NotAvailable_RedirectsWithoutDraft()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            var internalModel = NewModel(1, isGlobal: true);
            internalModel.IsForPartners = false;
            var examModel = NewModel(3, isGlobal: true);
            examModel.ModelType = ProfessionalModelType.Exam;
            db.ProfessionalModels.AddRange(internalModel, NewModel(2), examModel);
            db.ProfessionalModelPartners.Add(new ProfessionalModelPartner { ProfessionalModelId = 2, PartnerId = PartnerId + 1 });
            db.SaveChanges();

            var (controller, drafts) = CreateGenerationController(db);

            foreach (var id in new[] { 1, 2, 3, 999 })
            {
                var get = Assert.IsType<RedirectToActionResult>(await controller.FromProfessionalModel(id));
                Assert.Equal("ProfessionalModels", get.ControllerName);

                var post = Assert.IsType<RedirectToActionResult>(await controller.FromProfessionalModel(
                    new QdratNew.ViewModels.Partner.Homework.HomeworkGenerateFromProfessionalModelVM { ProfessionalModelId = id }));
                Assert.Equal("ProfessionalModels", post.ControllerName);
            }

            Assert.Empty(drafts.Invocations);
        }

        [Fact]
        public async Task FromProfessionalModel_Available_RedirectsToDirectSend_WithoutDraft()
        {
            using var db = TestDbContextFactory.CreateFreshContext(out _);
            db.ProfessionalModels.Add(NewModel(1, isGlobal: true));
            db.SaveChanges();

            var (controller, drafts) = CreateGenerationController(db);

            var get = Assert.IsType<RedirectToActionResult>(await controller.FromProfessionalModel(1));
            Assert.Equal("SendHomework", get.ActionName);
            Assert.Equal("ProfessionalModels", get.ControllerName);

            var post = Assert.IsType<RedirectToActionResult>(await controller.FromProfessionalModel(
                new QdratNew.ViewModels.Partner.Homework.HomeworkGenerateFromProfessionalModelVM { ProfessionalModelId = 1 }));
            Assert.Equal("SendHomework", post.ActionName);
            Assert.Equal(1, post.RouteValues!["id"]);

            Assert.Empty(drafts.Invocations);
        }
    }

    // PPL-S1.7 — المسار الكامل عبر Program.cs الحقيقي على قاعدة الاختبار المعزولة.
    [Collection("SharedIntegrationDb")]
    public class PartnerSecurityFoundationIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string PartnerCode = "PPL-S1-TEST";
        private const string LinkedUserName = "ppl-s1-partner-user";

        private readonly CustomWebApplicationFactory _factory;
        private int _partnerId;
        private int _subscriptionId;
        private string _userId = string.Empty;

        public PartnerSecurityFoundationIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var user = new ApplicationUser { UserName = LinkedUserName, Email = LinkedUserName + "@test.local", NationalID = "1000000077" };
            var created = await users.CreateAsync(user, "P@ssw0rd123!");
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join(", ", created.Errors.Select(e => e.Description)));
            _userId = user.Id;

            var partner = new Partner
            {
                Name = "شريك اختبار PPL",
                Code = PartnerCode,
                PartnershipStart = DateTime.Today.AddDays(-10),
                PartnershipEnd = DateTime.Today.AddDays(10)
            };
            db.Partners.Add(partner);
            await db.SaveChangesAsync();
            _partnerId = partner.Id;

            db.Branches.Add(new Branch { PartnerId = _partnerId, Name = "فرع اختبار PPL", Location = "-", City = "-", State = "-", Country = "-" });
            db.UserPartners.Add(new UserPartner { UserId = _userId, PartnerId = _partnerId });

            var subscription = new PartnerSubscription
            {
                PartnerId = _partnerId,
                StartDate = DateTime.Today.AddDays(-10),
                EndDate = DateTime.Today.AddDays(10),
                CanCreateExams = false,
                CanCreateHomework = true
            };
            db.PartnerSubscriptions.Add(subscription);
            await db.SaveChangesAsync();
            _subscriptionId = subscription.Id;

            db.PartnerSubscriptionPeriods.Add(new PartnerSubscriptionPeriod
            {
                PartnerSubscriptionId = _subscriptionId,
                StartDate = DateTime.Today.AddDays(-10),
                EndDate = DateTime.Today.AddDays(10)
            });
            await db.SaveChangesAsync();
        }

        public Task DisposeAsync() => CleanupAsync();

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var partnerIds = "(SELECT Id FROM Partners WHERE Code = '" + PartnerCode + "')";
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN (SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN {partnerIds})");
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM PartnerSubscriptions WHERE PartnerId IN {partnerIds}");
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM UserPartners WHERE PartnerId IN {partnerIds}");
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM Branches WHERE PartnerId IN {partnerIds}");
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM Partners WHERE Code = '{PartnerCode}'");
            await db.Database.ExecuteSqlRawAsync($"DELETE FROM AspNetUsers WHERE UserName = '{LinkedUserName}'");
        }

        private HttpClient CreateClient(string? userId = null, string role = "Partner")
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            if (userId != null)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
                client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
            }
            return client;
        }

        private async Task<HttpResponseMessage> PostSetAsync(HttpClient client, string userId, int partnerId)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var request = new HttpRequestMessage(HttpMethod.Post, "/Partner/PartnerContext/Set")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["partnerId"] = partnerId.ToString(),
                    ["__RequestVerificationToken"] = token
                })
            };
            request.Headers.Add("Cookie", cookie);
            return await client.SendAsync(request);
        }

        private async Task<string> SelectLinkedPartnerAndGetSessionCookieAsync(HttpClient client)
        {
            var response = await PostSetAsync(client, _userId, _partnerId);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            // Session cookie هي الوحيدة التي يصدرها هذا الرد
            return response.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        }

        private static async Task<HttpResponseMessage> GetWithCookieAsync(HttpClient client, string url, string cookie)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Cookie", cookie);
            return await client.SendAsync(request);
        }

        private async Task SetExamsInContractAsync(bool enabled)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var subscription = await db.PartnerSubscriptions.FirstAsync(s => s.Id == _subscriptionId);
            subscription.CanCreateExams = enabled;
            await db.SaveChangesAsync();
        }

        [Fact]
        public async Task PartnerExam_Anonymous_IsRejected()
        {
            var response = await CreateClient().GetAsync("/Partner/PartnerExam/Generate");

            Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found,
                response.StatusCode.ToString());
        }

        [Theory]
        [InlineData("/Partner/PartnerExam/Generate")]
        [InlineData("/Partner/Students")]
        [InlineData("/Partner/ProfessionalModels")]
        [InlineData("/Partner/PartnerContext/Select")]
        public async Task PartnerPages_StudentRole_AreForbidden(string url)
        {
            var response = await CreateClient("ppl-s1-student", "Student").GetAsync(url);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Set_WithoutAntiForgeryToken_ReturnsBadRequest()
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["partnerId"] = _partnerId.ToString() });

            var response = await CreateClient(_userId).PostAsync("/Partner/PartnerContext/Set", form);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Set_PartnerNotLinkedToUser_ReturnsForbidden()
        {
            const string outsider = "ppl-s1-outsider";

            var response = await PostSetAsync(CreateClient(outsider), outsider, _partnerId);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Select_PartnerUserWithoutLinks_RendersEmptyState()
        {
            var response = await CreateClient("ppl-s1-no-links").GetAsync("/Partner/PartnerContext/Select");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("لا توجد مدارس", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task ContractWithoutExams_ControllerLevelFeature_RendersFeatureNotAllowed()
        {
            var client = CreateClient(_userId);
            var cookie = await SelectLinkedPartnerAndGetSessionCookieAsync(client);

            var response = await GetWithCookieAsync(client, "/Partner/PartnerExam/Generate", cookie);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains("غير مشمولة في عقدكم الحالي", html);
            Assert.Contains("الخدمة المطلوبة: الاختبارات", System.Net.WebUtility.HtmlDecode(html));
        }

        [Fact]
        public async Task ContractWithExams_SameRequest_PassesTheFilter()
        {
            await SetExamsInContractAsync(true);
            var client = CreateClient(_userId);
            var cookie = await SelectLinkedPartnerAndGetSessionCookieAsync(client);

            var response = await GetWithCookieAsync(client, "/Partner/PartnerExam/Generate", cookie);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("غير مشمولة في عقدكم الحالي", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task SessionOfAnotherUser_IsClearedAndRedirectedToSelect()
        {
            var client = CreateClient(_userId);
            var cookie = await SelectLinkedPartnerAndGetSessionCookieAsync(client);

            var outsiderClient = CreateClient("ppl-s1-outsider");
            var response = await GetWithCookieAsync(outsiderClient, "/Partner/Students", cookie);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("/Partner/PartnerContext/Select", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task AccessDenied_RendersWithoutRedirectLoop()
        {
            var client = CreateClient(_userId);
            var cookie = await SelectLinkedPartnerAndGetSessionCookieAsync(client);

            var response = await GetWithCookieAsync(client, "/Partner/Home/AccessDenied", cookie);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("غير مصرح بالوصول", await response.Content.ReadAsStringAsync());
        }
    }
}
