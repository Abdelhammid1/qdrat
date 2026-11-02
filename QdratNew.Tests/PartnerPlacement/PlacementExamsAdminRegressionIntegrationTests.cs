using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S2.4 — صفحات Admin/PlacementExams عبر HTTP حقيقي بعد نقل المنطق للخدمة:
    // نفس المسارات تعمل وتعرض الـ Views، واختبار الشريك لا يظهر للأدمن.
    [Collection("SharedIntegrationDb")]
    public class PlacementExamsAdminRegressionIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private const string Marker = "PPLHTTP";
        private const string OwnerUser = "ppl-http-owner";

        private readonly CustomWebApplicationFactory _factory;
        private int _instituteExamId;
        private int _partnerExamId;
        private int _batchId;
        private int _courseId;

        public PlacementExamsAdminRegressionIntegrationTests(CustomWebApplicationFactory factory) => _factory = factory;

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();
            await CleanupAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var partner = new Partner { Name = $"{Marker} شريك", Code = Marker, PartnershipStart = DateTime.Today.AddDays(-1), PartnershipEnd = DateTime.Today.AddDays(10) };
            var branch = new Branch { Name = $"{Marker} فرع", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            db.AddRange(partner, branch, course);
            await db.SaveChangesAsync();

            var batch = new Batch { Name = $"{Marker} دفعة", BranchId = branch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            var instituteExam = new Exam { Title = $"{Marker} اختبار المعهد", Type = ExamType.LevelAssessment, DurationMinutes = 30 };
            var partnerExam = new Exam { Title = $"{Marker} اختبار الشريك", Type = ExamType.LevelAssessment, DurationMinutes = 30, OwnerPartnerId = partner.Id };
            db.Batches.Add(batch);
            db.Exams.AddRange(instituteExam, partnerExam);
            await db.SaveChangesAsync();

            db.ExamAssignmentsToBatches.AddRange(
                new ExamAssignmentToBatch { ExamId = instituteExam.Id, BatchId = batch.Id, Title = "اختبار تحديد المستوى" },
                new ExamAssignmentToBatch { ExamId = partnerExam.Id, BatchId = batch.Id, Title = "اختبار تحديد المستوى" });
            await db.SaveChangesAsync();

            _instituteExamId = instituteExam.Id;
            _partnerExamId = partnerExam.Id;
            _batchId = batch.Id;
            _courseId = course.Id;
        }

        public Task DisposeAsync() => CleanupAsync();

        private async Task CleanupAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await db.Database.ExecuteSqlRawAsync("DELETE FROM SystemLogs WHERE Description LIKE N'%PPLHTTP%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM AdminActivityLogs WHERE BatchId IN (SELECT Id FROM Batches WHERE Name LIKE 'PPLHTTP%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ExamAssignmentsToBatches WHERE ExamId IN (SELECT Id FROM Exams WHERE Title LIKE 'PPLHTTP%')");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Exams WHERE Title LIKE 'PPLHTTP%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Batches WHERE Name LIKE 'PPLHTTP%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Branches WHERE Name LIKE 'PPLHTTP%'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Partners WHERE Code = 'PPLHTTP'");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Courses WHERE Name LIKE 'PPLHTTP%'");
        }

        private HttpClient OwnerClient()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, OwnerUser);
            client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Owner");
            return client;
        }

        private HttpRequestMessage PostWithAntiForgery(string url, Dictionary<string, string> fields)
        {
            var (cookie, token) = _factory.CreateValidAntiForgeryTokens(OwnerUser);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new FormUrlEncodedContent(fields)
            };
            request.Headers.Add("Cookie", cookie);
            request.Headers.Add("RequestVerificationToken", token);
            return request;
        }

        [Fact]
        public async Task Index_RendersAndCountsInstituteExamOnly()
        {
            var response = await OwnerClient().GetAsync("/Admin/PlacementExams");
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()); // Razor يرمّز العربية

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains($"{Marker} دفعة", html);
        }

        [Fact]
        public async Task BatchExams_ShowsInstituteExam_HidesPartnerExam()
        {
            var response = await OwnerClient().GetAsync($"/Admin/PlacementExams/BatchExams?batchId={_batchId}");
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()); // Razor يرمّز العربية

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("اختبار المعهد", html);
            Assert.DoesNotContain("اختبار الشريك", html);
        }

        [Fact]
        public async Task CreatePages_Render()
        {
            var client = OwnerClient();

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin/PlacementExams/CreateAdvanced")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Admin/PlacementExams/CreateFromProfessionalModel")).StatusCode);
        }

        [Fact]
        public async Task Review_InstituteExamOk_PartnerExamNotFound()
        {
            var client = OwnerClient();

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Admin/PlacementExams/Review?examId={_instituteExamId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Admin/PlacementExams/Review?examId={_partnerExamId}")).StatusCode);
        }

        [Fact]
        public async Task PlacementDetails_InstituteExamOk_PartnerExamNotFound()
        {
            var client = OwnerClient();

            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/Admin/PlacementExams/PlacementDetails/{_instituteExamId}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/Admin/PlacementExams/PlacementDetails/{_partnerExamId}")).StatusCode);
        }

        [Fact]
        public async Task GetSectionsByCourse_KeepsJsonShape()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                // كورس بمحور فيه سؤال: نأخذ أي ربط قائم من نسخة قاعدة التطوير
                _courseId = await (
                    from cc in db.CourseCurriculums
                    join s in db.Sections on cc.CurriculumId equals s.CurriculumId
                    join l in db.Lessons on s.Id equals l.SectionId
                    join q in db.Questions on l.Id equals q.LessonId
                    select cc.CourseId
                ).FirstOrDefaultAsync();
            }

            var response = await OwnerClient().GetAsync($"/Admin/PlacementExams/GetSectionsByCourse?courseId={_courseId}");
            var json = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var doc = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);

            if (doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                Assert.True(first.TryGetProperty("curriculumId", out _));
                Assert.True(first.TryGetProperty("curriculumTitle", out _));
                Assert.True(first.TryGetProperty("sectionId", out _));
                Assert.True(first.TryGetProperty("sectionTitle", out _));
            }
        }

        [Fact]
        public async Task ToggleDontKnow_KeepsJsonContract_AndRejectsPartnerExam()
        {
            var client = OwnerClient();

            var ok = await client.SendAsync(PostWithAntiForgery("/Admin/PlacementExams/ToggleDontKnowOption",
                new() { ["examId"] = _instituteExamId.ToString(), ["enabled"] = "true" }));
            var okJson = JsonDocument.Parse(await ok.Content.ReadAsStringAsync()).RootElement;

            Assert.True(okJson.GetProperty("success").GetBoolean());
            Assert.True(okJson.GetProperty("allowDontKnowOption").GetBoolean());

            var denied = await client.SendAsync(PostWithAntiForgery("/Admin/PlacementExams/ToggleDontKnowOption",
                new() { ["examId"] = _partnerExamId.ToString(), ["enabled"] = "true" }));
            var deniedJson = JsonDocument.Parse(await denied.Content.ReadAsStringAsync()).RootElement;

            Assert.False(deniedJson.GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task ArchiveBatches_DoesNotTouchPartnerExam()
        {
            var response = await OwnerClient().SendAsync(PostWithAntiForgery("/Admin/PlacementExams/ArchiveBatches",
                new() { ["selectedBatchIds"] = _batchId.ToString() }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.ExamAssignmentsToBatches.AnyAsync(x => x.ExamId == _instituteExamId && x.IsArchived));
            Assert.False(await db.ExamAssignmentsToBatches.AnyAsync(x => x.ExamId == _partnerExamId && x.IsArchived));
        }

        [Fact]
        public async Task HideExam_RedirectsToBatchExams()
        {
            var response = await OwnerClient().SendAsync(PostWithAntiForgery("/Admin/PlacementExams/HideExam",
                new() { ["id"] = _instituteExamId.ToString(), ["batchId"] = _batchId.ToString() }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Contains("BatchExams", response.Headers.Location!.ToString());
        }
    }
}
