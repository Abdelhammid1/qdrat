using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using QdratNew.Data;
using QdratNew.Entities;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // Sprint 9 (QG-G / G3) — اختبار Browser/E2E حقيقي عبر Playwright على مسار واحد تمثيلي
    // (الاختبار، StudentExamFlow/StartExam) مقابل خادم Kestrel حقيقي (RealServerFixture) وتسجيل
    // دخول Identity فعلي (لا اختصار مصادقة): يحاكي تفعيل ترجمة المتصفح بإضافة class
    // "translated-ltr" على <html> (تمامًا كما تفعل Google Translate)، ويتحقق من: ظهور الـ
    // Overlay غير القابل للإغلاق، تعطّل عناصر التفاعل، ووصول طلب POST فعلي إلى
    // /Students/IntegrityGuard/ReportViolation. المسارات الخمسة الباقية تُختبَر يدويًا (QG-F1)
    // تفاديًا لتكرار غير ضروري لنفس آلية translation-guard.js المشتركة.
    public class TranslationGuardBrowserTests : IAsyncLifetime
    {
        private const string StudentUserName = "tg-e2e-student";
        private const string StudentPassword = "P@ssw0rd123!";
        private const string StudentNationalId = "1000000002";

        private RealServerFixture _server = null!;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private int _examAssignmentId;

        private static ApplicationDbContext CreateDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(TestDatabaseBaseline.TestDatabaseConnectionString)
                .Options;
            return new ApplicationDbContext(options);
        }

        public async Task InitializeAsync()
        {
            await TestDatabaseBaseline.EnsureAsync();

            await using (var db = CreateDb())
            {
                await CleanupAsync(db);

                var studentRoleId = await db.Roles
                    .Where(r => r.Name == "Student")
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();
                if (studentRoleId == null)
                    throw new InvalidOperationException("دور 'Student' غير موجود في قاعدة الاختبار المنسوخة — تحقّق من DbInitializer.SeedRolesAsync في قاعدة التطوير الأصلية.");

                var hasher = new PasswordHasher<ApplicationUser>();
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = StudentUserName,
                    NormalizedUserName = StudentUserName.ToUpperInvariant(),
                    Email = "tg-e2e-student@test.local",
                    NormalizedEmail = "TG-E2E-STUDENT@TEST.LOCAL",
                    EmailConfirmed = true,
                    NationalID = StudentNationalId,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                    IsActive = true
                };
                user.PasswordHash = hasher.HashPassword(user, StudentPassword);
                db.Users.Add(user);
                await db.SaveChangesAsync();

                db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = studentRoleId });

                var branch = new Branch { Name = "فرع اختبار E2E", Location = "-", City = "-", State = "-", Country = "-" };
                db.Branches.Add(branch);
                var course = new Course { Name = "دورة اختبار E2E", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
                db.Courses.Add(course);
                await db.SaveChangesAsync();

                var batch = new Batch { Name = "دفعة اختبار E2E", StartDate = DateTime.UtcNow, CourseId = course.Id, BranchId = branch.Id };
                db.Batches.Add(batch);
                await db.SaveChangesAsync();

                var student = new Student
                {
                    NationalID = StudentNationalId,
                    FullName = "طالب اختبار E2E",
                    Gender = "ذكر",
                    Age = 18,
                    School = "-",
                    Level = "-",
                    UserId = user.Id,
                    BranchId = branch.Id
                };
                db.Students.Add(student);
                await db.SaveChangesAsync();

                db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id });

                // استخدام سؤال حقيقي موجود بالفعل في قاعدة التطوير المنسوخة (له خيارات وإجابة
                // صحيحة) بدل بناء سلسلة Curriculum/Lesson/Question يدويًا من الصفر.
                var existingQuestion = await db.Questions
                    .AsNoTracking()
                    .Where(q => q.Options.Count > 0 && q.CorrectAnswer != null)
                    .Select(q => q.Id)
                    .FirstOrDefaultAsync();
                if (existingQuestion == Guid.Empty)
                    throw new InvalidOperationException("لا يوجد أي سؤال صالح (بخيارات وإجابة صحيحة) في قاعدة التطوير المنسوخة لبناء اختبار E2E.");

                var exam = new Exam { Title = "اختبار E2E — حماية الترجمة", TotalQuestions = 1, DurationMinutes = 30 };
                db.Exams.Add(exam);
                await db.SaveChangesAsync();

                var assignment = new ExamAssignmentToBatch
                {
                    ExamId = exam.Id,
                    BatchId = batch.Id,
                    Title = exam.Title,
                    DurationMinutes = 30,
                    IsInLab = false,
                    IsOnline = true
                };
                db.ExamAssignmentsToBatches.Add(assignment);
                await db.SaveChangesAsync();

                db.ExamQuestions.Add(new ExamQuestion { QuestionId = existingQuestion, ExamId = exam.Id, ExamAssignmentId = assignment.Id, Order = 1 });
                await db.SaveChangesAsync();

                _examAssignmentId = assignment.Id;
            }

            _server = await RealServerFixture.StartAsync();

            _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }

        private static async Task CleanupAsync(ApplicationDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM ExamStudentStatuses WHERE StudentId IN (SELECT StudentID FROM Students WHERE NationalID = {0})", StudentNationalId);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM ExamStudentQuestionOrders WHERE StudentId IN (SELECT StudentID FROM Students WHERE NationalID = {0})", StudentNationalId);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM IntegrityViolationLogs WHERE StudentId IN (SELECT StudentID FROM Students WHERE NationalID = {0})", StudentNationalId);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM ExamQuestions WHERE ExamId IN (SELECT Id FROM Exams WHERE Title = N'اختبار E2E — حماية الترجمة')");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM ExamAssignmentsToBatches WHERE Title = N'اختبار E2E — حماية الترجمة'");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Exams WHERE Title = N'اختبار E2E — حماية الترجمة'");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM StudentBatchEnrollments WHERE StudentID IN (SELECT StudentID FROM Students WHERE NationalID = {0})", StudentNationalId);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Students WHERE NationalID = {0}", StudentNationalId);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Batches WHERE Name = N'دفعة اختبار E2E'");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Courses WHERE Name = N'دورة اختبار E2E'");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM Branches WHERE Name = N'فرع اختبار E2E'");
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM AspNetUserRoles WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE UserName = {0})", StudentUserName);
            await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM AspNetUsers WHERE UserName = {0}", StudentUserName);
        }

        public async Task DisposeAsync()
        {
            if (_browser != null) await _browser.DisposeAsync();
            _playwright?.Dispose();
            if (_server != null) await _server.DisposeAsync();

            await using var db = CreateDb();
            await CleanupAsync(db);
        }

        [Fact]
        public async Task StartExam_SimulatedBrowserTranslation_LocksPageAndReportsViolation()
        {
            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions { BaseURL = _server.BaseUrl });
            var page = await context.NewPageAsync();

            // تسجيل دخول Identity حقيقي (لا اختصار مصادقة) — نفس نموذج /LMS/login الفعلي.
            var loginResp = await page.GotoAsync("/LMS/login");
            Assert.True(loginResp?.Ok, $"فشل تحميل صفحة الدخول: {loginResp?.Status}");
            await page.FillAsync("#Input_Email", StudentUserName);
            await page.FillAsync("#passwordField", StudentPassword);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 15000 });

            string? reportViolationMethod = null;
            page.Request += (_, request) =>
            {
                if (request.Url.Contains("/Students/IntegrityGuard/ReportViolation"))
                    reportViolationMethod = request.Method;
            };

            var response = await page.GotoAsync($"/Students/StudentExamFlow/StartExam?examAssignmentId={_examAssignmentId}");
            Assert.NotNull(response);
            Assert.True(response!.Ok, $"فشل تحميل صفحة الاختبار: {response.Status}");

            // تأكيد أن المكوّن (translation-guard.js + Overlay) مُحمَّل فعليًا في الصفحة قبل المحاكاة
            // ملاحظة: translation-guard.js يعرّف TranslationGuard عبر `const` في Script عادي (غير
            // module) — يصبح معرّفًا عامًا (Global) قابلًا للوصول باسمه مباشرة، لكن ليس كخاصية على
            // window (خاص بـ let/const في الـ Top-Level Scope، بخلاف var).
            await page.WaitForFunctionAsync("() => typeof TranslationGuard !== 'undefined'", new PageWaitForFunctionOptions { Timeout = 8000 });
            var overlayHiddenBefore = await page.EvalOnSelectorAsync<bool>(
                "#tg-violation-overlay", "el => el.classList.contains('tg-hidden')");
            Assert.True(overlayHiddenBefore, "الـ Overlay يجب أن يكون مخفيًا قبل أي محاكاة للترجمة.");

            // محاكاة تفعيل ترجمة المتصفح تمامًا كما تفعل Google Translate فعليًا
            await page.EvaluateAsync("document.documentElement.classList.add('translated-ltr')");

            await page.WaitForFunctionAsync(
                "() => !document.getElementById('tg-violation-overlay').classList.contains('tg-hidden')",
                new PageWaitForFunctionOptions { Timeout = 5000 });

            var overlayHiddenAfter = await page.EvalOnSelectorAsync<bool>(
                "#tg-violation-overlay", "el => el.classList.contains('tg-hidden')");
            Assert.False(overlayHiddenAfter, "الـ Overlay يجب أن يظهر فورًا بعد رصد الترجمة.");

            // انتظار وصول طلب الشبكة الفعلي لتسجيل المخالفة
            for (var i = 0; i < 30 && reportViolationMethod == null; i++)
                await Task.Delay(100);

            Assert.Equal("POST", reportViolationMethod);

            // تعطّل عناصر التفاعل (باستثناء الـ Overlay نفسه)
            var disabledCount = await page.EvalOnSelectorAllAsync<int>(
                "input, button, select, textarea",
                "els => els.filter(e => e.closest('#tg-violation-overlay') === null).filter(e => e.disabled).length");
            var totalInteractive = await page.EvalOnSelectorAllAsync<int>(
                "input, button, select, textarea",
                "els => els.filter(e => e.closest('#tg-violation-overlay') === null).length");
            if (totalInteractive > 0)
                Assert.Equal(totalInteractive, disabledCount);

            // انتظار وصول الصف فعليًا لقاعدة البيانات (لا يكفي فقط استلام الطلب على مستوى الشبكة)
            // — يمنح هامشًا أكبر تحت تحميل موازٍ (تشغيل حزمة الاختبارات بالكامل بالتوازي).
            var logged = false;
            for (var i = 0; i < 50 && !logged; i++)
            {
                await using var pollDb = CreateDb();
                logged = await pollDb.IntegrityViolationLogs
                    .AnyAsync(v => v.AttemptEntityId == _examAssignmentId && v.ViolationType == "BrowserTranslate");
                if (!logged) await Task.Delay(200);
            }
            Assert.True(logged, "يجب أن يُسجَّل سطر IntegrityViolationLog فعليًا لهذه المحاولة.");

            await context.CloseAsync();
        }
    }
}
