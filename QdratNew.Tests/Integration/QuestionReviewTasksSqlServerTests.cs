using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Instructors.Interfaces;
using QdratNew.Services.Interfaces;
using QdratNew.Services.QuestionReviewTasks;
using QdratNew.ViewModels.QuestionReviewTasks;
using Xunit;

namespace QdratNew.Tests.Integration
{
    // QRT-S8.3 — اختبارات تكامل على SQL Server حقيقي لما لا يثبته InMemory:
    // الفهرس الفريد المُصفّى (الحجز الحصري)، التزامن الحقيقي، RowVersion، وذرّية الإنشاء.
    //
    // التشغيل: اضبط QDRAT_TEST_SQL على Connection String لقاعدة اختبار (اسمها يجب أن يحوي "Test")،
    // مثال: Integrated Security=SSPI;Data Source=.;Initial Catalog=QdratNewDB_IntegrationTests;TrustServerCertificate=True
    // بدون المتغير تُتخطّى كل الاختبارات. لا تُوجَّه أبدًا لقاعدة الإنتاج (يُرفض الاتصال إن لم يحوِ الاسم "Test").
    // كل ما يُزرع يحمل علامة QRTSQL ويُحذف قبل التشغيل وبعده.

    public sealed class SqlServerFactAttribute : FactAttribute
    {
        public const string EnvName = "QDRAT_TEST_SQL";

