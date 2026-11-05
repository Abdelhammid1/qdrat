using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Implementations;
using QdratNew.Services.Interfaces;
using QdratNew.Services.ProfessionalModels;
using QdratNew.Tests.Integration;
using Xunit;
using Xunit.Abstractions;
using HomeworkEntity = QdratNew.Entities.Homework;

namespace QdratNew.Tests.PartnerProfessionalModels
{
    // PPM-S3.3 — BulkInsert + ExecutionStrategy + Transaction + sp_getapplock على SQL Server حقيقي بإعداد الإنتاج.
    // التشغيل: QDRAT_TEST_SQL على قاعدة الاختبار المعزولة. بدونه تُتخطّى.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class ProfessionalModelSendServiceSqlServerTests : IAsyncLifetime
    {
        private const string Marker = "PPMS3";
        private const string SenderId = "ppms3-sender";

        private readonly ITestOutputHelper _output;

        public ProfessionalModelSendServiceSqlServerTests(ITestOutputHelper output) => _output = output;

        private sealed record Seed(
            int PartnerA, int PartnerB, int CurriculumId, int LessonId,
            int BatchA, int BatchA2, int EmptyBatchA, int ArchivedBatchA, int OutOfContractBatchA, int OtherCurriculumBatchA, int BatchB, int InstituteBatch,
            int ActiveStudent1, int ActiveStudent2, int InactiveStudent, int LeftStudent, int ForeignStudent, int StudentInA2,
            int ModelId, Guid Q1, Guid Q2, Guid Q3,
            Guid Substitute, Guid RejectedSubstitute, Guid OtherLessonSubstitute, Guid PartnerOwnedSubstitute);

        private static ModelSendScope ScopeA(Seed seed) => ModelSendScope.Partner(seed.PartnerA, SenderId, $"{Marker} مرسل");

        private static ProfessionalModelSendService NewService(ApplicationDbContext db, IAdvancedNotificationService? notifications = null)
            => new(db, new ProfessionalModelAvailabilityService(db), new TimeZoneService(), notifications ?? new AdvancedNotificationService(db), NullLogger<ProfessionalModelSendService>.Instance);

        private sealed class FailingSendService : ProfessionalModelSendService
        {
            public FailingSendService(ApplicationDbContext db)
                : base(db, new ProfessionalModelAvailabilityService(db), new TimeZoneService(), new AdvancedNotificationService(db), NullLogger<ProfessionalModelSendService>.Instance)
            {
            }

            protected override Task InsertHomeworksAsync(List<HomeworkEntity> homeworks, CancellationToken ct)
                => throw new InvalidOperationException("فشل مصطنع في منتصف الإرسال");
        }

        // توقيت السعودية بلا Kind كما يصل من النموذج
        private static DateTime LocalInFuture(int days)
        {
            var saudi = DateTime.UtcNow.AddHours(3).AddDays(days);
            return DateTime.SpecifyKind(new DateTime(saudi.Year, saudi.Month, saudi.Day, saudi.Hour, saudi.Minute, 0), DateTimeKind.Unspecified);
        }

        private static SendModelHomeworkRequest Request(Seed seed, params int[] batchIds) => new()
        {
            ModelId = seed.ModelId,
            BatchIds = batchIds.ToList(),
            StartAtLocal = LocalInFuture(0),
            EndAtLocal = LocalInFuture(3)
        };

        public Task InitializeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPMS3%')";
            const string sets = "(SELECT Id FROM HomeworkSets WHERE BatchId IN " + batches + ")";
            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS3%')";
            const string partners = "(SELECT Id FROM Partners WHERE Code LIKE 'PPMS3%')";
            const string courses = "(SELECT Id FROM Courses WHERE Name LIKE 'PPMS3%')";

            foreach (var sql in new[]
            {
                $"DELETE FROM Notifications WHERE StudentID IN {students}",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPMS3%'",
                $"DELETE FROM Homeworks WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSetStudents WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSets WHERE BatchId IN {batches}",
                "DELETE FROM ProfessionalModelQuestions WHERE ModelId IN (SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPMS3%')",
                "DELETE FROM ProfessionalModels WHERE Title LIKE 'PPMS3%'",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPMS3%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPMS3%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPMS3%'",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'ppms3-%'",
                $"DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN (SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN {partners})",
                $"DELETE FROM PartnerSubscriptions WHERE PartnerId IN {partners}",
                $"DELETE FROM CourseCurriculums WHERE CourseId IN {courses}",
                "DELETE FROM Branches WHERE Name LIKE 'PPMS3%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPMS3%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPMS3%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        private static Question NewQuestion(string reference, int curriculumId, int lessonId, int? partnerId = null, bool rejected = false) => new()
        {
            Id = Guid.NewGuid(),
            Title = $"سؤال {reference}",
            ReferenceNumber = reference,
            CurriculumId = curriculumId,
            LessonId = lessonId,
            CorrectAnswer = "أ",
            IsComplete = true,
            IsReviewed = true,
            IsRejected = rejected,
            PartnerId = partnerId
        };

        private static Branch NewBranch(string name, int? partnerId)
            => new() { Name = $"{Marker} {name}", PartnerId = partnerId, Location = "-", City = "-", State = "-", Country = "-" };

        private static Student NewStudent(ApplicationDbContext db, int i, int branchId, bool active = true)
        {
            var userName = $"ppms3-s{i}";
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(), UserName = userName, NormalizedUserName = userName.ToUpperInvariant(),
                Email = $"{userName}@test.local", NormalizedEmail = $"{userName}@test.local".ToUpperInvariant(),
                NationalID = $"{Marker}{i}", FullName = $"طالب {Marker} {i}", IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N")
            };
            db.Users.Add(user);

            var student = new Student
            {
                NationalID = $"{Marker}{i}", FullName = $"طالب {Marker} {i}", Gender = "ذكر", Age = 17,
                School = "-", Level = "-", UserId = user.Id, BranchId = branchId, IsActiveForLearning = active
            };
            db.Students.Add(student);
            return student;
        }

        private static async Task<Seed> SeedAsync(bool canCreateHomework = true, bool bankAllowed = true)
        {
            await using var db = RtkRealDb.CreateDb();

            var anchor = await db.Lessons.AsNoTracking()
                .Where(l => l.Section != null)
                .Select(l => new { LessonId = l.Id, l.Section.CurriculumId })
                .FirstAsync();
            var otherLessonId = await db.Lessons.AsNoTracking().Where(l => l.Id != anchor.LessonId).Select(l => l.Id).FirstAsync();
            var otherCurriculumId = await db.Curriculums.AsNoTracking().Where(c => c.Id != anchor.CurriculumId).Select(c => c.Id).FirstAsync();

            db.Users.Add(new ApplicationUser
            {
                Id = SenderId, UserName = SenderId, NormalizedUserName = SenderId.ToUpperInvariant(),
                Email = $"{SenderId}@test.local", NormalizedEmail = $"{SenderId}@test.local".ToUpperInvariant(),
                NationalID = $"{Marker}-SENDER", FullName = $"{Marker} مرسل", IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N")
            });

            var today = DateTime.Today;
            var a = new Partner { Name = $"{Marker} A", Code = $"{Marker}-A", PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            var b = new Partner { Name = $"{Marker} B", Code = $"{Marker}-B", PartnershipStart = today.AddDays(-1), PartnershipEnd = today.AddDays(30) };
            db.Partners.AddRange(a, b);

            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var outOfContract = new Course { Name = $"{Marker} خارج العقد", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var otherCurriculum = new Course { Name = $"{Marker} منهج آخر", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            db.Courses.AddRange(course, outOfContract, otherCurriculum);
            await db.SaveChangesAsync();

            db.CourseCurriculums.AddRange(
                new CourseCurriculum { CourseId = course.Id, CurriculumId = anchor.CurriculumId },
                new CourseCurriculum { CourseId = outOfContract.Id, CurriculumId = anchor.CurriculumId },
                new CourseCurriculum { CourseId = otherCurriculum.Id, CurriculumId = otherCurriculumId });

            var contract = new PartnerSubscription
            {
                PartnerId = a.Id,
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(30),
                CanUseProfessionalModels = true,
                CanCreateHomework = canCreateHomework
            };
            db.PartnerSubscriptions.AddRange(contract, new PartnerSubscription
            {
                PartnerId = b.Id,
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(30),
                CanUseProfessionalModels = true,
                CanCreateHomework = true
            });
            await db.SaveChangesAsync();

            db.PartnerSubscriptionCourses.AddRange(
                new PartnerSubscriptionCourse { PartnerSubscriptionId = contract.Id, CourseId = course.Id, CanUsePlatformQuestionBank = bankAllowed },
                new PartnerSubscriptionCourse { PartnerSubscriptionId = contract.Id, CourseId = otherCurriculum.Id });

            var branchA = NewBranch("فرع A", a.Id);
            var branchB = NewBranch("فرع B", b.Id);
            var instituteBranch = NewBranch("معهد", null);
            db.Branches.AddRange(branchA, branchB, instituteBranch);
            await db.SaveChangesAsync();

            Batch NewBatch(string name, int branchId, int courseId, bool archived = false)
            {
                var batch = new Batch { Name = $"{Marker} {name}", BranchId = branchId, CourseId = courseId, StartDate = DateTime.UtcNow, IsArchived = archived };
                db.Batches.Add(batch);
                return batch;
            }

            var batchA = NewBatch("دفعة A1", branchA.Id, course.Id);
            var batchA2 = NewBatch("دفعة A2", branchA.Id, course.Id);
            var emptyA = NewBatch("دفعة A فارغة", branchA.Id, course.Id);
            var archivedA = NewBatch("دفعة A مؤرشفة", branchA.Id, course.Id, archived: true);
            var outOfContractA = NewBatch("دفعة A خارج العقد", branchA.Id, outOfContract.Id);
            var otherCurriculumA = NewBatch("دفعة A منهج آخر", branchA.Id, otherCurriculum.Id);
            var batchB = NewBatch("دفعة B", branchB.Id, course.Id);
            var instituteBatch = NewBatch("دفعة معهد", instituteBranch.Id, course.Id);
            await db.SaveChangesAsync();

            var active1 = NewStudent(db, 1, branchA.Id);
            var active2 = NewStudent(db, 2, branchA.Id);
            var inactive = NewStudent(db, 3, branchA.Id, active: false);
            var left = NewStudent(db, 4, branchA.Id);
            var foreign = NewStudent(db, 5, branchB.Id); // طالب شريك آخر مُسجَّل خطأً في دفعة الشريك A
            var inA2 = NewStudent(db, 6, branchA.Id);
            var inB = NewStudent(db, 7, branchB.Id);
            var inInstitute = NewStudent(db, 8, instituteBranch.Id);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = active1.StudentID, BatchId = batchA.Id },
                new StudentBatchEnrollment { StudentID = active2.StudentID, BatchId = batchA.Id },
                new StudentBatchEnrollment { StudentID = inactive.StudentID, BatchId = batchA.Id },
                new StudentBatchEnrollment { StudentID = left.StudentID, BatchId = batchA.Id, Status = "Withdrawn" },
                new StudentBatchEnrollment { StudentID = foreign.StudentID, BatchId = batchA.Id },
                new StudentBatchEnrollment { StudentID = inA2.StudentID, BatchId = batchA2.Id },
                new StudentBatchEnrollment { StudentID = inB.StudentID, BatchId = batchB.Id },
                new StudentBatchEnrollment { StudentID = inInstitute.StudentID, BatchId = instituteBatch.Id });

            var q1 = NewQuestion($"{Marker}-Q1", anchor.CurriculumId, anchor.LessonId);
            var q2 = NewQuestion($"{Marker}-Q2", anchor.CurriculumId, anchor.LessonId);
            var q3 = NewQuestion($"{Marker}-Q3", anchor.CurriculumId, otherLessonId);
            var substitute = NewQuestion($"{Marker}-SUB", anchor.CurriculumId, anchor.LessonId);
            var rejected = NewQuestion($"{Marker}-REJ", anchor.CurriculumId, anchor.LessonId, rejected: true);
            var otherLesson = NewQuestion($"{Marker}-OTH", anchor.CurriculumId, otherLessonId);
            var partnerOwned = NewQuestion($"{Marker}-OWN", anchor.CurriculumId, anchor.LessonId, partnerId: a.Id);
            db.Questions.AddRange(q1, q2, q3, substitute, rejected, otherLesson, partnerOwned);

            var model = new ProfessionalModel
            {
                Title = $"{Marker} نموذج",
                Description = "-",
                CreatedBy = "-",
                CreatedAt = DateTime.UtcNow,
                CurriculumId = anchor.CurriculumId,
                ModelType = ProfessionalModelType.Homework,
                IsForPartners = true,
                IsGlobal = true,
                Questions = new List<ProfessionalModelQuestion>
                {
                    new() { QuestionId = q1.Id, OrderNumber = 1 },
                    new() { QuestionId = q2.Id, OrderNumber = 2 },
                    new() { QuestionId = q3.Id, OrderNumber = 3 },
                    new() { QuestionId = q1.Id, OrderNumber = 4 } // بعض النماذج الحالية فيها سؤال مكرر
                }
            };
            db.ProfessionalModels.Add(model);
            await db.SaveChangesAsync();

            return new Seed(
                a.Id, b.Id, anchor.CurriculumId, anchor.LessonId,
                batchA.Id, batchA2.Id, emptyA.Id, archivedA.Id, outOfContractA.Id, otherCurriculumA.Id, batchB.Id, instituteBatch.Id,
                active1.StudentID, active2.StudentID, inactive.StudentID, left.StudentID, foreign.StudentID, inA2.StudentID,
                model.Id, q1.Id, q2.Id, q3.Id,
                substitute.Id, rejected.Id, otherLesson.Id, partnerOwned.Id);
        }

        private static async Task<int> AddModelAsync(string title, ProfessionalModelType type, bool forPartners, Seed seed)
        {
            await using var db = RtkRealDb.CreateDb();
            var model = new ProfessionalModel
            {
                Title = $"{Marker} {title}", Description = "-", CreatedBy = "-", CreatedAt = DateTime.UtcNow,
                CurriculumId = seed.CurriculumId, ModelType = type, IsForPartners = forPartners, IsGlobal = true,
                Questions = new List<ProfessionalModelQuestion> { new() { QuestionId = seed.Q1, OrderNumber = 1 } }
            };
            db.ProfessionalModels.Add(model);
            await db.SaveChangesAsync();
            return model.Id;
        }

        // إرسال سابق «قديم» خارج نافذة النقر المزدوج
        private static async Task AddOldSetAsync(int batchId, int studentId, Guid questionId, int? sourceModelId)
        {
            await using var db = RtkRealDb.CreateDb();
            var set = new HomeworkSet
            {
                BatchId = batchId,
                Title = $"{Marker} قديم",
                CompletionTitle = "-",
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                IsSent = true,
                SourceProfessionalModelId = sourceModelId
            };
            db.HomeworkSets.Add(set);
            await db.SaveChangesAsync();
            db.Homeworks.Add(new HomeworkEntity { HomeworkSetId = set.Id, StudentId = studentId, QuestionId = questionId, IsSent = true });
            await db.SaveChangesAsync();
        }

        private static async Task<(int Sets, int Students, int Homeworks)> CountWritesAsync(int batchId)
        {
            await using var db = RtkRealDb.CreateDb();
            return (
                await db.HomeworkSets.CountAsync(h => h.BatchId == batchId),
                await db.HomeworkSetStudents.CountAsync(s => s.HomeworkSet.BatchId == batchId),
                await db.Homeworks.CountAsync(h => h.HomeworkSet.BatchId == batchId));
        }

        // =====================================================
        // الإرسال السليم
        // =====================================================
        [SqlServerFact]
        public async Task Partner_Send_WritesFullSnapshot_WithTracking_AndUtcDates()
        {
            var seed = await SeedAsync();
            var request = Request(seed, seed.BatchA, seed.BatchA2);
            request.Title = "  واجب الأسبوع  ";

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(ScopeA(seed), request);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(2, result.BatchesSent);
            Assert.Equal(3, result.StudentsSent);

            await using var read = RtkRealDb.CreateDb();
            var set = await read.HomeworkSets.AsNoTracking().SingleAsync(h => h.BatchId == seed.BatchA);
            Assert.Equal("واجب الأسبوع", set.Title);
            Assert.Equal($"{Marker} نموذج", set.CompletionTitle);
            Assert.True(set.IsFromProfessionalModel);
            Assert.Equal(seed.ModelId, set.SourceProfessionalModelId);
            Assert.True(set.IsExtra);
            Assert.Null(set.LectureId);
            Assert.True(set.IsSent);
            Assert.Equal(SenderId, set.AssignedByUserId);
            Assert.Equal(request.StartAtLocal!.Value.AddHours(-3), set.StartAt);
            Assert.Equal(request.EndAtLocal!.Value.AddHours(-3), set.EndAt);

            // طالبان نشطان × 3 أسئلة بعد إسقاط السؤال المكرر في النموذج
            var homeworks = await read.Homeworks.AsNoTracking().Where(h => h.HomeworkSetId == set.Id).ToListAsync();
            Assert.Equal(6, homeworks.Count);
            Assert.All(homeworks, h =>
            {
                Assert.True(h.IsSent);
                Assert.Equal(HomeworkStatus.Pending, h.Status);
                Assert.NotNull(h.LessonId);
            });
            Assert.Equal(2, await read.HomeworkSetStudents.CountAsync(s => s.HomeworkSetId == set.Id));
            Assert.Equal((1, 1, 3), await CountWritesAsync(seed.BatchA2));

            // الإشعار والسجل بعد الـ Commit
            Assert.Equal(3, await read.Notifications.CountAsync(n =>
                n.Category == NotificationCategory.Homework &&
                (n.StudentID == seed.ActiveStudent1 || n.StudentID == seed.ActiveStudent2 || n.StudentID == seed.StudentInA2)));
            Assert.True(await read.SystemLogs.AnyAsync(l => l.EntityId == seed.ModelId && l.Action == "SendHomeworkFromModel" && l.Description.Contains(Marker)));
        }

        [SqlServerFact]
        public async Task Partner_Send_OnlyActiveEnrollmentsOfOwnBranch_Receive()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(ScopeA(seed), Request(seed, seed.BatchA));
            Assert.True(result.Succeeded, result.Message);

            await using var read = RtkRealDb.CreateDb();
            var recipients = await read.HomeworkSetStudents.Where(s => s.HomeworkSet.BatchId == seed.BatchA).Select(s => s.StudentId).ToListAsync();
            Assert.Equal(new[] { seed.ActiveStudent1, seed.ActiveStudent2 }.OrderBy(x => x), recipients.OrderBy(x => x));

            var withHomework = await read.Homeworks.Where(h => h.HomeworkSet.BatchId == seed.BatchA).Select(h => h.StudentId).Distinct().ToListAsync();
            Assert.DoesNotContain(seed.InactiveStudent, withHomework);
            Assert.DoesNotContain(seed.LeftStudent, withHomework);
            Assert.DoesNotContain(seed.ForeignStudent, withHomework);
        }

        [SqlServerFact]
        public async Task Admin_Send_ReachesAnyBatch_AndFiltersByEnrollmentStatusOnly()
        {
            var seed = await SeedAsync();
            var internalModel = await AddModelAsync("داخلي", ProfessionalModelType.Homework, forPartners: false, seed);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(ModelSendScope.Admin(SenderId, $"{Marker} أدمن"), new SendModelHomeworkRequest
            {
                ModelId = internalModel,
                BatchIds = new List<int> { seed.BatchA, seed.InstituteBatch }
            });

            Assert.True(result.Succeeded, result.Message);

            await using var read = RtkRealDb.CreateDb();
            var recipients = await read.HomeworkSetStudents.Where(s => s.HomeworkSet.BatchId == seed.BatchA).Select(s => s.StudentId).ToListAsync();
            Assert.Contains(seed.InactiveStudent, recipients);
            Assert.Contains(seed.ForeignStudent, recipients);
            Assert.DoesNotContain(seed.LeftStudent, recipients);
            Assert.Equal((1, 1, 1), await CountWritesAsync(seed.InstituteBatch));
        }

        [SqlServerFact]
        public async Task Send_SkipsBatchWithoutActiveStudents_AndNamesItInTheMessage()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(ScopeA(seed), Request(seed, seed.BatchA2, seed.EmptyBatchA));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(1, result.BatchesSent);
            Assert.Contains("دفعة A فارغة", result.Message);
            Assert.Equal((0, 0, 0), await CountWritesAsync(seed.EmptyBatchA));
        }

        [SqlServerFact]
        public async Task Send_OnlyEmptyBatches_Fails()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(ScopeA(seed), Request(seed, seed.EmptyBatchA));

            Assert.False(result.Succeeded);
            Assert.Contains("لا يوجد طلاب نشطون", result.Message);
        }

