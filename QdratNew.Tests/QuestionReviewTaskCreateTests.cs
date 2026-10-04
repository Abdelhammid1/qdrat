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

namespace QdratNew.Tests
{
    // QRT-S2.5 — إنشاء مهمة المراجعة: أهلية، عزل شركاء، صلاحية منهج، حجز، Audit، عدادات، إشعار
    // ملاحظة: مزوّد InMemory لا يفرض الفهرس الفريد المُصفّى؛ اختبار التزامن الحقيقي على SQL Server ضمن QRT-S8.3.
    public class QuestionReviewTaskCreateTests
    {
        private sealed class FixedTimeProvider : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private sealed class Harness
        {
            public required QuestionReviewTaskService Service { get; init; }
            public required TestDbContextFactory Factory { get; init; }
            public required Mock<IInstructorScopeService> Scope { get; init; }
            public required Mock<IAdvancedNotificationService> Notifications { get; init; }
        }

        private static readonly ReviewActor Admin = new("admin-1", "مدير النظام", UserRoleType.Admin);

        private static Harness Create(params int[] allowedCurriculumIds)
        {
            var factory = new TestDbContextFactory(Guid.NewGuid().ToString());
            var scope = new Mock<IInstructorScopeService>();
            scope.Setup(s => s.GetDirectCurriculumIdsAsync(It.IsAny<int>()))
                 .ReturnsAsync(allowedCurriculumIds.ToList());
            var notifications = new Mock<IAdvancedNotificationService>();

            var service = new QuestionReviewTaskService(
                factory,
                new FixedTimeProvider(new DateTimeOffset(2026, 10, 4, 10, 0, 0, TimeSpan.Zero)),
                scope.Object,
                notifications.Object,
                NullLogger<QuestionReviewTaskService>.Instance);

            return new Harness { Service = service, Factory = factory, Scope = scope, Notifications = notifications };
        }

        private static async Task<Instructor> SeedInstructorAsync(
            TestDbContextFactory factory, string? userId = "u-1", int? partnerId = null, bool active = true, string name = "أ. محمد")
        {
            using var db = factory.CreateDbContext();
            var instructor = new Instructor
            {
                FullName = name,
                NationalID = Guid.NewGuid().ToString("N")[..10],
                Email = $"{Guid.NewGuid():N}@test.local",
                Specialization = "عام",
                IsActive = active,
                UserId = userId,
                PartnerId = partnerId
            };
            db.Instructors.Add(instructor);
            await db.SaveChangesAsync();
            return instructor;
        }

        private static async Task<List<Question>> SeedQuestionsAsync(
            TestDbContextFactory factory, int count, int curriculumId = 1, int? partnerId = null,
            Action<Question, int>? customize = null)
        {
            using var db = factory.CreateDbContext();
            var list = new List<Question>();
            for (var i = 0; i < count; i++)
            {
                var q = new Question
                {
                    Title = $"سؤال {i}",
                    ReferenceNumber = $"Q-{Guid.NewGuid():N}"[..12],
                    CurriculumId = curriculumId,
                    LessonId = 1,
                    SectionId = 1,
                    PartnerId = partnerId,
                    CorrectAnswer = "أ",
                    IsComplete = true,
                    CreatedAt = new DateTime(2026, 1, 1).AddDays(i)
                };
                customize?.Invoke(q, i);
                list.Add(q);
            }
            db.Questions.AddRange(list);
            await db.SaveChangesAsync();
            return list;
        }

        private static CreateQuestionReviewTaskInput Input(int instructorId, IEnumerable<Guid> ids) => new()
        {
            Title = "مراجعة الجبر",
            InstructorId = instructorId,
            SelectedQuestionIds = ids.ToList()
        };

        [Fact]
        public async Task Create_Manual_InsertsTaskItemsAuditAndCounters_AndNotifies()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory);
            var questions = await SeedQuestionsAsync(h.Factory, 3);

            var result = await h.Service.CreateTaskAsync(Input(instructor.Id, questions.Select(q => q.Id)), Admin);

