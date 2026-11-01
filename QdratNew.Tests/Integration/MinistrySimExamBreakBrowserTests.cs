using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using QdratNew.Entities;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // اختبار معمل القياس — فاصل الدقيقة بين الأقسام بمتصفح حقيقي على 375px وقاعدة الاختبار المعزولة:
    // الانتقال المباشر عند انتهاء الوقت، والبدء التلقائي من الصفحة الترحيبية، وزر «ابدأ الآن»، والعودة بعد غياب طويل.
    // الوقت يُهيَّأ بإرجاع StartedAt في القاعدة بدل انتظار 26 دقيقة فعلية. المنفذ 5303.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "E2E")]
    public sealed class MinistrySimExamBreakBrowserTests : IAsyncLifetime
    {
        private const string ExamTitle = "RTKS7 فاصل معمل القياس";

        private RealServerFixture? _server;
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private RtkSeed _seed = null!;
        private int _examId;
        private int _attemptId;

        public async Task InitializeAsync()
        {
            if (!RtkRealDb.IsConfigured) return;

            await CleanupExamAsync();
            _seed = await RtkRealDb.SeedAsync(students: 1, withLogin: true);

            await using (var db = RtkRealDb.CreateDb())
            {
                var courseId = await db.Batches.Where(b => b.Id == _seed.BatchId).Select(b => b.CourseId).FirstAsync();
                var instructorId = await db.Instructors.Select(i => i.Id).FirstAsync();
                var sectionId = await db.Sections.Select(s => s.Id).FirstAsync();
                var questionId = await db.Questions
                    .Where(q => q.LessonId != null && q.Options.Any())
                    .Select(q => q.Id)
                    .FirstAsync();

                var exam = new MinistrySimExam
                {
                    CourseId = courseId, Title = ExamTitle, TotalStages = 5, IsPublished = true,
                    PublishedAt = DateTime.Now, CreatedByInstructorId = instructorId
                };
                for (var n = 1; n <= 5; n++)
                {
                    var stage = new MinistrySimExamStage
                    {
                        StageNumber = n, QuantSectionId = sectionId, QuantQuestionCount = 1,
                        VerbalSectionId = sectionId, VerbalQuestionCount = 0, DurationMinutes = 26
                    };
                    stage.Questions.Add(new MinistrySimExamStageQuestion
                    {
                        QuestionId = questionId, StageOrder = 1, GlobalOrder = n, IsQuant = true
                    });
                    exam.Stages.Add(stage);
                }
                db.MinistrySimExams.Add(exam);
                await db.SaveChangesAsync();

                var attempt = new MinistrySimExamStudentAttempt
                {
                    MinistrySimExamId = exam.Id, StudentId = _seed.Students[0].StudentId, StartedAt = DateTime.Now
                };
                db.MinistrySimExamStudentAttempts.Add(attempt);
                await db.SaveChangesAsync();

                _examId = exam.Id;
                _attemptId = attempt.Id;
            }

            _server = await RealServerFixture.StartAsync(5303);
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        }

        public async Task DisposeAsync()
        {
            if (_browser != null) await _browser.DisposeAsync();
            _playwright?.Dispose();
            if (_server != null) await _server.DisposeAsync();
            if (RtkRealDb.IsConfigured)
            {
                await CleanupExamAsync();
                await RtkRealDb.CleanupAsync();
            }
        }

        private static async Task CleanupExamAsync()
        {
            await using var db = RtkRealDb.CreateDb();
            var examIds = await db.MinistrySimExams.Where(e => e.Title == ExamTitle).Select(e => e.Id).ToListAsync();
            foreach (var id in examIds)
            {
                await db.MinistrySimExamStudentAttempts.Where(a => a.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExamStageQuestions.Where(q => q.MinistrySimExamStage.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExamStages.Where(s => s.MinistrySimExamId == id).ExecuteDeleteAsync();
                await db.MinistrySimExams.Where(e => e.Id == id).ExecuteDeleteAsync();
            }
        }

        // يضع القسم مفتوحًا بعد انقضاء مدته بـ secondsPastEnd ثانية
        private async Task SetStageExpiredAsync(int stageNumber, int secondsPastEnd)
        {
            var startedAt = DateTime.Now.AddMinutes(-26).AddSeconds(-secondsPastEnd);
            await using var db = RtkRealDb.CreateDb();
            var progress = await db.MinistrySimExamStudentStageProgresses
                .FirstOrDefaultAsync(p => p.MinistrySimExamStudentAttemptId == _attemptId && p.StageNumber == stageNumber);
            if (progress == null)
            {
                db.MinistrySimExamStudentStageProgresses.Add(new MinistrySimExamStudentStageProgress
                {
                    MinistrySimExamStudentAttemptId = _attemptId, StageNumber = stageNumber, StartedAt = startedAt
                });
            }
            else
            {
                progress.StartedAt = startedAt;
            }
            await db.SaveChangesAsync();
        }

        private async Task<MinistrySimExamStudentStageProgress?> GetProgressAsync(int stageNumber)
        {
            await using var db = RtkRealDb.CreateDb();
            return await db.MinistrySimExamStudentStageProgresses.AsNoTracking()
                .FirstOrDefaultAsync(p => p.MinistrySimExamStudentAttemptId == _attemptId && p.StageNumber == stageNumber);
        }

        private string SolveUrl(int stageNumber) => $"/Students/MinistrySimExam/Solve?examId={_examId}&stageNumber={stageNumber}";

        private static Task WaitForSolveAsync(IPage page, int stageNumber, int timeoutMs)
            => page.WaitForURLAsync(url => url.Contains("/MinistrySimExam/Solve") && url.Contains($"stageNumber={stageNumber}"),
                new PageWaitForURLOptions { Timeout = timeoutMs });

        [SqlServerFact]
        public async Task BreakBetweenStages_OnMobile375_AutoAdvances_AndCapsTotalTime()
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
            await page.FillAsync("#Input_Email", _seed.Students[0].UserName);
            await page.FillAsync("#passwordField", RtkRealDb.Password);
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(url => !url.Contains("/LMS/login"), new PageWaitForURLOptions { Timeout = 20000 });

            // صفحات الاختبار فقط: صفحة الدخول خارج نطاق هذا الاختبار
            page.PageError += (_, message) => pageErrors.Add(message);

            // ---------- انتهاء الوقت ينقل مباشرة للترحيبية: عدّاد من دقيقة كاملة، وسطر القسم السابق، و«ابدأ الآن» يعمل ----------
            await SetStageExpiredAsync(1, secondsPastEnd: 1);
            await page.GotoAsync(SolveUrl(1));
            Assert.Contains("/MinistrySimExam/Welcome", page.Url);
            Assert.True((await GetProgressAsync(1))!.IsLocked);
            Assert.Equal(0, await page.Locator(".swal2-confirm").CountAsync());
            var shownLabel = await page.InnerTextAsync("#mse-autostart-time");
            Assert.True(shownLabel == "01:00" || string.CompareOrdinal(shownLabel, "00:58") >= 0, $"العدّاد يبدأ من {shownLabel} بدل دقيقة كاملة");
            Assert.Contains("أجبت عن 0 من 1", await page.InnerTextAsync("#mse-prevstage"));
            var startBtn = page.Locator("#mse-start-btn");
            var box = await startBtn.BoundingBoxAsync();
            Assert.NotNull(box);
            Assert.True(box!.Y + box.Height <= 812, $"زر البدء خارج الشاشة (Y={box.Y})");
            var overflow = await page.EvaluateAsync<int>("() => document.documentElement.scrollWidth - window.innerWidth");
            Assert.True(overflow <= 1, $"تمرير أفقي في الصفحة الترحيبية (زيادة {overflow}px)");
            await startBtn.ClickAsync();
            await WaitForSolveAsync(page, 2, 15000);
            Assert.True((await GetProgressAsync(2))!.StartedAt >= DateTime.Now.AddSeconds(-30), "البدء المباشر يجب أن يحتسب من لحظة الضغط");

            // ---------- دون أي ضغط: العدّاد ينتهي فيبدأ القسم تلقائيًا ----------
            await SetStageExpiredAsync(2, secondsPastEnd: 50);
            await page.GotoAsync(SolveUrl(2));
            await page.Locator("#mse-autostart-time").WaitForAsync(new LocatorWaitForOptions { Timeout = 10000 });
            await WaitForSolveAsync(page, 3, 30000);
            Assert.NotNull(await GetProgressAsync(3));

            // ---------- العودة بعد غياب طويل: لا دقيقة جديدة، والقسم التالي يُحتسب من انتهاء الفاصل ----------
            var absence = DateTime.Now.AddDays(-1);
            await using (var db = RtkRealDb.CreateDb())
            {
                await db.MinistrySimExamStudentStageProgresses
                    .Where(p => p.MinistrySimExamStudentAttemptId == _attemptId && p.StageNumber == 3)
                    .ExecuteUpdateAsync(x => x.SetProperty(p => p.StartedAt, absence));
            }
            await page.GotoAsync(SolveUrl(3));

            // بعد يوم كامل انتهت مدة الرابع أيضًا، فيصل مباشرة لآخر قسم بملخصه وزر النتيجة اليدوي كما كان
            await WaitForSolveAsync(page, 5, 15000);
            var fourth = await GetProgressAsync(4);
            var fifth = await GetProgressAsync(5);
            Assert.Equal(26 + 65.0 / 60, (fourth!.StartedAt!.Value - absence).TotalMinutes, 0.01);
            Assert.Equal(2 * (26 + 65.0 / 60), (fifth!.StartedAt!.Value - absence).TotalMinutes, 0.01);
            Assert.True((await GetProgressAsync(4))!.IsLocked);

            await page.ClickAsync(".swal2-confirm");
            await page.WaitForURLAsync(url => url.Contains("/MinistrySimExam/Result"), new PageWaitForURLOptions { Timeout = 15000 });

            Assert.True(pageErrors.Count == 0, string.Join(" | ", pageErrors));
        }
    }
}
