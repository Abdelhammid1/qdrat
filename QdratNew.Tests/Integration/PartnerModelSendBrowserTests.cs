using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using QdratNew.Entities;
using QdratNew.Enums;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // PPM-S4 — صفحة إرسال واجب من نموذج بمتصفح حقيقي على 375px: الدفعات والاستبعاد ونافذة الاستبدال
    // وإلغاء صلاحية المعاينة بعد التغيير، ثم الإرسال والوسم في الواجبات المرسلة. المنفذ 5306.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public sealed class PartnerModelSendBrowserTests : IAsyncLifetime
    {
        private const string Marker = "PPMS4E2E";
        private const string UserName = "ppms4e2e-partner";
        private const string ModelTitle = "PPMS4E2E نموذج واجب";
        private const string ExamModelTitle = "PPMS4E2E نموذج اختبار";

        private RealServerFixture? _server;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private int _partnerId;
        private int _modelId;
        private int _examModelId;
        private int _batchId;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            await CleanupAsync();
            await SeedAsync();

            _server = await RealServerFixture.StartAsync(5306);
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }

        public async Task DisposeAsync()
        {
            if (_browser != null) await _browser.DisposeAsync();
            _playwright?.Dispose();
            if (_server != null) await _server.DisposeAsync();
            if (RtkRealDb.IsConfigured) await CleanupAsync();
        }

        private async Task SeedAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            var today = DateTime.Today;

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = UserName,
                NormalizedUserName = UserName.ToUpperInvariant(),
                Email = $"{UserName}@test.local",
                NormalizedEmail = $"{UserName}@TEST.LOCAL",
                FullName = UserName,
                NationalID = Marker,
                SecurityStamp = Guid.NewGuid().ToString(),
                IsActive = true,
                EmailConfirmed = true
            };
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, RtkRealDb.Password);
            db.Users.Add(user);

            var roleId = await db.Roles.Where(r => r.Name == "Partner").Select(r => r.Id).FirstAsync();
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleId });

            var partner = new Partner { Name = $"{Marker} شريك", Code = Marker, PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var branch = new Branch { Name = $"{Marker} فرع", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(partner, branch, course, curriculum, unit);
            await db.SaveChangesAsync();

            db.UserPartners.Add(new UserPartner { UserId = user.Id, PartnerId = partner.Id });

            var contract = new PartnerSubscription
            {
                PartnerId = partner.Id,
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(30),
                CanUseProfessionalModels = true,
                CanCreateHomework = true,
                SubscriptionCourses = new List<PartnerSubscriptionCourse> { new() { CourseId = course.Id, CanUsePlatformQuestionBank = true } }
            };
            db.PartnerSubscriptions.Add(contract);
            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            db.CourseCurriculums.Add(new CourseCurriculum { CourseId = course.Id, CurriculumId = curriculum.Id });
            await db.SaveChangesAsync();

            db.PartnerSubscriptionPeriods.Add(new PartnerSubscriptionPeriod { PartnerSubscriptionId = contract.Id, StartDate = today.AddDays(-1), EndDate = today.AddDays(30) });

            var lesson = new Lesson { Title = $"{Marker} درس", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();

            Question NewQuestion(string suffix) => new()
            {
                Id = Guid.NewGuid(),
                Title = $"{Marker} نص {suffix}",
                ReferenceNumber = $"{Marker}{suffix}",
                CorrectAnswer = "أ",
                CurriculumId = curriculum.Id,
                SectionId = section.Id,
                LessonId = lesson.Id,
                IsReviewed = true,
                IsComplete = true
            };

            var questions = new[] { NewQuestion("سؤال1"), NewQuestion("سؤال2"), NewQuestion("سؤال3") };
            db.Questions.AddRange(questions);
            db.Questions.Add(NewQuestion("بديل"));

            var model = new ProfessionalModel
            {
                Title = ModelTitle,
                Description = "وصف قصير",
                CreatedBy = "-",
                CreatedAt = DateTime.UtcNow,
                IsForPartners = true,
                IsGlobal = true,
                ModelType = ProfessionalModelType.Homework,
                CurriculumId = curriculum.Id
            };
            var examModel = new ProfessionalModel
            {
                Title = ExamModelTitle,
                Description = "وصف قصير",
                CreatedBy = "-",
                CreatedAt = DateTime.UtcNow,
                IsForPartners = true,
                IsGlobal = true,
                ModelType = ProfessionalModelType.Exam,
                CurriculumId = curriculum.Id
            };
            db.ProfessionalModels.AddRange(model, examModel);

            var batch = new Batch { Name = $"{Marker} دفعة", BranchId = branch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            db.Batches.Add(batch);
            var student = new Student
            {
                NationalID = $"{Marker}1",
                FullName = $"{Marker} طالب",
                Gender = "ذكر",
                Age = 17,
                School = "-",
                Level = "-",
                Branch = branch,
                IsActiveForLearning = true
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id });
            db.ProfessionalModelQuestions.AddRange(questions.Select((q, i) => new ProfessionalModelQuestion { ModelId = model.Id, QuestionId = q.Id, OrderNumber = i + 1 }));
            db.ProfessionalModelQuestions.AddRange(questions.Select((q, i) => new ProfessionalModelQuestion { ModelId = examModel.Id, QuestionId = q.Id, OrderNumber = i + 1 }));
            await db.SaveChangesAsync();

            _partnerId = partner.Id;
            _modelId = model.Id;
            _examModelId = examModel.Id;
            _batchId = batch.Id;
        }

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPMS4E2E%')";
            const string sets = "(SELECT Id FROM HomeworkSets WHERE BatchId IN " + batches + ")";
            const string assignments = "(SELECT Id FROM ExamAssignmentsToBatches WHERE BatchId IN " + batches + ")";
            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS4E2E%')";
            const string models = "(SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPMS4E2E%')";
            const string partners = "(SELECT Id FROM Partners WHERE Code = 'PPMS4E2E')";
            const string subscriptions = "(SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN " + partners + ")";
            const string users = "(SELECT Id FROM AspNetUsers WHERE UserName LIKE 'ppms4e2e-%')";

            foreach (var sql in new[]
            {
                "DELETE FROM Notifications WHERE StudentID IN (SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS4E2E%')",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPMS4E2E%'",
                $"DELETE FROM ExamStudentQuestionOrders WHERE ExamAssignmentId IN {assignments}",
                $"DELETE FROM ExamStudentStatuses WHERE StudentId IN {students}",
                $"DELETE FROM ExamAssignments WHERE StudentId IN {students}",
                $"DELETE FROM ExamQuestions WHERE ExamAssignmentId IN {assignments}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE BatchId IN {batches}",
                $"DELETE FROM Exams WHERE SourceProfessionalModelId IN {models}",
                $"DELETE FROM Homeworks WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSetStudents WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSets WHERE BatchId IN {batches}",
                $"DELETE FROM ProfessionalModelQuestions WHERE ModelId IN {models}",
                $"DELETE FROM ProfessionalModels WHERE Id IN {models}",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPMS4E2E%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPMS4E2E%'",
                "DELETE FROM Units WHERE Title LIKE 'PPMS4E2E%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPMS4E2E%'",
                "DELETE FROM CourseCurriculums WHERE CurriculumId IN (SELECT Id FROM Curriculums WHERE Title LIKE 'PPMS4E2E%')",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPMS4E2E%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPMS4E2E%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPMS4E2E%'",
                $"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptions WHERE Id IN {subscriptions}",
                $"DELETE FROM UserPartners WHERE PartnerId IN {partners}",
                "DELETE FROM Branches WHERE Name LIKE 'PPMS4E2E%'",
                "DELETE FROM Partners WHERE Code = 'PPMS4E2E'",
                "DELETE FROM Courses WHERE Name LIKE 'PPMS4E2E%'",
                $"DELETE FROM AspNetUserRoles WHERE UserId IN {users}",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'ppms4e2e-%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        [Fact]
        public async Task Partner_PicksBatch_ExcludesAndReplaces_PreviewsThenSends_On375px()
        {
            if (!RtkRealDb.IsConfigured) return;

            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = _server!.BaseUrl,
                ViewportSize = new ViewportSize { Width = 375, Height = 812 },
                IsMobile = true,
                HasTouch = true
            });
            var page = await context.NewPageAsync();
            var pageErrors = new List<string>();

            await page.GotoAsync("/LMS/login");
            await page.FillAsync("#Input_Email", UserName);
            await page.FillAsync("#passwordField", RtkRealDb.Password);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });
            await page.GotoAsync("/Partner/PartnerContext/Select");
            await page.SelectOptionAsync("select[name=partnerId]", _partnerId.ToString());
            await page.ClickAsync("form button[type=submit]");
            await page.WaitForLoadStateAsync();

            page.PageError += (_, message) => pageErrors.Add(message);

            async Task AssertNoOverflowAsync(string name)
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                var overflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1");
                Assert.False(overflow, $"تمرير أفقي في {name}: {page.Url}");
            }

            await page.GotoAsync("/Partner/ProfessionalModels");
            await AssertNoOverflowAsync("القائمة");
            await page.ClickAsync($"a[href$='/SendHomework/{_modelId}']");
            await page.WaitForURLAsync(url => url.Contains("/SendHomework/"));
            await AssertNoOverflowAsync("الإرسال");

            Assert.Contains("٠", await page.InnerTextAsync("#selectedBatchCount"));
            await page.CheckAsync($"input[name=BatchIds][value='{_batchId}']");
            Assert.Contains("١", await page.InnerTextAsync("#selectedBatchCount"));

            // الاستبعاد يحدّث العدّاد
            var questions = page.Locator(".pms-question");
            await questions.Nth(1).Locator("input[name=ExcludedQuestionIds]").CheckAsync();
            Assert.Equal("٢", await page.InnerTextAsync("#finalQuestionCount"));
            Assert.True(await questions.Nth(1).Locator(".pms-excluded-tag").IsVisibleAsync());

            // نافذة الاستبدال: تحميل ثم اختيار، والنص يظهر في بطاقة السؤال
            await questions.Nth(0).Locator("[data-replace]").ClickAsync();
            var candidate = page.Locator("#replacementModal [data-choose-candidate]").First;
            await candidate.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            Assert.Contains($"{Marker} نص بديل", await page.InnerTextAsync("#replacementModal .modal-body"));
            await candidate.ClickAsync();
            await page.Locator("#replacementModal").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
            Assert.Contains($"{Marker} نص بديل", await questions.Nth(0).Locator(".pms-substitute").InnerTextAsync());

            // المعاينة بلا إرسال، وتُبقي الاختيارات
            await page.ClickAsync("button[value=preview]");
            await page.Locator("#sendReview").WaitForAsync();
            await AssertNoOverflowAsync("المعاينة");
            Assert.True(await page.IsCheckedAsync($"input[name=BatchIds][value='{_batchId}']"));
            Assert.Contains($"{Marker} نص بديل", await page.Locator(".pms-question").Nth(0).Locator(".pms-substitute").InnerTextAsync());
            Assert.Equal("٢", await page.InnerTextAsync("#finalQuestionCount"));
            Assert.True(await page.IsEnabledAsync("#sendButton"));

            // أي تغيير بعد المعاينة يوقف الإرسال حتى إعادة المعاينة
            await page.FillAsync("#Title", $"{Marker} عنوان معدل");
            Assert.False(await page.IsEnabledAsync("#sendButton"));
            Assert.True(await page.IsVisibleAsync("#stalePreview"));

            await page.ClickAsync("button[value=preview]");
            await page.Locator("#sendReview").WaitForAsync();
            Assert.True(await page.IsEnabledAsync("#sendButton"));

            await page.ClickAsync("#sendButton");
            await page.WaitForURLAsync(url => url.Contains("/SentHomeworks"));

            await AssertNoOverflowAsync("الواجبات المرسلة");

            var sent = await page.InnerTextAsync("body");
            Assert.Contains($"{Marker} عنوان معدل", sent);
            Assert.Contains($"من نموذج: {ModelTitle}", sent);

            await using var db = RtkRealDb.CreateDb();
            var questionTitles = await db.Homeworks.AsNoTracking()
                .Where(h => h.HomeworkSet.SourceProfessionalModelId == _modelId)
                .Select(h => h.Question.Title)
                .OrderBy(t => t)
                .ToListAsync();
            Assert.Equal(new[] { $"{Marker} نص بديل", $"{Marker} نص سؤال3" }, questionTitles);

            Assert.Empty(pageErrors);
        }

        // PPM-S5.2: نفس النموذج المشترك بمدة الاختبار، من الرابط في السايدبار إلى الاختبارات المرسلة
        [Fact]
        public async Task Partner_SendsExamFromModel_ReplacesAndPreviews_On375px()
        {
            if (!RtkRealDb.IsConfigured) return;

            var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
            {
                BaseURL = _server!.BaseUrl,
                ViewportSize = new ViewportSize { Width = 375, Height = 812 },
                IsMobile = true,
                HasTouch = true
            });
            var page = await context.NewPageAsync();
            var pageErrors = new List<string>();

            await page.GotoAsync("/LMS/login");
            await page.FillAsync("#Input_Email", UserName);
            await page.FillAsync("#passwordField", RtkRealDb.Password);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });
            await page.GotoAsync("/Partner/PartnerContext/Select");
            await page.SelectOptionAsync("select[name=partnerId]", _partnerId.ToString());
            await page.ClickAsync("form button[type=submit]");
            await page.WaitForLoadStateAsync();

            page.PageError += (_, message) => pageErrors.Add(message);

            async Task AssertNoOverflowAsync(string name)
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                var overflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1");
                Assert.False(overflow, $"تمرير أفقي في {name}: {page.Url}");
            }

            await page.GotoAsync("/Partner/ProfessionalModels/Exams");
            await AssertNoOverflowAsync("قائمة نماذج الاختبار");
            await page.ClickAsync($"a[href$='/SendExam/{_examModelId}']");
            await page.WaitForURLAsync(url => url.Contains("/SendExam/"));
            await AssertNoOverflowAsync("إرسال الاختبار");

            Assert.Equal("60", await page.InputValueAsync("#DurationMinutes"));
            await page.FillAsync("#DurationMinutes", "35");
            await page.CheckAsync($"input[name=BatchIds][value='{_batchId}']");

            var questions = page.Locator(".pms-question");
            await questions.Nth(0).Locator("[data-replace]").ClickAsync();
            var candidate = page.Locator("#replacementModal [data-choose-candidate]").First;
            await candidate.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
            await candidate.ClickAsync();
            await page.Locator("#replacementModal").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });

            await page.ClickAsync("button[value=preview]");
            await page.Locator("#sendReview").WaitForAsync();
            await AssertNoOverflowAsync("معاينة الاختبار");
            Assert.Equal("35", await page.InputValueAsync("#DurationMinutes"));
            Assert.Contains("إرسال الاختبار", await page.InnerTextAsync("#sendButton"));

            // تغيير المدة بعد المعاينة يوقف الإرسال حتى إعادتها
            await page.FillAsync("#DurationMinutes", "40");
            Assert.False(await page.IsEnabledAsync("#sendButton"));

            await page.ClickAsync("button[value=preview]");
            await page.Locator("#sendReview").WaitForAsync();

            await page.ClickAsync("#sendButton");
            await page.WaitForURLAsync(url => url.Contains("/PartnerExam/SentExams"));
            await AssertNoOverflowAsync("الاختبارات المرسلة");

            var sent = await page.InnerTextAsync("body");
            Assert.Contains($"من نموذج: {ExamModelTitle}", sent);
            Assert.Contains("تم إرسال الاختبار", sent);

            await using var db = RtkRealDb.CreateDb();
            var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.SourceProfessionalModelId == _examModelId);
            Assert.Equal(_partnerId, exam.OwnerPartnerId);
            Assert.Equal(40, exam.DurationMinutes);
            Assert.Contains(
                $"{Marker} نص بديل",
                await db.ExamQuestions.AsNoTracking().Where(q => q.ExamId == exam.Id).Select(q => q.Question.Title).ToListAsync());

            Assert.Empty(pageErrors);
        }
    }
}