            Assert.True(result.Success, result.Message);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.Include(t => t.Items).SingleAsync();
            Assert.Equal("QRT-2026-0001", task.Code);
            Assert.Equal(QuestionReviewTaskStatus.Assigned, task.Status);
            Assert.Equal(instructor.Id, task.InstructorId);
            Assert.Equal(1, task.CurriculumId);
            Assert.Equal(3, task.TotalItems);
            Assert.Equal(3, task.PendingItems);
            Assert.All(task.Items, i =>
            {
                Assert.True(i.IsLockActive);
                Assert.Equal(QuestionReviewTaskItemStatus.Pending, i.Status);
                Assert.False(string.IsNullOrEmpty(i.ReferenceNumberSnapshot));
            });
            Assert.Equal(new[] { 1, 2, 3 }, task.Items.OrderBy(i => i.SortOrder).Select(i => i.SortOrder));

            var audits = await db.QuestionAuditLogs.Where(a => a.Action == "إسناد لمهمة مراجعة").ToListAsync();
            Assert.Equal(3, audits.Count);
            Assert.All(audits, a => Assert.Contains("QRT-2026-0001", a.ChangedFieldsSummary));

            h.Notifications.Verify(n => n.SendToUserAsync(
                "u-1", It.Is<string>(m => m.Contains("QRT-2026-0001")), NotificationCategory.Important,
                It.Is<string?>(u => u != null && u.EndsWith($"/{task.Id}"))), Times.Once);
        }

        [Fact]
        public async Task Create_SecondTask_GetsNextCode()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory);
            var first = await SeedQuestionsAsync(h.Factory, 1);
            var second = await SeedQuestionsAsync(h.Factory, 1);

            Assert.True((await h.Service.CreateTaskAsync(Input(instructor.Id, first.Select(q => q.Id)), Admin)).Success);
            Assert.True((await h.Service.CreateTaskAsync(Input(instructor.Id, second.Select(q => q.Id)), Admin)).Success);

            using var db = h.Factory.CreateDbContext();
            var codes = await db.QuestionReviewTasks.OrderBy(t => t.Id).Select(t => t.Code).ToListAsync();
            Assert.Equal(new[] { "QRT-2026-0001", "QRT-2026-0002" }, codes);
        }

        [Fact]
        public async Task Create_ExcludesIneligibleQuestions_AndReportsCount()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory);
            var questions = await SeedQuestionsAsync(h.Factory, 5, customize: (q, i) =>
            {
                if (i == 1) q.IsReviewed = true;
                if (i == 2) q.IsRejected = true;
                if (i == 3) q.IsComplete = false;
                if (i == 4) q.CorrectAnswer = null;
            });

            var result = await h.Service.CreateTaskAsync(Input(instructor.Id, questions.Select(q => q.Id)), Admin);

            Assert.True(result.Success, result.Message);
            Assert.Contains("استُبعد 4", result.Message);
            using var db = h.Factory.CreateDbContext();
            Assert.Equal(1, await db.QuestionReviewTaskItems.CountAsync());
        }

        [Fact]
        public async Task Create_AlreadyLockedQuestion_IsExcluded_AndAllIneligibleFails()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory);
            var questions = await SeedQuestionsAsync(h.Factory, 2);
            Assert.True((await h.Service.CreateTaskAsync(Input(instructor.Id, new[] { questions[0].Id }), Admin)).Success);

            // السؤال 0 محجوز: يُستبعد ويبقى 1
            var mixed = await h.Service.CreateTaskAsync(Input(instructor.Id, questions.Select(q => q.Id)), Admin);
            Assert.True(mixed.Success, mixed.Message);
            Assert.Contains("استُبعد 1", mixed.Message);

            // كلها محجوزة الآن: يفشل
            var none = await h.Service.CreateTaskAsync(Input(instructor.Id, questions.Select(q => q.Id)), Admin);
            Assert.False(none.Success);
        }

        [Fact]
        public async Task Create_InstructorWithoutUserId_Fails()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory, userId: null);
            var questions = await SeedQuestionsAsync(h.Factory, 1);

            var result = await h.Service.CreateTaskAsync(Input(instructor.Id, questions.Select(q => q.Id)), Admin);

            Assert.False(result.Success);
            Assert.Contains("حساب دخول", result.Message);
            using var db = h.Factory.CreateDbContext();
            Assert.Empty(db.QuestionReviewTasks);
        }

        [Fact]
        public async Task Create_InactiveInstructor_Fails()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory, active: false);
            var questions = await SeedQuestionsAsync(h.Factory, 1);

            var result = await h.Service.CreateTaskAsync(Input(instructor.Id, questions.Select(q => q.Id)), Admin);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Create_CurriculumNotOwnedByInstructor_FailsWithNames()
        {
            var h = Create(1); // المدرب يملك المنهج 1 فقط
            using (var db = h.Factory.CreateDbContext())
            {
                db.Curriculums.Add(new Curriculum { Id = 2, Title = "منهج اللفظي", Description = "-", CurriculumTypeName = "لفظي" });
                await db.SaveChangesAsync();
            }
            var instructor = await SeedInstructorAsync(h.Factory);
            var questions = await SeedQuestionsAsync(h.Factory, 2, curriculumId: 2);

            var result = await h.Service.CreateTaskAsync(Input(instructor.Id, questions.Select(q => q.Id)), Admin);

            Assert.False(result.Success);
            Assert.Contains("منهج اللفظي", result.Message);
            using var check = h.Factory.CreateDbContext();
            Assert.Empty(check.QuestionReviewTasks);
            Assert.Empty(check.QuestionReviewTaskItems);
        }

        [Fact]
        public async Task Create_PartnerIsolation_BlocksCrossPartnerAssignment()
        {
            var h = Create(1);
            var partnerInstructor = await SeedInstructorAsync(h.Factory, partnerId: 7);
            var generalInstructor = await SeedInstructorAsync(h.Factory, userId: "u-2", partnerId: null);
            var generalQuestions = await SeedQuestionsAsync(h.Factory, 1, partnerId: null);
            var partnerQuestions = await SeedQuestionsAsync(h.Factory, 1, partnerId: 7);

            // سؤال عام لا يُسند لمدرب شريك
            var r1 = await h.Service.CreateTaskAsync(Input(partnerInstructor.Id, generalQuestions.Select(q => q.Id)), Admin);
            Assert.False(r1.Success);

            // سؤال شريك لا يُسند لمدرب عام
            var r2 = await h.Service.CreateTaskAsync(Input(generalInstructor.Id, partnerQuestions.Select(q => q.Id)), Admin);
            Assert.False(r2.Success);

            // نفس الشريك: ينجح
            var r3 = await h.Service.CreateTaskAsync(Input(partnerInstructor.Id, partnerQuestions.Select(q => q.Id)), Admin);
            Assert.True(r3.Success, r3.Message);
        }

        [Fact]
        public async Task Create_DueDateInPast_Fails()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory);
            var questions = await SeedQuestionsAsync(h.Factory, 1);
            var input = Input(instructor.Id, questions.Select(q => q.Id));
            input.DueAtLocal = new DateTime(2026, 10, 3, 12, 0, 0);

            var result = await h.Service.CreateTaskAsync(input, Admin);

            Assert.False(result.Success);
            Assert.Contains("المستقبل", result.Message);
        }

        [Fact]
        public async Task Create_DueDate_IsStoredAsUtc_FromArabStandardTime()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory);
            var questions = await SeedQuestionsAsync(h.Factory, 1);
            var input = Input(instructor.Id, questions.Select(q => q.Id));
            input.DueAtLocal = new DateTime(2026, 10, 10, 15, 0, 0); // 15:00 بتوقيت السعودية = 12:00 UTC

            var result = await h.Service.CreateTaskAsync(input, Admin);

            Assert.True(result.Success, result.Message);
            using var db = h.Factory.CreateDbContext();
            var task = await db.QuestionReviewTasks.SingleAsync();
            Assert.Equal(new DateTime(2026, 10, 10, 12, 0, 0), task.DueAtUtc);
        }

        [Fact]
        public async Task Create_TooManyManualQuestions_Fails()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory);
            var ids = Enumerable.Range(0, 501).Select(_ => Guid.NewGuid());

            var result = await h.Service.CreateTaskAsync(Input(instructor.Id, ids), Admin);

            Assert.False(result.Success);
            Assert.Contains("500", result.Message);
        }

        [Fact]
        public async Task Create_TakeCountMode_PicksOldestFirst_AndSkipsLocked()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory);
            var questions = await SeedQuestionsAsync(h.Factory, 5); // CreatedAt يتصاعد مع الفهرس
            Assert.True((await h.Service.CreateTaskAsync(Input(instructor.Id, new[] { questions[0].Id }), Admin)).Success);

            var result = await h.Service.CreateTaskAsync(new CreateQuestionReviewTaskInput
            {
                Title = "أول 2 من المنهج",
                InstructorId = instructor.Id,
                CurriculumId = 1,
                TakeCount = 2
            }, Admin);

            Assert.True(result.Success, result.Message);
            using var db = h.Factory.CreateDbContext();
            var secondTask = await db.QuestionReviewTasks.Include(t => t.Items).OrderByDescending(t => t.Id).FirstAsync();
            var pickedIds = secondTask.Items.Select(i => i.QuestionId).ToHashSet();
            Assert.Equal(2, pickedIds.Count);
            Assert.Contains(questions[1].Id, pickedIds); // الأقدم غير المحجوز
            Assert.Contains(questions[2].Id, pickedIds);
            Assert.DoesNotContain(questions[0].Id, pickedIds);
        }

        [Fact]
        public async Task Create_NoSelectionAndNoTakeCount_Fails()
        {
            var h = Create(1);
            var instructor = await SeedInstructorAsync(h.Factory);

            var result = await h.Service.CreateTaskAsync(
                new CreateQuestionReviewTaskInput { Title = "بدون أسئلة", InstructorId = instructor.Id }, Admin);

            Assert.False(result.Success);
        }

        [Fact]
        public async Task Create_NotificationFailure_DoesNotFailOperation()
        {
            var h = Create(1);
            h.Notifications
                .Setup(n => n.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NotificationCategory>(), It.IsAny<string?>()))
                .ThrowsAsync(new InvalidOperationException("smtp down"));
            var instructor = await SeedInstructorAsync(h.Factory);
            var questions = await SeedQuestionsAsync(h.Factory, 1);

            var result = await h.Service.CreateTaskAsync(Input(instructor.Id, questions.Select(q => q.Id)), Admin);

            Assert.True(result.Success, result.Message);
            using var db = h.Factory.CreateDbContext();
            Assert.Single(db.QuestionReviewTasks);
        }

        [Fact]
        public async Task EligibleInstructors_ReturnsOnlyThoseCoveringAllCurriculaAndPartner_OrderedByLoad()
        {
            var h = Create(1);
            var busy = await SeedInstructorAsync(h.Factory, userId: "u-busy", name: "ب. مشغول");
            var free = await SeedInstructorAsync(h.Factory, userId: "u-free", name: "أ. متاح");
            var noCurriculum = await SeedInstructorAsync(h.Factory, userId: "u-x", name: "ج. بلا منهج");
            var noAccount = await SeedInstructorAsync(h.Factory, userId: null, name: "د. بلا حساب");
            var otherPartner = await SeedInstructorAsync(h.Factory, userId: "u-p", partnerId: 9, name: "هـ. شريك");

            using (var db = h.Factory.CreateDbContext())
            {
                var batch = new Batch { Id = 1, Name = "دفعة اختبار", IsActive = true, IsDeleted = false, IsArchived = false };
                db.Batches.Add(batch);
                foreach (var ins in new[] { busy, free, noAccount, otherPartner })
                    db.InstructorCurriculumBatches.Add(new InstructorCurriculumBatch { InstructorId = ins.Id, CurriculumId = 1, BatchId = 1, UserId = "x" });
                await db.SaveChangesAsync();
            }

            // عبء: المدرب المشغول لديه مهمة سابقة
            var seeded = await SeedQuestionsAsync(h.Factory, 2);
            Assert.True((await h.Service.CreateTaskAsync(Input(busy.Id, new[] { seeded[0].Id }), Admin)).Success);

            var target = await SeedQuestionsAsync(h.Factory, 2);
            var result = await h.Service.GetEligibleInstructorsAsync(
                new EligibleInstructorsInput { SelectedQuestionIds = target.Select(q => q.Id).ToList() });

            Assert.True(result.Success, result.Message);
            var data = result.Data!;
            var instructors = (IReadOnlyList<EligibleInstructorDto>)data.GetType().GetProperty("instructors")!.GetValue(data)!;
            Assert.Equal(new[] { free.Id, busy.Id }, instructors.Select(i => i.Id));
            Assert.Equal(0, instructors[0].ActiveLockedItems);
            Assert.Equal(1, instructors[1].ActiveLockedItems);
        }

        [Fact]
        public async Task EligibleInstructors_MixedPartners_Fails()
        {
            var h = Create(1);
            var a = await SeedQuestionsAsync(h.Factory, 1, partnerId: null);
            var b = await SeedQuestionsAsync(h.Factory, 1, partnerId: 7);

            var result = await h.Service.GetEligibleInstructorsAsync(
                new EligibleInstructorsInput { SelectedQuestionIds = a.Concat(b).Select(q => q.Id).ToList() });

            Assert.False(result.Success);
        }
    }
}
