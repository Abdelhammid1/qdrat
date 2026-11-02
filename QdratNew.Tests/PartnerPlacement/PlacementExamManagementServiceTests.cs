using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Exams.Generators;
using QdratNew.Services.Exams.Placement;
using QdratNew.Services.Interfaces;
using QdratNew.Services.Notifications;
using QdratNew.ViewModels.Exam;
using Xunit;

namespace QdratNew.Tests.PartnerPlacement
{
    // PPL-S2.4 — انحدار الأدمن وعزل النطاق في خدمة إدارة اختبارات تحديد المستوى (EF InMemory).
    // مسارات BulkInsert والـ Transaction مغطاة في PlacementExamManagementSqlServerTests.
    public class PlacementExamManagementServiceTests
    {
        private const int PartnerA = 1;
        private const int PartnerB = 2;
        private const int CourseA = 1;
        private const int CourseNoBank = 2;
        private const int CourseOutsideContract = 3;
        private const int BatchInstitute = 10;
        private const int BatchA = 11;
        private const int BatchB = 12;
        private const int BatchANoBank = 13;
        private const int StudentInstitute = 100;
        private const int StudentA = 101;
        private const int StudentB = 102;
        private const int StudentAArchived = 103;
        private const int ExamInstitute = 500;
        private const int ExamOfA = 501;
        private const int ExamOfB = 502;
        private const int SectionOfCourseA = 70;
        private const int SectionOtherCourse = 71;
        private const int LessonOfCourseA = 80;

        private static readonly Guid QuestionInExam = Guid.NewGuid();
        private static readonly Guid QuestionCandidate = Guid.NewGuid();
        private static readonly Guid QuestionOtherLesson = Guid.NewGuid();

        private static readonly PlacementScope Admin = PlacementScope.Admin("admin", "مدير");
        private static readonly PlacementScope ScopeA = PlacementScope.Partner(PartnerA, "user-a", "شريك A");
        private static readonly PlacementScope ScopeB = PlacementScope.Partner(PartnerB, "user-b", "شريك B");

        private sealed class Harness
        {
            public required ApplicationDbContext Db { get; init; }
            public required PlacementExamManagementService Service { get; init; }
            public required TestDbContextFactory Factory { get; init; }
        }

        private static Harness Seed(bool partnerAContractActive = true, bool partnerAModels = true)
        {
            var name = Guid.NewGuid().ToString();
            var factory = new TestDbContextFactory(name);
            var db = factory.CreateDbContext();

            db.Partners.AddRange(
                new Partner { Id = PartnerA, Name = "A", Code = "A" },
                new Partner { Id = PartnerB, Name = "B", Code = "B" });
            db.Branches.AddRange(
                new Branch { Id = 1, Name = "معهد", Location = "-", City = "-", State = "-", Country = "-" },
                new Branch { Id = 2, PartnerId = PartnerA, Name = "A", Location = "-", City = "-", State = "-", Country = "-" },
                new Branch { Id = 3, PartnerId = PartnerB, Name = "B", Location = "-", City = "-", State = "-", Country = "-" });
            db.Courses.AddRange(
                new Course { Id = CourseA, Name = "كورس A", Description = "-" },
                new Course { Id = CourseNoBank, Name = "كورس بلا بنك", Description = "-" },
                new Course { Id = CourseOutsideContract, Name = "كورس خارج العقد", Description = "-" });
            db.Batches.AddRange(
                new Batch { Id = BatchInstitute, Name = "دفعة المعهد", BranchId = 1, CourseId = CourseA, StartDate = DateTime.Today },
                new Batch { Id = BatchA, Name = "دفعة A", BranchId = 2, CourseId = CourseA, StartDate = DateTime.Today },
                new Batch { Id = BatchB, Name = "دفعة B", BranchId = 3, CourseId = CourseA, StartDate = DateTime.Today },
                new Batch { Id = BatchANoBank, Name = "دفعة A بلا بنك", BranchId = 2, CourseId = CourseNoBank, StartDate = DateTime.Today });
            db.Students.AddRange(
                NewStudent(StudentInstitute, 1),
                NewStudent(StudentA, 2),
                NewStudent(StudentB, 3),
                NewStudent(StudentAArchived, 2, active: false));
            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = StudentInstitute, BatchId = BatchInstitute },
                new StudentBatchEnrollment { StudentID = StudentA, BatchId = BatchA },
                new StudentBatchEnrollment { StudentID = StudentAArchived, BatchId = BatchA },
                new StudentBatchEnrollment { StudentID = StudentB, BatchId = BatchB });

