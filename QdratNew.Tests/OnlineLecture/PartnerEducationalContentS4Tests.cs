using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using QdratNew.Areas.Partner.Controllers;
using QdratNew.Attributes;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Implementations;
using QdratNew.Security.Permissions;
using QdratNew.Services.OnlineLecture;
using QdratNew.ViewModels.OnlineLecture;
using Xunit;

namespace QdratNew.Tests.OnlineLecture
{
    /// <summary>بيانات OLC-S4 المشتركة (InMemory): منصة + شريكان A (البند مفعّل) وB (عقد نشط بلا البند).</summary>
    internal static class OlcS4Data
    {
        public const int PlatformBranch = 1, BranchA = 2, BranchB = 3;
        public const int PartnerA = 10, PartnerB = 20, PartnerC = 30;
        public const int PlatformBatch = 100, BatchA = 110, BatchB = 120, BatchAOffline = 111;
        public const int LecturePlatform = 1000, LectureA1 = 1100, LectureA2 = 1101, LectureB = 1200;

        public static ApplicationDbContext Create()
        {
            var db = TestDbContextFactory.CreateFreshContext(out _);
            var today = DateTime.Today;

            db.Courses.Add(new Course { Id = 1, Name = "قدرات", Description = "-" });
            db.Sections.Add(new Section { Id = 1, Title = "محور الحساب", CurriculumId = 1 });
            db.Partners.AddRange(
                new Partner { Id = PartnerA, Name = "مدرسة أ", Code = "A", PartnershipStart = today.AddYears(-1), PartnershipEnd = today.AddYears(1) },
                new Partner { Id = PartnerB, Name = "مدرسة ب", Code = "B", PartnershipStart = today.AddYears(-1), PartnershipEnd = today.AddYears(1) },
                new Partner { Id = PartnerC, Name = "مدرسة ج", Code = "C", PartnershipStart = today.AddYears(-2), PartnershipEnd = today.AddYears(-1) });
            db.Branches.AddRange(
                NewBranch(PlatformBranch, "الفرع الرئيسي", null),
                NewBranch(BranchA, "فرع أ", PartnerA),
                NewBranch(BranchB, "فرع ب", PartnerB));
            db.PartnerSubscriptions.AddRange(
                Contract(PartnerA, today.AddMonths(-1), today.AddMonths(6), clause: true),
                Contract(PartnerB, today.AddMonths(-1), today.AddMonths(6), clause: false));
            db.Batches.AddRange(
                new Batch { Id = PlatformBatch, Name = "دفعة المنصة", BranchId = PlatformBranch, CourseId = 1, IsOnlineLearning = true },
                new Batch { Id = BatchA, Name = "دفعة أ أونلاين", BranchId = BranchA, CourseId = 1, IsOnlineLearning = true },
                new Batch { Id = BatchAOffline, Name = "دفعة أ حضورية", BranchId = BranchA, CourseId = 1 },
                new Batch { Id = BatchB, Name = "دفعة ب", BranchId = BranchB, CourseId = 1 });
            db.Lecture.AddRange(
                NewLecture(LecturePlatform, PlatformBatch, new DateTime(2026, 11, 1)),
                NewLecture(LectureA1, BatchA, new DateTime(2026, 11, 1)),
                NewLecture(LectureA2, BatchA, new DateTime(2026, 11, 8)),
                NewLecture(LectureB, BatchB, new DateTime(2026, 11, 1)));
            db.SaveChanges();
            db.ChangeTracker.Clear();
            return db;
        }

        public static Branch NewBranch(int id, string name, int? partnerId)
            => new() { Id = id, Name = name, Location = "-", City = "-", State = "-", Country = "-", PartnerId = partnerId, IsPartner = partnerId.HasValue };

        public static PartnerSubscription Contract(int partnerId, DateTime start, DateTime end, bool clause, DateTime? accessUntil = null)
            => new() { PartnerId = partnerId, StartDate = start, EndDate = end, CanAccessEducationalContent = clause, AccessUntilDate = accessUntil };