        public SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvName)))
                Skip = $"متغير البيئة {EnvName} غير مضبوط — اختبارات SQL Server متخطّاة.";
        }
    }

    // مجموعة واحدة متسلسلة مع E2E: كلاهما يكتب في قاعدة الاختبار نفسها ويحذف بالعلامة (تجنّب Timeout من التوازي)
    [CollectionDefinition(Name)]
    public sealed class QrtSharedTestDbCollection
    {
        public const string Name = "QRT shared SQL test database";
    }

    [Collection(QrtSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class QuestionReviewTasksSqlServerTests : IAsyncLifetime
    {
        private const string Marker = "QRTSQL";

        private sealed class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
        }

        // نفس إعداد الإنتاج (EnableRetryOnFailure + UseCompatibilityLevel(120) كما في Program.cs) لاختبار أن Transaction داخل ExecutionStrategy تعمل فعليًا
        private sealed class SqlFactory : IDbContextFactory<ApplicationDbContext>
        {
            private readonly DbContextOptions<ApplicationDbContext> _options;

            public SqlFactory(string connectionString)
            {
                _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseSqlServer(connectionString, o => o.EnableRetryOnFailure().UseCompatibilityLevel(120).CommandTimeout(180))
                    .Options;
            }

            public ApplicationDbContext CreateDbContext() => new(_options);
        }

        private static readonly ReviewActor Admin = new("qrtsql-admin", "مدير الاختبار", UserRoleType.Admin);

        private SqlFactory? _factory;
        private int _curriculumId, _lessonId, _sectionId;

        public async Task InitializeAsync()
        {
            var raw = Environment.GetEnvironmentVariable(SqlServerFactAttribute.EnvName);
            if (string.IsNullOrWhiteSpace(raw)) return;

            var catalog = new SqlConnectionStringBuilder(raw).InitialCatalog;
            if (!catalog.Contains("Test", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"رُفض الاتصال: القاعدة '{catalog}' ليست قاعدة اختبار (يجب أن يحوي الاسم 'Test').");

            _factory = new SqlFactory(raw);
            await CleanupAsync();

            await using var db = _factory.CreateDbContext();
            var anchor = await db.Questions.AsNoTracking()
                .Where(q => q.SectionId != null)
                .Select(q => new { q.CurriculumId, q.LessonId, SectionId = q.SectionId!.Value })
                .FirstOrDefaultAsync();
            Assert.NotNull(anchor); // تحتاج قاعدة الاختبار سؤالًا واحدًا على الأقل لاستعارة المنهج/الدرس/القسم
            _curriculumId = anchor!.CurriculumId;
            _lessonId = anchor.LessonId;
            _sectionId = anchor.SectionId;
        }

        public async Task DisposeAsync()
        {
            if (_factory is not null) await CleanupAsync();
        }

        // ---------- أدوات ----------

        private async Task CleanupAsync()
        {
            await using var db = _factory!.CreateDbContext();
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                const string questions = "(SELECT Id FROM Questions WHERE ReferenceNumber LIKE 'QRTSQL-%')";
                const string instructors = "(SELECT Id FROM Instructors WHERE NationalID LIKE 'QRTSQL%')";
                await db.Database.ExecuteSqlRawAsync(
                    $"DELETE FROM QuestionAuditLogs WHERE QuestionId IN {questions};" +
                    $"DELETE FROM QuestionReviewTaskItems WHERE QuestionId IN {questions};" +
                    $"DELETE FROM QuestionReviewTasks WHERE ParentTaskId IS NOT NULL AND InstructorId IN {instructors};" +
                    $"DELETE FROM QuestionReviewTasks WHERE InstructorId IN {instructors};" +
                    $"DELETE FROM Questions WHERE ReferenceNumber LIKE 'QRTSQL-%';" +
                    $"DELETE FROM Instructors WHERE NationalID LIKE 'QRTSQL%';" +
                    $"DELETE FROM AspNetUsers WHERE UserName LIKE 'qrtsql-%';");
            });
        }

        private QuestionReviewTaskService NewService()
        {
            var scope = new Mock<IInstructorScopeService>();
            scope.Setup(s => s.GetDirectCurriculumIdsAsync(It.IsAny<int>()))
                 .ReturnsAsync(new List<int> { _curriculumId });

            return new QuestionReviewTaskService(
                _factory!, new FixedTimeProvider(), scope.Object,
                new Mock<IAdvancedNotificationService>().Object,
                NullLogger<QuestionReviewTaskService>.Instance);
        }

        private async Task<Instructor> SeedInstructorAsync(string name)
        {
            await using var db = _factory!.CreateDbContext();
            // Instructors.UserId مفتاح أجنبي إلى AspNetUsers
            var userId = $"qrtsql-{Guid.NewGuid():N}";
            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = userId,
                NormalizedUserName = userId.ToUpperInvariant(),
                Email = $"{userId}@qrtsql.local",
                NormalizedEmail = $"{userId}@qrtsql.local".ToUpperInvariant(),
                FullName = name,
                SecurityStamp = Guid.NewGuid().ToString()
            });
            var instructor = new Instructor
            {
                FullName = name,
                NationalID = $"{Marker}{Guid.NewGuid():N}"[..20],
                Email = $"{Guid.NewGuid():N}@qrtsql.local",
                Specialization = "عام",
                IsActive = true,
                UserId = userId
            };
            db.Instructors.Add(instructor);
            await db.SaveChangesAsync();
            return instructor;
        }

        private async Task<List<Guid>> SeedQuestionsAsync(int count)
        {
            await using var db = _factory!.CreateDbContext();
            var list = Enumerable.Range(0, count).Select(i => new Question
            {
                Title = $"سؤال اختبار {i}",
                ReferenceNumber = $"{Marker}-{Guid.NewGuid():N}"[..16],
                CurriculumId = _curriculumId,
                LessonId = _lessonId,
                SectionId = _sectionId,
                CorrectAnswer = "أ",
                IsComplete = true
            }).ToList();
            db.Questions.AddRange(list);
            await db.SaveChangesAsync();
            return list.Select(q => q.Id).ToList();
        }

        private static CreateQuestionReviewTaskInput Input(int instructorId, IEnumerable<Guid> ids) => new()
        {
            Title = "مراجعة اختبار SQL",
            InstructorId = instructorId,
            SelectedQuestionIds = ids.ToList()
        };

        private static ReviewActor InstructorActor(Instructor i) => new(i.UserId!, i.FullName, UserRoleType.Instructor);

        private async Task<int> ActiveLocksAsync(IEnumerable<Guid> questionIds)
        {
            var ids = questionIds.ToList();
            await using var db = _factory!.CreateDbContext();
            var locked = await db.QuestionReviewTaskItems.AsNoTracking()
                .Where(i => i.IsLockActive).Select(i => i.QuestionId).ToListAsync();
            return locked.Count(ids.Contains);
        }

        // ---------- الاختبارات ----------

        [SqlServerFact]
        public async Task ActiveLockIndex_RejectsSecondActiveItem_ButAllowsInactiveHistory()
        {
            var a = await SeedInstructorAsync("أ. الأول");
            var b = await SeedInstructorAsync("أ. الثاني");
            var qid = (await SeedQuestionsAsync(1))[0];

            await using (var db = _factory!.CreateDbContext())
            {
                var t1 = NewTask(a.Id, "QRTSQL-T1-" + Guid.NewGuid().ToString("N")[..6]);
                t1.Items.Add(new QuestionReviewTaskItem { QuestionId = qid, SortOrder = 1, IsLockActive = true });
                db.QuestionReviewTasks.Add(t1);
                await db.SaveChangesAsync();
            }

            // عنصر ثانٍ نشط لنفس السؤال في مهمة أخرى ← ينتهك الفهرس الفريد المُصفّى
            await using (var db = _factory.CreateDbContext())
            {
                var t2 = NewTask(b.Id, "QRTSQL-T2-" + Guid.NewGuid().ToString("N")[..6]);
                t2.Items.Add(new QuestionReviewTaskItem { QuestionId = qid, SortOrder = 1, IsLockActive = true });
                db.QuestionReviewTasks.Add(t2);
                var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Contains("UX_QuestionReviewTaskItems_ActiveLock", ex.ToString());
            }

            // نفس السؤال بعنصر غير نشط (تاريخ) مسموح
            await using (var db = _factory.CreateDbContext())
            {
                var t3 = NewTask(b.Id, "QRTSQL-T3-" + Guid.NewGuid().ToString("N")[..6]);
                t3.Items.Add(new QuestionReviewTaskItem
                {
                    QuestionId = qid, SortOrder = 1, IsLockActive = false, Status = QuestionReviewTaskItemStatus.Removed
                });
                db.QuestionReviewTasks.Add(t3);
                await db.SaveChangesAsync();
            }

            Assert.Equal(1, await ActiveLocksAsync(new[] { qid }));
        }

        private static QuestionReviewTask NewTask(int instructorId, string code) => new()
        {
            Code = code,
            Title = "مهمة اختبار",
            InstructorId = instructorId,
            Status = QuestionReviewTaskStatus.Assigned,
            CreatedByUserId = "qrtsql-admin",
            CreatedAtUtc = DateTime.UtcNow,
            TotalItems = 1,
            PendingItems = 1
        };

        [SqlServerFact]
        public async Task Create_ConcurrentSameQuestions_ExactlyOneWins_NoPartialRows()
        {
            // عدة جولات لرفع احتمال التصادم الحقيقي بين معاملتين
            for (var round = 0; round < 6; round++)
            {
                var a = await SeedInstructorAsync($"أ. تزامن أ{round}");
                var b = await SeedInstructorAsync($"أ. تزامن ب{round}");
                var questions = await SeedQuestionsAsync(5);

                var gate = new TaskCompletionSource();
                async Task<OperationResult> Run(int instructorId)
                {
                    await gate.Task;
                    return await NewService().CreateTaskAsync(Input(instructorId, questions), Admin);
                }

                var t1 = Run(a.Id);
                var t2 = Run(b.Id);
                gate.SetResult();
                var results = await Task.WhenAll(t1, t2);

                Assert.Equal(1, results.Count(r => r.Success));

                await using var db = _factory!.CreateDbContext();
                var tasks = await db.QuestionReviewTasks.AsNoTracking()
                    .Where(t => t.InstructorId == a.Id || t.InstructorId == b.Id)
                    .Select(t => new { t.InstructorId, t.TotalItems, Items = t.Items.Count })
                    .ToListAsync();
                Assert.Single(tasks);                         // الخاسر لم يترك مهمة جزئية
                Assert.Equal(5, tasks[0].Items);
                Assert.Equal(5, tasks[0].TotalItems);
                Assert.Equal(5, await ActiveLocksAsync(questions)); // حجز واحد لكل سؤال
            }
        }

        [SqlServerFact]
        public async Task Create_WithAlreadyLockedQuestions_Fails_WithoutPartialInsert()
        {
            var a = await SeedInstructorAsync("أ. حجز");
            var b = await SeedInstructorAsync("أ. آخر");
            var questions = await SeedQuestionsAsync(3);
            var svc = NewService();

            Assert.True((await svc.CreateTaskAsync(Input(a.Id, questions), Admin)).Success);
            var second = await svc.CreateTaskAsync(Input(b.Id, questions), Admin);

            Assert.False(second.Success);
            await using var db = _factory!.CreateDbContext();
            Assert.Equal(0, await db.QuestionReviewTasks.CountAsync(t => t.InstructorId == b.Id));
            Assert.Equal(3, await ActiveLocksAsync(questions));
        }

        [SqlServerFact]
        public async Task RowVersion_SecondConflictingSave_ThrowsConcurrencyException()
        {
            var a = await SeedInstructorAsync("أ. نسخة");
            var questions = await SeedQuestionsAsync(1);
            Assert.True((await NewService().CreateTaskAsync(Input(a.Id, questions), Admin)).Success);

            await using var first = _factory!.CreateDbContext();
            await using var second = _factory.CreateDbContext();
            var itemA = await first.QuestionReviewTaskItems.FirstAsync(i => i.QuestionId == questions[0]);
            var itemB = await second.QuestionReviewTaskItems.FirstAsync(i => i.QuestionId == questions[0]);

            itemA.ReturnNote = "تعديل أول";
            await first.SaveChangesAsync();

            itemB.ReturnNote = "تعديل متعارض";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }

        [SqlServerFact]
        public async Task Return_ConcurrentOnSameItem_OnlyOneSucceeds_CountersConsistent()
        {
            var a = await SeedInstructorAsync("أ. إرجاع");
            var questions = await SeedQuestionsAsync(1);
            Assert.True((await NewService().CreateTaskAsync(Input(a.Id, questions), Admin)).Success);

            long itemId;
            int taskId;
            await using (var db = _factory!.CreateDbContext())
            {
                var item = await db.QuestionReviewTaskItems.AsNoTracking().FirstAsync(i => i.QuestionId == questions[0]);
                itemId = item.Id;
                taskId = item.TaskId;
            }

            var gate = new TaskCompletionSource();
            async Task<OperationResult> Run()
            {
                await gate.Task;
                return await NewService().ReturnItemAsync(a.Id, itemId, "ملاحظة إرجاع كافية الطول", InstructorActor(a));
            }
            var t1 = Run();
            var t2 = Run();
            gate.SetResult();
            var results = await Task.WhenAll(t1, t2);

            Assert.Equal(1, results.Count(r => r.Success));

            await using var check = _factory!.CreateDbContext();
            var task = await check.QuestionReviewTasks.AsNoTracking().FirstAsync(t => t.Id == taskId);
            Assert.Equal(1, task.ReturnedItems);
            Assert.Equal(0, task.PendingItems);
            var audits = await check.QuestionAuditLogs.CountAsync(l => l.QuestionId == questions[0] && l.Action == "إرجاع من مهمة مراجعة");
            Assert.Equal(1, audits);
        }

        [SqlServerFact]
        public async Task Cancel_ReleasesAllLocks_AndQuestionsBecomeAssignableAgain()
        {
            var a = await SeedInstructorAsync("أ. إلغاء");
            var b = await SeedInstructorAsync("أ. بديل");
            var questions = await SeedQuestionsAsync(4);
            var svc = NewService();
            Assert.True((await svc.CreateTaskAsync(Input(a.Id, questions), Admin)).Success);

            int taskId;
            await using (var db = _factory!.CreateDbContext())
                taskId = await db.QuestionReviewTasks.Where(t => t.InstructorId == a.Id).Select(t => t.Id).SingleAsync();

            var cancel = await svc.CancelTaskAsync(taskId, "إلغاء لاختبار التكامل", Admin);
            Assert.True(cancel.Success, cancel.Message);
            Assert.Equal(0, await ActiveLocksAsync(questions));

            var again = await svc.CreateTaskAsync(Input(b.Id, questions), Admin);
            Assert.True(again.Success, again.Message);
            Assert.Equal(4, await ActiveLocksAsync(questions));
        }

        [SqlServerFact]
        public async Task Reassign_MovesPendingToNewInstructor_ReleasingOldLockBeforeNewInsert()
        {
            var a = await SeedInstructorAsync("أ. مصدر");
            var b = await SeedInstructorAsync("أ. وجهة");
            var questions = await SeedQuestionsAsync(3);
            var svc = NewService();
            Assert.True((await svc.CreateTaskAsync(Input(a.Id, questions), Admin)).Success);

            int taskId;
            await using (var db = _factory!.CreateDbContext())
                taskId = await db.QuestionReviewTasks.Where(t => t.InstructorId == a.Id).Select(t => t.Id).SingleAsync();

            var result = await svc.ReassignRemainingAsync(taskId, b.Id, Admin);
            Assert.True(result.Success, result.Message);

            await using var check = _factory!.CreateDbContext();
            var newTask = await check.QuestionReviewTasks.AsNoTracking()
                .SingleAsync(t => t.InstructorId == b.Id);
            Assert.Equal(taskId, newTask.ParentTaskId);
            Assert.Equal(3, newTask.TotalItems);
            Assert.Equal(3, await ActiveLocksAsync(questions)); // حجز واحد لكل سؤال بعد النقل
        }

        [SqlServerFact]
        public async Task HeldQuestionIds_AndHold_WorkOnSqlServer_WithoutOpenJson()
        {
            var a = await SeedInstructorAsync("أ. حجز عام");
            var questions = await SeedQuestionsAsync(2);
            Assert.True((await NewService().CreateTaskAsync(Input(a.Id, questions), Admin)).Success);

            var locks = new QuestionReviewLockService(_factory!);
            await using var db = _factory!.CreateDbContext();

            var held = locks.HeldQuestionIds(db);
            var composed = db.Questions.AsNoTracking().Where(q => !held.Contains(q.Id)).Select(q => q.Id).Take(25);
            var sql = composed.ToQueryString();
            Assert.DoesNotContain("OPENJSON", sql, StringComparison.OrdinalIgnoreCase);

            // الاستعلام المركّب يُنفَّذ فعلًا، والمحجوز لا يظهر في المسار العام
            var visible = await composed.ToListAsync();
            Assert.DoesNotContain(visible, id => questions.Contains(id));

            Assert.NotNull(await locks.GetHoldAsync(questions[0]));
        }
    }
}
