using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using QdratNew.Entities;
using QdratNew.Enums;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // PPL-S4 — صفحات Partner/PlacementExams بمتصفح حقيقي على 375px وقاعدة الاختبار المعزولة:
    // JS صفحة الإنشاء (تحميل المحاور وفلترة الدفعات والعدّاد) ثم المراجعة والمعاينة و«لا أعرف» والإخفاء. المنفذ 5304.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public sealed class PartnerPlacementBrowserTests : IAsyncLifetime
    {
        private const string Marker = "PPLE2E";
        private const string UserName = "pple2e-partner";

        private RealServerFixture? _server;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private int _partnerId;
        private int _courseId;
        private int _batchId;
        private int _otherCourseBatchId;
        private int _sectionId;
        private int _studentId;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            await CleanupAsync();
            await SeedAsync();

            _server = await RealServerFixture.StartAsync(5304);
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

            var user = NewUser(UserName);
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, RtkRealDb.Password);
            var studentUser = NewUser("pple2e-student");
            studentUser.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(studentUser, RtkRealDb.Password);
            db.Users.AddRange(user, studentUser);

            var roleId = await db.Roles.Where(r => r.Name == "Partner").Select(r => r.Id).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("دور 'Partner' غير موجود في قاعدة الاختبار.");
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleId });

            var studentRoleId = await db.Roles.Where(r => r.Name == "Student").Select(r => r.Id).FirstOrDefaultAsync()
                ?? throw new InvalidOperationException("دور 'Student' غير موجود في قاعدة الاختبار.");
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = studentUser.Id, RoleId = studentRoleId });

            var partner = new Partner { Name = $"{Marker} شريك", Code = Marker, PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var branch = new Branch { Name = $"{Marker} فرع", Partner = partner, Location = "-", City = "-", State = "-", Country = "-" };
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var otherCourse = new Course { Name = $"{Marker} دورة ثانية", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var curriculum = new Curriculum { Title = $"{Marker} منهج", Description = "-", CurriculumTypeName = "-" };
            var unit = new Unit { Title = $"{Marker} وحدة" };
            db.AddRange(partner, branch, course, otherCourse, curriculum, unit);
            await db.SaveChangesAsync();

            db.UserPartners.Add(new UserPartner { UserId = user.Id, PartnerId = partner.Id });

            var subscription = new PartnerSubscription
            {
                PartnerId = partner.Id,
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(30),
                CanUsePlacementExams = true,
                SubscriptionCourses = new List<PartnerSubscriptionCourse>
                {
                    new() { CourseId = course.Id, CanUsePlatformQuestionBank = true },
                    new() { CourseId = otherCourse.Id, CanUsePlatformQuestionBank = true }
                }
            };
            db.PartnerSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            db.PartnerSubscriptionPeriods.Add(new PartnerSubscriptionPeriod
            {
                PartnerSubscriptionId = subscription.Id,
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(30)
            });

            var section = new Section { Title = $"{Marker} محور", CurriculumId = curriculum.Id };
            db.Sections.Add(section);
            db.CourseCurriculums.Add(new CourseCurriculum { CourseId = course.Id, CurriculumId = curriculum.Id });
            await db.SaveChangesAsync();

            var lesson = new Lesson { Title = $"{Marker} مؤشر", Content = "-", SectionId = section.Id, UnitId = unit.Id };
            db.Lessons.Add(lesson);
            await db.SaveChangesAsync();

            for (var i = 1; i <= 4; i++)
            {
                db.Questions.Add(new Question
                {
                    Id = Guid.NewGuid(),
                    Title = $"{Marker} سؤال {i}",
                    ReferenceNumber = $"{Marker}{i}{Guid.NewGuid():N}",
                    CorrectAnswer = "أ",
                    CurriculumId = curriculum.Id,
                    SectionId = section.Id,
                    LessonId = lesson.Id,
                    Difficulty = DifficultyLevel.Medium,
                    IsReviewed = true,
                    IsComplete = true
                });
            }

            var batch = new Batch { Name = $"{Marker} دفعة أولى", BranchId = branch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            var otherCourseBatch = new Batch { Name = $"{Marker} دفعة ثانية", BranchId = branch.Id, CourseId = otherCourse.Id, StartDate = DateTime.UtcNow };
            db.Batches.AddRange(batch, otherCourseBatch);

            var student = new Student
            {
                NationalID = $"{Marker}1", FullName = $"طالب {Marker}", Gender = "ذكر", Age = 17,
                School = "-", Level = "-", UserId = studentUser.Id, BranchId = branch.Id, IsActiveForLearning = true
            };
            db.Students.Add(student);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = student.StudentID, BatchId = batch.Id });
            await db.SaveChangesAsync();

            _partnerId = partner.Id;
            _courseId = course.Id;
            _batchId = batch.Id;
            _otherCourseBatchId = otherCourseBatch.Id;
            _sectionId = section.Id;
            _studentId = student.StudentID;
        }

        private static ApplicationUser NewUser(string name) => new()
        {
            Id = Guid.NewGuid().ToString(),
            UserName = name,
            NormalizedUserName = name.ToUpperInvariant(),
            Email = $"{name}@test.local",
            NormalizedEmail = $"{name}@test.local".ToUpperInvariant(),
            FullName = name,
            NationalID = name,
            IsActive = true,
            EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            const string partners = "(SELECT Id FROM Partners WHERE Code = 'PPLE2E')";
            const string subscriptions = "(SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN " + partners + ")";
            const string exams = "(SELECT Id FROM Exams WHERE OwnerPartnerId IN " + partners + ")";
            const string users = "(SELECT Id FROM AspNetUsers WHERE UserName LIKE 'pple2e-%')";

            foreach (var sql in new[]
            {
                "DELETE FROM Notifications WHERE StudentID IN (SELECT StudentID FROM Students WHERE NationalID LIKE 'PPLE2E%')",
                "DELETE FROM QuestionAttemptNew WHERE StudentId IN (SELECT StudentID FROM Students WHERE NationalID LIKE 'PPLE2E%')",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPLE2E%'",
                $"DELETE FROM ExamQuestions WHERE ExamId IN {exams}",
                $"DELETE FROM ExamStudentStatuses WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignments WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE ExamId IN {exams}",
                $"DELETE FROM Exams WHERE Id IN {exams}",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPLE2E%'",
                "DELETE FROM Lessons WHERE Title LIKE 'PPLE2E%'",
                "DELETE FROM Units WHERE Title LIKE 'PPLE2E%'",
                "DELETE FROM Sections WHERE Title LIKE 'PPLE2E%'",
                "DELETE FROM CourseCurriculums WHERE CurriculumId IN (SELECT Id FROM Curriculums WHERE Title LIKE 'PPLE2E%')",
                "DELETE FROM Curriculums WHERE Title LIKE 'PPLE2E%'",
                "DELETE FROM StudentBatchEnrollments WHERE BatchId IN (SELECT Id FROM Batches WHERE Name LIKE 'PPLE2E%')",
                "DELETE FROM Batches WHERE Name LIKE 'PPLE2E%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPLE2E%'",
                $"DELETE FROM PartnerSubscriptionPeriods WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN {subscriptions}",
                $"DELETE FROM PartnerSubscriptions WHERE Id IN {subscriptions}",
                $"DELETE FROM UserPartners WHERE PartnerId IN {partners}",
                "DELETE FROM Branches WHERE Name LIKE 'PPLE2E%'",
                "DELETE FROM Partners WHERE Code = 'PPLE2E'",
                "DELETE FROM Courses WHERE Name LIKE 'PPLE2E%'",
                $"DELETE FROM AspNetUserRoles WHERE UserId IN {users}",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'pple2e-%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        // PPL-S5 — الرحلة كاملة: مراجعة → تأكيد → إرسال، ثم الطالب يجد الاختبار في لوحته ويبدأه
        // PPL-S6: رحلة التقارير على 375px: لا تمرير أفقي للصفحة ولا أخطاء JS
        [SqlServerFact]
        public async Task Partner_OpensReports_FromBatchExams_OnMobile()
        {
            int examId;
            await using (var db = RtkRealDb.CreateDb())
            {
                var exam = new Exam { Title = $"{Marker} اختبار التقارير", Type = ExamType.LevelAssessment, DurationMinutes = 30, TotalQuestions = 2, IsActive = true, CourseId = _courseId, OwnerPartnerId = _partnerId };
                db.Exams.Add(exam);
                await db.SaveChangesAsync();
                examId = exam.Id;

                var questions = await db.Questions.Where(q => q.ReferenceNumber.StartsWith(Marker)).OrderBy(q => q.Id).Take(2).ToListAsync();
                db.ExamQuestions.AddRange(questions.Select((q, i) => new ExamQuestion { ExamId = examId, QuestionId = q.Id, Order = i + 1 }));
                db.ExamAssignmentsToBatches.Add(new ExamAssignmentToBatch { ExamId = examId, BatchId = _batchId, Title = "اختبار تحديد المستوى", CreatedAt = DateTime.UtcNow, AssignedAt = DateTime.UtcNow, IsSentToStudents = true });

                var assignment = new ExamAssignment { ExamId = examId, StudentId = _studentId, AssignedAt = DateTime.UtcNow };
                db.ExamAssignments.Add(assignment);
                db.ExamStudentStatuses.Add(new ExamStudentStatus { ExamId = examId, StudentId = _studentId, Status = ExamStatus.Completed, IsSubmitted = true, StartedAt = DateTime.UtcNow.AddMinutes(-10), SubmittedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();

                db.QuestionAttemptNew.AddRange(questions.Select((q, i) => new QuestionAttemptNew
                {
                    StudentId = _studentId,
                    QuestionId = q.Id,
                    ExamId = examId,
                    ExamAssignmentId = assignment.Id,
                    SelectedAnswer = i == 0 ? q.CorrectAnswer! : "<b>إجابة أخرى</b>",
                    IsCorrect = i == 0,
                    AttemptedAt = DateTime.UtcNow,
                    TimeTakenSeconds = 30,
                    LessonId = q.LessonId,
                    SectionId = q.SectionId
                }));
                await db.SaveChangesAsync();
            }

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

            async Task AssertNoPageOverflowAsync(string name)
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                var overflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1");
                Assert.False(overflow, $"تمرير أفقي في {name}: {page.Url}");

                var clippedTable = await page.EvaluateAsync<bool>("() => [...document.querySelectorAll('.pp-table-wrap')].some(w => w.scrollWidth > w.clientWidth + 1)");
                Assert.False(clippedTable, $"جدول مقصوص في {name}: {page.Url}");
            }

            await page.GotoAsync($"/Partner/PlacementExams/BatchExams?batchId={_batchId}");
            await page.ClickAsync($"a[href*='BatchStudents?examId={examId}']");
            await page.WaitForURLAsync(url => url.Contains("BatchStudents"));
            await AssertNoPageOverflowAsync("BatchStudents");

            await page.ClickAsync("a[href*='StudentPlacementReport']");
            await page.WaitForURLAsync(url => url.Contains("StudentPlacementReport"));
            await AssertNoPageOverflowAsync("StudentPlacementReport");
            Assert.Contains("بالتعاون مع معهد القدرات", await page.InnerTextAsync(".pp-report-brand"));

            await page.ClickAsync("a[href*='ReviewLessonQuestions']");
            await page.WaitForURLAsync(url => url.Contains("ReviewLessonQuestions"));
            await AssertNoPageOverflowAsync("ReviewLessonQuestions");

            await page.GotoAsync($"/Partner/PlacementExams/StudentExamDetails?examId={examId}&studentId={_studentId}&batchId={_batchId}");
            await AssertNoPageOverflowAsync("StudentExamDetails");
            Assert.Contains("إجابة أخرى", await page.InnerTextAsync(".pp-table")); // الوسوم أُزيلت من إجابة الطالب

            await page.GotoAsync($"/Partner/PlacementExams/BatchPerformanceReport?examId={examId}&batchId={_batchId}");
            await AssertNoPageOverflowAsync("BatchPerformanceReport");

            await page.ClickAsync("a[href*='ReviewLessonQuestionsBatch']");
            await page.WaitForURLAsync(url => url.Contains("ReviewLessonQuestionsBatch"));
            await AssertNoPageOverflowAsync("ReviewLessonQuestionsBatch");

            await page.GotoAsync($"/Partner/PlacementExams/StudentPlacementReportPrint?examId={examId}&studentId={_studentId}");
            await AssertNoPageOverflowAsync("StudentPlacementReportPrint");

            await page.GotoAsync($"/Partner/Reports/Student?batchId={_batchId}&studentId={_studentId}");
            await AssertNoPageOverflowAsync("ReportsStudent");

            Assert.Empty(pageErrors);
        }

        [SqlServerFact]
        public async Task Partner_SendsDraft_StudentFindsAndStartsIt_OnMobile()
        {
            int examId;
            await using (var db = RtkRealDb.CreateDb())
            {
                var exam = new Exam { Title = $"{Marker} اختبار الإرسال", Type = ExamType.LevelAssessment, DurationMinutes = 30, TotalQuestions = 2, IsActive = true, CourseId = _courseId, OwnerPartnerId = _partnerId };
                db.Exams.Add(exam);
                await db.SaveChangesAsync();
                examId = exam.Id;

                var questionIds = await db.Questions.Where(q => q.ReferenceNumber.StartsWith(Marker)).OrderBy(q => q.Id).Select(q => q.Id).Take(2).ToListAsync();
                db.ExamQuestions.AddRange(questionIds.Select((id, i) => new ExamQuestion { ExamId = examId, QuestionId = id, Order = i + 1 }));
                db.ExamAssignmentsToBatches.Add(new ExamAssignmentToBatch { ExamId = examId, BatchId = _batchId, Title = "اختبار تحديد المستوى", CreatedAt = DateTime.UtcNow, AssignedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }

            var mobile = new BrowserNewContextOptions
            {
                BaseURL = _server!.BaseUrl,
                ViewportSize = new ViewportSize { Width = 375, Height = 812 },
                IsMobile = true,
                HasTouch = true
            };

            // ---------- الشريك ----------
            var partnerContext = await _browser!.NewContextAsync(mobile);
            var page = await partnerContext.NewPageAsync();
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

            await page.GotoAsync($"/Partner/PlacementExams/Review?examId={examId}");
            await page.ClickAsync($"a[href*='ConfirmSend']");
            await page.WaitForURLAsync(url => url.Contains("/ConfirmSend"));
            Assert.Contains("إرسال الاختبار لـ 1 طالب", await page.InnerTextAsync("form[action*='SendExamToStudents'] button"));
            Assert.False(await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth + 1"), "صفحة التأكيد تتجاوز عرض 375px");

            await page.ClickAsync("form[action*='SendExamToStudents'] button");
            await page.WaitForURLAsync(url => url.Contains("/PlacementDetails"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains("تم إرسال الاختبار بنجاح", await page.InnerTextAsync(".pp-alert-success"));

            // الطلاب الجدد: تأكيد ثم رسالة أنه لا يوجد جدد
            await page.ClickAsync("form[action*='SendExamToNewStudents'] button");
            await page.ClickAsync(".swal2-confirm");
            await page.WaitForSelectorAsync("text=لا يوجد طلاب جدد");

            // المراجعة لم تعد تعرض زر الإرسال
            await page.GotoAsync($"/Partner/PlacementExams/Review?examId={examId}");
            Assert.Equal(0, await page.Locator("a[href*='ConfirmSend']").CountAsync());

            Assert.True(pageErrors.Count == 0, "أخطاء JS عند الشريك: " + string.Join(" | ", pageErrors));

            // ---------- الطالب ----------
            var studentContext = await _browser!.NewContextAsync(mobile);
            var studentPage = await studentContext.NewPageAsync();

            await studentPage.GotoAsync("/LMS/login");
            await studentPage.FillAsync("#Input_Email", "pple2e-student");
            await studentPage.FillAsync("#passwordField", RtkRealDb.Password);
            await studentPage.ClickAsync("button[type=submit]");
            await studentPage.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });

            await studentPage.GotoAsync("/Students/PlacementExams/PlacementExamDashboard");
            Assert.Contains($"{Marker} اختبار الإرسال", await studentPage.ContentAsync());

            await studentPage.ClickAsync("a[href*='StartPlacementExam']");
            await studentPage.WaitForURLAsync(url => url.Contains("StartPlacementExam"), new PageWaitForURLOptions { Timeout = 20000 });
            Assert.Contains($"{Marker} سؤال", await studentPage.ContentAsync());
        }

        // PPL-S7.3 — الرحلة كاملة من الواجهة: إنشاء → مراجعة → إرسال → حل الطالب وتسليمه → تقرير الشريك
        [SqlServerFact]
        public async Task FullJourney_PartnerCreatesAndSends_StudentSolves_PartnerSeesReport_OnMobile()
        {
            var mobile = new BrowserNewContextOptions
            {
                BaseURL = _server!.BaseUrl,
                IgnoreHTTPSErrors = true,
                ViewportSize = new ViewportSize { Width = 375, Height = 800 }
            };

            var partnerContext = await _browser!.NewContextAsync(mobile);
            var partnerPage = await partnerContext.NewPageAsync();
            var pageErrors = new List<string>();
            await partnerPage.GotoAsync("/LMS/login");
            await partnerPage.FillAsync("#Input_Email", UserName);
            await partnerPage.FillAsync("#passwordField", RtkRealDb.Password);
            await partnerPage.ClickAsync("button[type=submit]");
            await partnerPage.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });
            await partnerPage.GotoAsync("/Partner/PartnerContext/Select");
            await partnerPage.SelectOptionAsync("select[name=partnerId]", _partnerId.ToString());
            await partnerPage.ClickAsync("form button[type=submit]");
            await partnerPage.WaitForLoadStateAsync();

            // صفحة الدخول خارج نطاق هذا الاختبار، فتبدأ المراقبة من صفحات الشريك
            partnerPage.PageError += (_, message) => pageErrors.Add(message);

            // ---------- إنشاء ----------
            await partnerPage.GotoAsync("/Partner/PlacementExams/CreateAdvanced");
            await partnerPage.SelectOptionAsync("select[name=CourseId]", _courseId.ToString());
            await partnerPage.WaitForSelectorAsync("input[name='SectionSelections[0].QuestionCount']");
            await partnerPage.WaitForFunctionAsync($"() => !!document.querySelector(\"select[name=BatchId] option[value='{_batchId}']\")");
            await partnerPage.SelectOptionAsync("select[name=BatchId]", _batchId.ToString());
            await partnerPage.FillAsync("input[name=TotalQuestions]", "2");
            await partnerPage.FillAsync("input[name='SectionSelections[0].QuestionCount']", "2");
            await partnerPage.ClickAsync("form button[type=submit]");
            await partnerPage.WaitForURLAsync(url => url.Contains("/PlacementExams/Review"), new PageWaitForURLOptions { Timeout = 30000 });

            // ---------- مراجعة وإرسال ----------
            var examId = int.Parse(new Uri(partnerPage.Url).Query.Split("examId=")[1].Split('&')[0]);
            Assert.False(await partnerPage.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth + 1"), "صفحة المراجعة تتجاوز عرض 375px");

            // PPL-S7.4: هدف اللمس على الجوال
            var smallButtons = await partnerPage.EvaluateAsync<int>(
                "() => [...document.querySelectorAll('.pp-btn')].filter(b => b.offsetParent && b.getBoundingClientRect().height < 44).length");
            Assert.Equal(0, smallButtons);

            await partnerPage.ClickAsync("a[href*='ConfirmSend']");
            await partnerPage.WaitForURLAsync(url => url.Contains("/ConfirmSend"));
            await partnerPage.ClickAsync("form[action*='SendExamToStudents'] button");
            await partnerPage.WaitForURLAsync(url => url.Contains("/PlacementDetails"), new PageWaitForURLOptions { Timeout = 20000 });

            Assert.True(pageErrors.Count == 0, "أخطاء JS عند الشريك: " + string.Join(" | ", pageErrors));

            // ---------- الطالب: حل وتسليم ----------
            var studentContext = await _browser!.NewContextAsync(mobile);
            var studentPage = await studentContext.NewPageAsync();
            await studentPage.GotoAsync("/LMS/login");
            await studentPage.FillAsync("#Input_Email", "pple2e-student");
            await studentPage.FillAsync("#passwordField", RtkRealDb.Password);
            await studentPage.ClickAsync("button[type=submit]");
            await studentPage.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });

            await studentPage.GotoAsync("/Students/PlacementExams/PlacementExamDashboard");
            Assert.Contains("بدء الاختبار", await studentPage.ContentAsync());
            await studentPage.ClickAsync("a[href*='StartPlacementExam']");
            await studentPage.WaitForURLAsync(url => url.Contains("StartPlacementExam"), new PageWaitForURLOptions { Timeout = 20000 });

            // السؤال الأول عبر المسار الحقيقي للحفظ (FormData + توكن النموذج)
            var saved = await studentPage.EvaluateAsync<bool>(@"async () => {
                const form = document.getElementById('exam-question-form');
                const data = new FormData(form);
                data.set('SelectedOption', 'أ');
                data.set('nav', 'next');
                const res = await fetch('/Students/PlacementExams/SubmitAnswerFetch', { method: 'POST', body: data });
                return res.ok && (await res.text()).trim().startsWith('<');
            }");
            Assert.True(saved, "لم يُحفظ الجواب عبر النموذج الحقيقي");

            // أثناء الاختبار صفحات النتيجة والمراجعة مقفلة لأنها تكشف الإجابات الصحيحة
            var assignmentId = int.Parse(new Uri(studentPage.Url).Query.Split("assignmentId=")[1].Split('&')[0]);
            await studentPage.GotoAsync($"/Students/PlacementExams/PlacementReview?assignmentId={assignmentId}");
            Assert.Contains("PlacementExamDashboard", studentPage.Url);

            // التسليم عبر زر الواجهة حتى يُختبر توكن الهيدر في JS: تأكيد ثم «إنهاء الآن» لوجود سؤال بلا إجابة
            Guid lastQuestionId;
            await using (var db = RtkRealDb.CreateDb())
            {
                lastQuestionId = await db.ExamQuestions.Where(q => q.ExamId == examId).OrderByDescending(q => q.Order).Select(q => q.QuestionId).FirstAsync();
            }

            await studentPage.GotoAsync($"/Students/PlacementExams/StartPlacementExam?assignmentId={assignmentId}&q={lastQuestionId}");
            await studentPage.ClickAsync("#submitExam");
            await studentPage.ClickAsync(".swal2-confirm");
            await studentPage.ClickAsync(".swal2-cancel");
            await studentPage.WaitForURLAsync(url => url.Contains("ResultPlacementExam"), new PageWaitForURLOptions { Timeout = 20000 });

            // ---------- تقرير الشريك ----------
            await partnerPage.GotoAsync($"/Partner/PlacementExams/BatchStudents?examId={examId}&batchId={_batchId}");
            Assert.Contains($"طالب {Marker}", await partnerPage.ContentAsync());
            await partnerPage.GotoAsync($"/Partner/PlacementExams/StudentPlacementReport?studentId={_studentId}&examId={examId}&batchId={_batchId}");
            Assert.Contains($"طالب {Marker}", await partnerPage.ContentAsync());
            Assert.False(await partnerPage.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth + 1"), "تقرير الطالب يتجاوز عرض 375px");

            await using var check = RtkRealDb.CreateDb();
            Assert.True(await check.ExamStudentStatuses.AnyAsync(s => s.ExamId == examId && s.StudentId == _studentId && s.IsSubmitted));
            Assert.True(await check.QuestionAttemptNew.AnyAsync(a => a.ExamId == examId && a.StudentId == _studentId && a.IsCorrect));
            Assert.Equal(_partnerId, await check.Exams.Where(e => e.Id == examId).Select(e => e.OwnerPartnerId).SingleAsync());
        }

        [SqlServerFact]
        public async Task Partner_CreatesDraft_Reviews_AndHides_OnMobile()
        {
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

            // اختيار الشريك عبر الصفحة الحقيقية
            await page.GotoAsync("/Partner/PartnerContext/Select");
            await page.SelectOptionAsync("select[name=partnerId]", _partnerId.ToString());
            await page.ClickAsync("form button[type=submit]");
            await page.WaitForLoadStateAsync();

            page.PageError += (_, message) => pageErrors.Add(message);

            // ---------- القائمة: حالة فارغة + شارة العقد ----------
            await page.GotoAsync("/Partner/PlacementExams");
            Assert.Contains("لا توجد اختبارات تحديد مستوى بعد", await page.InnerTextAsync(".pp-page"));
            Assert.Contains("العقد مفعّل", await page.InnerTextAsync(".pp-stats"));

            // ---------- الإنشاء: JS يحمّل المحاور ويفلتر الدفعات حسب الدورة ----------
            await page.GotoAsync("/Partner/PlacementExams/CreateAdvanced");
            await page.SelectOptionAsync("#courseSelect", _courseId.ToString());
            await page.WaitForSelectorAsync("#sectionsTable input[type=number]");

            var batchValues = await page.Locator("#batchSelect option").EvaluateAllAsync<string[]>("els => els.map(e => e.value)");
            Assert.Contains(_batchId.ToString(), batchValues);
            Assert.DoesNotContain(_otherCourseBatchId.ToString(), batchValues);

            await page.FillAsync("#totalQuestions", "2");
            await page.SelectOptionAsync("#batchSelect", _batchId.ToString());

            await page.FillAsync("#sectionsTable input[type=number]", "1");
            Assert.Contains("is-mismatch", await page.GetAttributeAsync("#sectionsTotal", "class") ?? "");

            // خطأ من الخادم: يبقى الاختيار والعدد بعد إعادة العرض
            await page.ClickAsync("#mainForm button[type=submit]");
            await page.WaitForSelectorAsync(".pp-alert-danger");
            Assert.Contains("لا يساوي", await page.InnerTextAsync(".pp-alert-danger"));
            await page.WaitForSelectorAsync("#sectionsTable input[type=number]");
            Assert.Equal(_courseId.ToString(), await page.InputValueAsync("#courseSelect"));
            Assert.Equal(_batchId.ToString(), await page.InputValueAsync("#batchSelect"));
            Assert.Equal("1", await page.InputValueAsync("#sectionsTable input[type=number]"));

            await page.FillAsync("#sectionsTable input[type=number]", "2");
            Assert.Contains("is-match", await page.GetAttributeAsync("#sectionsTotal", "class") ?? "");

            await page.ClickAsync("#mainForm button[type=submit]");
            await page.WaitForURLAsync(url => url.Contains("/Partner/PlacementExams/Review"), new PageWaitForURLOptions { Timeout = 20000 });

            int examId;
            await using (var db = RtkRealDb.CreateDb())
            {
                var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.OwnerPartnerId == _partnerId);
                examId = exam.Id;

                // المسودة لا تصل لأي طالب قبل الإرسال
                Assert.False(await db.ExamAssignments.AnyAsync(a => a.ExamId == examId));
                Assert.Equal(2, await db.ExamQuestions.CountAsync(q => q.ExamId == examId));
            }

            // ---------- المراجعة: معاينة السؤال داخل المودال ----------
            await page.Locator("[data-preview-url]").First.ClickAsync();
            await page.WaitForFunctionAsync("() => document.querySelector('#questionPreviewModal .modal-body').innerText.includes('PPLE2E')");
            await page.ClickAsync("#questionPreviewModal .btn-close");
            await page.WaitForSelectorAsync("#questionPreviewModal", new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden });

            // ---------- خيار «لا أعرف» يُحفظ ----------
            await page.ClickAsync("#allowDontKnowSwitch");
            await page.WaitForSelectorAsync(".swal2-popup");
            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.True(await db.Exams.Where(e => e.Id == examId).Select(e => e.AllowDontKnowOption).SingleAsync());
            }
            await page.GotoAsync($"/Partner/PlacementExams/Review?examId={examId}");

            // ---------- إضافة سؤال من البنك تحدّث الإجمالي ----------
            await page.GotoAsync($"/Partner/PlacementExams/AddQuestionFromBank?examId={examId}&sectionId={_sectionId}");
            await page.Locator("button[name=questionId]").First.ClickAsync();
            await page.WaitForURLAsync(url => url.Contains("/Partner/PlacementExams/Review"));
            Assert.Contains("تمت إضافة السؤال", await page.InnerTextAsync(".pp-alert-success"));

            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.Equal(3, await db.Exams.Where(e => e.Id == examId).Select(e => e.TotalQuestions).SingleAsync());
            }

            // ---------- الدفعة: الإخفاء بتأكيد ثم رسالة النتيجة ----------
            await page.GotoAsync($"/Partner/PlacementExams/BatchExams?batchId={_batchId}");
            Assert.Contains("مسودة", await page.InnerTextAsync(".pp-table"));
            await page.ClickAsync("form[action*='HideExam'] button[type=submit]");
            await page.ClickAsync(".swal2-confirm");
            await page.WaitForSelectorAsync(".pp-alert-success");
            Assert.Contains("مخفي", await page.InnerTextAsync(".pp-table"));

            // الدفعة تبقى ظاهرة بعد إخفاء آخر اختبار فيها، فيمكن استرجاعه
            await page.GotoAsync("/Partner/PlacementExams");
            Assert.Contains($"{Marker} دفعة أولى", await page.InnerTextAsync(".pp-batches"));
            await page.GotoAsync($"/Partner/PlacementExams/BatchExams?batchId={_batchId}");

            // صفحة الدفعة لا تتجاوز عرض الموبايل (الجدول يتمرر داخل حاويته)
            var overflow = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > window.innerWidth + 1");
            Assert.False(overflow, "الصفحة تتجاوز عرض 375px");

            // ---------- بعد إصلاح مسار jQuery في الـ layout: الصفحات التي تعتمد عليه تعمل ----------
            await page.GotoAsync($"/Partner/Reports/BatchExams?batchId={_batchId}");
            await page.WaitForSelectorAsync("#examsTable_wrapper");

            await page.GotoAsync($"/Partner/Students/AddToBatch?studentId={_studentId}");
            Assert.True(await page.EvaluateAsync<bool>("() => !!(window.jQuery && jQuery.fn.validate)"), "jQuery Validate لم يُحمَّل");

            Assert.True(pageErrors.Count == 0, "أخطاء JS: " + string.Join(" | ", pageErrors));
        }
    }
}
