using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using QdratNew.Entities;
using QdratNew.Enums;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // PPM-S6.3 — الرحلة كاملة بمتصفح حقيقي: الأدمن يتيح نموذجين لشريك واحد ويرسل من شاشتيه،
    // والشريك يرسل واجبًا واختبارًا وتحديد مستوى، والطالب يرى كل ذلك، وشريك آخر لا يرى النموذجين. المنفذ 5307.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public sealed class PartnerModelsJourneyBrowserTests : IAsyncLifetime
    {
        private const string Marker = "PPMS6E2E";
        private const string OwnerUser = "ppms6e2e-owner";
        private const string PartnerUser = "ppms6e2e-partner";
        private const string OtherPartnerUser = "ppms6e2e-other";
        private const string StudentUser = "ppms6e2e-student";
        private const string HomeworkModelTitle = "PPMS6E2E نموذج واجب";
        private const string ExamModelTitle = "PPMS6E2E نموذج اختبار";

        private RealServerFixture? _server;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private int _partnerId;
        private int _otherPartnerId;
        private int _courseId;
        private int _batchId;
        private int _instituteBatchId;
        private int _homeworkModelId;
        private int _examModelId;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            await CleanupAsync();
            await SeedAsync();

            _server = await RealServerFixture.StartAsync(5307);
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

        private static ApplicationUser NewUser(string userName)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = userName,
                NormalizedUserName = userName.ToUpperInvariant(),
                Email = $"{userName}@test.local",
                NormalizedEmail = $"{userName}@TEST.LOCAL",
                FullName = userName,
                NationalID = userName,
                SecurityStamp = Guid.NewGuid().ToString(),
                IsActive = true,
                EmailConfirmed = true
            };
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, RtkRealDb.Password);
            return user;
        }

        private async Task SeedAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            var today = DateTime.Today;

            var owner = NewUser(OwnerUser);
            var partnerUser = NewUser(PartnerUser);
            var otherUser = NewUser(OtherPartnerUser);
            var studentUser = NewUser(StudentUser);
            db.Users.AddRange(owner, partnerUser, otherUser, studentUser);

            async Task<string> RoleAsync(string name) => await db.Roles.Where(r => r.Name == name).Select(r => r.Id).FirstAsync();
            db.UserRoles.AddRange(
                new IdentityUserRole<string> { UserId = owner.Id, RoleId = await RoleAsync("Owner") },
                new IdentityUserRole<string> { UserId = partnerUser.Id, RoleId = await RoleAsync("Partner") },
                new IdentityUserRole<string> { UserId = otherUser.Id, RoleId = await RoleAsync("Partner") },
                new IdentityUserRole<string> { UserId = studentUser.Id, RoleId = await RoleAsync("Student") });

            Partner NewPartner(string suffix) => new() { Name = $"{Marker} شريك {suffix}", Code = $"{Marker}{suffix}", PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };

            var partner = NewPartner("A");
            var other = NewPartner("B");
            var branch = new Branch { Name = $"{Marker} فرع", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };
            var otherBranch = new Branch { Name = $"{Marker} فرع آخر", Partner = other, Location = "-", City = "-", State = "-", Country = "-" };
            var instituteBranch = new Branch { Name = $"{Marker} فرع المعهد", Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(partner, other, branch, otherBranch, instituteBranch, course, curriculum, unit);
            await db.SaveChangesAsync();

            db.UserPartners.AddRange(
                new UserPartner { UserId = partnerUser.Id, PartnerId = partner.Id },
                new UserPartner { UserId = otherUser.Id, PartnerId = other.Id });

            PartnerSubscription NewContract(int partnerId) => new()
            {
                PartnerId = partnerId,
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(30),
                CanUseProfessionalModels = true,
                CanCreateHomework = true,
                CanCreateExams = true,
                CanUsePlacementExams = true,
                SubscriptionCourses = new List<PartnerSubscriptionCourse> { new() { CourseId = course.Id, CanUsePlatformQuestionBank = true } }
            };

            var contract = NewContract(partner.Id);
            var otherContract = NewContract(other.Id);
            db.PartnerSubscriptions.AddRange(contract, otherContract);

            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            db.CourseCurriculums.Add(new CourseCurriculum { CourseId = course.Id, CurriculumId = curriculum.Id });
            await db.SaveChangesAsync();

            db.PartnerSubscriptionPeriods.AddRange(
                new PartnerSubscriptionPeriod { PartnerSubscriptionId = contract.Id, StartDate = today.AddDays(-1), EndDate = today.AddDays(30) },
                new PartnerSubscriptionPeriod { PartnerSubscriptionId = otherContract.Id, StartDate = today.AddDays(-1), EndDate = today.AddDays(30) });

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
                IsComplete = true,
                Options = new List<QuestionOption> { new() { Text = "أ" }, new() { Text = "ب" } }
            };

            var questions = new[] { NewQuestion("سؤال1"), NewQuestion("سؤال2") };
            db.Questions.AddRange(questions);

            // النموذجان داخليان في البداية: الأدمن يتيحهما من الواجهة
            ProfessionalModel NewModel(string title, ProfessionalModelType type) => new()
            {
                Title = title,
                Description = "-",
                CreatedBy = "-",
                CreatedAt = DateTime.UtcNow,
                ModelType = type,
                CurriculumId = curriculum.Id
            };

            var homeworkModel = NewModel(HomeworkModelTitle, ProfessionalModelType.Homework);
            var examModel = NewModel(ExamModelTitle, ProfessionalModelType.Exam);
            db.ProfessionalModels.AddRange(homeworkModel, examModel);

            var batch = new Batch { Name = $"{Marker} دفعة", BranchId = branch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            var instituteBatch = new Batch { Name = $"{Marker} دفعة المعهد", BranchId = instituteBranch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            db.Batches.AddRange(batch, instituteBatch);

            var student = new Student
            {
                NationalID = $"{Marker}1",
                FullName = $"{Marker} طالب",
                Gender = "ذكر",
                Age = 17,
                School = "-",
                Level = "-",
                UserId = studentUser.Id,
                Branch = branch,
                IsActiveForLearning = true
            };
            var instituteStudent = new Student
            {
                NationalID = $"{Marker}2",
                FullName = $"{Marker} طالب المعهد",
                Gender = "ذكر",
                Age = 17,
                School = "-",
                Level = "-",
                Branch = instituteBranch,
                IsActiveForLearning = true
            };
            db.Students.AddRange(student, instituteStudent);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id },
                new StudentBatchEnrollment { StudentID = instituteStudent.StudentID, BatchId = instituteBatch.Id });

            foreach (var model in new[] { homeworkModel, examModel })
            {
                db.ProfessionalModelQuestions.AddRange(questions.Select((q, i) => new ProfessionalModelQuestion { ModelId = model.Id, QuestionId = q.Id, OrderNumber = i + 1 }));
            }

            await db.SaveChangesAsync();

            _partnerId = partner.Id;
            _otherPartnerId = other.Id;
            _courseId = course.Id;
            _batchId = batch.Id;
            _instituteBatchId = instituteBatch.Id;
            _homeworkModelId = homeworkModel.Id;
            _examModelId = examModel.Id;
        }

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPMS6E2E%')";
            const string sets = "(SELECT Id FROM HomeworkSets WHERE BatchId IN " + batches + ")";
            const string assignments = "(SELECT Id FROM ExamAssignmentsToBatches WHERE BatchId IN " + batches + ")";
            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS6E2E%')";
            const string models = "(SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPMS6E2E%')";
            const string partners = "(SELECT Id FROM Partners WHERE Code LIKE 'PPMS6E2E%')";
            const string subscriptions = "(SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN " + partners + ")";
            const string users = "(SELECT Id FROM AspNetUsers WHERE UserName LIKE 'ppms6e2e-%')";
            const string questions = "(SELECT Id FROM Questions WHERE ReferenceNumber LIKE 'PPMS6E2E%')";

            const string performanceExams = "(SELECT Id FROM PerformanceIndicatorExams WHERE Title LIKE 'PPMS6E2E%')";

            foreach (var sql in new[]
            {
                $"DELETE FROM PartnerDispatchLogs WHERE PartnerId IN {partners}",
                $"DELETE FROM PerformanceIndicatorExamStudents WHERE PerformanceIndicatorExamId IN {performanceExams}",
                $"DELETE FROM PerformanceIndicatorExamQuestions WHERE PerformanceIndicatorExamId IN {performanceExams}",
                $"DELETE FROM PerformanceIndicatorExamSections WHERE PerformanceIndicatorExamId IN {performanceExams}",
                $"DELETE FROM PerformanceIndicatorExamToBatch WHERE PerformanceIndicatorExamId IN {performanceExams}",
                "DELETE FROM PerformanceIndicatorExams WHERE Title LIKE 'PPMS6E2E%'",
                $"DELETE FROM Notifications WHERE StudentID IN {students}",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPMS6E2E%'",
                $"DELETE FROM ExamStudentQuestionOrders WHERE ExamAssignmentId IN {assignments}",
                $"DELETE FROM ExamStudentStatuses WHERE StudentId IN {students}",
                $"DELETE FROM ExamAssignments WHERE StudentId IN {students}",
                $"DELETE FROM ExamQuestions WHERE QuestionId IN {questions}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE BatchId IN {batches}",
                $"DELETE FROM Exams WHERE SourceProfessionalModelId IN {models} OR OwnerPartnerId IN {partners}",
                $"DELETE FROM Homeworks WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSetStudents WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSets WHERE BatchId IN {batches}",
                $"DELETE FROM ProfessionalModelPartners WHERE ProfessionalModelId IN {models}",
                $"DELETE FROM ProfessionalModelQuestions WHERE ModelId IN {models}",
                $"DELETE FROM ProfessionalModels WHERE Id IN {models}",
                $"DELETE FROM QuestionOptions WHERE QuestionId IN {questions}",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPMS6E2E%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPMS6E2E%'",
                "DELETE FROM Units WHERE Title LIKE 'PPMS6E2E%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPMS6E2E%'",
                "DELETE FROM CourseCurriculums WHERE CurriculumId IN (SELECT Id FROM Curriculums WHERE Title LIKE 'PPMS6E2E%')",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPMS6E2E%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPMS6E2E%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPMS6E2E%'",
                $"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptions WHERE Id IN {subscriptions}",
                $"DELETE FROM UserPartners WHERE PartnerId IN {partners}",
                "DELETE FROM Branches WHERE Name LIKE 'PPMS6E2E%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPMS6E2E%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPMS6E2E%'",
                $"DELETE FROM AspNetUserRoles WHERE UserId IN {users}",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'ppms6e2e-%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        private async Task<IPage> LoginAsync(string userName, bool mobile = true)
        {
            var options = new BrowserNewContextOptions { BaseURL = _server!.BaseUrl, IgnoreHTTPSErrors = true };
            if (mobile)
            {
                options.ViewportSize = new ViewportSize { Width = 375, Height = 812 };
                options.IsMobile = true;
                options.HasTouch = true;
            }

            var context = await _browser!.NewContextAsync(options);
            var page = await context.NewPageAsync();

            await page.GotoAsync("/LMS/login");
            await page.FillAsync("#Input_Email", userName);
            await page.FillAsync("#passwordField", RtkRealDb.Password);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });
            return page;
        }

        private async Task<IPage> LoginPartnerAsync(string userName, int partnerId)
        {
            var page = await LoginAsync(userName);
            await page.GotoAsync("/Partner/PartnerContext/Select");
            await page.SelectOptionAsync("select[name=partnerId]", partnerId.ToString());
            await page.ClickAsync("form button[type=submit]");
            await page.WaitForLoadStateAsync();
            return page;
        }

        private static string SaudiLocal(int days)
            => DateTime.UtcNow.AddHours(3).AddDays(days).ToString("yyyy-MM-ddTHH:mm");

        // الأدمن يوجّه النموذج للشريك A فقط من شاشة التعديل
        private async Task ShareWithPartnerAsync(IPage admin, int modelId)
        {
            await admin.GotoAsync($"/Admin/ProfessionalModels/Edit/{modelId}");
            await admin.Locator("#IsForPartners").CheckAsync();
            await admin.Locator("#PartnerAudienceSelected").CheckAsync();
            await admin.Locator($"input[name=SelectedPartnerIds][value='{_partnerId}']").CheckAsync();
            await admin.Locator("form button[type=submit]").Last.ClickAsync();
            await admin.WaitForURLAsync(url => !url.Contains("/Edit/"), new PageWaitForURLOptions { Timeout = 20000 });
        }

        // APD-S2.2: اختيار «المدارس الشريكة» بمتصفح حقيقي: الـ JS يفعّل الدفعات، والإرسال يظهر للشريك ويختفي من الأدمن
        [SqlServerFact]
        public async Task Admin_SendsToSchools_ResultPage_PartnerSeesInstituteBadge_AdminDoesNot()
        {
            var admin = await LoginAsync(OwnerUser, mobile: false);
            await ShareWithPartnerAsync(admin, _homeworkModelId);
            var pageErrors = new List<string>();
            admin.PageError += (_, message) => pageErrors.Add(message);

            var title = $"{Marker} واجب للمدارس";
            await admin.GotoAsync("/Admin/AdminLessonCompletions/CreateModelHomework");

            // دفعة الشريك ليست ضمن دفعات المنصة
            Assert.Equal(0, await admin.Locator($"#apd-platform-{_batchId}").CountAsync());

            await admin.FillAsync("#Title", title);
            await admin.CheckAsync($"input[name=Audience][value='{(int)DispatchAudience.Partners}']");

            // قبل اختيار النموذج: لا تحميل، بل تنبيه
            await admin.Locator("[data-apd-need-model]").WaitForAsync();

            await admin.SelectOptionAsync("#ModelIdSelect", _homeworkModelId.ToString());
            await admin.Locator($"#apd-partner-{_partnerId}").WaitForAsync();
            Assert.True(await admin.Locator("#lectureSection").IsHiddenAsync());
            Assert.True(await admin.Locator($"#apd-batch-{_partnerId}-{_batchId}").IsDisabledAsync());

            await admin.CheckAsync($"#apd-partner-{_partnerId}");
            Assert.True(await admin.Locator($"#apd-batch-{_partnerId}-{_batchId}").IsEnabledAsync());
            Assert.True(await admin.IsCheckedAsync($"#apd-batch-{_partnerId}-{_batchId}"));
            Assert.Equal("1", await admin.InnerTextAsync("[data-apd-count-partners]"));

            await admin.FillAsync("#StartAt", SaudiLocal(0));
            await admin.FillAsync("#EndAt", SaudiLocal(3));
            await admin.ClickAsync("#createModelHomeworkForm button[type=submit]");

            await admin.WaitForURLAsync(url => url.Contains("/PartnerDispatch/Result"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains("نجح", await admin.InnerTextAsync($"[data-apd-result-partner='{_partnerId}']"));

            await admin.GotoAsync("/Admin/AdminLessonCompletions/ModelAssignments");
            Assert.DoesNotContain(title, await admin.InnerTextAsync("body"));
            Assert.Empty(pageErrors);

            int setId;
            await using (var db = RtkRealDb.CreateDb())
            {
                var set = await db.HomeworkSets.AsNoTracking().SingleAsync(h => h.Title == title);
                Assert.Equal(_partnerId, set.OwnerPartnerId);
                setId = set.Id;
                Assert.Equal(1, await db.PartnerDispatchLogs.AsNoTracking().CountAsync(l => l.TargetEntityId == set.Id && l.PartnerId == _partnerId));
            }

            var partner = await LoginPartnerAsync(PartnerUser, _partnerId);
            await partner.GotoAsync("/Partner/SentHomeworks");
            var row = partner.Locator("tr", new PageLocatorOptions { HasTextString = title });
            Assert.Contains("مرسل من المعهد", await row.InnerTextAsync());

            // الشريك يدير ما أصبح ملكه: تعديل العنوان
            await partner.GotoAsync($"/Partner/SentHomeworks/EditSent/{setId}");
            await partner.FillAsync("#Title", $"{title} معدل");
            await partner.ClickAsync("form[action*='EditSent'] button[type=submit]");
            await partner.WaitForLoadStateAsync();
            await partner.GotoAsync("/Partner/SentHomeworks");
            Assert.Contains($"{title} معدل", await partner.InnerTextAsync("body"));

            // ---------- الاختبار: طريقة الإرسال تختفي، والإرسال يشمل كل طلاب الدفعة ----------
            await ShareWithPartnerAsync(admin, _examModelId);
            var examTitle = $"{Marker} اختبار للمدارس";
            await admin.GotoAsync("/Admin/AdminLessonCompletions/CreateModelExam");
            await admin.FillAsync("#Title", examTitle);
            await admin.CheckAsync("#sendToBatchFalse");
            await admin.CheckAsync($"input[name=Audience][value='{(int)DispatchAudience.Partners}']");
            Assert.True(await admin.Locator("#sendModeSection").IsHiddenAsync());
            Assert.True(await admin.IsCheckedAsync("#sendToBatchTrue"));

            await admin.SelectOptionAsync("#ModelIdSelect", _examModelId.ToString());
            await admin.Locator($"#apd-partner-{_partnerId}").WaitForAsync();
            await admin.CheckAsync($"#apd-partner-{_partnerId}");
            await admin.FillAsync("#DurationMinutes", "45");
            await admin.ClickAsync("#createModelExamForm button[type=submit]");

            // نفس أسئلة الواجب أعلاه وصلت الدفعة، فيظهر التحذير ويبقى اختيار المدرسة
            await admin.Locator("#modelSendWarnings").WaitForAsync();
            Assert.True(await admin.IsCheckedAsync($"#apd-partner-{_partnerId}"));
            Assert.True(await admin.Locator("#sendModeSection").IsHiddenAsync());
            await admin.CheckAsync("#confirmModelSend");
            await admin.ClickAsync("#createModelExamForm button[type=submit]");

            await admin.WaitForURLAsync(url => url.Contains("/PartnerDispatch/Result"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains("نجح", await admin.InnerTextAsync($"[data-apd-result-partner='{_partnerId}']"));
            Assert.Empty(pageErrors);

            await partner.GotoAsync("/Partner/PartnerExam/SentExams");
            Assert.Contains("مرسل من المعهد", await partner.Locator("tr", new PageLocatorOptions { HasTextString = examTitle }).InnerTextAsync());
        }

        // APD-S3: تحديد المستوى للمدارس بمتصفح حقيقي: الكورس يحمّل الدفعات، والمسودة تظهر للشريك فيرسلها، والأدمن لا يراها
        [SqlServerFact]
        public async Task Admin_SendsPlacementDraftToSchool_PartnerSendsIt_AdminDoesNotSeeIt()
        {
            var admin = await LoginAsync(OwnerUser, mobile: false);
            await ShareWithPartnerAsync(admin, _examModelId);
            var pageErrors = new List<string>();
            admin.PageError += (_, message) => pageErrors.Add(message);

            await admin.GotoAsync("/Admin/PlacementExams/CreateFromProfessionalModel");

            // دفعة الشريك ليست ضمن دفعات المنصة، ودفعات المنصة اختيار واحد
            Assert.Equal(0, await admin.Locator($"#apd-platform-{_batchId}").CountAsync());
            Assert.Equal("radio", await admin.GetAttributeAsync($"#apd-platform-{_instituteBatchId}", "type"));
            Assert.True(await admin.Locator("#partnerDeliverySection").IsHiddenAsync());

            await admin.CheckAsync($"input[name=Audience][value='{(int)DispatchAudience.Partners}']");
            Assert.True(await admin.Locator("#partnerDeliverySection").IsVisibleAsync());
            await admin.Locator("[data-apd-need-model]").WaitForAsync();

            await admin.SelectOptionAsync("#modelSelect", _examModelId.ToString());
            await admin.Locator("[data-apd-need-course]").WaitForAsync();

            await admin.SelectOptionAsync("#SelectedCourseId", _courseId.ToString());
            await admin.Locator($"#apd-partner-{_partnerId}").WaitForAsync();

            await admin.CheckAsync($"#apd-partner-{_partnerId}");
            Assert.True(await admin.IsCheckedAsync($"#apd-batch-{_partnerId}-{_batchId}"));

            await admin.CheckAsync("#deliveryDraft");
            await admin.FillAsync("#DurationMinutes", "45");
            await admin.ClickAsync("form[action*='CreateFromProfessionalModel'] button[type=submit]");

            await admin.WaitForURLAsync(url => url.Contains("/PartnerDispatch/Result"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains("مسودة", await admin.InnerTextAsync("#partnerDispatchResult"));
            Assert.Empty(pageErrors);

            int examId;
            await using (var db = RtkRealDb.CreateDb())
            {
                var exam = await db.Exams.AsNoTracking().SingleAsync(e =>
                    e.OwnerPartnerId == _partnerId && e.SourceProfessionalModelId == _examModelId && e.Type == ExamType.LevelAssessment);
                examId = exam.Id;
                Assert.False(await db.ExamAssignments.AsNoTracking().AnyAsync(a => a.ExamId == examId));
            }

            await admin.GotoAsync($"/Admin/PlacementExams/BatchExams?batchId={_batchId}");
            Assert.DoesNotContain("اختبار تحديد مستوى", await admin.InnerTextAsync("body"));

            var partner = await LoginPartnerAsync(PartnerUser, _partnerId);
            await partner.GotoAsync($"/Partner/PlacementExams/BatchExams?batchId={_batchId}");
            var row = partner.Locator("tr.pp-exam-row");
            Assert.Contains("من المعهد", await row.InnerTextAsync());
            Assert.Contains("مسودة", await row.InnerTextAsync());

            await partner.GotoAsync($"/Partner/PlacementExams/ConfirmSend?examId={examId}");
            await partner.ClickAsync("form[action*='SendExamToStudents'] button");
            await partner.WaitForURLAsync(url => url.Contains("/PlacementDetails"), new PageWaitForURLOptions { Timeout = 20000 });

            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.True(await db.ExamAssignments.AsNoTracking().AnyAsync(a => a.ExamId == examId));
            }
        }

        // APD-S4: من شاشة الإنشاء حتى صفحة المتابعة لدى المدرسة، واختفاء النسخة عن الأدمن
        [SqlServerFact]
        public async Task Admin_SendsPerformanceExamToSchool_PartnerFollowsIt_AdminDoesNotSeeIt()
        {
            int curriculumId;
            await using (var db = RtkRealDb.CreateDb())
            {
                var contract = await db.PartnerSubscriptions.FirstAsync(s => s.PartnerId == _partnerId);
                contract.CanUsePerformanceIndicatorExams = true;
                await db.SaveChangesAsync();

                curriculumId = await db.CourseCurriculums.AsNoTracking()
                    .Where(c => c.CourseId == _courseId)
                    .Select(c => c.CurriculumId)
                    .FirstAsync();
            }

            var admin = await LoginAsync(OwnerUser, mobile: false);
            var pageErrors = new List<string>();
            admin.PageError += (_, message) => pageErrors.Add(message);

            await admin.GotoAsync("/Admin/PerformanceIndicatorExams/Create");

            Assert.Equal(0, await admin.Locator($"#apd-platform-{_batchId}").CountAsync());
            Assert.Equal(1, await admin.Locator($"#apd-platform-{_instituteBatchId}").CountAsync());

            var title = $"{Marker} مؤشر للمدارس";
            await admin.FillAsync("#ExamTitle", title);
            await admin.CheckAsync($"input[name=Audience][value='{(int)DispatchAudience.Partners}']");
            await admin.Locator("[data-apd-need-curriculum]").WaitForAsync();
            Assert.True(await admin.Locator("#platformStudentsSection").IsHiddenAsync());

            await admin.SelectOptionAsync("#CurriculumId", curriculumId.ToString());
            await admin.Locator($"#apd-partner-{_partnerId}").WaitForAsync();
            await admin.CheckAsync($"#apd-partner-{_partnerId}");
            Assert.True(await admin.IsCheckedAsync($"#apd-batch-{_partnerId}-{_batchId}"));

            await admin.FillAsync("#StartAt", DateTime.Today.ToString("yyyy-MM-ddT08:00"));
            await admin.FillAsync("#EndAt", DateTime.Today.AddDays(7).ToString("yyyy-MM-ddT08:00"));
            await admin.ClickAsync("#createExamSubmit");

            await admin.WaitForURLAsync(url => url.Contains("/PerformanceIndicatorExams/ConfirmSend"), new PageWaitForURLOptions { Timeout = 20000 });

            // الاختيار من شاشة الإنشاء محفوظ، وخيار المنصة غير موجود للقالب
            Assert.Equal(0, await admin.Locator($"input[name=Audience][value='{(int)DispatchAudience.Platform}']").CountAsync());
            Assert.True(await admin.IsCheckedAsync($"#apd-partner-{_partnerId}"));

            await admin.ClickAsync("#partnerTemplateSendForm button[type=submit]");
            await admin.WaitForURLAsync(url => url.Contains("/PartnerDispatch/Result"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains("اختبار مؤشر الأداء", await admin.InnerTextAsync("#partnerDispatchResult"));
            Assert.Empty(pageErrors);

            int copyId;
            await using (var db = RtkRealDb.CreateDb())
            {
                copyId = await db.PerformanceIndicatorExams.AsNoTracking()
                    .Where(e => e.OwnerPartnerId == _partnerId && e.Title == title)
                    .Select(e => e.Id)
                    .SingleAsync();
            }

            await admin.GotoAsync("/Admin/PerformanceIndicatorExams");
            Assert.DoesNotContain(title, await admin.InnerTextAsync("body"));

            var response = await admin.GotoAsync($"/Admin/PerformanceIndicatorExams/Details/{copyId}");
            Assert.Equal(404, response!.Status);

            var partner = await LoginPartnerAsync(PartnerUser, _partnerId);
            await partner.GotoAsync("/Partner/PerformanceIndicatorExams");
            var row = partner.Locator($"tr[data-performance-exam='{copyId}']");
            Assert.Contains("من المعهد", await row.InnerTextAsync());

            await row.Locator("a").First.ClickAsync();
            await partner.WaitForURLAsync(url => url.Contains("/ExamStudents"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains(title, await partner.InnerTextAsync("h1"));

            var other = await LoginPartnerAsync(OtherPartnerUser, _otherPartnerId);
            var otherResponse = await other.GotoAsync($"/Partner/PerformanceIndicatorExams/ExamStudents/{copyId}");
            Assert.NotEqual(200, otherResponse!.Status);
        }

        [SqlServerFact]
        public async Task AdminSharesModel_PartnerSendsAllThree_StudentSeesThem_OtherPartnerDoesNot()
        {
            // ---------- الأدمن يتيح النموذجين ----------
            var admin = await LoginAsync(OwnerUser, mobile: false);
            await ShareWithPartnerAsync(admin, _homeworkModelId);
            await ShareWithPartnerAsync(admin, _examModelId);

            // ---------- شريك آخر لا يرى النموذجين ----------
            var other = await LoginPartnerAsync(OtherPartnerUser, _otherPartnerId);
            await other.GotoAsync("/Partner/ProfessionalModels");
            Assert.DoesNotContain(HomeworkModelTitle, await other.InnerTextAsync("body"));
            await other.GotoAsync("/Partner/ProfessionalModels/Exams");
            Assert.DoesNotContain(ExamModelTitle, await other.InnerTextAsync("body"));
            var denied = await other.GotoAsync($"/Partner/ProfessionalModels/SendHomework/{_homeworkModelId}");
            Assert.Equal(404, denied!.Status);

            // ---------- الشريك يرسل واجبًا ----------
            var partner = await LoginPartnerAsync(PartnerUser, _partnerId);
            var pageErrors = new List<string>();
            partner.PageError += (_, message) => pageErrors.Add(message);

            await partner.GotoAsync($"/Partner/ProfessionalModels/SendHomework/{_homeworkModelId}");
            await partner.FillAsync("#Title", $"{Marker} واجب الشريك");
            await partner.CheckAsync($"input[name=BatchIds][value='{_batchId}']");
            await partner.ClickAsync("button[value=preview]");
            await partner.Locator("#sendReview").WaitForAsync();
            await partner.ClickAsync("#sendButton");
            await partner.WaitForURLAsync(url => url.Contains("/SentHomeworks"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains($"من نموذج: {HomeworkModelTitle}", await partner.InnerTextAsync("body"));

            // ---------- الشريك يرسل اختبارًا ----------
            await partner.GotoAsync($"/Partner/ProfessionalModels/SendExam/{_examModelId}");
            await partner.FillAsync("#Title", $"{Marker} اختبار الشريك");
            await partner.CheckAsync($"input[name=BatchIds][value='{_batchId}']");
            await partner.ClickAsync("button[value=preview]");
            await partner.Locator("#sendReview").WaitForAsync();

            // نفس الأسئلة وصلت الدفعة في الواجب، فالإرسال متوقف حتى التأكيد
            Assert.False(await partner.IsEnabledAsync("#sendButton"));
            await partner.CheckAsync("#confirmWarnings");
            await partner.ClickAsync("#sendButton");
            await partner.WaitForURLAsync(url => url.Contains("/PartnerExam/SentExams"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains($"من نموذج: {ExamModelTitle}", await partner.InnerTextAsync("body"));

            // ---------- الشريك يرسل تحديد مستوى من نفس نموذج الاختبار ----------
            await partner.GotoAsync($"/Partner/PlacementExams/CreateFromProfessionalModel?modelId={_examModelId}");
            Assert.Equal(_examModelId.ToString(), await partner.InputValueAsync("#modelSelect"));
            await partner.SelectOptionAsync("#courseSelect", _courseId.ToString());
            await partner.Locator($"#batchSelect option[value='{_batchId}']").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await partner.SelectOptionAsync("#batchSelect", _batchId.ToString());
            await partner.Locator("#modelSummary a").WaitForAsync();
            await partner.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await partner.ClickAsync("form[action*='CreateFromProfessionalModel'] button[type=submit]");
            await partner.ClickAsync(".swal2-confirm");
            await partner.WaitForURLAsync(url => !url.Contains("CreateFromProfessionalModel"), new PageWaitForURLOptions { Timeout = 20000 });

            Assert.True(pageErrors.Count == 0, "أخطاء JS عند الشريك: " + string.Join(" | ", pageErrors));

            // ---------- الأدمن لا يرى اختبار الشريك ولا تحديد مستواه في قائمته ----------
            await admin.GotoAsync("/Admin/AdminLessonCompletions/ModelAssignments");
            Assert.DoesNotContain($"{Marker} اختبار الشريك", await admin.InnerTextAsync("body"));

            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.Equal(1, await db.HomeworkSets.AsNoTracking().CountAsync(h => h.SourceProfessionalModelId == _homeworkModelId));
                var owned = await db.Exams.AsNoTracking()
                    .Where(e => e.SourceProfessionalModelId == _examModelId)
                    .Select(e => new { e.Type, e.OwnerPartnerId })
                    .ToListAsync();
                Assert.Equal(2, owned.Count);
                Assert.All(owned, e => Assert.Equal(_partnerId, e.OwnerPartnerId));
                Assert.Contains(owned, e => e.Type == ExamType.Manual);
                Assert.Contains(owned, e => e.Type == ExamType.LevelAssessment);
            }

            // ---------- الطالب يرى الثلاثة ----------
            var student = await LoginAsync(StudentUser);

            await student.GotoAsync("/Students/StudentHomeworkDashboard/AllHomeworks");
            Assert.Contains(HomeworkModelTitle, await student.InnerTextAsync("body")); // القائمة تعرض CompletionTitle

            await student.GotoAsync("/Students/StudentExamPages/RequiredExams");
            Assert.Contains($"{Marker} اختبار الشريك", await student.InnerTextAsync("body"));

            await student.GotoAsync("/Students/PlacementExams/PlacementExamDashboard");
            Assert.True(await student.Locator("a[href*='StartPlacementExam']").CountAsync() > 0);
        }

        // S6.1 + S6.2: شاشتا الأدمن: إرسال ثم تحذير التكرار مع بقاء الاختيارات حتى التأكيد
        [SqlServerFact]
        public async Task Admin_SendsFromBothScreens_RepeatShowsWarning_AndKeepsChoices_UntilConfirmed()
        {
            var admin = await LoginAsync(OwnerUser, mobile: false);
            var pageErrors = new List<string>();
            admin.PageError += (_, message) => pageErrors.Add(message);

            // ---------- الواجب ----------
            await admin.GotoAsync("/Admin/AdminLessonCompletions/CreateModelHomework");
            await admin.FillAsync("#Title", $"{Marker} واجب الأدمن");
            await admin.SelectOptionAsync("#ModelIdSelect", _homeworkModelId.ToString());
            await admin.CheckAsync($"#apd-platform-{_instituteBatchId}");
            await admin.FillAsync("#StartAt", SaudiLocal(0));
            await admin.FillAsync("#EndAt", SaudiLocal(3));
            await admin.ClickAsync("#createModelHomeworkForm button[type=submit]");
            await admin.WaitForURLAsync(url => url.Contains("/ModelAssignments"), new PageWaitForURLOptions { Timeout = 20000 });

            await using (var db = RtkRealDb.CreateDb())
            {
                await db.HomeworkSets.Where(h => h.SourceProfessionalModelId == _homeworkModelId)
                    .ExecuteUpdateAsync(x => x.SetProperty(h => h.CreatedAt, DateTime.UtcNow.AddDays(-1)));
            }

            await admin.GotoAsync("/Admin/AdminLessonCompletions/CreateModelHomework");
            await admin.FillAsync("#Title", $"{Marker} واجب الأدمن ثانيًا");
            await admin.SelectOptionAsync("#ModelIdSelect", _homeworkModelId.ToString());
            await admin.CheckAsync($"#apd-platform-{_instituteBatchId}");
            await admin.ClickAsync("#createModelHomeworkForm button[type=submit]");

            await admin.Locator("#modelSendWarnings").WaitForAsync();
            Assert.Contains($"{Marker} دفعة المعهد", await admin.InnerTextAsync("#modelSendWarnings"));
            Assert.Equal($"{Marker} واجب الأدمن ثانيًا", await admin.InputValueAsync("#Title"));
            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.Equal(1, await db.HomeworkSets.AsNoTracking().CountAsync(h => h.SourceProfessionalModelId == _homeworkModelId));
            }

            await admin.CheckAsync("#confirmModelSend");
            await admin.ClickAsync("#createModelHomeworkForm button[type=submit]");
            await admin.WaitForURLAsync(url => url.Contains("/ModelAssignments"), new PageWaitForURLOptions { Timeout = 20000 });

            // ---------- الاختبار: الوضع الافتراضي كل الدفعة، ثم طلاب محددون، والمدة تبقى عند التحذير ----------
            await admin.GotoAsync("/Admin/AdminLessonCompletions/CreateModelExam");
            Assert.True(await admin.IsCheckedAsync("#sendToBatchTrue"));
            Assert.Equal("30", await admin.InputValueAsync("#DurationMinutes"));

            await admin.SelectOptionAsync("#ModelIdSelect", _examModelId.ToString());
            await admin.CheckAsync($"#apd-platform-{_instituteBatchId}");
            await admin.FillAsync("#DurationMinutes", "45");
            await admin.ClickAsync("#createModelExamForm button[type=submit]");

            // نفس الأسئلة وصلت الدفعة في الواجب أعلاه، والخدمة تحسب الواجبات والاختبارات معًا
            await admin.Locator("#modelSendWarnings").WaitForAsync();
            Assert.Equal("45", await admin.InputValueAsync("#DurationMinutes"));
            await admin.CheckAsync("#confirmModelSend");
            await admin.ClickAsync("#createModelExamForm button[type=submit]");
            await admin.WaitForURLAsync(url => url.Contains("/ModelAssignments"), new PageWaitForURLOptions { Timeout = 20000 });

            await using (var db = RtkRealDb.CreateDb())
            {
                var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.SourceProfessionalModelId == _examModelId);
                Assert.Null(exam.OwnerPartnerId);
                Assert.Equal(45, exam.DurationMinutes);
                Assert.Equal(1, await db.ExamStudentStatuses.AsNoTracking().CountAsync(s => s.ExamId == exam.Id));

                await db.ExamAssignmentsToBatches.Where(a => a.ExamId == exam.Id)
                    .ExecuteUpdateAsync(x => x.SetProperty(a => a.CreatedAt, DateTime.UtcNow.AddDays(-1)));
            }

            await admin.GotoAsync("/Admin/AdminLessonCompletions/CreateModelExam");
            await admin.SelectOptionAsync("#ModelIdSelect", _examModelId.ToString());
            await admin.CheckAsync($"#apd-platform-{_instituteBatchId}");
            await admin.FillAsync("#DurationMinutes", "50");
            await admin.CheckAsync("#sendToBatchFalse");
            await admin.ClickAsync("#btnLoadStudents");
            await admin.Locator("#studentsSelect option").First.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached });
            await admin.Locator("#studentsSelect").SelectOptionAsync(new SelectOptionValue { Index = 0 });
            await admin.ClickAsync("#createModelExamForm button[type=submit]");

            await admin.Locator("#modelSendWarnings").WaitForAsync();
            Assert.Equal("50", await admin.InputValueAsync("#DurationMinutes"));
            Assert.True(await admin.IsCheckedAsync("#sendToBatchFalse"));
            Assert.True(await admin.IsVisibleAsync("#studentsSelect"));
            Assert.Equal(1, await admin.Locator("#studentsSelect option:checked").CountAsync());

            // أي تغيير بعد التأكيد يلغيه، فلا يُرسل بلا مراجعة
            await admin.CheckAsync("#confirmModelSend");
            await admin.FillAsync("#DurationMinutes", "55");
            await admin.Locator("#DurationMinutes").BlurAsync();
            Assert.False(await admin.IsCheckedAsync("#confirmModelSend"));

            await admin.CheckAsync("#confirmModelSend");
            await admin.ClickAsync("#createModelExamForm button[type=submit]");
            await admin.WaitForURLAsync(url => url.Contains("/ModelAssignments"), new PageWaitForURLOptions { Timeout = 20000 });

            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.Equal(2, await db.Exams.AsNoTracking().CountAsync(e => e.SourceProfessionalModelId == _examModelId));
                Assert.True(await db.Exams.AsNoTracking().AnyAsync(e => e.SourceProfessionalModelId == _examModelId && e.DurationMinutes == 55));
            }

            Assert.True(pageErrors.Count == 0, "أخطاء JS عند الأدمن: " + string.Join(" | ", pageErrors));
        }
    }
}