            var today = DateTime.Today;
            db.PartnerSubscriptions.Add(new PartnerSubscription
            {
                Id = 1,
                PartnerId = PartnerA,
                StartDate = partnerAContractActive ? today.AddDays(-1) : today.AddDays(-30),
                EndDate = partnerAContractActive ? today.AddDays(30) : today.AddDays(-1),
                CanUsePlacementExams = true,
                CanUseProfessionalModels = partnerAModels,
                SubscriptionCourses = new List<PartnerSubscriptionCourse>
                {
                    new() { CourseId = CourseA, CanUsePlatformQuestionBank = true },
                    new() { CourseId = CourseNoBank, CanUsePlatformQuestionBank = false }
                }
            });

            db.Exams.AddRange(
                NewExam(ExamInstitute, null),
                NewExam(ExamOfA, PartnerA),
                NewExam(ExamOfB, PartnerB));
            db.ExamAssignmentsToBatches.AddRange(
                new ExamAssignmentToBatch { Id = 1, ExamId = ExamInstitute, BatchId = BatchInstitute, Title = "t" },
                // اختبار للأدمن على دفعة شريك يبقى ظاهرًا للأدمن (OwnerPartnerId == null)
                new ExamAssignmentToBatch { Id = 2, ExamId = ExamInstitute, BatchId = BatchA, Title = "t" },
                new ExamAssignmentToBatch { Id = 3, ExamId = ExamOfA, BatchId = BatchA, Title = "t" },
                new ExamAssignmentToBatch { Id = 4, ExamId = ExamOfB, BatchId = BatchB, Title = "t" });

            db.CourseCurriculums.Add(new CourseCurriculum { CourseId = CourseA, CurriculumId = 60 });
            db.Curriculums.AddRange(new Curriculum { Id = 60, Title = "منهج", Description = "-", CurriculumTypeName = "-" }, new Curriculum { Id = 61, Title = "منهج آخر", Description = "-", CurriculumTypeName = "-" });
            db.Sections.AddRange(
                new Section { Id = SectionOfCourseA, Title = "محور", CurriculumId = 60 },
                new Section { Id = SectionOtherCourse, Title = "محور آخر", CurriculumId = 61 });
            db.Lessons.AddRange(
                new Lesson { Id = LessonOfCourseA, Title = "مؤشر", Content = "-", SectionId = SectionOfCourseA },
                new Lesson { Id = 81, Title = "مؤشر آخر", Content = "-", SectionId = SectionOtherCourse });
            db.Questions.AddRange(
                NewQuestion(QuestionInExam, LessonOfCourseA, SectionOfCourseA),
                NewQuestion(QuestionCandidate, LessonOfCourseA, SectionOfCourseA),
                NewQuestion(QuestionOtherLesson, 81, SectionOtherCourse));
            db.ExamQuestions.AddRange(
                new ExamQuestion { ExamId = ExamOfA, QuestionId = QuestionInExam, Order = 1 },
                new ExamQuestion { ExamId = ExamInstitute, QuestionId = QuestionInExam, Order = 1 });

            db.SaveChanges();
            db.ChangeTracker.Clear();

            var tz = new Mock<ITimeZoneService>();
            tz.Setup(x => x.GetNowUtc()).Returns(DateTime.UtcNow);
            tz.Setup(x => x.GetNowSaudi()).Returns(DateTime.UtcNow.AddHours(3));

            var generator = new PlacementExamGeneratorService(factory, new Mock<INotificationCenterService>().Object, tz.Object);

            var service = new PlacementExamManagementService(
                db,
                generator,
                new PlacementExamScopeGuard(),
                tz.Object,
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<PlacementExamManagementService>.Instance);

