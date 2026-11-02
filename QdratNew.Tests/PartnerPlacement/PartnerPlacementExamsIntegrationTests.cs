using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Areas.Partner.Controllers;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S4 — صفحات Partner/PlacementExams عبر HTTP حقيقي: تعرض بنطاق الشريك، وتعزل اختبارات الشركاء والمعهد.
    [Collection("SharedIntegrationDb")]
    public class PartnerPlacementExamsIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPLS4";
        private const string UserId = "ppl-s4-partner-user";
        private const string UserNoFeature = "ppl-s4-nofeature-user";
        private const string Base = "/Partner/PlacementExams";

        private readonly CustomWebApplicationFactory _factory;
        private int _partnerId;
        private int _partnerNoFeatureId;
        private int _courseId;
        private int _batchId;
        private int _foreignBatchId;
        private int _ownExamId;
        private int _foreignExamId;
        private int _instituteExamId;

        public PartnerPlacementExamsIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var today = DateTime.Today;

            var partner = new Partner { Name = $"{Marker} شريك", Code = Marker, PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var other = new Partner { Name = $"{Marker} شريك آخر", Code = $"{Marker}B", PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var noFeature = new Partner { Name = $"{Marker} بلا ميزة", Code = $"{Marker}C", PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var branch = new Branch { Name = $"{Marker} فرع", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };
            var otherBranch = new Branch { Name = $"{Marker} فرع آخر", Partner = other, Location = "-", City = "-", State = "-", Country = "-" };
            var noFeatureBranch = new Branch { Name = $"{Marker} فرع بلا ميزة", Partner = noFeature, Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            db.AddRange(partner, other, noFeature, branch, otherBranch, noFeatureBranch, course);

            db.Users.AddRange(NewUser(UserId), NewUser(UserNoFeature));
            await db.SaveChangesAsync();

            db.UserPartners.AddRange(
                new UserPartner { UserId = UserId, PartnerId = partner.Id },
                new UserPartner { UserId = UserNoFeature, PartnerId = noFeature.Id });

            var subscription = NewSubscription(partner.Id, canUsePlacement: true);
            subscription.SubscriptionCourses = new List<PartnerSubscriptionCourse>
            {
                new() { CourseId = course.Id, CanUsePlatformQuestionBank = true }
            };
            var noFeatureSubscription = NewSubscription(noFeature.Id, canUsePlacement: false);
            db.PartnerSubscriptions.AddRange(subscription, noFeatureSubscription);
            await db.SaveChangesAsync();

            db.PartnerSubscriptionPeriods.AddRange(
                NewPeriod(subscription.Id),
                NewPeriod(noFeatureSubscription.Id));

            var batch = new Batch { Name = $"{Marker} دفعة", BranchId = branch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            var foreignBatch = new Batch { Name = $"{Marker} دفعة أخرى", BranchId = otherBranch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            var ownExam = new Exam { Title = $"{Marker} اختباري", Type = ExamType.LevelAssessment, DurationMinutes = 30, IsActive = true, CourseId = course.Id, OwnerPartnerId = partner.Id };
            var foreignExam = new Exam { Title = $"{Marker} اختبار شريك آخر", Type = ExamType.LevelAssessment, DurationMinutes = 30, IsActive = true, CourseId = course.Id, OwnerPartnerId = other.Id };
            var instituteExam = new Exam { Title = $"{Marker} اختبار المعهد", Type = ExamType.LevelAssessment, DurationMinutes = 30, IsActive = true, CourseId = course.Id };
            db.Batches.AddRange(batch, foreignBatch);
            db.Exams.AddRange(ownExam, foreignExam, instituteExam);
            await db.SaveChangesAsync();

            db.ExamAssignmentsToBatches.AddRange(
                new ExamAssignmentToBatch { ExamId = ownExam.Id, BatchId = batch.Id, Title = "اختبار تحديد المستوى", CreatedAt = DateTime.UtcNow },
                new ExamAssignmentToBatch { ExamId = instituteExam.Id, BatchId = batch.Id, Title = "اختبار تحديد المستوى", CreatedAt = DateTime.UtcNow },
                new ExamAssignmentToBatch { ExamId = foreignExam.Id, BatchId = foreignBatch.Id, Title = "اختبار تحديد المستوى", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();

            _partnerId = partner.Id;
            _partnerNoFeatureId = noFeature.Id;
            _courseId = course.Id;
            _batchId = batch.Id;
            _foreignBatchId = foreignBatch.Id;
            _ownExamId = ownExam.Id;
            _foreignExamId = foreignExam.Id;
            _instituteExamId = instituteExam.Id;
        }

        public Task DisposeAsync() => CleanupAsync();

        private static ApplicationUser NewUser(string id) => new()
        {
            Id = id,
            UserName = id,
            NormalizedUserName = id.ToUpperInvariant(),
            Email = $"{id}@test.local",
            NormalizedEmail = $"{id}@TEST.LOCAL",
            FullName = id,
            SecurityStamp = Guid.NewGuid().ToString(),
            IsActive = true
        };

        private static PartnerSubscription NewSubscription(int partnerId, bool canUsePlacement) => new()
        {
            PartnerId = partnerId,
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = DateTime.Today.AddDays(30),
            CanUsePlacementExams = canUsePlacement,
            CanUseProfessionalModels = false
        };

        private static PartnerSubscriptionPeriod NewPeriod(int subscriptionId) => new()
        {
            PartnerSubscriptionId = subscriptionId,
            StartDate = DateTime.Today.AddDays(-1),
            EndDate = DateTime.Today.AddDays(30)
        };

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.ExecuteSqlRawAsync("DELETE FROM SystemLogs WHERE Description LIKE N'%PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ExamQuestions WHERE ExamId IN (SELECT Id FROM Exams WHERE Title LIKE 'PPLS4%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ExamAssignmentsToBatches WHERE ExamId IN (SELECT Id FROM Exams WHERE Title LIKE 'PPLS4%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Exams WHERE Title LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Lessons WHERE Title LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Units WHERE Title LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Sections WHERE Title LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM CourseCurriculums WHERE CurriculumId IN (SELECT Id FROM Curriculums WHERE Title LIKE 'PPLS4%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Curriculums WHERE Title LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Batches WHERE Name LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN (SELECT s.Id FROM PartnerSubscriptions s JOIN Partners p ON s.PartnerId = p.Id WHERE p.Code LIKE 'PPLS4%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN (SELECT s.Id FROM PartnerSubscriptions s JOIN Partners p ON s.PartnerId = p.Id WHERE p.Code LIKE 'PPLS4%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM PartnerSubscriptions WHERE PartnerId IN (SELECT Id FROM Partners WHERE Code LIKE 'PPLS4%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM UserPartners WHERE PartnerId IN (SELECT Id FROM Partners WHERE Code LIKE 'PPLS4%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Branches WHERE Name LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Partners WHERE Code LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Courses WHERE Name LIKE 'PPLS4%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM AspNetUsers WHERE Id LIKE 'ppl-s4-%'");
        }

        // اختيار الشريك عبر المسار الحقيقي، ثم إعادة كوكي الجلسة في كل طلب
        private async Task<HttpClient> PartnerClientAsync(string userId, int partnerId)
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Partner");

            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(userId);
            var select = new HttpRequestMessage(HttpMethod.Post, "/Partner/PartnerContext/Set")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["partnerId"] = partnerId.ToString() })
            };
            select.Headers.Add("Cookie", cookie);
            select.Headers.Add("RequestVerificationToken", token);

            var response = await client.SendAsync(select);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            var sessionCookie = string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
            client.DefaultRequestHeaders.Add("Cookie", sessionCookie);
            return client;
        }

        private Task<HttpClient> OwnClientAsync() => PartnerClientAsync(UserId, _partnerId);

        private HttpRequestMessage PostWithAntiForgery(HttpClient client, string url, Dictionary<string, string> fields)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(UserId);
            var session = client.DefaultRequestHeaders.GetValues("Cookie").Single();
            client.DefaultRequestHeaders.Remove("Cookie");

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) };
            request.Headers.Add("Cookie", $"{session}; {cookie}");
            request.Headers.Add("RequestVerificationToken", token);
            return request;
        }

        private static async Task<string> HtmlOf(HttpResponseMessage response)
            => WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()); // Razor يرمّز العربية

        [Fact]
        public async Task Index_ShowsOwnBatch_WithContractBadge()
        {
            var client = await OwnClientAsync();

            var response = await client.GetAsync(Base);
            var html = await HtmlOf(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains($"{Marker} دفعة", html);
            Assert.DoesNotContain("دفعة أخرى", html);
            Assert.Contains("العقد مفعّل", html);
            Assert.Contains("partner-placement.css", html);
            Assert.DoesNotContain("/Admin/", html.Substring(html.IndexOf("pp-page", StringComparison.Ordinal)));
        }

        [Fact]
        public async Task BatchExams_ShowsOwnExamOnly_ForeignBatchNotFound()
        {
            var client = await OwnClientAsync();

            var response = await client.GetAsync($"{Base}/BatchExams?batchId={_batchId}");
            var html = await HtmlOf(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("اختباري", html);
            Assert.Contains("مسودة", html);
            Assert.DoesNotContain("اختبار المعهد", html);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/BatchExams?batchId={_foreignBatchId}")).StatusCode);
        }

        [Fact]
        public async Task CreateAdvanced_ListsContractCourseAndOwnBatch()
        {
            var client = await OwnClientAsync();

            var response = await client.GetAsync($"{Base}/CreateAdvanced");
            var html = await HtmlOf(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains($"{Marker} دورة", html);
            Assert.Contains($"{Marker} دفعة", html);
            Assert.DoesNotContain("دفعة أخرى", html);
            Assert.Contains($"{Base}/GetSectionsByCourse", html);
            Assert.DoesNotContain("/Admin/PlacementExams", html);
        }

        [Fact]
        public async Task CreateFromModel_WithoutModelsInContract_RedirectsToIndex()
        {
            var client = await OwnClientAsync();

            var response = await client.GetAsync($"{Base}/CreateFromProfessionalModel");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.EndsWith("/Partner/PlacementExams", response.Headers.Location!.ToString());
        }

        [Fact]
        public async Task GetBatchesByCourse_ReturnsOwnBatchJson()
        {
            var client = await OwnClientAsync();

            var response = await client.GetAsync($"{Base}/GetBatchesByCourse?courseId={_courseId}");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var batch = Assert.Single(doc.RootElement.EnumerateArray());
            Assert.Equal(_batchId, batch.GetProperty("batchId").GetInt32());
        }

        [Fact]
        public async Task Review_And_Details_OwnOk_ForeignAndInstituteNotFound()
        {
            var client = await OwnClientAsync();

            var review = await client.GetAsync($"{Base}/Review?examId={_ownExamId}");
            Assert.Equal(HttpStatusCode.OK, review.StatusCode);
            Assert.DoesNotContain("/Admin/", (await HtmlOf(review)).Split("pp-page", 2)[1]);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Base}/PlacementDetails/{_ownExamId}")).StatusCode);

            foreach (var examId in new[] { _foreignExamId, _instituteExamId })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/Review?examId={examId}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/PlacementDetails/{examId}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Base}/PreviewQuestion?examId={examId}&questionId={Guid.NewGuid()}")).StatusCode);
            }
        }

        [Fact]
        public async Task Review_WithQuestions_RendersEditingLinks_And_QuestionPages()
        {
            int sectionId;
            var questionId = Guid.NewGuid();
            var candidateId = Guid.NewGuid();

            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
                db.Curriculums.Add(curriculum);
                await db.SaveChangesAsync();

                var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
                db.Sections.Add(section);
                await db.SaveChangesAsync();

                var unit = new Unit { Title = $"{Marker} وحدة" };
                db.Units.Add(unit);
                await db.SaveChangesAsync();

                var lesson = new Lesson { Title = $"{Marker} مؤشر", Content = "-", SectionId = section.Id, UnitId = unit.Id };
                db.Lessons.Add(lesson);
                await db.SaveChangesAsync();

                db.CourseCurriculums.Add(new CourseCurriculum { CourseId = _courseId, CurriculumId = curriculum.Id });
                db.Questions.AddRange(
                    NewQuestion(questionId, "1", curriculum.Id, section.Id, lesson.Id),
                    NewQuestion(candidateId, "2", curriculum.Id, section.Id, lesson.Id));
                await db.SaveChangesAsync();

                db.ExamQuestions.Add(new ExamQuestion { ExamId = _ownExamId, QuestionId = questionId, Order = 1 });
                await db.SaveChangesAsync();

                sectionId = section.Id;
            }

            var client = await OwnClientAsync();

            var review = await client.GetAsync($"{Base}/Review?examId={_ownExamId}");
            var html = await HtmlOf(review);

            Assert.Equal(HttpStatusCode.OK, review.StatusCode);
            Assert.Contains($"{Base}/ReplaceQuestion?examId={_ownExamId}", html);
            Assert.Contains($"{Base}/AddQuestionFromBank?examId={_ownExamId}", html);
            Assert.Contains($"{Base}/PreviewQuestion?examId={_ownExamId}", html);

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Base}/PreviewQuestion?examId={_ownExamId}&questionId={questionId}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Base}/PreviewQuestion?examId={_ownExamId}&questionId={candidateId}")).StatusCode);

            var replace = await client.GetAsync($"{Base}/ReplaceQuestion?examId={_ownExamId}&questionId={questionId}");
            Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
            Assert.Contains(candidateId.ToString(), await HtmlOf(replace));

            var add = await client.GetAsync($"{Base}/AddQuestionFromBank?examId={_ownExamId}&sectionId={sectionId}");
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
            Assert.Contains(candidateId.ToString(), await HtmlOf(add));
        }

        private static Question NewQuestion(Guid id, string suffix, int curriculumId, int sectionId, int lessonId) => new()
        {
            Id = id,
            Title = $"{Marker} سؤال {suffix}",
            ReferenceNumber = $"{Marker}{suffix}{id:N}",
            CorrectAnswer = "أ",
            CurriculumId = curriculumId,
            SectionId = sectionId,
            LessonId = lessonId,
            Difficulty = DifficultyLevel.Medium,
            IsReviewed = true,
            IsComplete = true
        };

        [Fact]
        public async Task Post_WithoutAntiForgery_IsRejected()
        {
            var client = await OwnClientAsync();

            var response = await client.PostAsync($"{Base}/HideExam", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = _ownExamId.ToString()
            }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Hide_OwnExam_Works_ForeignExam_Untouched()
        {
            var client = await OwnClientAsync();

            var own = await client.SendAsync(PostWithAntiForgery(client, $"{Base}/HideExam", new Dictionary<string, string>
            {
                ["id"] = _ownExamId.ToString(),
                ["batchId"] = _batchId.ToString()
            }));
            var foreign = await client.SendAsync(PostWithAntiForgery(await OwnClientAsync(), $"{Base}/HideExam", new Dictionary<string, string>
            {
                ["id"] = _foreignExamId.ToString()
            }));

            Assert.Equal(HttpStatusCode.Redirect, own.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, foreign.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Exams.Where(e => e.Id == _ownExamId).Select(e => e.IsActive).SingleAsync());
            Assert.True(await db.Exams.Where(e => e.Id == _foreignExamId).Select(e => e.IsActive).SingleAsync());
        }

        [Fact]
        public async Task PartnerWithoutFeature_GetsFeatureNotAllowed()
        {
            var client = await PartnerClientAsync(UserNoFeature, _partnerNoFeatureId);

            var response = await client.GetAsync(Base);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task StudentRole_IsNotAllowed()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, UserId);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Student");

            var response = await client.GetAsync(Base);

            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }
    }

    // PPL-S4 — فحوص بنيوية لا تحتاج قاعدة بيانات
    public class PartnerPlacementExamsControllerShapeTests
    {
        [Fact]
        public void Controller_HasFeature_AndEveryPostHasAntiForgery()
        {
            var type = typeof(PlacementExamsController);

            Assert.Equal(PartnerFeatureType.PlacementExams, type.GetCustomAttribute<PartnerFeatureAttribute>()!.Feature);

            var posts = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<HttpPostAttribute>() != null)
                .ToList();

            Assert.NotEmpty(posts);
            Assert.All(posts, m => Assert.NotNull(m.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
        }

        [Fact]
        public void Controller_DoesNotUseDbContextDirectly()
        {
            var source = File.ReadAllText(FindRepoFile("Areas/Partner/Controllers/PlacementExamsController.cs"));

            Assert.DoesNotContain("_context.", source);
        }

        private static string FindRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, relativePath)))
                dir = dir.Parent;

            return Path.Combine(dir?.FullName ?? throw new FileNotFoundException(relativePath), relativePath);
        }
    }
}