        // =====================================================
        // العزل والعقد
        // =====================================================
        [SqlServerFact]
        public async Task Partner_OtherPartnersBatch_Fails_WithoutWriting()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(ScopeA(seed), Request(seed, seed.BatchA, seed.BatchB));

            Assert.False(result.Succeeded);
            Assert.False(result.IsNotFound);
            Assert.Contains("دفعات غير صالحة", result.Message);
            Assert.Equal((0, 0, 0), await CountWritesAsync(seed.BatchA));
            Assert.Equal((0, 0, 0), await CountWritesAsync(seed.BatchB));
        }

        [SqlServerFact]
        public async Task Partner_IneligibleOwnBatches_Fail()
        {
            var seed = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            foreach (var batchId in new[] { seed.ArchivedBatchA, seed.OutOfContractBatchA, seed.OtherCurriculumBatchA, seed.InstituteBatch })
            {
                var result = await service.SendHomeworkAsync(ScopeA(seed), Request(seed, batchId));
                Assert.False(result.Succeeded);
                Assert.Contains("دفعات غير صالحة", result.Message);
                Assert.Equal((0, 0, 0), await CountWritesAsync(batchId));
            }
        }

        [SqlServerFact]
        public async Task GetEligibleBatches_Partner_ListsOnlyItsEligibleBatches_WithActiveCounts()
        {
            var seed = await SeedAsync();
            await AddOldSetAsync(seed.BatchA2, seed.StudentInA2, seed.Q1, seed.ModelId);

            await using var db = RtkRealDb.CreateDb();
            var batches = await NewService(db).GetEligibleBatchesAsync(ScopeA(seed), seed.ModelId);

            Assert.Equal(
                new[] { seed.BatchA, seed.BatchA2, seed.EmptyBatchA }.OrderBy(x => x),
                batches.Select(b => b.BatchId).OrderBy(x => x));
            Assert.Equal(2, batches.Single(b => b.BatchId == seed.BatchA).ActiveStudents);
            Assert.False(batches.Single(b => b.BatchId == seed.BatchA).AlreadySentFromModel);
            Assert.True(batches.Single(b => b.BatchId == seed.BatchA2).AlreadySentFromModel);
        }

        [SqlServerFact]
        public async Task Partner_ModelNotAvailable_IsNotFound()
        {
            var seed = await SeedAsync();
            var internalModel = await AddModelAsync("داخلي", ProfessionalModelType.Homework, forPartners: false, seed);
            var examModel = await AddModelAsync("اختبار", ProfessionalModelType.Exam, forPartners: true, seed);

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            foreach (var modelId in new[] { internalModel, examModel })
            {
                var request = Request(seed, seed.BatchA);
                request.ModelId = modelId;

                var result = await service.SendHomeworkAsync(ScopeA(seed), request);

                Assert.False(result.Succeeded);
                Assert.True(result.IsNotFound);
                Assert.Null(await service.PreviewAsync(ScopeA(seed), ProfessionalModelType.Homework, request));
            }

            // نموذج الاختبار له دفعاته وبدائله منذ PPM-S5، لكنه لا يُرسل كواجب كما فوق
            Assert.Empty(await service.GetEligibleBatchesAsync(ScopeA(seed), internalModel));
            Assert.Empty(await service.GetReplacementCandidatesAsync(ScopeA(seed), internalModel, seed.Q1));

            Assert.Equal((0, 0, 0), await CountWritesAsync(seed.BatchA));
        }

        [SqlServerFact]
        public async Task Partner_ContractWithoutCreateHomework_IsNotFound()
        {
            var seed = await SeedAsync(canCreateHomework: false);

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);
            var result = await service.SendHomeworkAsync(ScopeA(seed), Request(seed, seed.BatchA));

            Assert.True(result.IsNotFound);
            Assert.Empty(await service.GetModelsAsync(ScopeA(seed), ProfessionalModelType.Homework));
            Assert.Equal((0, 0, 0), await CountWritesAsync(seed.BatchA));
        }

        [SqlServerFact]
        public async Task GetModels_FiltersByType_AndCountsQuestions()
        {
            var seed = await SeedAsync();
            await AddModelAsync("اختبار", ProfessionalModelType.Exam, forPartners: true, seed);

            await using var db = RtkRealDb.CreateDb();
            var models = await NewService(db).GetModelsAsync(ScopeA(seed), ProfessionalModelType.Homework);

            var model = Assert.Single(models, m => m.Title.StartsWith(Marker));
            Assert.Equal(seed.ModelId, model.Id);
            Assert.Equal(4, model.QuestionCount);
        }

        // =====================================================
        // التكرار
        // =====================================================
        [SqlServerFact]
        public async Task AlreadySentModel_RequiresConfirmation_ThenSends()
        {
            var seed = await SeedAsync();
            await AddOldSetAsync(seed.BatchA2, seed.StudentInA2, seed.Q1, seed.ModelId);
            var before = await CountWritesAsync(seed.BatchA2);

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);
            var request = Request(seed, seed.BatchA2);

            var blocked = await service.SendHomeworkAsync(ScopeA(seed), request);

            Assert.False(blocked.Succeeded);
            Assert.True(blocked.RequiresConfirmation);
            var warning = Assert.Single(blocked.Preview!.Batches);
            Assert.True(warning.AlreadySentFromModel);
            Assert.Equal(1, warning.RepeatedQuestionCount);
            Assert.Equal(before, await CountWritesAsync(seed.BatchA2));

            request.ConfirmWarnings = true;
            var confirmed = await service.SendHomeworkAsync(ScopeA(seed), request);

            Assert.True(confirmed.Succeeded, confirmed.Message);
            Assert.Equal(2, (await CountWritesAsync(seed.BatchA2)).Sets);
        }

        [SqlServerFact]
        public async Task RepeatedQuestionFromAnotherHomework_RequiresConfirmation()
        {
            var seed = await SeedAsync();
            await AddOldSetAsync(seed.BatchA2, seed.StudentInA2, seed.Q2, sourceModelId: null);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(ScopeA(seed), Request(seed, seed.BatchA2));

            Assert.True(result.RequiresConfirmation);
            var warning = Assert.Single(result.Preview!.Batches);
            Assert.False(warning.AlreadySentFromModel);
            Assert.Equal(1, warning.RepeatedQuestionCount);
        }

        [SqlServerFact]
        public async Task ExcludingTheRepeatedQuestion_RemovesTheWarning()
        {
            var seed = await SeedAsync();
            await AddOldSetAsync(seed.BatchA2, seed.StudentInA2, seed.Q2, sourceModelId: null);
            var request = Request(seed, seed.BatchA2);
            request.ExcludedQuestionIds = new List<Guid> { seed.Q2 };

            await using var db = RtkRealDb.CreateDb();
            var preview = await NewService(db).PreviewAsync(ScopeA(seed), ProfessionalModelType.Homework, request);

            Assert.False(preview!.HasWarnings);
            Assert.Equal(new[] { seed.Q1, seed.Q3 }, preview.Questions.Select(q => q.QuestionId));
        }

        [SqlServerFact]
        public async Task ArchivedPreviousSet_IsNotARepeat()
        {
            var seed = await SeedAsync();
            await AddOldSetAsync(seed.BatchA2, seed.StudentInA2, seed.Q1, seed.ModelId);

            await using (var pre = RtkRealDb.CreateDb())
                await pre.HomeworkSets.Where(h => h.BatchId == seed.BatchA2).ExecuteUpdateAsync(x => x.SetProperty(h => h.IsArchived, true));

            await using var db = RtkRealDb.CreateDb();
            var preview = await NewService(db).PreviewAsync(ScopeA(seed), ProfessionalModelType.Homework, Request(seed, seed.BatchA2));

            Assert.False(preview!.HasWarnings);
        }

        [SqlServerFact]
        public async Task SecondSend_WithinWindow_IsRejected_EvenWhenConfirmed()
        {
            var seed = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);
            var request = Request(seed, seed.BatchA2);

            Assert.True((await service.SendHomeworkAsync(ScopeA(seed), request)).Succeeded);

            request.ConfirmWarnings = true;
            var second = await service.SendHomeworkAsync(ScopeA(seed), request);

            Assert.False(second.Succeeded);
            Assert.Contains("قبل لحظات", second.Message);
            Assert.Equal(1, (await CountWritesAsync(seed.BatchA2)).Sets);
        }

        [SqlServerFact]
        public async Task ConcurrentSends_CreateOneHomeworkOnly()
        {
            var seed = await SeedAsync();

            async Task<ModelSendResult> SendAsync()
            {
                await using var db = RtkRealDb.CreateDb();
                var request = Request(seed, seed.BatchA);
                request.ConfirmWarnings = true;
                return await NewService(db).SendHomeworkAsync(ScopeA(seed), request);
            }

            var results = await Task.WhenAll(SendAsync(), SendAsync(), SendAsync());

            Assert.Single(results, r => r.Succeeded);
            Assert.Equal((1, 2, 6), await CountWritesAsync(seed.BatchA));
        }

        // =====================================================
        // الـ Transaction
        // =====================================================
        [SqlServerFact]
        public async Task FailureMidSend_RollsEverythingBack_AndHidesTheError()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await new FailingSendService(db).SendHomeworkAsync(ScopeA(seed), Request(seed, seed.BatchA, seed.BatchA2));

            Assert.False(result.Succeeded);
            Assert.DoesNotContain("مصطنع", result.Message);
            Assert.Equal((0, 0, 0), await CountWritesAsync(seed.BatchA));
            Assert.Equal((0, 0, 0), await CountWritesAsync(seed.BatchA2));

            await using var read = RtkRealDb.CreateDb();
            Assert.False(await read.Notifications.AnyAsync(n => n.StudentID == seed.ActiveStudent1));
            Assert.False(await read.SystemLogs.AnyAsync(l => l.EntityId == seed.ModelId && l.Action == "SendHomeworkFromModel"));
        }

        [SqlServerFact]
        public async Task NotificationFailure_DoesNotFailTheSend()
        {
            var seed = await SeedAsync();
            var notifications = new Mock<IAdvancedNotificationService>();
            notifications
                .Setup(x => x.SendToStudentsAsync(It.IsAny<List<int>>(), It.IsAny<string>(), It.IsAny<NotificationCategory>(), It.IsAny<string?>(), It.IsAny<int?>()))
                .ThrowsAsync(new InvalidOperationException("تعذر الإشعار"));

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db, notifications.Object).SendHomeworkAsync(ScopeA(seed), Request(seed, seed.BatchA2));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal((1, 1, 3), await CountWritesAsync(seed.BatchA2));
        }

        // =====================================================
        // الأسئلة: الاستبعاد والاستبدال
        // =====================================================
        [SqlServerFact]
        public async Task Replacement_SameLesson_IsSentInTheOriginalPosition()
        {
            var seed = await SeedAsync();
            var request = Request(seed, seed.BatchA2);
            request.ExcludedQuestionIds = new List<Guid> { seed.Q3 };
            request.Replacements = new Dictionary<Guid, Guid> { [seed.Q1] = seed.Substitute };

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            var preview = await service.PreviewAsync(ScopeA(seed), ProfessionalModelType.Homework, request);
            Assert.Null(preview!.ValidationError);
            Assert.Equal(new[] { seed.Substitute, seed.Q2 }, preview.Questions.Select(q => q.QuestionId));
            Assert.Equal(seed.Q1, preview.Questions[0].ReplacesQuestionId);
            Assert.Equal(new[] { 1, 2 }, preview.Questions.Select(q => q.OrderNumber));

            var result = await service.SendHomeworkAsync(ScopeA(seed), request);
            Assert.True(result.Succeeded, result.Message);

            await using var read = RtkRealDb.CreateDb();
            var sent = await read.Homeworks.Where(h => h.HomeworkSet.BatchId == seed.BatchA2).Select(h => h.QuestionId).ToListAsync();
            Assert.Equal(new[] { seed.Q2, seed.Substitute }.OrderBy(x => x), sent.OrderBy(x => x));
        }

        [SqlServerFact]
        public async Task InvalidReplacements_Fail_WithoutWriting()
        {
            var seed = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            var cases = new (Guid Old, Guid New, string Message)[]
            {
                (seed.Q1, seed.OtherLessonSubstitute, "ليس من نفس الدرس"),
                (seed.Q1, seed.RejectedSubstitute, "غير صالح"),
                (seed.Q1, seed.PartnerOwnedSubstitute, "غير صالح"),
                (seed.Q1, Guid.NewGuid(), "غير صالح"),
                (seed.Substitute, seed.Q1, "ليس ضمن النموذج")
            };

            foreach (var (oldId, newId, message) in cases)
            {
                var request = Request(seed, seed.BatchA2);
                request.Replacements = new Dictionary<Guid, Guid> { [oldId] = newId };

                var result = await service.SendHomeworkAsync(ScopeA(seed), request);

                Assert.False(result.Succeeded);
                Assert.Contains(message, result.Message);
            }

            // بديل موجود أصلًا في القائمة
            var duplicate = Request(seed, seed.BatchA2);
            duplicate.Replacements = new Dictionary<Guid, Guid> { [seed.Q1] = seed.Q2 };
            var duplicateResult = await service.SendHomeworkAsync(ScopeA(seed), duplicate);
            Assert.False(duplicateResult.Succeeded);
            Assert.Contains("موجود بالفعل", duplicateResult.Message);

            Assert.Equal((0, 0, 0), await CountWritesAsync(seed.BatchA2));
        }

        [SqlServerFact]
        public async Task Partner_WithoutPlatformBank_CannotReplace_ButCanExclude()
        {
            var seed = await SeedAsync(bankAllowed: false);
            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            var replace = Request(seed, seed.BatchA2);
            replace.Replacements = new Dictionary<Guid, Guid> { [seed.Q1] = seed.Substitute };

            var rejected = await service.SendHomeworkAsync(ScopeA(seed), replace);
            Assert.False(rejected.Succeeded);
            Assert.Contains("بنك المنصة", rejected.Message);
            Assert.Empty(await service.GetReplacementCandidatesAsync(ScopeA(seed), seed.ModelId, seed.Q1));

            var exclude = Request(seed, seed.BatchA2);
            exclude.ExcludedQuestionIds = new List<Guid> { seed.Q1 };

            var sent = await service.SendHomeworkAsync(ScopeA(seed), exclude);
            Assert.True(sent.Succeeded, sent.Message);
            Assert.Equal((1, 1, 2), await CountWritesAsync(seed.BatchA2));
        }

        [SqlServerFact]
        public async Task ExcludingAllQuestions_Fails()
        {
            var seed = await SeedAsync();
            var request = Request(seed, seed.BatchA2);
            request.ExcludedQuestionIds = new List<Guid> { seed.Q1, seed.Q2, seed.Q3 };

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(ScopeA(seed), request);

            Assert.False(result.Succeeded);
            Assert.Contains("لا توجد أسئلة", result.Message);
        }

        [SqlServerFact]
        public async Task ReplacementCandidates_AreValidBankQuestionsOfSameLesson_OutsideTheModel()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);
            var ids = (await service.GetReplacementCandidatesAsync(ScopeA(seed), seed.ModelId, seed.Q1)).Select(c => c.QuestionId).ToList();

            Assert.Contains(seed.Substitute, ids);
            Assert.DoesNotContain(seed.RejectedSubstitute, ids);
            Assert.DoesNotContain(seed.OtherLessonSubstitute, ids);
            Assert.DoesNotContain(seed.PartnerOwnedSubstitute, ids);
            Assert.DoesNotContain(seed.Q2, ids);

            // سؤال خارج النموذج لا يكشف بدائل
            Assert.Empty(await service.GetReplacementCandidatesAsync(ScopeA(seed), seed.ModelId, seed.Substitute));
        }

        // =====================================================
        // التواريخ والمحاضرة والعنوان
        // =====================================================
        [SqlServerFact]
        public async Task InvalidInput_Fails_WithClearMessage()
        {
            var seed = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);
            var partner = ScopeA(seed);

            async Task AssertFailsAsync(ModelSendScope scope, Action<SendModelHomeworkRequest> change, string message)
            {
                var request = Request(seed, seed.BatchA2);
                change(request);
                var result = await service.SendHomeworkAsync(scope, request);
                Assert.False(result.Succeeded);
                Assert.Contains(message, result.Message);
            }

            await AssertFailsAsync(partner, r => r.EndAtLocal = r.StartAtLocal, "بعد موعد البداية");
            await AssertFailsAsync(partner, r => r.EndAtLocal = null, "حدد موعد");
            await AssertFailsAsync(partner, r => { r.StartAtLocal = LocalInFuture(-3); r.EndAtLocal = LocalInFuture(-1); }, "في المستقبل");
            await AssertFailsAsync(partner, r => r.BatchIds = new List<int>(), "دفعة واحدة على الأقل");
            await AssertFailsAsync(partner, r => r.Title = new string('ع', 201), "عنوان الواجب");
            await AssertFailsAsync(partner, r => r.LectureId = 1, "غير متاح");

            var admin = ModelSendScope.Admin(SenderId, $"{Marker} أدمن");
            await AssertFailsAsync(admin, r => r.LectureId = -1, "لا تنتمي");
            await AssertFailsAsync(admin, r => { r.LectureId = 1; r.BatchIds = new List<int> { seed.BatchA, seed.BatchA2 }; }, "دفعة واحدة فقط");

            Assert.Equal((0, 0, 0), await CountWritesAsync(seed.BatchA2));
        }

        [SqlServerFact]
        public async Task Snapshot_ChangingTheModelAfterSending_DoesNotChangeWhatWasSent()
        {
            var seed = await SeedAsync();

            await using (var db = RtkRealDb.CreateDb())
                Assert.True((await NewService(db).SendHomeworkAsync(ScopeA(seed), Request(seed, seed.BatchA2))).Succeeded);

            await using (var change = RtkRealDb.CreateDb())
            {
                await change.ProfessionalModelQuestions.Where(q => q.ModelId == seed.ModelId).ExecuteDeleteAsync();
                await change.ProfessionalModels.Where(m => m.Id == seed.ModelId).ExecuteUpdateAsync(x => x.SetProperty(m => m.IsForPartners, false));
            }

            Assert.Equal((1, 1, 3), await CountWritesAsync(seed.BatchA2));
        }

        // =====================================================
        // الأداء: الهدف في الخطة 300 طالب × 30 سؤال (9000 صف) في أقل من 5 ثوانٍ
        // =====================================================
        [SqlServerFact]
        public async Task Send_300Students_30Questions_IsUnder5Seconds()
        {
            var seed = await SeedAsync();
            const int studentCount = 300;
            const int questionCount = 30;

            await using (var pre = RtkRealDb.CreateDb())
            {
                pre.ChangeTracker.AutoDetectChangesEnabled = false;
                var branchId = await pre.Batches.Where(b => b.Id == seed.EmptyBatchA).Select(b => b.BranchId).FirstAsync();

                var students = Enumerable.Range(100, studentCount).Select(i => NewStudent(pre, i, branchId)).ToList();
                pre.ChangeTracker.DetectChanges();
                await pre.SaveChangesAsync();

                pre.StudentBatchEnrollments.AddRange(students.Select(s => new StudentBatchEnrollment { StudentID = s.StudentID, BatchId = seed.EmptyBatchA }));

                await pre.ProfessionalModelQuestions.Where(q => q.ModelId == seed.ModelId).ExecuteDeleteAsync();

                var questions = Enumerable.Range(1, questionCount)
                    .Select(i => NewQuestion($"{Marker}-L{i}", seed.CurriculumId, seed.LessonId))
                    .ToList();
                pre.Questions.AddRange(questions);
                pre.ProfessionalModelQuestions.AddRange(questions.Select((q, i) => new ProfessionalModelQuestion { ModelId = seed.ModelId, QuestionId = q.Id, OrderNumber = i + 1 }));
                pre.ChangeTracker.DetectChanges();
                await pre.SaveChangesAsync();
            }

            await using var db = RtkRealDb.CreateDb();
            var stopwatch = Stopwatch.StartNew();
            var result = await NewService(db).SendHomeworkAsync(ScopeA(seed), Request(seed, seed.EmptyBatchA));
            stopwatch.Stop();

            _output.WriteLine($"Send {studentCount} x {questionCount}: {stopwatch.ElapsedMilliseconds} ms");

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal((1, studentCount, studentCount * questionCount), await CountWritesAsync(seed.EmptyBatchA));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"{stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
