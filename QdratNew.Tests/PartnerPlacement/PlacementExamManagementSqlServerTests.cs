using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Interfaces;
using QdratNew.Tests.Integration;
using Xunit;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S2.4 — مسارات BulkInsert + ExecutionStrategy + Transaction على SQL Server حقيقي بإعداد الإنتاج (UseCompatibilityLevel(120)).
    // التشغيل: QDRAT_TEST_SQL على قاعدة الاختبار المعزولة، مع تطبيق Migrations/Scripts/PPL_ExamOwnerPartner.sql عليها. بدونه تُتخطّى.
    [Collection(RemedialTrackSharedTestDbCollection.Name)]
    [Trait("Category", "SqlServer")]
    public sealed class PlacementExamManagementSqlServerTests : IAsyncLifetime
    {
        private const string Marker = "PPLS2";

        private sealed record SeedData(int PartnerId, int OtherPartnerId, int InstituteBatch, int PartnerBatch, int[] InstituteStudents, int ActivePartnerStudent, int ArchivedPartnerStudent, int OtherPartnerStudent);

        private static PlacementExamManagementService NewService(ApplicationDbContext db)
        {
            var tz = new Mock<ITimeZoneService>();
            tz.Setup(x => x.GetNowUtc()).Returns(DateTime.UtcNow);

            return new PlacementExamManagementService(
                db,
                new Mock<IPlacementExamGeneratorService>().Object,
                new PlacementExamScopeGuard(),
                tz.Object,
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<PlacementExamManagementService>.Instance);
        }

        public Task InitializeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        public Task DisposeAsync() => RtkRealDb.IsConfigured ? CleanupAsync() : Task.CompletedTask;

        private static async Task CleanupAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            const string exams = "(SELECT Id FROM Exams WHERE Title LIKE N'%PPLS2%')";
            const string batches = "(SELECT Id FROM Batches WHERE Name LIKE 'PPLS2%')";
            const string students = "(SELECT StudentID FROM Students WHERE NationalID LIKE 'PPLS2%')";

            foreach (var sql in new[]
            {
                "DELETE FROM SystemLogs WHERE Description LIKE N'%PPLS2%'",
                $"DELETE FROM QuestionAttemptNew WHERE ExamId IN {exams}",
                $"DELETE FROM ExamQuestions WHERE ExamId IN {exams}",
                $"DELETE FROM ExamStudentStatuses WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignments WHERE ExamId IN {exams}",
                $"DELETE FROM ExamAssignmentsToBatches WHERE ExamId IN {exams}",
                "DELETE FROM Exams WHERE Title LIKE N'%PPLS2%'",
                "DELETE FROM ProfessionalModelQuestions WHERE ModelId IN (SELECT Id FROM ProfessionalModels WHERE Title LIKE 'PPLS2%')",
                "DELETE FROM ProfessionalModels WHERE Title LIKE 'PPLS2%'",
                $"DELETE FROM StudentBatchEnrollments WHERE BatchId IN {batches}",
                "DELETE FROM Batches WHERE Name LIKE 'PPLS2%'",
                "DELETE FROM Students WHERE NationalID LIKE 'PPLS2%'",
                "DELETE FROM AspNetUsers WHERE UserName LIKE 'ppls2-%'",
                "DELETE FROM PartnerSubscriptions WHERE PartnerId IN (SELECT Id FROM Partners WHERE Code LIKE 'PPLS2%')",
                "DELETE FROM Branches WHERE Name LIKE 'PPLS2%'",
                "DELETE FROM Partners WHERE Code LIKE 'PPLS2%'",
                "DELETE FROM Courses WHERE Name LIKE 'PPLS2%'"
            })
            {
                await db.Database.ExecuteSqlRawAsync(sql);
            }
        }

        private static async Task<SeedData> SeedAsync()
        {
            await using var db = RtkRealDb.CreateDb();

            var partner = new Partner { Name = $"{Marker} A", Code = $"{Marker}-A", PartnershipStart = DateTime.Today.AddDays(-1), PartnershipEnd = DateTime.Today.AddDays(30) };
            var other = new Partner { Name = $"{Marker} B", Code = $"{Marker}-B", PartnershipStart = DateTime.Today.AddDays(-1), PartnershipEnd = DateTime.Today.AddDays(30) };
            db.Partners.AddRange(partner, other);
            await db.SaveChangesAsync();

            db.PartnerSubscriptions.Add(new PartnerSubscription
            {
                PartnerId = partner.Id,
                StartDate = DateTime.Today.AddDays(-1),
                EndDate = DateTime.Today.AddDays(30),
                CanUsePlacementExams = true
            });

            var instituteBranch = NewBranch($"{Marker} معهد", null);
            var partnerBranch = NewBranch($"{Marker} فرع A", partner.Id);
            var otherBranch = NewBranch($"{Marker} فرع B", other.Id);
            var course = new Course { Name = $"{Marker} دورة", Description = "-", StartDate = DateTime.UtcNow, IsActive = true };
            db.Branches.AddRange(instituteBranch, partnerBranch, otherBranch);
            db.Courses.Add(course);
            await db.SaveChangesAsync();

            var instituteBatch = new Batch { Name = $"{Marker} دفعة معهد", BranchId = instituteBranch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            var partnerBatch = new Batch { Name = $"{Marker} دفعة A", BranchId = partnerBranch.Id, CourseId = course.Id, StartDate = DateTime.UtcNow };
            db.Batches.AddRange(instituteBatch, partnerBatch);
            await db.SaveChangesAsync();

            var inst1 = NewStudent(db, 1, instituteBranch.Id);
            var inst2 = NewStudent(db, 2, instituteBranch.Id);
            var inst3 = NewStudent(db, 3, instituteBranch.Id);
            var active = NewStudent(db, 4, partnerBranch.Id);
            var archived = NewStudent(db, 5, partnerBranch.Id, active: false);
            var foreign = NewStudent(db, 6, otherBranch.Id); // طالب شريك آخر مُسجَّل خطأً في دفعة الشريك A
            await db.SaveChangesAsync();

            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = inst1.StudentID, BatchId = instituteBatch.Id },
                new StudentBatchEnrollment { StudentID = inst2.StudentID, BatchId = instituteBatch.Id },
                new StudentBatchEnrollment { StudentID = inst3.StudentID, BatchId = instituteBatch.Id },
                new StudentBatchEnrollment { StudentID = active.StudentID, BatchId = partnerBatch.Id },
                new StudentBatchEnrollment { StudentID = archived.StudentID, BatchId = partnerBatch.Id },
                new StudentBatchEnrollment { StudentID = foreign.StudentID, BatchId = partnerBatch.Id });
            await db.SaveChangesAsync();

            return new SeedData(partner.Id, other.Id, instituteBatch.Id, partnerBatch.Id,
                new[] { inst1.StudentID, inst2.StudentID, inst3.StudentID }, active.StudentID, archived.StudentID, foreign.StudentID);
        }

        private static Branch NewBranch(string name, int? partnerId)
            => new() { Name = name, PartnerId = partnerId, Location = "-", City = "-", State = "-", Country = "-" };

        private static Student NewStudent(ApplicationDbContext db, int i, int branchId, bool active = true)
        {
            var userName = $"ppls2-s{i}";
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

        private static async Task<int> AddExamAsync(int? ownerPartnerId, int batchId, bool sent = false)
        {
            await using var db = RtkRealDb.CreateDb();
            var exam = new Exam { Title = $"{Marker} اختبار", Type = ExamType.LevelAssessment, DurationMinutes = 30, OwnerPartnerId = ownerPartnerId };
            db.Exams.Add(exam);
            await db.SaveChangesAsync();

            db.ExamAssignmentsToBatches.Add(new ExamAssignmentToBatch
            {
                ExamId = exam.Id, BatchId = batchId, Title = "اختبار تحديد المستوى", IsSentToStudents = sent
            });
            await db.SaveChangesAsync();
            return exam.Id;
        }

        private static async Task<(int Assignments, int Statuses, bool Sent)> ReadSendStateAsync(int examId)
        {
            await using var db = RtkRealDb.CreateDb();
            return (
                await db.ExamAssignments.CountAsync(a => a.ExamId == examId),
                await db.ExamStudentStatuses.CountAsync(s => s.ExamId == examId),
                await db.ExamAssignmentsToBatches.AnyAsync(x => x.ExamId == examId && x.IsSentToStudents));
        }

        [SqlServerFact]
        public async Task Admin_Send_CreatesAssignmentsAndStatusesPerStudent()
        {
            var seed = await SeedAsync();
            var examId = await AddExamAsync(null, seed.InstituteBatch);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendToStudentsAsync(PlacementScope.Admin("admin"), examId);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("✅ تم إرسال الاختبار بنجاح.", result.Message);
            Assert.Equal((3, 3, true), await ReadSendStateAsync(examId));
        }

        [SqlServerFact]
        public async Task Admin_SendTwice_IsRejectedWithSameMessage()
        {
            var seed = await SeedAsync();
            var examId = await AddExamAsync(null, seed.InstituteBatch);

            await using (var first = RtkRealDb.CreateDb())
                Assert.True((await NewService(first).SendToStudentsAsync(PlacementScope.Admin("admin"), examId)).Succeeded);

            await using var db = RtkRealDb.CreateDb();
            var again = await NewService(db).SendToStudentsAsync(PlacementScope.Admin("admin"), examId);

            Assert.False(again.Succeeded);
            Assert.Equal("⚠️ تم الإرسال مسبقًا.", again.Message);
            Assert.Equal((3, 3, true), await ReadSendStateAsync(examId));
        }

        [SqlServerFact]
        public async Task Admin_SendPartnerExam_IsRejected()
        {
            var seed = await SeedAsync();
            var examId = await AddExamAsync(seed.PartnerId, seed.PartnerBatch);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendToStudentsAsync(PlacementScope.Admin("admin"), examId);

            Assert.Equal("❌ الاختبار غير موجود.", result.Message);
            Assert.Equal((0, 0, false), await ReadSendStateAsync(examId));
        }

        [SqlServerFact]
        public async Task Admin_SendToNewStudents_OnlyAddsMissing()
        {
            var seed = await SeedAsync();
            var examId = await AddExamAsync(null, seed.InstituteBatch, sent: true);

            await using (var pre = RtkRealDb.CreateDb())
            {
                pre.ExamAssignments.Add(new ExamAssignment { ExamId = examId, StudentId = seed.InstituteStudents[0], AssignedAt = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddHours(1) });
                await pre.SaveChangesAsync();
            }

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendToNewStudentsAsync(PlacementScope.Admin("admin"), examId, seed.InstituteBatch);

            Assert.Equal("✅ تم إرسال الاختبار لـ 2 طالب جديد انضم للدفعة.", result.Message);
            Assert.Equal(3, (await ReadSendStateAsync(examId)).Assignments);
        }

        [SqlServerFact]
        public async Task Partner_Send_ReachesOnlyOwnActiveStudents()
        {
            var seed = await SeedAsync();
            var examId = await AddExamAsync(seed.PartnerId, seed.PartnerBatch);

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).SendToStudentsAsync(PlacementScope.Partner(seed.PartnerId, "user"), examId);

            Assert.True(result.Succeeded, result.Message);

            await using var read = RtkRealDb.CreateDb();
            var assigned = await read.ExamAssignments.Where(a => a.ExamId == examId).Select(a => a.StudentId).ToListAsync();
            Assert.Equal(new[] { seed.ActivePartnerStudent }, assigned);
        }

        [SqlServerFact]
        public async Task Partner_SendOtherPartnerExam_OrWithoutContract_IsRejected()
        {
            var seed = await SeedAsync();
            var examId = await AddExamAsync(seed.PartnerId, seed.PartnerBatch);

            await using var db = RtkRealDb.CreateDb();
            // الشريك B بلا عقد، وحتى مع العقد الاختبار ليس له
            var result = await NewService(db).SendToStudentsAsync(PlacementScope.Partner(seed.OtherPartnerId, "user"), examId);

            Assert.False(result.Succeeded);
            Assert.Equal((0, 0, false), await ReadSendStateAsync(examId));
        }

        private static async Task<List<Guid>> AnyQuestionIdsAsync(ApplicationDbContext db, int count)
        {
            var ids = await db.Questions.AsNoTracking().OrderBy(q => q.Id).Select(q => q.Id).Take(count).ToListAsync();
            Assert.Equal(count, ids.Count); // قاعدة الاختبار نسخة من قاعدة التطوير وفيها أسئلة
            return ids;
        }

        [SqlServerFact]
        public async Task Admin_CreateFromProfessionalModel_CreatesAndSendsOwnedByInstitute()
        {
            var seed = await SeedAsync();
            int modelId;
            int courseId;

            await using (var pre = RtkRealDb.CreateDb())
            {
                var questionIds = await AnyQuestionIdsAsync(pre, 2);
                var model = new ProfessionalModel
                {
                    Title = $"{Marker} نموذج", Description = "-", CreatedBy = "-",
                    Questions = questionIds.Select((id, i) => new ProfessionalModelQuestion { QuestionId = id, OrderNumber = i + 1 }).ToList()
                };
                pre.ProfessionalModels.Add(model);
                await pre.SaveChangesAsync();
                modelId = model.Id;
                courseId = await pre.Batches.Where(b => b.Id == seed.InstituteBatch).Select(b => b.CourseId).FirstAsync();
            }

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).CreateFromProfessionalModelAsync(
                PlacementScope.Admin("admin"),
                new QdratNew.ViewModels.Exam.PlacementExamFromModelVm { SelectedModelId = modelId, SelectedBatchId = seed.InstituteBatch, SelectedCourseId = courseId, DurationMinutes = 45 });

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("✅ تم إنشاء وإرسال الاختبار بنجاح.", result.Message);

            await using var read = RtkRealDb.CreateDb();
            var exam = await read.Exams.SingleAsync(e => e.Title == $"اختبار تحديد مستوى - {Marker} نموذج");
            Assert.Null(exam.OwnerPartnerId);
            Assert.True(exam.IsFromProfessionalModel);
            Assert.Equal(2, await read.ExamQuestions.CountAsync(q => q.ExamId == exam.Id));
            Assert.Equal((3, 3, true), await ReadSendStateAsync(exam.Id));
        }

        [SqlServerFact]
        public async Task Admin_DeletePermanently_RemovesAllExamRowsAndLogs()
        {
            var seed = await SeedAsync();
            var examId = await AddExamAsync(null, seed.InstituteBatch, sent: true);
            var studentId = seed.InstituteStudents[0];

            await using (var pre = RtkRealDb.CreateDb())
            {
                var questionId = (await AnyQuestionIdsAsync(pre, 1))[0];
                pre.ExamQuestions.Add(new ExamQuestion { ExamId = examId, QuestionId = questionId, Order = 1 });
                pre.ExamAssignments.Add(new ExamAssignment { ExamId = examId, StudentId = studentId, AssignedAt = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddHours(1) });
                pre.ExamStudentStatuses.Add(new ExamStudentStatus { ExamId = examId, StudentId = studentId, IsSubmitted = true, Status = ExamStatus.Completed });
                pre.QuestionAttemptNew.Add(new QuestionAttemptNew { ExamId = examId, StudentId = studentId, QuestionId = questionId, SelectedAnswer = "أ" });
                await pre.SaveChangesAsync();
            }

            await using var db = RtkRealDb.CreateDb();
            var result = await NewService(db).DeletePermanentlyAsync(PlacementScope.Admin("admin", $"{Marker}-admin"), examId, seed.InstituteBatch);

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("🗑️ تم حذف الاختبار نهائياً من قاعدة البيانات.", result.Message);

            await using var read = RtkRealDb.CreateDb();
            Assert.False(await read.Exams.AnyAsync(e => e.Id == examId));
            Assert.False(await read.ExamQuestions.AnyAsync(q => q.ExamId == examId));
            Assert.False(await read.QuestionAttemptNew.AnyAsync(a => a.ExamId == examId));
            Assert.False(await read.ExamAssignmentsToBatches.AnyAsync(x => x.ExamId == examId));
            Assert.Equal((0, 0, false), await ReadSendStateAsync(examId));

            var log = await read.SystemLogs.SingleAsync(l => l.EntityId == examId && l.Action == "PermanentDelete");
            Assert.Contains("اختبره 1 طالب", log.Description);
        }

        [SqlServerFact]
        public async Task Admin_Backfill_BuildsMissingRowsFromAttempts()
        {
            var seed = await SeedAsync();
            var examId = await AddExamAsync(null, seed.InstituteBatch, sent: true);
            var studentId = seed.InstituteStudents[0];

            await using (var pre = RtkRealDb.CreateDb())
            {
                var questionId = (await AnyQuestionIdsAsync(pre, 1))[0];
                pre.QuestionAttemptNew.Add(new QuestionAttemptNew { ExamId = examId, StudentId = studentId, QuestionId = questionId, SelectedAnswer = "أ" });
                await pre.SaveChangesAsync();
            }

            await using (var db = RtkRealDb.CreateDb())
            {
                var result = await NewService(db).BackfillMissingAssignmentsAsync(PlacementScope.Admin("admin"), examId, seed.InstituteBatch);
                Assert.Equal("✅ تم إصلاح بيانات 1 طالب. أصبح بإمكانك الآن عرض وطباعة تقاريرهم.", result.Message);
            }

            await using var read = RtkRealDb.CreateDb();
            var assignment = await read.ExamAssignments.SingleAsync(a => a.ExamId == examId);
            var status = await read.ExamStudentStatuses.SingleAsync(st => st.ExamId == examId);
            var attempt = await read.QuestionAttemptNew.SingleAsync(a => a.ExamId == examId);
            Assert.Equal(studentId, assignment.StudentId);
            Assert.True(status.IsSubmitted);
            Assert.Equal(ExamStatus.Completed, status.Status);
            Assert.Equal(assignment.Id, attempt.ExamAssignmentId);

            await using var again = RtkRealDb.CreateDb();
            var second = await NewService(again).BackfillMissingAssignmentsAsync(PlacementScope.Admin("admin"), examId, seed.InstituteBatch);
            Assert.Equal(PlacementOperationOutcome.Info, second.Outcome);
        }

        [SqlServerFact]
        public async Task Queries_TranslateOnCompatibilityLevel120()
        {
            var seed = await SeedAsync();
            var adminExam = await AddExamAsync(null, seed.PartnerBatch);
            var partnerExam = await AddExamAsync(seed.PartnerId, seed.PartnerBatch);

            await using var db = RtkRealDb.CreateDb();
            var service = NewService(db);
            var admin = PlacementScope.Admin("admin");
            var partner = PlacementScope.Partner(seed.PartnerId, "user");

            var adminBatchExams = await service.GetBatchExamsAsync(admin, seed.PartnerBatch);
            Assert.Equal(new[] { adminExam }, adminBatchExams!.Select(e => e.ExamId));

            var partnerBatchExams = await service.GetBatchExamsAsync(partner, seed.PartnerBatch);
            Assert.Equal(new[] { partnerExam }, partnerBatchExams!.Select(e => e.ExamId));
            Assert.Equal(1, partnerBatchExams![0].StudentCount);

            Assert.DoesNotContain((await service.GetIndexAsync(admin)).ActiveBatches, b => b.BatchId == seed.PartnerBatch && b.ExamsCount != 1);
            Assert.Single((await service.GetIndexAsync(partner)).ActiveBatches);

            Assert.NotNull(await service.GetPlacementDetailsAsync(partner, partnerExam));
            Assert.Null(await service.GetPlacementDetailsAsync(admin, partnerExam));

            var vm = await service.GetCreateFromModelAsync(partner);
            Assert.Empty(vm.AvailableCourses);
            Assert.Single((await service.GetCreateAdvancedAsync(partner)).Batches);
        }
    }
}