            return new Harness { Db = db, Service = service, Factory = factory };
        }

        private static Student NewStudent(int id, int branchId, bool active = true) => new()
        {
            StudentID = id,
            BranchId = branchId,
            FullName = $"طالب {id}",
            NationalID = $"N{id}",
            Gender = "ذكر",
            School = "-",
            Level = "-",
            UserId = $"u{id}",
            IsActiveForLearning = active
        };

        private static Exam NewExam(int id, int? ownerPartnerId) => new()
        {
            Id = id,
            Title = $"اختبار {id}",
            Type = ExamType.LevelAssessment,
            CourseId = CourseA,
            OwnerPartnerId = ownerPartnerId
        };

        private static Question NewQuestion(Guid id, int lessonId, int sectionId) => new()
        {
            Id = id,
            Title = "سؤال",
            ReferenceNumber = id.ToString(),
            CorrectAnswer = "أ",
            LessonId = lessonId,
            SectionId = sectionId,
            Difficulty = DifficultyLevel.Medium,
            IsReviewed = true,
            IsComplete = true
        };

        private static PlacementExamCreateAdvancedViewModel NewCreateModel(int courseId, int? batchId = null, int? studentId = null, int sectionId = SectionOfCourseA) => new()
        {
            CourseId = courseId,
            BatchId = batchId,
            StudentId = studentId,
            TotalQuestions = 2,
            DurationMinutes = 30,
            SectionSelections = new List<PlacementExamSectionSelectionVm>
            {
                new() { SectionId = sectionId, QuestionCount = 2 }
            }
        };

        // =========================================
        // انحدار الأدمن
        // =========================================
        [Fact]
        public async Task Admin_CreateAdvanced_ForBatch_CreatesDraftOwnedByInstitute()
        {
            var h = Seed();

            var result = await h.Service.CreateAdvancedAsync(Admin, NewCreateModel(CourseA, batchId: BatchInstitute));

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal("تم إنشاء الاختبار كمسودة. يرجى مراجعته قبل الإرسال.", result.Message);

            var exam = await h.Db.Exams.AsNoTracking().FirstAsync(e => e.Id == result.ExamId);
            Assert.Null(exam.OwnerPartnerId);
            Assert.Equal(ExamType.LevelAssessment, exam.Type);

            var link = await h.Db.ExamAssignmentsToBatches.AsNoTracking().SingleAsync(x => x.ExamId == result.ExamId);
            Assert.Equal(BatchInstitute, link.BatchId);
            Assert.False(link.IsSentToStudents);
        }

        [Fact]
        public async Task Admin_CreateAdvanced_ValidationMessagesUnchanged()
        {
            var h = Seed();

            var noSections = NewCreateModel(CourseA, batchId: BatchInstitute);
            noSections.SectionSelections = new();
            Assert.Equal("⚠️ لم يتم استقبال أي محور.", (await h.Service.CreateAdvancedAsync(Admin, noSections)).Message);

            var wrongSum = NewCreateModel(CourseA, batchId: BatchInstitute);
            wrongSum.TotalQuestions = 5;
            Assert.Equal("⚠️ مجموع الأسئلة (2) لا يساوي المطلوب (5).", (await h.Service.CreateAdvancedAsync(Admin, wrongSum)).Message);

            Assert.Equal("⚠️ يجب اختيار طالب أو دفعة.", (await h.Service.CreateAdvancedAsync(Admin, NewCreateModel(CourseA))).Message);
        }

        [Fact]
        public async Task Admin_Index_HidesPartnerExams()
        {
            var h = Seed();

            var index = await h.Service.GetIndexAsync(Admin);

            // اختبار المعهد فقط، بما فيه المرسل لدفعة شريك A
            Assert.Equal(new[] { BatchInstitute, BatchA }, index.ActiveBatches.Select(b => b.BatchId).OrderBy(x => x));
            Assert.DoesNotContain(index.ActiveBatches, b => b.BatchId == BatchB);
            Assert.All(index.ActiveBatches, b => Assert.Equal(1, b.ExamsCount));
        }

        [Fact]
        public async Task Admin_BatchExams_HidesPartnerExamOnSameBatch()
        {
            var h = Seed();

            var exams = await h.Service.GetBatchExamsAsync(Admin, BatchA);

            Assert.Equal(new[] { ExamInstitute }, exams!.Select(e => e.ExamId));
        }

        [Fact]
        public async Task Admin_BatchExams_UnknownBatch_ReturnsEmptyLikeBefore()
        {
            var h = Seed();

            var exams = await h.Service.GetBatchExamsAsync(Admin, 99999);

            Assert.NotNull(exams);
            Assert.Empty(exams!);
            Assert.Null(await h.Service.GetBatchNameAsync(Admin, 99999));
        }

        [Fact]
        public async Task Admin_CannotTouchPartnerExam()
        {
            var h = Seed();

            Assert.Null(await h.Service.GetReviewAsync(Admin, ExamOfA));
            Assert.Null(await h.Service.GetPlacementDetailsAsync(Admin, ExamOfA));
            Assert.Equal("⚠️ لم يتم العثور على الاختبار.", (await h.Service.HideAsync(Admin, ExamOfA)).Message);
            Assert.Equal("⚠️ لم يتم العثور على الاختبار.", (await h.Service.ToggleDontKnowOptionAsync(Admin, ExamOfA, true)).Message);
            Assert.Equal("⚠️ لم يتم العثور على الاختبار.", (await h.Service.DeletePermanentlyAsync(Admin, ExamOfA, BatchA)).Message);
            Assert.Equal("❌ لا يمكن تحديد الدفعة المرتبطة بالاختبار.", (await h.Service.ResendToStudentAsync(Admin, ExamOfA, StudentA)).Message);

            var exam = await h.Db.Exams.AsNoTracking().FirstAsync(e => e.Id == ExamOfA);
            Assert.True(exam.IsActive);
            Assert.False(exam.AllowDontKnowOption);
        }

        [Fact]
        public async Task Admin_HideRestoreToggle_KeepMessagesAndLog()
        {
            var h = Seed();

            var hidden = await h.Service.HideAsync(Admin, ExamInstitute);
            Assert.Equal("🚫 تم إخفاء الاختبار ولن يظهر للطلاب بعد الآن.", hidden.Message);

            var again = await h.Service.HideAsync(Admin, ExamInstitute);
            Assert.Equal(PlacementOperationOutcome.Info, again.Outcome);
            Assert.Equal("⚠️ هذا الاختبار مخفي بالفعل.", again.Message);

            Assert.Equal("✅ تم استرجاع الاختبار بنجاح.", (await h.Service.RestoreAsync(Admin, ExamInstitute)).Message);

            var toggled = await h.Service.ToggleDontKnowOptionAsync(Admin, ExamInstitute, true);
            Assert.Equal("✅ تم تفعيل خيار \"لا أعرف الإجابة\" لهذا الاختبار.", toggled.Message);

            var log = await h.Db.SystemLogs.AsNoTracking().FirstAsync(l => l.Action == "Hide");
            Assert.Equal("مدير", log.UserName);
            Assert.Equal(ExamInstitute, log.EntityId);
        }

        [Fact]
        public async Task Admin_Resend_ReturnsBatchForRedirect()
        {
            var h = Seed();

            var result = await h.Service.ResendToStudentAsync(Admin, ExamInstitute, StudentInstitute);

            Assert.True(result.Succeeded, result.Message);
            Assert.Contains(result.BatchId, new int?[] { BatchInstitute, BatchA });
            Assert.Equal(1, await h.Db.ExamStudentStatuses.CountAsync(s => s.ExamId == ExamInstitute && s.StudentId == StudentInstitute));
        }

        // =========================================
        // نطاق الشريك
        // =========================================
        [Fact]
        public void PartnerScope_RequiresValidPartnerId()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PlacementScope.Partner(0, "u"));
        }

        [Fact]
        public async Task Partner_SeesOnlyOwnExamsAndBatches()
        {
            var h = Seed();

            var index = await h.Service.GetIndexAsync(ScopeA);
            Assert.Equal(new[] { BatchA }, index.ActiveBatches.Select(b => b.BatchId));
            Assert.Equal(1, index.ActiveBatches[0].ExamsCount);

            var batchExams = await h.Service.GetBatchExamsAsync(ScopeA, BatchA);
            Assert.Equal(new[] { ExamOfA }, batchExams!.Select(e => e.ExamId));
            Assert.Equal(1, batchExams![0].StudentCount); // المؤرشف لا يُحسب

            Assert.Null(await h.Service.GetBatchExamsAsync(ScopeA, BatchB));
            Assert.Null(await h.Service.GetBatchNameAsync(ScopeA, BatchB));
            Assert.Null(await h.Service.GetReviewAsync(ScopeA, ExamOfB));
            Assert.Null(await h.Service.GetReviewAsync(ScopeA, ExamInstitute));
        }

        [Fact]
        public async Task Partner_Lookups_RestrictedToContractAndOwnData()
        {
            var h = Seed();

            var vm = await h.Service.GetCreateAdvancedAsync(ScopeA);

            Assert.Equal(new[] { CourseA.ToString() }, vm.Courses.Select(c => c.Value));
            Assert.Equal(new[] { BatchA.ToString(), BatchANoBank.ToString() }, vm.Batches.Select(b => b.Value).OrderBy(x => x));
            Assert.Equal(new[] { StudentA.ToString() }, vm.Students.Select(s => s.Value));

            Assert.Empty(await h.Service.GetSectionsByCourseAsync(ScopeA, CourseOutsideContract));
            Assert.Empty(await h.Service.GetSectionsByCourseAsync(ScopeA, CourseNoBank));
        }

        [Fact]
        public async Task Partner_CreateAdvanced_SetsOwnerAndTargetsOwnBatch()
        {
            var h = Seed();

            var result = await h.Service.CreateAdvancedAsync(ScopeA, NewCreateModel(CourseA, batchId: BatchA));

            Assert.True(result.Succeeded, result.Message);
            var exam = await h.Db.Exams.AsNoTracking().FirstAsync(e => e.Id == result.ExamId);
            Assert.Equal(PartnerA, exam.OwnerPartnerId);
        }

        [Theory]
        [InlineData(CourseA, BatchB, null, SectionOfCourseA)]            // دفعة شريك آخر
        [InlineData(CourseA, BatchInstitute, null, SectionOfCourseA)]    // دفعة المعهد
        [InlineData(CourseA, null, StudentB, SectionOfCourseA)]          // طالب شريك آخر
        [InlineData(CourseA, null, StudentAArchived, SectionOfCourseA)]  // طالب مؤرشف
        [InlineData(CourseNoBank, BatchANoBank, null, SectionOfCourseA)] // كورس بلا بنك المنصة
        [InlineData(CourseOutsideContract, BatchA, null, SectionOfCourseA)] // كورس خارج العقد
        [InlineData(CourseA, BatchA, null, SectionOtherCourse)]          // محور خارج الكورس
        public async Task Partner_CreateAdvanced_RejectsForeignIds(int courseId, int? batchId, int? studentId, int sectionId)
        {
            var h = Seed();
            var examsBefore = await h.Db.Exams.CountAsync();

            var result = await h.Service.CreateAdvancedAsync(ScopeA, NewCreateModel(courseId, batchId, studentId, sectionId));

            Assert.False(result.Succeeded);
            Assert.Equal(examsBefore, await h.Db.Exams.CountAsync());
        }

        [Fact]
        public async Task Partner_ExpiredContract_CannotCreateOrModify_ButStillReads()
        {
            var h = Seed(partnerAContractActive: false);

            Assert.False((await h.Service.CreateAdvancedAsync(ScopeA, NewCreateModel(CourseA, batchId: BatchA))).Succeeded);
            Assert.False((await h.Service.ToggleDontKnowOptionAsync(ScopeA, ExamOfA, true)).Succeeded);
            Assert.False((await h.Service.SendToNewStudentsAsync(ScopeA, ExamOfA, BatchA)).Succeeded);
            Assert.False((await h.Service.ResendToStudentAsync(ScopeA, ExamOfA, StudentA)).Succeeded);

            Assert.NotNull(await h.Service.GetBatchExamsAsync(ScopeA, BatchA));
        }

        [Fact]
        public async Task Partner_CannotTouchOtherPartnerExam()
        {
            var h = Seed();

            Assert.False((await h.Service.HideAsync(ScopeA, ExamOfB)).Succeeded);
            Assert.False((await h.Service.DeletePermanentlyAsync(ScopeA, ExamOfB, BatchB)).Succeeded);
            Assert.False((await h.Service.ToggleDontKnowOptionAsync(ScopeA, ExamOfB, true)).Succeeded);
            Assert.False((await h.Service.SendToNewStudentsAsync(ScopeA, ExamOfB, BatchB)).Succeeded);
            Assert.False((await h.Service.ResendToStudentAsync(ScopeA, ExamOfB, StudentB)).Succeeded);

            var exam = await h.Db.Exams.AsNoTracking().FirstAsync(e => e.Id == ExamOfB);
            Assert.True(exam.IsActive);
            Assert.False(exam.AllowDontKnowOption);
        }

        [Theory]
        [InlineData(StudentB)]         // طالب شريك آخر
        [InlineData(StudentAArchived)] // طالب مؤرشف
        [InlineData(StudentInstitute)] // طالب المعهد
        public async Task Partner_Resend_RejectsStudentOutsideScope(int studentId)
        {
            var h = Seed();

            var result = await h.Service.ResendToStudentAsync(ScopeA, ExamOfA, studentId);

            Assert.Equal(PlacementOperationOutcome.NotFound, result.Outcome);
            Assert.False(await h.Db.ExamAssignments.AnyAsync(a => a.ExamId == ExamOfA && a.StudentId == studentId));
        }

        [Fact]
        public async Task Partner_ReplaceQuestion_OnlyWithSameLessonCandidate()
        {
            var h = Seed();

            var foreign = await h.Service.ReplaceQuestionAsync(ScopeA, ExamOfA, QuestionInExam, QuestionOtherLesson);
            Assert.Equal(PlacementOperationOutcome.NotFound, foreign.Outcome);

            var valid = await h.Service.ReplaceQuestionAsync(ScopeA, ExamOfA, QuestionInExam, QuestionCandidate);
            Assert.True(valid.Succeeded, valid.Message);
            Assert.True(await h.Db.ExamQuestions.AnyAsync(eq => eq.ExamId == ExamOfA && eq.QuestionId == QuestionCandidate));
        }

        [Fact]
        public async Task Partner_AddQuestionFromBank_RejectsSectionOutsideExamCourse()
        {
            var h = Seed();

            var foreign = await h.Service.AddQuestionFromBankAsync(ScopeA, ExamOfA, SectionOtherCourse, QuestionOtherLesson);
            Assert.Equal(PlacementOperationOutcome.NotFound, foreign.Outcome);

            var valid = await h.Service.AddQuestionFromBankAsync(ScopeA, ExamOfA, SectionOfCourseA, QuestionCandidate);
            Assert.True(valid.Succeeded, valid.Message);
        }

        [Fact]
        public async Task Partner_ModelLookups_FollowContractAndAvailability()
        {
            var h = Seed();
            h.Db.ProfessionalModels.AddRange(
                new ProfessionalModel { Id = 900, Title = "عام", Description = "-", CreatedBy = "-", IsGlobal = true },
                new ProfessionalModel { Id = 901, Title = "مخصص لغيره", Description = "-", CreatedBy = "-" },
                new ProfessionalModel { Id = 902, Title = "مؤرشف", Description = "-", CreatedBy = "-", IsGlobal = true, IsArchived = true });
            await h.Db.SaveChangesAsync();

            var vm = await h.Service.GetCreateFromModelAsync(ScopeA);
            Assert.Equal(new[] { "900" }, vm.AvailableModels.Select(m => m.Value));

            var noContract = await h.Service.GetCreateFromModelAsync(ScopeB);
            Assert.Empty(noContract.AvailableModels);

            var admin = await h.Service.GetCreateFromModelAsync(Admin);
            Assert.Equal(new[] { "900", "901" }, admin.AvailableModels.Select(m => m.Value).OrderBy(x => x));
        }

        [Fact]
        public async Task Partner_NoModelsFeature_HidesAllModels()
        {
            var h = Seed(partnerAModels: false);
            h.Db.ProfessionalModels.Add(new ProfessionalModel { Id = 900, Title = "عام", Description = "-", CreatedBy = "-", IsGlobal = true });
            await h.Db.SaveChangesAsync();

            Assert.Empty((await h.Service.GetCreateFromModelAsync(ScopeA)).AvailableModels);
        }

        // =========================================
        // الحارس
        // =========================================
        [Fact]
        public async Task Guard_AssignmentAccess_RespectsOwnerAndStudent()
        {
            var h = Seed();
            h.Db.ExamAssignments.AddRange(
                new ExamAssignment { Id = 1000, ExamId = ExamOfA, StudentId = StudentA },
                new ExamAssignment { Id = 1001, ExamId = ExamOfA, StudentId = StudentAArchived },
                new ExamAssignment { Id = 1002, ExamId = ExamOfB, StudentId = StudentB },
                new ExamAssignment { Id = 1003, ExamId = ExamInstitute, StudentId = StudentInstitute });
            await h.Db.SaveChangesAsync();
            var guard = new PlacementExamScopeGuard();

            Assert.True(await guard.CanAccessAssignmentAsync(h.Db, ScopeA, 1000));
            Assert.True(await guard.CanAccessAssignmentAsync(h.Db, ScopeA, 1001)); // نتائج المؤرشف تبقى مقروءة
            Assert.False(await guard.CanAccessAssignmentAsync(h.Db, ScopeA, 1002));
            Assert.False(await guard.CanAccessAssignmentAsync(h.Db, ScopeA, 1003));
            Assert.True(await guard.CanAccessAssignmentAsync(h.Db, Admin, 1003));
            Assert.False(await guard.CanAccessAssignmentAsync(h.Db, Admin, 1000));
        }
    }
}
