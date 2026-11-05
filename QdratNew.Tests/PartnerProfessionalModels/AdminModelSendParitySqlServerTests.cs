using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Implementations;
using QdratNew.Services.ProfessionalModels;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.PartnerProfessionalModels
{
    // PPM-S6.1/S6.2 — مطابقة ما يكتبه نطاق الأدمن مع ما كان يكتبه CreateModelHomework/CreateModelExam قبل نقلهما للخدمة.
    // القيم المتوقعة منقولة من الكود القديم (63eeac5)، والفروق المقصودة لها اختبارات باسمها.
    // التشغيل: QDRAT_TEST_SQL على قاعدة الاختبار المعزولة. بدونه تُتخطّى.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class AdminModelSendParitySqlServerTests : IAsyncLifetime
    {
        private const string Marker = "PPMS6";
        private const string SenderId = "ppms6-sender";

        private static ModelSendScope Admin => ModelSendScope.Admin(SenderId, $"{Marker} أدمن");

        private sealed record Seed(
            int CurriculumId, int LessonId, int BatchA, int BatchB, int EmptyBatch, int LectureInA,
            int StudentA1, int StudentA2, int Withdrawn, int NotLearning, int StudentB,
            int HomeworkModel, int ExamModel, int UntypedModel, Guid Q1, Guid Q2);

        private static ProfessionalModelSendService NewService(ApplicationDbContext db)
            => new(db, new ProfessionalModelAvailabilityService(db), new TimeZoneService(), new AdvancedNotificationService(db), new QdratNew.Services.PartnerDispatch.PartnerDispatchLogService(db), NullLogger<ProfessionalModelSendService>.Instance);

        public Task InitializeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPMS6%')";
            const string sets = "(SELECT Id FROM HomeworkSets WHERE BatchId IN " + batches + ")";
            const string assignments = "(SELECT Id FROM ExamAssignmentsToBatches WHERE BatchId IN " + batches + ")";
            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPMS6%')";
            const string models = "(SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPMS6%')";

            foreach (var sql in new[]
            {
                $"DELETE FROM Notifications WHERE StudentID IN {students}",
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPMS6%'",
                $"DELETE FROM ExamStudentStatuses WHERE StudentId IN {students}",
                $"DELETE FROM ExamQuestions WHERE ExamAssignmentId IN {assignments}",
                $"DELETE FROM ExamAssignments WHERE StudentId IN {students}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE BatchId IN {batches}",
                $"DELETE FROM Exams WHERE SourceProfessionalModelId IN {models} OR Title LIKE N'%PPMS6%'",
                $"DELETE FROM Homeworks WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSetStudents WHERE HomeworkSetId IN {sets}",
                $"DELETE FROM HomeworkSets WHERE BatchId IN {batches}",
                $"DELETE FROM ProfessionalModelQuestions WHERE ModelId IN {models}",
                "DELETE FROM ProfessionalModels WHERE Title LIKE 'PPMS6%'",
                "DELETE FROM Questions WHERE ReferenceNumber LIKE 'PPMS6%'",
                $"DELETE FROM Lecture WHERE BatchId IN {batches}",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPMS6%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPMS6%'",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'ppms6-%'",
                "DELETE FROM Branches WHERE Name LIKE 'PPMS6%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPMS6%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        private static ApplicationUser NewUser(string userName, string nationalId) => new()
        {
            Id = userName == SenderId ? SenderId : Guid.NewGuid().ToString(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@test.local",
            NormalizedEmail = $"{userName}@test.local".ToUpperInvariant(),
            NationalID = nationalId,
            FullName = userName,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };

        private static async Task<Seed> SeedAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            var anchor = await db.Lessons.AsNoTracking()
                .Where(l => l.Section != null)
                .Select(l => new { LessonId = l.Id, l.Section.CurriculumId })
                .FirstAsync();

            db.Users.Add(NewUser(SenderId, $"{Marker}-SENDER"));

            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            var branch = new Branch { Name = $"{Marker} فرع", Location = "-", City = "-", State = "-", Country = "-" };
            db.AddRange(course, branch);
            await db.SaveChangesAsync();

            Batch NewBatch(string name)
            {
                var batch = new Batch { Name = $"{Marker} {name}", BranchId = branch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
                db.Batches.Add(batch);
                return batch;
            }

            var batchA = NewBatch("دفعة A");
            var batchB = NewBatch("دفعة B");
            var empty = NewBatch("دفعة فارغة");
            await db.SaveChangesAsync();

            var lecture = new Lecture { Title = $"{Marker} محاضرة", BatchId = batchA.Id, Date = DateTime.UtcNow };
            db.Lecture.Add(lecture);

            Student NewStudent(int i, bool learning = true)
            {
                var user = NewUser($"ppms6-s{i}", $"{Marker}{i}");
                db.Users.Add(user);
                var student = new Student
                {
                    NationalID = $"{Marker}{i}", FullName = $"{Marker} طالب {i}", Gender = "ذكر", Age = 17,
                    School = "-", Level = "-", UserId = user.Id, BranchId = branch.Id, IsActiveForLearning = learning
                };
                db.Students.Add(student);
                return student;
            }

            var a1 = NewStudent(1);
            var a2 = NewStudent(2);
            var withdrawn = NewStudent(3);
            var notLearning = NewStudent(4, learning: false);
            var b1 = NewStudent(5);
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = a1.StudentID, BatchId = batchA.Id },
                new StudentBatchEnrollment { StudentID = a2.StudentID, BatchId = batchA.Id },
                new StudentBatchEnrollment { StudentID = withdrawn.StudentID, BatchId = batchA.Id, Status = "Withdrawn" },
                new StudentBatchEnrollment { StudentID = notLearning.StudentID, BatchId = batchA.Id },
                new StudentBatchEnrollment { StudentID = b1.StudentID, BatchId = batchB.Id });

            Question NewQuestion(string suffix) => new()
            {
                Id = Guid.NewGuid(),
                Title = $"{Marker} سؤال {suffix}",
                ReferenceNumber = $"{Marker}-{suffix}",
                CurriculumId = anchor.CurriculumId,
                LessonId = anchor.LessonId,
                CorrectAnswer = "أ",
                IsComplete = true,
                IsReviewed = true
            };

            var q1 = NewQuestion("Q1");
            var q2 = NewQuestion("Q2");
            db.Questions.AddRange(q1, q2);

            ProfessionalModel NewModel(string title, ProfessionalModelType type) => new()
            {
                Title = $"{Marker} {title}",
                Description = "-",
                CreatedBy = "-",
                CreatedAt = DateTime.UtcNow,
                CurriculumId = anchor.CurriculumId,
                ModelType = type,
                Questions = new List<ProfessionalModelQuestion>
                {
                    new() { QuestionId = q2.Id, OrderNumber = 1 },
                    new() { QuestionId = q1.Id, OrderNumber = 2 }
                }
            };

            var homeworkModel = NewModel("واجب", ProfessionalModelType.Homework);
            var examModel = NewModel("اختبار", ProfessionalModelType.Exam);
            var untypedModel = NewModel("بلا نوع", 0);
            db.ProfessionalModels.AddRange(homeworkModel, examModel, untypedModel);
            await db.SaveChangesAsync();

            return new Seed(
                anchor.CurriculumId, anchor.LessonId, batchA.Id, batchB.Id, empty.Id, lecture.Id,
                a1.StudentID, a2.StudentID, withdrawn.StudentID, notLearning.StudentID, b1.StudentID,
                homeworkModel.Id, examModel.Id, untypedModel.Id, q1.Id, q2.Id);
        }

        private static DateTime Local(int year, int month, int day, int hour)
            => DateTime.SpecifyKind(new DateTime(year, month, day, hour, 0, 0), DateTimeKind.Unspecified);

        private static SendModelExamRequest ExamRequest(Seed seed, params int[] batchIds) => new()
        {
            ModelId = seed.ExamModel,
            BatchIds = batchIds.ToList(),
            DurationMinutes = 30,
            StartAtLocal = Local(2031, 5, 1, 10),
            EndAtLocal = Local(2031, 5, 2, 10)
        };

        // =====================================================
        // الواجب (S6.1)
        // =====================================================
        [SqlServerFact]
        public async Task Homework_WritesSameRowsAsOldScreen_ForEachBatch()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(Admin, new SendModelHomeworkRequest
            {
                ModelId = seed.HomeworkModel,
                BatchIds = new List<int> { seed.BatchA, seed.BatchB },
                Title = "  واجب الأسبوع  ",
                StartAtLocal = Local(2031, 5, 1, 10),
                EndAtLocal = Local(2031, 5, 2, 10)
            });

            Assert.True(result.Succeeded, result.Message);

            await using var read = RtkRealDb.CreateDb();
            var sets = await read.HomeworkSets.AsNoTracking().Where(h => h.SourceProfessionalModelId == seed.HomeworkModel).ToListAsync();
            Assert.Equal(new[] { seed.BatchA, seed.BatchB }.OrderBy(x => x), sets.Select(s => s.BatchId).OrderBy(x => x));

            foreach (var set in sets)
            {
                Assert.Equal("واجب الأسبوع", set.Title);
                Assert.Equal($"{Marker} واجب", set.CompletionTitle);
                Assert.Equal(new DateTime(2031, 5, 1, 7, 0, 0), set.StartAt); // UTC
                Assert.Equal(new DateTime(2031, 5, 2, 7, 0, 0), set.EndAt);
                Assert.True(set.IsExtra);
                Assert.Null(set.LectureId);
                Assert.True(set.IsFromProfessionalModel);
                Assert.True(set.IsSent);
                Assert.Equal(SenderId, set.AssignedByUserId);
            }

            var setA = sets.Single(s => s.BatchId == seed.BatchA);
            var recipients = await read.HomeworkSetStudents.AsNoTracking().Where(s => s.HomeworkSetId == setA.Id).ToListAsync();
            Assert.All(recipients, r => Assert.True(r.NotificationSent));

            var homeworks = await read.Homeworks.AsNoTracking().Where(h => h.HomeworkSetId == setA.Id).ToListAsync();
            Assert.Equal(recipients.Count * 2, homeworks.Count); // طالب × سؤال
            Assert.All(homeworks, h =>
            {
                Assert.Equal(seed.LessonId, h.LessonId);
                Assert.Equal(HomeworkStatus.Pending, h.Status);
                Assert.True(h.IsSent);
                Assert.Null(h.LectureId);
            });
        }

        [SqlServerFact]
        public async Task Homework_NoTitle_UsesOldDefaultTitle_AndAllowsNoDates()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(Admin, new SendModelHomeworkRequest
            {
                ModelId = seed.HomeworkModel,
                BatchIds = new List<int> { seed.BatchB }
            });

            Assert.True(result.Succeeded, result.Message);

            var set = await db.HomeworkSets.AsNoTracking().SingleAsync(h => h.SourceProfessionalModelId == seed.HomeworkModel);
            Assert.Equal($"واجب من النموذج {Marker} واجب", set.Title);
            Assert.Null(set.StartAt);
            Assert.Null(set.EndAt);
        }

        [SqlServerFact]
        public async Task Homework_LinkedToLecture_MatchesOldRules()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            var twoBatches = await service.SendHomeworkAsync(Admin, new SendModelHomeworkRequest { ModelId = seed.HomeworkModel, BatchIds = new List<int> { seed.BatchA, seed.BatchB }, LectureId = seed.LectureInA });
            Assert.Equal("⚠️ ربط الواجب بمحاضرة يتطلب اختيار دفعة واحدة فقط.", twoBatches.Message);

            var otherBatch = await service.SendHomeworkAsync(Admin, new SendModelHomeworkRequest { ModelId = seed.HomeworkModel, BatchIds = new List<int> { seed.BatchB }, LectureId = seed.LectureInA });
            Assert.Equal("⚠️ المحاضرة المختارة لا تنتمي للدفعة المحددة.", otherBatch.Message);

            var linked = await service.SendHomeworkAsync(Admin, new SendModelHomeworkRequest { ModelId = seed.HomeworkModel, BatchIds = new List<int> { seed.BatchA }, LectureId = seed.LectureInA });
            Assert.True(linked.Succeeded, linked.Message);

            var set = await db.HomeworkSets.AsNoTracking().SingleAsync(h => h.SourceProfessionalModelId == seed.HomeworkModel);
            Assert.False(set.IsExtra);
            Assert.Equal(seed.LectureInA, set.LectureId);
            Assert.True(await db.Homeworks.AsNoTracking().Where(h => h.HomeworkSetId == set.Id).AllAsync(h => h.LectureId == seed.LectureInA));
        }

        // =====================================================
        // الاختبار (S6.2)
        // =====================================================
        [SqlServerFact]
        public async Task Exam_WritesSameRowsAsOldScreen_OneExamSharedByBatches()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(Admin, ExamRequest(seed, seed.BatchA, seed.BatchB));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(2, result.BatchesSent);

            await using var read = RtkRealDb.CreateDb();

            var exam = await read.Exams.AsNoTracking().SingleAsync(e => e.SourceProfessionalModelId == seed.ExamModel);
            Assert.Equal($"اختبار من النموذج {Marker} اختبار", exam.Title);
            Assert.Equal(ExamType.Manual, exam.Type);
            Assert.Equal(2, exam.TotalQuestions);
            Assert.Equal(30, exam.DurationMinutes);
            Assert.True(exam.IsActive);
            Assert.True(exam.IsFromProfessionalModel);
            Assert.True(exam.RandomizeQuestions);
            Assert.Null(exam.OwnerPartnerId);
            Assert.Null(exam.CurriculumId);
            Assert.Null(exam.ReferenceCode);

            var assignments = await read.ExamAssignmentsToBatches.AsNoTracking().Where(a => a.ExamId == exam.Id).ToListAsync();
            Assert.Equal(new[] { seed.BatchA, seed.BatchB }.OrderBy(x => x), assignments.Select(a => a.BatchId).OrderBy(x => x));
            Assert.All(assignments, a =>
            {
                Assert.Equal(exam.Title, a.Title);
                Assert.Equal(new DateTime(2031, 5, 1, 7, 0, 0), a.ScheduledDate);
                Assert.Equal(new DateTime(2031, 5, 2, 7, 0, 0), a.EndAt);
                Assert.Equal(30, a.DurationMinutes);
                Assert.Equal(2, a.TotalQuestions);
                Assert.True(a.IsSentToStudents);
                Assert.True(a.IsOnline);
                Assert.False(a.IsInLab);
                Assert.Null(a.CurriculumId);
            });

            // ترتيب النموذج محفوظ لكل دفعة
            foreach (var assignment in assignments)
            {
                var questions = await read.ExamQuestions.AsNoTracking()
                    .Where(q => q.ExamAssignmentId == assignment.Id)
                    .OrderBy(q => q.Order)
                    .ToListAsync();
                Assert.Equal(new[] { seed.Q2, seed.Q1 }, questions.Select(q => q.QuestionId));
                Assert.Equal(new[] { 1, 2 }, questions.Select(q => q.Order));
                Assert.All(questions, q => { Assert.Equal(exam.Id, q.ExamId); Assert.True(q.IsManuallySelected); });
            }

            var statuses = await read.ExamStudentStatuses.AsNoTracking().Where(s => s.ExamId == exam.Id).ToListAsync();
            Assert.All(statuses, s => { Assert.Equal(ExamStatus.Pending, s.Status); Assert.False(s.IsSubmitted); });

            // الشاشة القديمة لا تنشئ ExamAssignments
            Assert.False(await read.ExamAssignments.AsNoTracking().AnyAsync(a => a.ExamId == exam.Id));
        }

        [SqlServerFact]
        public async Task Exam_InLab_SharesOneReferenceCode_AndKeepsRandomizeChoice()
        {
            var seed = await SeedAsync();
            var request = ExamRequest(seed, seed.BatchA, seed.BatchB);
            request.IsInLab = true;
            request.RandomizeQuestions = false;

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(Admin, request);
            Assert.True(result.Succeeded, result.Message);

            var exam = await db.Exams.AsNoTracking().SingleAsync(e => e.SourceProfessionalModelId == seed.ExamModel);
            Assert.Matches("^[0-9]{6}$", exam.ReferenceCode!);
            Assert.False(exam.RandomizeQuestions);

            var assignments = await db.ExamAssignmentsToBatches.AsNoTracking().Where(a => a.ExamId == exam.Id).ToListAsync();
            Assert.Equal(2, assignments.Count);
            Assert.All(assignments, a => { Assert.True(a.IsInLab); Assert.False(a.IsOnline); });
        }

        [SqlServerFact]
        public async Task Exam_SelectedStudents_OnlyThoseReceive_AndBatchWithoutThemIsSkipped()
        {
            var seed = await SeedAsync();
            var request = ExamRequest(seed, seed.BatchA, seed.BatchB);
            request.StudentIds = new List<int> { seed.StudentA1, seed.Withdrawn };

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(Admin, request);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(1, result.BatchesSent);
            Assert.Equal(1, result.StudentsSent);

            var statuses = await db.ExamStudentStatuses.AsNoTracking().Where(s => s.Exam.SourceProfessionalModelId == seed.ExamModel).ToListAsync();
            Assert.Equal(seed.StudentA1, Assert.Single(statuses).StudentId);
            Assert.False(await db.ExamAssignmentsToBatches.AsNoTracking().AnyAsync(a => a.Exam.SourceProfessionalModelId == seed.ExamModel && a.BatchId == seed.BatchB));
        }

        [SqlServerFact]
        public async Task Exam_SendToSelectedWithNoStudents_FailsInsteadOfEmptyExam()
        {
            var seed = await SeedAsync();
            var request = ExamRequest(seed, seed.BatchA);
            request.StudentIds = new List<int>();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(Admin, request);

            Assert.False(result.Succeeded);
            Assert.Contains("لا يوجد طلاب", result.Message);
            Assert.False(await db.Exams.AsNoTracking().AnyAsync(e => e.SourceProfessionalModelId == seed.ExamModel));
        }

        [SqlServerFact]
        public async Task Exam_NoDates_StartsNow_AndEndsAfterDuration_AsOldScreen()
        {
            var seed = await SeedAsync();
            var request = ExamRequest(seed, seed.BatchB);
            request.StartAtLocal = null;
            request.EndAtLocal = null;

            var before = DateTime.UtcNow.AddSeconds(-5);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(Admin, request);
            Assert.True(result.Succeeded, result.Message);

            var assignment = await db.ExamAssignmentsToBatches.AsNoTracking().SingleAsync(a => a.Exam.SourceProfessionalModelId == seed.ExamModel);
            Assert.True(assignment.ScheduledDate >= before && assignment.ScheduledDate <= DateTime.UtcNow.AddSeconds(5));
            Assert.Equal(assignment.ScheduledDate!.Value.AddMinutes(30), assignment.EndAt);
        }

        // =====================================================
        // الفروق المقصودة عن الشاشة القديمة
        // =====================================================
        [SqlServerFact]
        public async Task Intended_ActiveEnrollmentsOnly_WithdrawnExcluded_InactiveForLearningKept()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendHomeworkAsync(Admin, new SendModelHomeworkRequest { ModelId = seed.HomeworkModel, BatchIds = new List<int> { seed.BatchA } });
            Assert.True(result.Succeeded, result.Message);

            var recipients = await db.HomeworkSetStudents.AsNoTracking()
                .Where(s => s.HomeworkSet.SourceProfessionalModelId == seed.HomeworkModel)
                .Select(s => s.StudentId)
                .ToListAsync();

            Assert.Equal(new[] { seed.StudentA1, seed.StudentA2, seed.NotLearning }.OrderBy(x => x), recipients.OrderBy(x => x));
            Assert.DoesNotContain(seed.Withdrawn, recipients);
        }

        [SqlServerFact]
        public async Task Intended_EmptyBatch_IsSkipped_ForExamToo()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(Admin, ExamRequest(seed, seed.BatchB, seed.EmptyBatch));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(1, result.BatchesSent);
            Assert.False(await db.ExamAssignmentsToBatches.AsNoTracking().AnyAsync(a => a.BatchId == seed.EmptyBatch));
        }

        [SqlServerFact]
        public async Task Intended_EndBeforeStart_IsRejected_WithoutWrites()
        {
            var seed = await SeedAsync();
            var request = ExamRequest(seed, seed.BatchB);
            request.EndAtLocal = request.StartAtLocal!.Value.AddHours(-1);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendExamAsync(Admin, request);

            Assert.Equal("⚠️ موعد النهاية يجب أن يكون بعد موعد البداية.", result.Message);
            Assert.False(await db.Exams.AsNoTracking().AnyAsync(e => e.SourceProfessionalModelId == seed.ExamModel));
        }

        [SqlServerFact]
        public async Task Intended_RepeatSend_RequiresConfirmation_ThenWritesOnce()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);
            Assert.True((await service.SendExamAsync(Admin, ExamRequest(seed, seed.BatchB))).Succeeded);

            // خارج نافذة النقر المزدوج
            await db.ExamAssignmentsToBatches
                .Where(a => a.BatchId == seed.BatchB)
                .ExecuteUpdateAsync(x => x.SetProperty(a => a.CreatedAt, DateTime.UtcNow.AddDays(-1)));

            var unconfirmed = await service.SendExamAsync(Admin, ExamRequest(seed, seed.BatchB));
            Assert.True(unconfirmed.RequiresConfirmation);
            Assert.True(unconfirmed.Preview!.Batches.Single().AlreadySentFromModel);
            Assert.Equal(2, unconfirmed.Preview.Batches.Single().RepeatedQuestionCount);
            Assert.Equal(1, await db.Exams.AsNoTracking().CountAsync(e => e.SourceProfessionalModelId == seed.ExamModel));

            var confirmed = ExamRequest(seed, seed.BatchB);
            confirmed.ConfirmWarnings = true;
            Assert.True((await service.SendExamAsync(Admin, confirmed)).Succeeded);
            Assert.Equal(2, await db.Exams.AsNoTracking().CountAsync(e => e.SourceProfessionalModelId == seed.ExamModel));
        }

        [SqlServerFact]
        public async Task Intended_WrongOrMissingType_IsNotFound()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);

            var homeworkModelAsExam = ExamRequest(seed, seed.BatchB);
            homeworkModelAsExam.ModelId = seed.HomeworkModel;
            Assert.True((await service.SendExamAsync(Admin, homeworkModelAsExam)).IsNotFound);

            var untyped = ExamRequest(seed, seed.BatchB);
            untyped.ModelId = seed.UntypedModel;
            Assert.True((await service.SendExamAsync(Admin, untyped)).IsNotFound);
        }

        // =====================================================
        // خيارات الأدمن لا تصل نطاق الشريك
        // =====================================================
        [SqlServerFact]
        public async Task Partner_CannotUseAdminOnlyOptions()
        {
            var seed = await SeedAsync();

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);
            var partner = ModelSendScope.Partner(999999, SenderId);

            foreach (var configure in new Action<SendModelExamRequest>[]
            {
                r => r.IsInLab = true,
                r => r.RandomizeQuestions = false,
                r => r.StudentIds = new List<int> { seed.StudentA1 }
            })
            {
                var request = ExamRequest(seed, seed.BatchA);
                configure(request);
                var result = await service.SendExamAsync(partner, request);
                Assert.Equal("⚠️ هذه الخيارات غير متاحة.", result.Message);
            }
        }
    }
}