        private static Lecture NewLecture(int id, int batchId, DateTime date)
            => new() { Id = id, Title = $"محاضرة {id}", Location = "قاعة", Date = date, InstructorId = 1, SectionId = 1, CourseId = 1, BatchId = batchId };
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S4.1 / S4.4 — مصفوفة البند: مفعّل / ملغى / عقد منتهٍ ضمن AccessUntil / بعده
    // ═════════════════════════════════════════════════════════════════════
    public class PartnerEducationalContentPolicyTests
    {
        private const int P = 50;

        private static async Task<(PartnerEducationalContentPolicy Policy, PartnerSubscriptionService Subs)> WithContractsAsync(params PartnerSubscription[] contracts)
        {
            var db = TestDbContextFactory.CreateFreshContext(out _);
            db.PartnerSubscriptions.AddRange(contracts);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return (new PartnerEducationalContentPolicy(db), new PartnerSubscriptionService(db));
        }

        public static IEnumerable<object?[]> Matrix()
        {
            var t = DateTime.Today;
            // الحالة، بداية، نهاية، البند، AccessUntil ⇒ حالة R16، وصول الطلاب R17، قراءة الشريك R19
            yield return new object?[] { "بند مفعّل وعقد نشط", t.AddMonths(-1), t.AddMonths(1), true, null, PartnerEducationalContentState.Enabled, true, true };
            yield return new object?[] { "بند ملغى وعقد نشط", t.AddMonths(-1), t.AddMonths(1), false, null, PartnerEducationalContentState.NotInContract, false, false };
            yield return new object?[] { "منتهٍ ضمن AccessUntil", t.AddMonths(-6), t.AddDays(-1), true, t.AddDays(10), PartnerEducationalContentState.NoActiveContract, true, true };
            yield return new object?[] { "منتهٍ بعد AccessUntil", t.AddMonths(-6), t.AddDays(-10), true, t.AddDays(-1), PartnerEducationalContentState.NoActiveContract, false, false };
            yield return new object?[] { "منتهٍ بلا AccessUntil", t.AddMonths(-6), t.AddDays(-1), true, null, PartnerEducationalContentState.NoActiveContract, false, false };
            yield return new object?[] { "منتهٍ ضمن AccessUntil لكن البند ملغى", t.AddMonths(-6), t.AddDays(-1), false, t.AddDays(10), PartnerEducationalContentState.NoActiveContract, false, false };
            yield return new object?[] { "عقد لم يبدأ بعد", t.AddDays(5), t.AddMonths(6), true, null, PartnerEducationalContentState.NoActiveContract, false, false };
            yield return new object?[] { "ينتهي اليوم", t.AddMonths(-1), t, true, null, PartnerEducationalContentState.Enabled, true, true };
        }

        [Theory]
        [MemberData(nameof(Matrix))]
        public async Task Matrix_R16_R17_R19(string _, DateTime start, DateTime end, bool clause, DateTime? accessUntil,
            PartnerEducationalContentState expectedState, bool studentsAccess, bool partnerRead)
        {
            var (policy, subs) = await WithContractsAsync(OlcS4Data.Contract(P, start, end, clause, accessUntil));

            Assert.Equal(expectedState, await policy.GetStateAsync(P));
            Assert.Equal(studentsAccess, await policy.CanStudentsAccessAsync(P));
            Assert.Equal(partnerRead, await policy.CanPartnerReadAsync(P));

            // النسخة async في PartnerSubscriptionService بنفس القواعد
            Assert.Equal(expectedState == PartnerEducationalContentState.Enabled, await subs.CanAccessEducationalContentAsync(P, forStudents: false));
            Assert.Equal(studentsAccess, await subs.CanAccessEducationalContentAsync(P, forStudents: true));

            // R19 = نفس قرار PartnerFeatureFilter (البند في العقد النشط، أو القراءة بعد الانتهاء)
            var filterDecision = subs.CanAccessEducationalContent(P)
                || (subs.GetActiveSubscription(P) == null && subs.CanReadFeatureAfterExpiry(P, PartnerFeatureType.EducationalContent));
            Assert.Equal(filterDecision, partnerRead);
        }

