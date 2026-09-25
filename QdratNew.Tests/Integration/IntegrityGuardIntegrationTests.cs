using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // Sprint 9 (QG-G / G2) — اختبارات تكامل عبر WebApplicationFactory<Program> (تطبيق حقيقي كامل
    // على قاعدة اختبار معزولة، راجع CustomWebApplicationFactory): تُغطّي مصفوفة الاختبارات
    // الأمنية الموصى بها في مراجعة Sprint 8 (طلب بدون مصادقة، بدون CSRF صحيح، طلب صحيح،
    // طلبان متزامنان لنفس المحاولة، ووصول طالب/موظف بلا صلاحية للوحة الأدمن).
    [Collection("SharedIntegrationDb")]
    public class IntegrityGuardIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
    {
        private readonly CustomWebApplicationFactory _factory;
        private int _seededStudentId;
        private string _studentUserId = null!;

        public IntegrityGuardIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var user = new ApplicationUser
            {
                UserName = "tg-integration-student",
                Email = "tg-integration-student@test.local",
                NationalID = "1000000001"
            };
            var createResult = await userManager.CreateAsync(user, "P@ssw0rd123!");
            if (!createResult.Succeeded)
                throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
            _studentUserId = user.Id;

            var branch = new Branch { Name = "فرع اختبار التكامل", Location = "-", City = "-", State = "-", Country = "-" };
            db.Branches.Add(branch);
            var course = new Course { Name = "دورة اختبار التكامل", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            var batch = new Batch { Name = "دفعة اختبار التكامل", StartDate = DateTime.UtcNow, CourseId = course.Id, BranchId = branch.Id };
            db.Batches.Add(batch);
            await db.SaveChangesAsync();

            // StudentID عمود Identity في القاعدة الحقيقية (المنسوخة من QdratNewDB) — يتولد
            // تلقائيًا ولا يُمرَّر صراحة.
            var student = new Student
            {
                NationalID = "1000000001",
                FullName = "طالب اختبار التكامل",
                Gender = "ذكر",
                Age = 18,
                School = "-",
                Level = "-",
                UserId = _studentUserId,
                BranchId = branch.Id
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();
            _seededStudentId = student.StudentID;

            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = _seededStudentId, BatchId = batch.Id });
            await db.SaveChangesAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private HttpClient CreateClient(string? userId = null, string? role = null)
        {
            // HandleCookies=false: نحتاج إرسال كوكي antiforgery يدويًا (Cookie header) بدون أن
            // يتدخل CookieContainerHandler الافتراضي في WebApplicationFactory ويتجاهله/يستبدله.
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
            if (userId != null)
            {
                client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId);
                client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role ?? "Student");
            }
            return client;
        }

        private static void AttachAntiForgery(HttpClient client, (string CookieHeader, string RequestToken) tokens)
        {
            client.DefaultRequestHeaders.Add("Cookie", tokens.CookieHeader);
            client.DefaultRequestHeaders.Add("RequestVerificationToken", tokens.RequestToken);
        }

        private static StringContent BuildReportBody(int attemptType, int attemptEntityId)
            => new StringContent(
                $"{{\"AttemptType\":{attemptType},\"AttemptEntityId\":{attemptEntityId},\"ViolationType\":\"BrowserTranslate\",\"PageUrl\":\"/test\"}}",
                Encoding.UTF8, "application/json");

        [Fact]
        public async Task ReportViolation_Unauthenticated_IsRejected()
        {
            var client = CreateClient();

            var response = await client.PostAsync("/Students/IntegrityGuard/ReportViolation", BuildReportBody(2, 111));

            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Redirect or HttpStatusCode.Found,
                $"Unexpected status for unauthenticated request: {response.StatusCode}");
        }

        [Fact]
        public async Task ReportViolation_MissingAntiForgeryToken_ReturnsBadRequest()
        {
            var client = CreateClient(_studentUserId, "Student");

            var response = await client.PostAsync("/Students/IntegrityGuard/ReportViolation", BuildReportBody(2, 112));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task ReportViolation_ValidAuthenticatedRequest_CreatesRowAndReturnsSuccess()
        {
            var client = CreateClient(_studentUserId, "Student");
            AttachAntiForgery(client, _factory.CreateValidAntiForgeryTokens(_studentUserId));

            var response = await client.PostAsync("/Students/IntegrityGuard/ReportViolation", BuildReportBody(2, 200));
            response.EnsureSuccessStatusCode();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = await db.IntegrityViolationLogs.SingleAsync(v => v.StudentId == _seededStudentId && v.AttemptEntityId == 200);
            Assert.Equal(IntegrityAttemptType.Exam, row.AttemptType);
            Assert.False(row.IsResolved);
        }

        [Fact]
        public async Task ReportViolation_ConcurrentDuplicateRequests_OnlyOneRowPersists()
        {
            var tokens = _factory.CreateValidAntiForgeryTokens(_studentUserId);

            HttpClient MakeClient()
            {
                var c = CreateClient(_studentUserId, "Student");
                AttachAntiForgery(c, tokens);
                return c;
            }

            var clients = Enumerable.Range(0, 8).Select(_ => MakeClient()).ToArray();
            var tasks = clients.Select(c => c.PostAsync("/Students/IntegrityGuard/ReportViolation", BuildReportBody(6, 300))).ToArray();

            var responses = await Task.WhenAll(tasks);
            Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, $"Unexpected status: {r.StatusCode}"));

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var count = await db.IntegrityViolationLogs.CountAsync(v => v.StudentId == _seededStudentId && v.AttemptEntityId == 300);
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task Resolve_EmployeeWithoutPermissionProfile_ReturnsForbidden()
        {
            var client = CreateClient("admin-employee-1", "Employee");
            AttachAntiForgery(client, _factory.CreateValidAntiForgeryTokens("admin-employee-1"));

            var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = "1" });
            var response = await client.PostAsync("/Admin/IntegrityViolations/Resolve", form);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Resolve_OwnerRole_BypassesPermissionCheckAndUnblocksViolation()
        {
            int logId;
            using (var scope = _factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var log = new IntegrityViolationLog
                {
                    StudentId = _seededStudentId,
                    AttemptType = IntegrityAttemptType.MinistrySim,
                    AttemptEntityId = 400,
                    ViolationType = "BrowserTranslate"
                };
                db.IntegrityViolationLogs.Add(log);
                await db.SaveChangesAsync();
                logId = log.Id;
            }

            var client = CreateClient("admin-owner-1", "Owner");
            AttachAntiForgery(client, _factory.CreateValidAntiForgeryTokens("admin-owner-1"));

            var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = logId.ToString(), ["note"] = "تحقق آلي" });
            var response = await client.PostAsync("/Admin/IntegrityViolations/Resolve", form);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var verifyScope = _factory.Services.CreateScope();
            var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var updated = await verifyDb.IntegrityViolationLogs.FindAsync(logId);
            Assert.True(updated!.IsResolved);
        }
    }
}
