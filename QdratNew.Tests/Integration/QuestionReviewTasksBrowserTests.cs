using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using QdratNew.Data;
using QdratNew.Entities;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // QRT-S8.4 — E2E بمتصفح حقيقي على خادم QdratNew الحقيقي وقاعدة الاختبار المعزولة (نسخة QdratNewDB):
    // الأدمن يُسند سؤالين لمدرب ← المدرب يعتمد سؤالًا ويُرجع الآخر بملاحظة ← الأدمن يرى نسبة الاعتماد 50%.
    // يتخطّى ذاتيًا إن غاب QDRAT_TEST_SQL (نفس بوابة اختبارات SQL Server). المنفذ 5298 كي لا يتعارض
    // مع اختبار حماية الترجمة (5299) إن عملا بالتوازي.
    [Collection(QrtSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public class QuestionReviewTasksBrowserTests : IAsyncLifetime
    {
        private const string AdminUser = "qrt-e2e-admin";
        private const string InstructorUser = "qrt-e2e-inst";
        private const string Password = "P@ssw0rd123!";
        private const string Marker = "QRTE2E";

        private RealServerFixture? _server;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private List<Guid> _questionIds = new();

        private static ApplicationDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(TestDatabaseBaseline.TestDatabaseConnectionString, o => o.CommandTimeout(180))
                .Options;
            return new ApplicationDbContext(options);
        }

        public async Task InitializeAsync()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.EnvName)))
                return;

            await TestDatabaseBaseline.EnsureAsync();
            await using (var db = CreateDb())
            {
                await CleanupAsync(db);
                await SeedAsync(db);
            }

            _server = await RealServerFixture.StartAsync(5298);
            _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }

        public async Task DisposeAsync()
        {
            if (_browser != null) await _browser.DisposeAsync();
            _playwright?.Dispose();
            if (_server != null) await _server.DisposeAsync();

            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerFactAttribute.EnvName)))
            {
                await using var db = CreateDb();
                await CleanupAsync(db);
            }
        }

        private async Task SeedAsync(ApplicationDbContext db)
        {
            var hasher = new PasswordHasher<ApplicationUser>();

            async Task<ApplicationUser> AddUserAsync(string userName, string nationalId, string roleName)
            {
                var roleId = await db.Roles.Where(r => r.Name == roleName).Select(r => r.Id).FirstOrDefaultAsync();
                if (roleId == null)
                    throw new InvalidOperationException($"دور '{roleName}' غير موجود في قاعدة الاختبار المنسوخة.");

                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = userName,
                    NormalizedUserName = userName.ToUpperInvariant(),
                    Email = $"{userName}@test.local",
                    NormalizedEmail = $"{userName}@test.local".ToUpperInvariant(),
                    EmailConfirmed = true,
                    NationalID = nationalId,
                    FullName = userName,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                    IsActive = true
                };
                user.PasswordHash = hasher.HashPassword(user, Password);
                db.Users.Add(user);
                await db.SaveChangesAsync();
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleId });
                await db.SaveChangesAsync();
                return user;
            }

            await AddUserAsync(AdminUser, "1000000901", "SuperAdmin");
            var instUser = await AddUserAsync(InstructorUser, "1000000902", "Instructor");

            var anchor = await db.Questions.AsNoTracking()
                .Where(q => q.SectionId != null)
                .Select(q => new { q.CurriculumId, q.LessonId, SectionId = q.SectionId!.Value })
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("قاعدة الاختبار بلا أي سؤال لاستعارة المنهج/الدرس/القسم.");

            var branch = new Branch { Name = $"{Marker} فرع", Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            db.Branches.Add(branch);
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            var batch = new Batch { Name = $"{Marker} دفعة", StartDate = DateTime.UtcNow, CourseId = course.Id, BranchId = branch.Id };
            db.Batches.Add(batch);

            var instructor = new Instructor
            {
                FullName = "مدرب E2E",
                NationalID = $"{Marker}0001",
                Email = $"{Marker.ToLowerInvariant()}@test.local",
                Specialization = "عام",
                IsActive = true,
                UserId = instUser.Id
            };
            db.Instructors.Add(instructor);
            await db.SaveChangesAsync();

            db.InstructorCurriculumBatches.Add(new InstructorCurriculumBatch
            {
                InstructorId = instructor.Id, CurriculumId = anchor.CurriculumId, BatchId = batch.Id, UserId = instUser.Id
            });

            for (var i = 1; i <= 2; i++)
            {
                db.Questions.Add(new Question
                {
                    Title = $"سؤال E2E {i}",
                    ReferenceNumber = $"{Marker}-{i}-{Guid.NewGuid():N}"[..16],
                    CurriculumId = anchor.CurriculumId,
                    LessonId = anchor.LessonId,
                    SectionId = anchor.SectionId,
                    CorrectAnswer = "الجواب الصحيح",
                    IsComplete = true,
                    Options = new List<QuestionOption>
                    {
                        new() { Text = "الجواب الصحيح" },
                        new() { Text = "جواب خاطئ" }
                    }
                });
            }
            await db.SaveChangesAsync();

            _questionIds = await db.Questions.Where(q => q.ReferenceNumber.StartsWith(Marker + "-"))
                .OrderBy(q => q.ReferenceNumber).Select(q => q.Id).ToListAsync();
        }

        private static async Task CleanupAsync(ApplicationDbContext db)
        {
            const string questions = "(SELECT Id FROM Questions WHERE ReferenceNumber LIKE 'QRTE2E-%')";
            const string instructors = "(SELECT Id FROM Instructors WHERE NationalID LIKE 'QRTE2E%')";
            foreach (var sql in new[]
            {
                $"DELETE FROM QuestionAuditLogs WHERE QuestionId IN {questions}",
                $"DELETE FROM QuestionReviewTaskItems WHERE QuestionId IN {questions}",
                $"DELETE FROM QuestionReviewTasks WHERE ParentTaskId IS NOT NULL AND InstructorId IN {instructors}",
                $"DELETE FROM QuestionReviewTasks WHERE InstructorId IN {instructors}",
                $"DELETE FROM QuestionOptions WHERE QuestionId IN {questions}",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'QRTE2E-%'",
                $"DELETE FROM InstructorCurriculumBatches WHERE InstructorId IN {instructors}",
                "DELETE FROM Instructors WHERE NationalID LIKE 'QRTE2E%'",
                "DELETE FROM Batches WHERE Name LIKE 'QRTE2E %'",
                "DELETE FROM Courses WHERE Name LIKE 'QRTE2E %'",
                "DELETE FROM Branches WHERE Name LIKE 'QRTE2E %'",
                "DELETE FROM Notifications WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE UserName IN ('qrt-e2e-admin','qrt-e2e-inst'))",
                "DELETE FROM AspNetUserRoles WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE UserName IN ('qrt-e2e-admin','qrt-e2e-inst'))",
                "DELETE FROM AspNetUsers WHERE UserName IN ('qrt-e2e-admin','qrt-e2e-inst')"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        private async Task<IPage> LoginAsync(string userName)
        {
            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = _server!.BaseUrl });
            var page = await context.NewPageAsync();
            var resp = await page.GotoAsync("/LMS/login");
            Assert.True(resp?.Ok, $"فشل تحميل صفحة الدخول: {resp?.Status}");
            await page.FillAsync("#Input_Email", userName);
            await page.FillAsync("#passwordField", Password);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });
            return page;
        }

        // نص صفحة الخطأ (Development Exception Page) لتسهيل تشخيص فشل E2E
        private static async Task<string> PageErrorAsync(IPage page)
        {
            var text = await page.InnerTextAsync("body");
            return text.Length > 1500 ? text[..1500] : text;
        }

        // نافذة SweetAlert (تنبيه النتيجة) تُغلق بزر «حسنًا» قبل التفاعل التالي
        private static async Task DismissSwalAsync(IPage page)
        {
            var ok = page.Locator(".swal2-container .swal2-confirm");
            await ok.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
            await ok.First.ClickAsync();
            await page.Locator(".swal2-container").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = 10000 });
        }

        [SqlServerFact]
        public async Task AssignThenApproveAndReturn_AdminSeesFiftyPercentApproval()
        {
            // ---------- 1) الأدمن يُسند السؤالين (نقطة النهاية الحقيقية + Antiforgery حقيقي) ----------
            var admin = await LoginAsync(AdminUser);
            var indexResp = await admin.GotoAsync("/Admin/QuestionReviewTasks");
            Assert.True(indexResp?.Ok,
                $"فشل تحميل متابعة المهام: {indexResp?.Status}{Environment.NewLine}{await PageErrorAsync(admin)}{Environment.NewLine}--- server ---{Environment.NewLine}{_server!.RecentOutput}");

            // صفحات الأدمن الأخرى تُعرض بلا أخطاء Razor (تغطية تجميع الـ Views فعليًا)
            foreach (var url in new[] { "/Admin/QuestionReviewTasks/Reviewers", "/Admin/QuestionReviewTasks/AutoDistribute" })
            {
                var r = await admin.GotoAsync(url);
                Assert.True(r?.Ok, $"فشل تحميل {url}: {r?.Status}{Environment.NewLine}{await PageErrorAsync(admin)}");
            }
            await admin.GotoAsync("/Admin/QuestionReviewTasks");

            int instructorId;
            await using (var db = CreateDb())
                instructorId = await db.Instructors.Where(i => i.NationalID == $"{Marker}0001").Select(i => i.Id).SingleAsync();

            var created = await admin.EvaluateAsync<System.Text.Json.JsonElement>(@"async (args) => {
                const token = document.querySelector('input[name=""__RequestVerificationToken""]').value;
                const fd = new FormData();
                fd.append('__RequestVerificationToken', token);
                fd.append('Title', 'مراجعة E2E');
                fd.append('InstructorId', args.instructorId);
                args.ids.forEach(id => fd.append('SelectedQuestionIds', id));
                const r = await fetch('/Admin/QuestionReviewTasks/Create', { method: 'POST', headers: { 'RequestVerificationToken': token }, body: fd });
                return await r.json();
            }", new { instructorId, ids = _questionIds.Select(i => i.ToString()).ToArray() });
            Assert.True(created.GetProperty("success").GetBoolean(), created.GetRawText());

            int taskId;
            string taskCode;
            await using (var db = CreateDb())
            {
                var task = await db.QuestionReviewTasks.AsNoTracking().SingleAsync(t => t.InstructorId == instructorId);
                taskId = task.Id;
                taskCode = task.Code;
                Assert.Equal(2, task.TotalItems);
            }

            // ---------- 2) المدرب يرى المهمة، يعتمد سؤالًا ويُرجع الآخر بملاحظة ----------
            var instructor = await LoginAsync(InstructorUser);
            await instructor.GotoAsync("/Instructors/QuestionReviewTasks");
            await Assertions.Expect(instructor.Locator($"text={taskCode}").First).ToBeVisibleAsync(new() { Timeout = 15000 });

            var reviewResp = await instructor.GotoAsync($"/Instructors/QuestionReviewTasks/Review/{taskId}");
            Assert.True(reviewResp?.Ok, $"فشل تحميل صفحة المراجعة: {reviewResp?.Status}");
            try
            {
                await Assertions.Expect(instructor.Locator("#qrtTable tbody .qrt-act-approve")).ToHaveCountAsync(2, new() { Timeout = 15000 });
            }
            catch (Exception ex)
            {
                var body = await instructor.InnerTextAsync("#qrtReview");
                throw new Xunit.Sdk.XunitException($"جدول المراجعة لم يعرض أزرار الاعتماد.{Environment.NewLine}{(body.Length > 1200 ? body[..1200] : body)}{Environment.NewLine}--- server ---{Environment.NewLine}{_server!.RecentOutput}", ex);
            }

            await instructor.Locator("#qrtTable tbody .qrt-act-approve").First.ClickAsync();
            await DismissSwalAsync(instructor);
            await Assertions.Expect(instructor.Locator("#qrtCntApproved")).ToHaveTextAsync("1", new() { Timeout = 10000 });

            await Assertions.Expect(instructor.Locator("#qrtTable tbody .qrt-act-return")).ToHaveCountAsync(1, new() { Timeout = 10000 });
            await instructor.Locator("#qrtTable tbody .qrt-act-return").First.ClickAsync();
            await Assertions.Expect(instructor.Locator("#qrtReturnModal")).ToBeVisibleAsync();

            // الإرسال بملاحظة قصيرة يُرفض في الواجهة (التحقق من أن الملاحظة إلزامية)
            await instructor.FillAsync("#qrtReturnNote", "قصيرة");
            await instructor.ClickAsync("#qrtReturnSubmit");
            await Assertions.Expect(instructor.Locator("#qrtReturnError")).Not.ToBeEmptyAsync();

            await instructor.FillAsync("#qrtReturnNote", "الإجابة الصحيحة غير واضحة في نص السؤال");
            await instructor.ClickAsync("#qrtReturnSubmit");
            await DismissSwalAsync(instructor);
            await Assertions.Expect(instructor.Locator("#qrtCntReturned")).ToHaveTextAsync("1", new() { Timeout = 10000 });
            await Assertions.Expect(instructor.Locator("#qrtCntPending")).ToHaveTextAsync("0");

            // ---------- 3) الأدمن يرى نسبة الاعتماد 50% وملاحظة الإرجاع ----------
            var detailsResp = await admin.GotoAsync($"/Admin/QuestionReviewTasks/Details?id={taskId}");
            Assert.True(detailsResp?.Ok, $"فشل تحميل تفاصيل المهمة: {detailsResp?.Status}");
            await Assertions.Expect(admin.Locator("#qrtAdminDetails")).ToContainTextAsync("50%", new() { Timeout = 15000 });
            await Assertions.Expect(admin.Locator("#qrtItemsTable")).ToContainTextAsync("الإجابة الصحيحة غير واضحة", new() { Timeout = 15000 });

            // وحالة قاعدة البيانات متسقة مع الواجهة
            await using var check = CreateDb();
            var final = await check.QuestionReviewTasks.AsNoTracking().SingleAsync(t => t.Id == taskId);
            Assert.Equal(1, final.ApprovedItems);
            Assert.Equal(1, final.ReturnedItems);
            Assert.Equal(0, final.PendingItems);
            Assert.Equal(1, await check.Questions.CountAsync(q => q.ReferenceNumber.StartsWith(Marker + "-") && q.IsReviewed));
        }
    }
}