        [Fact]
        public async Task NoContractAtAll_IsNoActiveContract_AndNoAccess()
        {
            var (policy, subs) = await WithContractsAsync();
            Assert.Equal(PartnerEducationalContentState.NoActiveContract, await policy.GetStateAsync(P));
            Assert.False(await policy.CanStudentsAccessAsync(P));
            Assert.False(await policy.CanPartnerReadAsync(P));
            Assert.False(await subs.CanAccessEducationalContentAsync(P, true));
        }

        [Fact]
        public async Task TwoActiveContracts_AnyWithClause_IsEnabled()
        {
            var t = DateTime.Today;
            var (policy, _) = await WithContractsAsync(
                OlcS4Data.Contract(P, t.AddMonths(-2), t.AddMonths(2), clause: false),
                OlcS4Data.Contract(P, t.AddMonths(-1), t.AddMonths(1), clause: true));

            Assert.Equal(PartnerEducationalContentState.Enabled, await policy.GetStateAsync(P));
            var states = await policy.GetStatesAsync();
            Assert.Equal(PartnerEducationalContentState.Enabled, states[P]);
        }

        [Fact]
        public async Task AccessUntilBeforeEndDate_DoesNotCutActiveContract()
        {
            var t = DateTime.Today;
            var (policy, _) = await WithContractsAsync(OlcS4Data.Contract(P, t.AddMonths(-1), t.AddMonths(1), true, accessUntil: t.AddDays(-3)));
            Assert.True(await policy.CanStudentsAccessAsync(P));
        }

        [Fact]
        public async Task GetStates_OmitsPartnersWithoutActiveContract()
        {
            var t = DateTime.Today;
            var (policy, _) = await WithContractsAsync(
                OlcS4Data.Contract(P, t.AddMonths(-1), t.AddMonths(1), clause: false),
                OlcS4Data.Contract(P + 1, t.AddYears(-2), t.AddYears(-1), clause: true));

            var states = await policy.GetStatesAsync();
            Assert.Equal(PartnerEducationalContentState.NotInContract, states[P]);
            Assert.False(states.ContainsKey(P + 1));
        }

        [Fact]
        public async Task WhereStudentAccessible_PlatformAlways_PartnerOnlyWithClauseWindow()
        {
            using var db = OlcS4Data.Create();
            var ids = await db.Batches.AsNoTracking()
                .WhereStudentAccessible(db.PartnerSubscriptions, DateTime.Today)
                .Select(b => b.Id)
                .ToListAsync();

            Assert.Contains(OlcS4Data.PlatformBatch, ids);
            Assert.Contains(OlcS4Data.BatchA, ids);
            Assert.DoesNotContain(OlcS4Data.BatchB, ids);
        }

