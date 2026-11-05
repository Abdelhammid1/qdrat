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
using QdratNew.Services.Reports.Implementations;
using QdratNew.Tests.Integration;
using Xunit;
using Xunit.Abstractions;
using HomeworkEntity = QdratNew.Entities.Homework;

namespace QdratNew.Tests.PartnerProfessionalModels
{
    // PPM-S5 — SendExamAsync: BulkInsert + ExecutionStrategy + Transaction + sp_getapplock على SQL Server حقيقي.
    // التشغيل: QDRAT_TEST_SQL على قاعدة الاختبار المعزولة. بدونه تُتخطّى.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class ProfessionalModelExamSendSqlServerTests : IAsyncLifetime
    {
        private const string Marker = "PPMS5";
        private const string SenderId = "ppms5-sender";

        private readonly ITestOutputHelper _output;

        public ProfessionalModelExamSendSqlServerTests(ITestOutputHelper output) => _output = output;

        private sealed record Seed(
            int PartnerA, int PartnerB, int CurriculumId, int LessonId,
            int BatchA, int BatchA2, int EmptyBatchA, int BatchB, int InstituteBatch,
            int Active1, int Active2, int Inactive, int Left, int Foreign, int InA2, int InInstitute,
            int ModelId, Guid Q1, Guid Q2, Guid Q3, Guid Substitute);

        private static ModelSendScope ScopeA(Seed seed) => ModelSendScope.Partner(seed.PartnerA, SenderId, $"{Marker} مرسل");

        private static ProfessionalModelSendService NewService(ApplicationDbContext db, IAdvancedNotificationService? notifications = null)
            => new(db, new ProfessionalModelAvailabilityService(db), new TimeZoneService(), notifications ?? new AdvancedNotificationService(db), NullLogger<ProfessionalModelSendService>.Instance);

        private sealed class FailingSendService : ProfessionalModelSendService
        {
            public FailingSendService(ApplicationDbContext db)
                : base(db, new ProfessionalModelAvailabilityService(db), new TimeZoneService(), new AdvancedNotificationService(db), NullLogger<ProfessionalModelSendService>.Instance)
            {
            }

            protected override Task InsertExamStatusesAsync(List<ExamStudentStatus> statuses, CancellationToken ct)
                => throw new InvalidOperationException("فشل مصطنع في منتصف الإرسال");
        }

        // توقيت السعودية بلا Kind كما يصل من النموذج
        private static DateTime LocalInFuture(int days)
        {
            var saudi = DateTime.UtcNow.AddHours(3).AddDays(days);
            return DateTime.SpecifyKind(new DateTime(saudi.Year, saudi.Month, saudi.Day, saudi.Hour, saudi.Minute, 0), DateTimeKind.Unspecified);
        }

        private static SendModelExamRequest Request(Seed seed, params int[] batchIds) => new()
        {
            ModelId = seed.ModelId,
            BatchIds = batchIds.ToList(),
            StartAtLocal = LocalInFuture(0),
            EndAtLocal = LocalInFuture(3),
            DurationMinutes = 45
        };

        public Task InitializeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPMS5%')";
            const string sets = "(SELECT Id FROM HomeworkSets WHERE BatchId IN " + batches + ")";
            const string assignments = "(SELECT Id FROM ExamAssignmentsToBatches WHERE BatchId IN " + batches + ")";
            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS5%')";
            const string partners = "(SELECT Id FROM Partners WHERE Code LIKE 'PPMS5%')";
            const string courses = "(SELECT Id FROM Courses WHERE Name LIKE 'PPMS5%')";
            const string models = "(SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPMS5%')";

            foreach (var sql in new[]
            {
                $"DELETE FROM Notifications WHERE StudentID IN {students}",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPMS5%'",
                $"DELETE FROM ExamStudentQuestionOrders WHERE ExamAssignmentId IN {assignments}",
                $"DELETE FROM ExamStudentStatuses WHERE StudentId IN {students}",
                $"DELETE FROM ExamAssignments WHERE StudentId IN {students}",
                $"DELETE FROM ExamQuestions WHERE ExamAssignmentId IN {assignments}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE BatchId IN {batches}",
                $"DELETE FROM Exams WHERE SourceProfessionalModelId IN {models} OR OwnerPartnerId IN {partners} OR Title LIKE N'%PPMS5%'",
                $"DELETE FROM Homeworks WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSetStudents WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSets WHERE BatchId IN {batches}",
                $"DELETE FROM ProfessionalModelQuestions WHERE ModelId IN {models}",
                "DELETE FROM ProfessionalModels WHERE Title LIKE 'PPMS5%'",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPMS5%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPMS5%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPMS5%'",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'ppms5-%'",
                $"DELETE FROM PartnerSubscriptionCourses WHERE PartnerSubscriptionId IN (SELECT Id FROM PartnerSubscriptions WHERE PartnerId IN {partners})",
                $"DELETE FROM PartnerSubscriptions WHERE PartnerId IN {partners}",
                $"DELETE FROM CourseCurriculums WHERE CourseId IN {courses}",
                "DELETE FROM Branches WHERE Name LIKE 'PPMS5%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPMS5%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPMS5%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        private static Question NewQuestion(string reference, int curriculumId, int lessonId) => new()
        {
            Id = Guid.NewGuid(),
            Title = $"سؤال {reference}",
            ReferenceNumber = reference,
            CurriculumId = curriculumId,
            LessonId = lessonId,
            CorrectAnswer = "أ",
            IsComplete = true,
            IsReviewed = true
        };

        private static Branch NewBranch(string name, int? partnerId)
            => new() { Name = $"{Marker} {name}", PartnerId = partnerId, Location = "-", City = "-", State = "-", Country = "-" };

        private static Student NewStudent(ApplicationDbContext db, int i, int branchId, bool active = true)
        {
            var userName = $"ppms5-s{i}";
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

        private static async Task<Seed> SeedAsync(bool canCreateExams = true)
        {
            await using var db = RtkRealDb.CreateDb();

            var anchor = await db.Lessons.AsNoTracking()
                .Where(l => l.Section != null)
                .Select(l => new { LessonId = l.Id, l.Section.CurriculumId })
                .FirstAsync();
            var otherLessonId = await db.Lessons.AsNoTracking().Where(l => l.Id != anchor.LessonId).Select(l => l.Id).FirstAsync();

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
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            db.CourseCurriculums.Add(new CourseCurriculum { CourseId = course.Id, CurriculumId = anchor.CurriculumId });

            var contract = new PartnerSubscription
            {
                PartnerId = a.Id,
                StartDate = today.AddDays(-1),
                EndDate = today.AddDays(30),
                CanUseProfessionalModels = true,
                CanCreateHomework = true,
                CanCreateExams = canCreateExams
            };
            db.PartnerSubscriptions.Add(contract);
            await db.SaveChangesAsync();

            db.PartnerSubscriptionCourses.Add(new PartnerSubscriptionCourse { PartnerSubscriptionId = contract.Id, CourseId = course.Id, CanUsePlatformQuestionBank = true });

            var branchA = NewBranch("فرع A", a.Id);
            var branchB = NewBranch("فرع B", b.Id);
            var instituteBranch = NewBranch("معهد", null);
            db.Branches.AddRange(branchA, branchB, instituteBranch);
            await db.SaveChangesAsync();

            Batch NewBatch(string name, int branchId)
            {
                var batch = new Batch { Name = $"{Marker} {name}", BranchId = branchId, CourseId = course.Id, StartDate = DateTime.UtcNow };
                db.Batches.Add(batch);
                return batch;
            }

            var batchA = NewBatch("دفعة A1", branchA.Id);
            var batchA2 = NewBatch("دفعة A2", branchA.Id);
            var emptyA = NewBatch("دفعة A فارغة", branchA.Id);
            var batchB = NewBatch("دفعة B", branchB.Id);
            var instituteBatch = NewBatch("دفعة معهد", instituteBranch.Id);
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
            db.Questions.AddRange(q1, q2, q3, substitute);

            var model = new ProfessionalModel
            {
                Title = $"{Marker} نموذج",
                Description = "-",
                CreatedBy = "-",
                CreatedAt = DateTime.UtcNow,
                CurriculumId = anchor.CurriculumId,
                ModelType = ProfessionalModelType.Exam,
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
                batchA.Id, batchA2.Id, emptyA.Id, batchB.Id, instituteBatch.Id,
                active1.StudentID, active2.StudentID, inactive.StudentID, left.StudentID, foreign.StudentID, inA2.StudentID, inInstitute.StudentID,
                model.Id, q1.Id, q2.Id, q3.Id, substitute.Id);
        }

        private static async Task<int> AddModelAsync(string title, ProfessionalModelType type, Seed seed)
        {
            await using var db = RtkRealDb.CreateDb();
            var model = new ProfessionalModel
            {
                Title = $"{Marker} {title}", Description = "-", CreatedBy = "-", CreatedAt = DateTime.UtcNow,
                CurriculumId = seed.CurriculumId, ModelType = type, IsForPartners = true, IsGlobal = true,
                Questions = new List<ProfessionalModelQuestion> { new() { QuestionId = seed.Q1, OrderNumber = 1 } }
            };
            db.ProfessionalModels.Add(model);
            await db.SaveChangesAsync();
            return model.Id;
        }

        // إرسال سابق «قديم» خارج نافذة النقر المزدوج
        private static async Task AddOldExamAsync(int batchId, Guid questionId, int? sourceModelId, bool archived = false, bool sent = true)
        {
            await using var db = RtkRealDb.CreateDb();
            var exam = new Exam
            {
                Title = $"{Marker} قديم",
                Type = ExamType.Manual,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                SourceProfessionalModelId = sourceModelId
            };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();

            var assignment = new ExamAssignmentToBatch
            {
                ExamId = exam.Id,
                BatchId = batchId,
                Title = exam.Title,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                IsSentToStudents = sent,
                IsArchived = archived
            };
            db.ExamAssignmentsToBatches.Add(assignment);
            await db.SaveChangesAsync();

            db.ExamQuestions.Add(new ExamQuestion { ExamId = exam.Id, ExamAssignmentId = assignment.Id, QuestionId = questionId, Order = 1 });
            await db.SaveChangesAsync();
        }

        private static async Task<(int Exams, int Assignments, int Questions, int StudentAssignments, int Statuses)> CountWritesAsync(int modelId)
        {
            await using var db = RtkRealDb.CreateDb();
            return (
                await db.Exams.CountAsync(e => e.SourceProfessionalModelId == modelId),
                await db.ExamAssignmentsToBatches.CountAsync(a => a.Exam.SourceProfessionalModelId == modelId),
                await db.ExamQuestions.CountAsync(q => q.Exam!.SourceProfessionalModelId == modelId),
                await db.ExamAssignments.CountAsync(a => a.Exam.SourceProfessionalModelId == modelId),
                await db.ExamStudentStatuses.CountAsync(s => s.Exam.SourceProfessionalModelId == modelId));
        }

        // =====================================================
        // الإرسال السليم
        // =====================================================
        [SqlServerFact]
        public async Task Partner_SendExam_WritesFullSnapshot_WithOwner_AndUtcDates()
        {
            var seed = await SeedAsync();
            var request = Request(seed, seed.BatchA, seed.BatchA2);
            request.Title = "  اختبار الأسبوع  ";

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(ScopeA(seed), request);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(2, result.BatchesSent);
            Assert.Equal(3, result.StudentsSent);

            await using var read = RtkRealDb.CreateDb();
            var assignment = await read.ExamAssignmentsToBatches.AsNoTracking().Include(a => a.Exam).SingleAsync(a => a.BatchId == seed.BatchA);
            var exam = assignment.Exam;

            Assert.Equal("اختبار الأسبوع", exam.Title);
            Assert.Equal(ExamType.Manual, exam.Type);
            Assert.Equal(seed.PartnerA, exam.OwnerPartnerId);
            Assert.True(exam.IsFromProfessionalModel);
            Assert.True(exam.IsActive);
            Assert.Equal(seed.ModelId, exam.SourceProfessionalModelId);
            Assert.Equal(seed.CurriculumId, exam.CurriculumId);
            Assert.Equal(45, exam.DurationMinutes);
            Assert.Equal(3, exam.TotalQuestions);

            Assert.Equal("اختبار الأسبوع", assignment.Title);
            Assert.True(assignment.IsSentToStudents);
            Assert.True(assignment.IsOnline);
            Assert.False(assignment.IsInLab);
            Assert.Equal(45, assignment.DurationMinutes);
            Assert.Equal(3, assignment.TotalQuestions);
            Assert.Equal(seed.CurriculumId, assignment.CurriculumId);
            Assert.Equal(request.StartAtLocal!.Value.AddHours(-3), assignment.ScheduledDate);
            Assert.Equal(request.EndAtLocal!.Value.AddHours(-3), assignment.EndAt);

            // بعد إسقاط السؤال المكرر، وبنفس ترتيب النموذج
            var questions = await read.ExamQuestions.AsNoTracking()
                .Where(q => q.ExamAssignmentId == assignment.Id)
                .OrderBy(q => q.Order)
                .ToListAsync();
            Assert.Equal(new[] { seed.Q1, seed.Q2, seed.Q3 }, questions.Select(q => q.QuestionId));
            Assert.Equal(new[] { 1, 2, 3 }, questions.Select(q => q.Order));
            Assert.All(questions, q =>
            {
                Assert.Equal(exam.Id, q.ExamId);
                Assert.True(q.IsManuallySelected);
            });

            var statuses = await read.ExamStudentStatuses.AsNoTracking().Where(s => s.ExamAssignmentId == assignment.Id).ToListAsync();
            Assert.Equal(new[] { seed.Active1, seed.Active2 }.OrderBy(x => x), statuses.Select(s => s.StudentId).OrderBy(x => x));
            Assert.All(statuses, s =>
            {
                Assert.Equal(exam.Id, s.ExamId);
                Assert.Equal(ExamStatus.Pending, s.Status);
                Assert.False(s.IsSubmitted);
                Assert.Null(s.StartedAt);
            });

            var due = await read.ExamAssignments.AsNoTracking().Where(a => a.ExamId == exam.Id).ToListAsync();
            Assert.Equal(2, due.Count);
            Assert.All(due, a => Assert.Equal(assignment.EndAt, a.DueDate));

            // اختبار مستقل لكل دفعة، ومعرفات المخرجات مربوطة بدفعتها
            var second = await read.ExamAssignmentsToBatches.AsNoTracking().SingleAsync(a => a.BatchId == seed.BatchA2);
            Assert.NotEqual(assignment.ExamId, second.ExamId);
            Assert.Equal((2, 2, 6, 3, 3), await CountWritesAsync(seed.ModelId));
            Assert.Equal(seed.InA2, (await read.ExamStudentStatuses.AsNoTracking().SingleAsync(s => s.ExamAssignmentId == second.Id)).StudentId);

            Assert.Equal(3, await read.Notifications.CountAsync(n =>
                n.Category == NotificationCategory.Exam &&
                (n.StudentID == seed.Active1 || n.StudentID == seed.Active2 || n.StudentID == seed.InA2)));
            Assert.True(await read.SystemLogs.AnyAsync(l => l.EntityId == seed.ModelId && l.Action == "SendExamFromModel" && l.Description.Contains(Marker)));
        }

        [SqlServerFact]
        public async Task Partner_SendExam_DefaultTitle_UsesTheModel()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            Assert.True((await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2))).Succeeded);

            await using var read = RtkRealDb.CreateDb();
            Assert.Equal($"اختبار من النموذج {Marker} نموذج", await read.Exams.Where(e => e.SourceProfessionalModelId == seed.ModelId).Select(e => e.Title).SingleAsync());
        }

        [SqlServerFact]
        public async Task Partner_SendExam_OnlyActiveEnrollmentsOfOwnBranch_Receive()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            Assert.True((await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA))).Succeeded);

            await using var read = RtkRealDb.CreateDb();
            var receivers = await read.ExamStudentStatuses.Where(s => s.Exam.SourceProfessionalModelId == seed.ModelId).Select(s => s.StudentId).ToListAsync();
            Assert.DoesNotContain(seed.Inactive, receivers);
            Assert.DoesNotContain(seed.Left, receivers);
            Assert.DoesNotContain(seed.Foreign, receivers);
            Assert.Equal(2, receivers.Count);
        }

        [SqlServerFact]
        public async Task Admin_SendExam_HasNoOwner_AndReachesAnyBatch()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(ModelSendScope.Admin(SenderId, $"{Marker} أدمن"), Request(seed, seed.InstituteBatch));

            Assert.True(result.Succeeded, result.Message);

            await using var read = RtkRealDb.CreateDb();
            var exam = await read.Exams.AsNoTracking().SingleAsync(e => e.SourceProfessionalModelId == seed.ModelId);
            Assert.Null(exam.OwnerPartnerId);
            Assert.Equal(seed.InInstitute, (await read.ExamStudentStatuses.SingleAsync(s => s.ExamId == exam.Id)).StudentId);
        }

        [SqlServerFact]
        public async Task SendExam_SkipsBatchWithoutActiveStudents_AndNamesItInTheMessage()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2, seed.EmptyBatchA));

            Assert.True(result.Succeeded, result.Message);
            Assert.Contains("فارغة", result.Message);
            Assert.Contains("الاختبار", result.Message);
            Assert.Equal((1, 1, 3, 1, 1), await CountWritesAsync(seed.ModelId));
        }

        // =====================================================
        // العزل والعقد والنوع
        // =====================================================
        [SqlServerFact]
        public async Task Partner_OtherPartnersBatch_Fails_WithoutWriting()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA, seed.BatchB));

            Assert.False(result.Succeeded);
            Assert.Contains("دفعات غير صالحة", result.Message);
            Assert.Equal((0, 0, 0, 0, 0), await CountWritesAsync(seed.ModelId));
        }

        [SqlServerFact]
        public async Task Partner_ContractWithoutCreateExams_IsNotFound_AndModelsAreHidden()
        {
            var seed = await SeedAsync(canCreateExams: false);

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);
            var result = await service.SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA));

            Assert.True(result.IsNotFound);
            Assert.Empty(await service.GetModelsAsync(ScopeA(seed), ProfessionalModelType.Exam));
            Assert.Empty(await service.GetEligibleBatchesAsync(ScopeA(seed), seed.ModelId));
            Assert.Null(await service.PreviewAsync(ScopeA(seed), ProfessionalModelType.Exam, Request(seed, seed.BatchA)));
            Assert.Empty(await service.GetReplacementCandidatesAsync(ScopeA(seed), seed.ModelId, seed.Q1));
            Assert.Equal((0, 0, 0, 0, 0), await CountWritesAsync(seed.ModelId));
        }

        [SqlServerFact]
        public async Task ModelOfWrongType_IsNotFound_InBothDirections()
        {
            var seed = await SeedAsync();
            var homeworkModel = await AddModelAsync("نموذج واجب", ProfessionalModelType.Homework, seed);
            var untypedModel = await AddModelAsync("نموذج بلا نوع", default, seed);

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            var homeworkAsExam = Request(seed, seed.BatchA);
            homeworkAsExam.ModelId = homeworkModel;
            Assert.True((await service.SendExamAsync(ScopeA(seed), homeworkAsExam)).IsNotFound);

            var untyped = Request(seed, seed.BatchA);
            untyped.ModelId = untypedModel;
            Assert.True((await service.SendExamAsync(ScopeA(seed), untyped)).IsNotFound);

            var examAsHomework = await service.SendHomeworkAsync(ScopeA(seed), new SendModelHomeworkRequest
            {
                ModelId = seed.ModelId,
                BatchIds = new List<int> { seed.BatchA },
                StartAtLocal = LocalInFuture(0),
                EndAtLocal = LocalInFuture(3)
            });
            Assert.True(examAsHomework.IsNotFound);

            await using var read = RtkRealDb.CreateDb();
            Assert.False(await read.Exams.AnyAsync(e => e.SourceProfessionalModelId == homeworkModel || e.SourceProfessionalModelId == untypedModel));
            Assert.False(await read.HomeworkSets.AnyAsync(h => h.SourceProfessionalModelId == seed.ModelId));
        }

        [SqlServerFact]
        public async Task GetEligibleBatches_ExamModel_ListsOwnBatches_AndMarksAlreadySent()
        {
            var seed = await SeedAsync();
            await AddOldExamAsync(seed.BatchA2, seed.Q2, seed.ModelId);

            await using var db = RtkRealDb.CreateDb();
            var batches = await NewService(db).GetEligibleBatchesAsync(ScopeA(seed), seed.ModelId);

            Assert.Equal(new[] { seed.BatchA, seed.BatchA2, seed.EmptyBatchA }.OrderBy(x => x), batches.Select(b => b.BatchId).OrderBy(x => x));
            Assert.True(batches.Single(b => b.BatchId == seed.BatchA2).AlreadySentFromModel);
            Assert.False(batches.Single(b => b.BatchId == seed.BatchA).AlreadySentFromModel);
            Assert.Equal(2, batches.Single(b => b.BatchId == seed.BatchA).ActiveStudents);
        }

        // =====================================================
        // التكرار
        // =====================================================
        [SqlServerFact]
        public async Task AlreadySentModel_RequiresConfirmation_ThenSends()
        {
            var seed = await SeedAsync();
            await AddOldExamAsync(seed.BatchA2, seed.Q2, seed.ModelId);

            await using (var db = RtkRealDb.CreateDb())
            {
                var unconfirmed = await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2));

                Assert.False(unconfirmed.Succeeded);
                Assert.True(unconfirmed.RequiresConfirmation);
                Assert.True(unconfirmed.Preview!.Batches.Single().AlreadySentFromModel);
            }

            Assert.Equal(1, (await CountWritesAsync(seed.ModelId)).Exams);

            await using (var db = RtkRealDb.CreateDb())
            {
                var request = Request(seed, seed.BatchA2);
                request.ConfirmWarnings = true;
                Assert.True((await NewService(db).SendExamAsync(ScopeA(seed), request)).Succeeded);
            }

            Assert.Equal(2, (await CountWritesAsync(seed.ModelId)).Exams);
        }

        [SqlServerFact]
        public async Task RepeatedQuestion_FromAnotherExam_RequiresConfirmation()
        {
            var seed = await SeedAsync();
            await AddOldExamAsync(seed.BatchA2, seed.Q2, null);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2));

            Assert.True(result.RequiresConfirmation);
            var batch = result.Preview!.Batches.Single();
            Assert.Equal(1, batch.RepeatedQuestionCount);
            Assert.False(batch.AlreadySentFromModel);
        }

        [SqlServerFact]
        public async Task RepeatedQuestion_FromHomework_RequiresConfirmation_CountedOnce()
        {
            var seed = await SeedAsync();
            await AddOldExamAsync(seed.BatchA2, seed.Q2, null);

            await using (var pre = RtkRealDb.CreateDb())
            {
                var set = new HomeworkSet { BatchId = seed.BatchA2, Title = $"{Marker} قديم", CompletionTitle = "-", CreatedAt = DateTime.UtcNow.AddDays(-10), IsSent = true };
                pre.HomeworkSets.Add(set);
                await pre.SaveChangesAsync();
                pre.Homeworks.AddRange(
                    new HomeworkEntity { HomeworkSetId = set.Id, StudentId = seed.InA2, QuestionId = seed.Q1, IsSent = true },
                    new HomeworkEntity { HomeworkSetId = set.Id, StudentId = seed.InA2, QuestionId = seed.Q2, IsSent = true });
                await pre.SaveChangesAsync();
            }

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2));

            Assert.True(result.RequiresConfirmation);
            Assert.Equal(2, result.Preview!.Batches.Single().RepeatedQuestionCount);
        }

        [SqlServerFact]
        public async Task ArchivedOrUnsentPreviousExam_IsNotARepeat()
        {
            var seed = await SeedAsync();
            await AddOldExamAsync(seed.BatchA2, seed.Q1, seed.ModelId, archived: true);
            await AddOldExamAsync(seed.BatchA2, seed.Q2, seed.ModelId, sent: false);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2));

            Assert.True(result.Succeeded, result.Message);
        }

        [SqlServerFact]
        public async Task SecondSend_WithinWindow_IsRejected_EvenWhenConfirmed()
        {
            var seed = await SeedAsync();

            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.True((await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2))).Succeeded);
            }

            await using (var db = RtkRealDb.CreateDb())
            {
                var request = Request(seed, seed.BatchA2);
                request.ConfirmWarnings = true;
                var result = await NewService(db).SendExamAsync(ScopeA(seed), request);

                Assert.False(result.Succeeded);
                Assert.Contains("قبل لحظات", result.Message);
                Assert.Contains("الاختبارات المرسلة", result.Message);
            }

            Assert.Equal(1, (await CountWritesAsync(seed.ModelId)).Exams);
        }

        [SqlServerFact]
        public async Task ConcurrentSends_CreateOneExamOnly()
        {
            var seed = await SeedAsync();

            async Task<ModelSendResult> SendOnceAsync()
            {
                await using var db = RtkRealDb.CreateDb();
                var request = Request(seed, seed.BatchA);
                request.ConfirmWarnings = true;
                return await NewService(db).SendExamAsync(ScopeA(seed), request);
            }

            var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendOnceAsync()));

            Assert.Equal(1, results.Count(r => r.Succeeded));
            Assert.Equal((1, 1, 3, 2, 2), await CountWritesAsync(seed.ModelId));
        }

        // =====================================================
        // الـ Transaction
        // =====================================================
        [SqlServerFact]
        public async Task FailureMidSend_RollsEverythingBack_AndHidesTheError()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await new FailingSendService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA, seed.BatchA2));

            Assert.False(result.Succeeded);
            Assert.DoesNotContain("مصطنع", result.Message);
            Assert.Contains("الاختبار", result.Message);
            Assert.Equal((0, 0, 0, 0, 0), await CountWritesAsync(seed.ModelId));

            await using var read = RtkRealDb.CreateDb();
            Assert.False(await read.Notifications.AnyAsync(n => n.StudentID == seed.Active1));
            Assert.False(await read.SystemLogs.AnyAsync(l => l.EntityId == seed.ModelId && l.Action == "SendExamFromModel"));
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
            var result = await NewService(db, notifications.Object).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal((1, 1, 3, 1, 1), await CountWritesAsync(seed.ModelId));
        }

        // =====================================================
        // الأسئلة والمدخلات
        // =====================================================
        [SqlServerFact]
        public async Task ExcludedAndReplaced_AreSentInTheOriginalOrder()
        {
            var seed = await SeedAsync();
            var request = Request(seed, seed.BatchA2);
            request.ExcludedQuestionIds = new List<Guid> { seed.Q2 };
            request.Replacements = new Dictionary<Guid, Guid> { [seed.Q1] = seed.Substitute };

            await using var db = RtkRealDb.CreateDb();
            Assert.True((await NewService(db).SendExamAsync(ScopeA(seed), request)).Succeeded);

            await using var read = RtkRealDb.CreateDb();
            var sent = await read.ExamQuestions.Where(q => q.Exam!.SourceProfessionalModelId == seed.ModelId).OrderBy(q => q.Order).Select(q => q.QuestionId).ToListAsync();
            Assert.Equal(new[] { seed.Substitute, seed.Q3 }, sent);
            Assert.Equal(2, await read.Exams.Where(e => e.SourceProfessionalModelId == seed.ModelId).Select(e => e.TotalQuestions).SingleAsync());
        }

        [SqlServerFact]
        public async Task InvalidDuration_Fails_WithoutWriting()
        {
            var seed = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            foreach (var minutes in new[] { 0, 4, 301 })
            {
                var request = Request(seed, seed.BatchA);
                request.DurationMinutes = minutes;

                var result = await service.SendExamAsync(ScopeA(seed), request);

                Assert.False(result.Succeeded);
                Assert.Contains("مدة الاختبار", result.Message);
                Assert.NotNull(service.ValidateSchedule(ScopeA(seed), request));
            }

            Assert.Equal((0, 0, 0, 0, 0), await CountWritesAsync(seed.ModelId));
        }

        [SqlServerFact]
        public async Task InvalidDates_Fail_WithoutWriting()
        {
            var seed = await SeedAsync();
            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            var missing = Request(seed, seed.BatchA);
            missing.StartAtLocal = null;
            Assert.Contains("الاختبار", (await service.SendExamAsync(ScopeA(seed), missing)).Message);

            var reversed = Request(seed, seed.BatchA);
            reversed.EndAtLocal = reversed.StartAtLocal!.Value.AddHours(-1);
            Assert.Contains("بعد موعد البداية", (await service.SendExamAsync(ScopeA(seed), reversed)).Message);

            var past = Request(seed, seed.BatchA);
            past.StartAtLocal = LocalInFuture(-3);
            past.EndAtLocal = LocalInFuture(-2);
            Assert.Contains("في المستقبل", (await service.SendExamAsync(ScopeA(seed), past)).Message);

            Assert.Equal((0, 0, 0, 0, 0), await CountWritesAsync(seed.ModelId));
        }

        [SqlServerFact]
        public async Task Snapshot_ChangingTheModelAfterSending_DoesNotChangeWhatWasSent()
        {
            var seed = await SeedAsync();

            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.True((await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2))).Succeeded);
            }

            await using (var edit = RtkRealDb.CreateDb())
            {
                await edit.ProfessionalModelQuestions.Where(q => q.ModelId == seed.ModelId).ExecuteDeleteAsync();
                await edit.ProfessionalModels.Where(m => m.Id == seed.ModelId).ExecuteUpdateAsync(x => x.SetProperty(m => m.IsArchived, true));
            }

            Assert.Equal(3, (await CountWritesAsync(seed.ModelId)).Questions);
        }

        // =====================================================
        // تقارير الشريك: الاستبعاد يخص تحديد المستوى فقط
        // =====================================================
        [SqlServerFact]
        public async Task PartnerBatchReports_IncludeModelExam_ButNotPartnerPlacementExam()
        {
            var seed = await SeedAsync();

            await using (var db = RtkRealDb.CreateDb())
            {
                Assert.True((await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.BatchA2))).Succeeded);

                var placement = new Exam { Title = $"{Marker} تحديد مستوى", Type = ExamType.LevelAssessment, OwnerPartnerId = seed.PartnerA };
                db.Exams.Add(placement);
                await db.SaveChangesAsync();
                db.ExamAssignmentsToBatches.Add(new ExamAssignmentToBatch { ExamId = placement.Id, BatchId = seed.BatchA2, Title = placement.Title, IsSentToStudents = true });
                await db.SaveChangesAsync();
            }

            await using var read = RtkRealDb.CreateDb();
            var report = new BatchReportService(read).GetBatchExamReport(seed.PartnerA, seed.BatchA2);

            Assert.Equal($"اختبار من النموذج {Marker} نموذج", Assert.Single(report.Exams).Title);
        }

        // =====================================================
        // الأداء: الهدف في الخطة 300 طالب × 30 سؤال في أقل من 5 ثوانٍ
        // =====================================================
        [SqlServerFact]
        public async Task SendExam_300Students_30Questions_IsUnder5Seconds()
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
            var result = await NewService(db).SendExamAsync(ScopeA(seed), Request(seed, seed.EmptyBatchA));
            stopwatch.Stop();

            _output.WriteLine($"SendExam {studentCount} x {questionCount}: {stopwatch.ElapsedMilliseconds} ms");

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal((1, 1, questionCount, studentCount, studentCount), await CountWritesAsync(seed.ModelId));
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"{stopwatch.ElapsedMilliseconds} ms");
        }
    }
}