        [Fact]
        public async Task CheckBatch_ReportsPartnerAndState()
        {
            using var db = OlcS4Data.Create();
            var policy = new PartnerEducationalContentPolicy(db);

            var platform = await policy.CheckBatchAsync(OlcS4Data.PlatformBatch);
            Assert.False(platform!.IsPartnerBatch);
            Assert.True(platform.CanActivate);

            var a = await policy.CheckBatchAsync(OlcS4Data.BatchA);
            Assert.Equal(OlcS4Data.PartnerA, a!.PartnerId);
            Assert.True(a.CanActivate);

            var b = await policy.CheckBatchAsync(OlcS4Data.BatchB);
            Assert.False(b!.CanActivate);
            Assert.Contains("لا يشمل بند «المحتوى التعليمي»", policy.DescribeBlock(b));

            Assert.Null(await policy.CheckBatchAsync(999));
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S4.2 — الأدمن: R16 عند التفعيل + الشريك لكل محاضرة/دفعة + «الدفعات الأونلاين»
    // ═════════════════════════════════════════════════════════════════════
    public class LectureContentAdminPartnerTests
    {
        private static (LectureContentAdminService Service, ApplicationDbContext Db) Create()
        {
            var db = OlcS4Data.Create();
            return (new LectureContentAdminService(db, NullLogger<LectureContentAdminService>.Instance, new PartnerEducationalContentPolicy(db)), db);
        }

        [Fact]
        public async Task Enable_PartnerBatchWithoutClause_IsRejected_R16()
        {
            var (svc, db) = Create();
            var r = await svc.SetBatchOnlineLearningAsync(OlcS4Data.BatchB, true);

            Assert.False(r.Success);
            Assert.Contains("مدرسة ب", r.Message);
            Assert.False(await db.Batches.Where(b => b.Id == OlcS4Data.BatchB).Select(b => b.IsOnlineLearning).SingleAsync());
        }

        [Fact]
        public async Task Enable_PartnerBatchWithClause_Succeeds()
        {
            var (svc, db) = Create();
            Assert.True((await svc.SetBatchOnlineLearningAsync(OlcS4Data.BatchAOffline, true)).Success);
            Assert.True(await db.Batches.Where(b => b.Id == OlcS4Data.BatchAOffline).Select(b => b.IsOnlineLearning).SingleAsync());
        }

        [Fact]
        public async Task Enable_PartnerBatchWithExpiredContract_IsRejected_EvenWithinAccessUntil()
        {
            var (svc, db) = Create();
            var contract = await db.PartnerSubscriptions.SingleAsync(s => s.PartnerId == OlcS4Data.PartnerA);
            contract.StartDate = DateTime.Today.AddMonths(-6);
            contract.EndDate = DateTime.Today.AddDays(-1);
            contract.AccessUntilDate = DateTime.Today.AddDays(30);
            await db.SaveChangesAsync();

            var r = await svc.SetBatchOnlineLearningAsync(OlcS4Data.BatchAOffline, true);
            Assert.False(r.Success);
            Assert.Contains("عقد نشط", r.Message);
        }

        [Fact]
        public async Task Disable_PartnerBatch_IsAllowed_WhateverTheClause()
        {
            var (svc, db) = Create();
            (await db.Batches.SingleAsync(b => b.Id == OlcS4Data.BatchB)).IsOnlineLearning = true; // InMemory: لا ExecuteUpdate
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.True((await svc.SetBatchOnlineLearningAsync(OlcS4Data.BatchB, false)).Success);
        }

        [Fact]
        public async Task Summary_CarriesPartnerState_AndBlockReason()
        {
            var (svc, _) = Create();
            var a = await svc.GetBatchOnlineSummaryAsync(OlcS4Data.BatchA);
            Assert.True(a!.IsPartnerBatch);
            Assert.Equal(PartnerEducationalContentState.Enabled, a.PartnerContractState);
            Assert.Null(a.PartnerBlockReason);

            var b = await svc.GetBatchOnlineSummaryAsync(OlcS4Data.BatchB);
            Assert.Equal(PartnerEducationalContentState.NotInContract, b!.PartnerContractState);
            Assert.NotNull(b.PartnerBlockReason);

            var platform = await svc.GetBatchOnlineSummaryAsync(OlcS4Data.PlatformBatch);
            Assert.False(platform!.IsPartnerBatch);
            Assert.Null(platform.PartnerContractState);
        }

        [Fact]
        public async Task PartnerIdLookups_ForBatchAndLecture()
        {
            var (svc, _) = Create();
            Assert.Null(await svc.GetBatchPartnerIdAsync(OlcS4Data.PlatformBatch));
            Assert.Equal(OlcS4Data.PartnerA, await svc.GetBatchPartnerIdAsync(OlcS4Data.BatchA));
            Assert.Null(await svc.GetLecturePartnerIdAsync(OlcS4Data.LecturePlatform));
            Assert.Equal(OlcS4Data.PartnerB, await svc.GetLecturePartnerIdAsync(OlcS4Data.LectureB));
            Assert.Null(await svc.GetLecturePartnerIdAsync(999));
        }

        [Fact]
        public async Task OnlineBatches_Platform_ExcludesPartnerBatches()
        {
            var (svc, _) = Create();
            var page = await svc.GetOnlineBatchesAsync(new OnlineBatchesFilterVm(), canManagePartners: true);

            Assert.Equal(DispatchAudience.Platform, page.Audience);
            Assert.Equal(new[] { OlcS4Data.PlatformBatch }, page.Batches.Select(b => b.BatchId));
            Assert.Empty(page.Partners);
        }

        [Fact]
        public async Task OnlineBatches_PartnersAudience_WithoutPermission_FallsBackToPlatform()
        {
            var (svc, _) = Create();
            var page = await svc.GetOnlineBatchesAsync(new OnlineBatchesFilterVm { Audience = DispatchAudience.Partners }, canManagePartners: false);

            Assert.Equal(DispatchAudience.Platform, page.Audience);
            Assert.DoesNotContain(page.Batches, b => b.PartnerId.HasValue);
        }

        [Fact]
        public async Task OnlineBatches_Partners_ListsStatesAndOnlineBatches()
        {
            var (svc, _) = Create();
            var page = await svc.GetOnlineBatchesAsync(new OnlineBatchesFilterVm { Audience = DispatchAudience.Partners }, canManagePartners: true);

            Assert.Equal(DispatchAudience.Partners, page.Audience);
            // ج: شراكة منتهية بلا عقد ولا دفعات ⇒ لا تُعرض
            Assert.Equal(new[] { "مدرسة أ", "مدرسة ب" }, page.Partners.Select(p => p.Name));
            Assert.Equal(PartnerEducationalContentState.Enabled, page.Partners[0].State);
            Assert.Equal(1, page.Partners[0].OnlineBatchesCount);
            Assert.Equal(PartnerEducationalContentState.NotInContract, page.Partners[1].State);

            // بلا اختيار: كل دفعات الشركاء الأونلاين فقط
            var row = Assert.Single(page.Batches);
            Assert.Equal(OlcS4Data.BatchA, row.BatchId);
            Assert.Equal(PartnerEducationalContentState.Enabled, row.ContractState);
            Assert.Equal(2, row.LecturesCount);
        }

        [Fact]
        public async Task OnlineBatches_IncludeInactive_ShowsCandidatesWithTheirState()
        {
            var (svc, _) = Create();
            var page = await svc.GetOnlineBatchesAsync(
                new OnlineBatchesFilterVm { Audience = DispatchAudience.Partners, IncludeInactive = true }, canManagePartners: true);

            Assert.Equal(3, page.Batches.Count);
            var b = page.Batches.Single(x => x.BatchId == OlcS4Data.BatchB);
            Assert.False(b.IsOnlineLearning);
            Assert.Equal(PartnerEducationalContentState.NotInContract, b.ContractState);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S4.3 — منطقة الشريك للقراءة فقط (R19): النطاق والأعداد
    // ═════════════════════════════════════════════════════════════════════
    public class PartnerLearningContentServiceTests
    {
        private const int StudentA1 = 501, StudentA2 = 502, StudentOtherPartner = 503, StudentWithdrawn = 504;

        private static async Task<(PartnerLearningContentService Service, ApplicationDbContext Db, int Full, int Indicator)> CreateAsync()
        {
            var db = OlcS4Data.Create();
            db.Students.AddRange(
                NewStudent(StudentA1, "أحمد", OlcS4Data.BranchA),
                NewStudent(StudentA2, "خالد", OlcS4Data.BranchA),
                NewStudent(StudentOtherPartner, "طالب شريك آخر", OlcS4Data.BranchB),
                NewStudent(StudentWithdrawn, "منسحب", OlcS4Data.BranchA));
            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = StudentA1, BatchId = OlcS4Data.BatchA },
                new StudentBatchEnrollment { StudentID = StudentA2, BatchId = OlcS4Data.BatchA },
                new StudentBatchEnrollment { StudentID = StudentOtherPartner, BatchId = OlcS4Data.BatchA },
                new StudentBatchEnrollment { StudentID = StudentWithdrawn, BatchId = OlcS4Data.BatchA, Status = "Withdrawn" });

            var full = new LectureContent { LectureId = OlcS4Data.LectureA1, Title = "فيديو كامل", ContentMode = LectureContentMode.FullVideo, Status = LectureContentStatus.Published, DurationSeconds = 1000 };
            var indicator = new LectureContent { LectureId = OlcS4Data.LectureA2, Title = "فيديوهات", ContentMode = LectureContentMode.IndicatorVideos, Status = LectureContentStatus.Published };
            var otherPartner = new LectureContent { LectureId = OlcS4Data.LectureB, Title = "ب", ContentMode = LectureContentMode.FullVideo, Status = LectureContentStatus.Published };
            db.LectureContents.AddRange(full, indicator, otherPartner);
            await db.SaveChangesAsync();

            var item1 = new LectureContentItem { LectureContentId = indicator.Id, LessonId = 1, SortOrder = 1 };
            var item2 = new LectureContentItem { LectureContentId = indicator.Id, LessonId = 2, SortOrder = 2 };
            var deleted = new LectureContentItem { LectureContentId = indicator.Id, LessonId = 3, SortOrder = 3, IsDeleted = true };
            db.LectureContentItems.AddRange(item1, item2, deleted);
            await db.SaveChangesAsync();

            db.LectureContentProgress.AddRange(
                new LectureContentProgress { LectureContentId = full.Id, StudentId = StudentA1, WatchedSeconds = 900, IsCompleted = true },
                new LectureContentProgress { LectureContentId = full.Id, StudentId = StudentA2, WatchedSeconds = 250 },
                new LectureContentProgress { LectureContentId = full.Id, StudentId = StudentOtherPartner, WatchedSeconds = 999, IsCompleted = true });
            db.LectureContentItemProgress.AddRange(
                new LectureContentItemProgress { LectureContentItemId = item1.Id, StudentId = StudentA1, IsCompleted = true },
                new LectureContentItemProgress { LectureContentItemId = item2.Id, StudentId = StudentA1, IsCompleted = true },
                new LectureContentItemProgress { LectureContentItemId = item1.Id, StudentId = StudentA2, IsCompleted = true });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            return (new PartnerLearningContentService(db), db, full.Id, indicator.Id);
        }

        private static Student NewStudent(int id, string name, int branchId)
            => new() { StudentID = id, FullName = name, NationalID = $"N{id}", Gender = "ذكر", School = "-", BranchId = branchId, IsActiveForLearning = true };

        [Fact]
        public async Task Batches_OnlyPartnerOnlineBatches_WithRealCompletion()
        {
            var (svc, _, _, _) = await CreateAsync();

            var batches = await svc.GetBatchesAsync(OlcS4Data.PartnerA);
            var a = Assert.Single(batches); // الحضورية لا تظهر
            Assert.Equal(OlcS4Data.BatchA, a.BatchId);
            Assert.Equal(2, a.StudentsCount);       // لا المنسحب ولا طالب شريك آخر
            Assert.Equal(2, a.PublishedCount);
            Assert.Equal(2, a.CompletedPairs);       // A1 أتم الكامل وكل الفيديوهات
            Assert.Equal(50, a.CompletionPercent);   // 2 من (2 طالب × 2 محاضرة)

            // B: دفعتها غير أونلاين ⇒ لا شيء
            Assert.Empty(await svc.GetBatchesAsync(OlcS4Data.PartnerB));
            Assert.Empty(await svc.GetBatchesAsync(0));
        }

        [Fact]
        public async Task Batch_CountsStartedAndCompleted_PerLecture()
        {
            var (svc, _, full, indicator) = await CreateAsync();

            var vm = await svc.GetBatchAsync(OlcS4Data.PartnerA, OlcS4Data.BatchA);
            Assert.NotNull(vm);
            Assert.Equal(2, vm!.StudentsCount);

            var f = vm.Lectures.Single(l => l.LectureContentId == full);
            Assert.Equal(2, f.StartedCount);
            Assert.Equal(1, f.CompletedCount);

            var i = vm.Lectures.Single(l => l.LectureContentId == indicator);
            Assert.Equal(2, i.ItemsCount);           // المحذوف لا يُحسب
            Assert.Equal(2, i.StartedCount);
            Assert.Equal(1, i.CompletedCount);
        }

        [Fact]
        public async Task OtherPartnerBatchOrLecture_IsNull_NoExistenceLeak()
        {
            var (svc, db, full, _) = await CreateAsync();

            Assert.Null(await svc.GetBatchAsync(OlcS4Data.PartnerB, OlcS4Data.BatchA));
            Assert.Null(await svc.GetLectureAsync(OlcS4Data.PartnerB, full));
            Assert.Null(await svc.GetBatchAsync(OlcS4Data.PartnerA, OlcS4Data.PlatformBatch));

            // محتوى غير منشور لا يُعرض للشريك
            (await db.LectureContents.SingleAsync(c => c.Id == full)).Status = LectureContentStatus.Hidden;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            Assert.Null(await svc.GetLectureAsync(OlcS4Data.PartnerA, full));
            Assert.DoesNotContain((await svc.GetBatchAsync(OlcS4Data.PartnerA, OlcS4Data.BatchA))!.Lectures, l => l.LectureContentId == full);
        }

        [Fact]
        public async Task Lecture_StudentRows_FullVideoAndIndicator()
        {
            var (svc, _, full, indicator) = await CreateAsync();

            var f = await svc.GetLectureAsync(OlcS4Data.PartnerA, full);
            Assert.Equal(new[] { "أحمد", "خالد" }, f!.Students.Select(s => s.FullName));
            Assert.Equal(100, f.Students[0].ProgressPercent);
            Assert.True(f.Students[0].IsCompleted);
            Assert.Equal(25, f.Students[1].ProgressPercent);   // 250 من 1000 ثانية
            Assert.False(f.Students[1].IsCompleted);

            var i = await svc.GetLectureAsync(OlcS4Data.PartnerA, indicator);
            var khaled = i!.Students.Single(s => s.StudentId == StudentA2);
            Assert.Equal(1, khaled.CompletedItems);
            Assert.Equal(50, khaled.ProgressPercent);
            Assert.True(i.Students.Single(s => s.StudentId == StudentA1).IsCompleted);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S4.3 — بنية Controller الشريك + الصلاحية ManagePartners في الكتالوج
    // ═════════════════════════════════════════════════════════════════════
    public class PartnerLearningContentStructureTests
    {
        [Fact]
        public void PartnerController_IsReadOnly_AndGuardedByClauseWithReadAfterExpiry()
        {
            var type = typeof(LearningContentController);
            Assert.True(typeof(PartnerBaseController).IsAssignableFrom(type));

            var feature = Assert.Single(type.GetCustomAttributes<PartnerFeatureAttribute>());
            Assert.Equal(PartnerFeatureType.EducationalContent, feature.Feature);
            Assert.True(feature.AllowReadAfterExpiry);

            var actions = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => typeof(Task<IActionResult>).IsAssignableFrom(m.ReturnType))
                .ToList();
            Assert.Equal(new[] { "Batch", "Index", "Lecture" }, actions.Select(a => a.Name).OrderBy(n => n, StringComparer.Ordinal));
            Assert.All(actions, a =>
            {
                Assert.NotNull(a.GetCustomAttribute<HttpGetAttribute>());
                Assert.Null(a.GetCustomAttribute<HttpPostAttribute>());
                // لا partnerId من الطلب: النطاق ActivePartnerId فقط
                Assert.DoesNotContain(a.GetParameters(), p => p.Name!.Contains("partner", StringComparison.OrdinalIgnoreCase));
            });
        }

        [Fact]
        public void Catalog_HasManagePartners()
        {
            Assert.Contains(AdminControllerActionCatalog.GetActions("LectureContents"), a => a.Key == "ManagePartners");
        }
    }
}
