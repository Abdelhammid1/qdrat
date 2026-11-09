using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Moq;
using QdratNew.Areas.Students.Controllers;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.OnlineLecture;
using QdratNew.ViewModels.OnlineLecture;
using Xunit;

namespace QdratNew.Tests.OnlineLecture
{
    // ═════════════════════════════════════════════════════════════════════
    // OLC-S5.2 — LectureSequenceCalculator (دالة نقية، R13)
    // ═════════════════════════════════════════════════════════════════════
    public class LectureSequenceCalculatorTests
    {
        private static readonly DateTime D1 = new(2026, 11, 1), D2 = new(2026, 11, 2), D3 = new(2026, 11, 3), D4 = new(2026, 11, 4);

        private static SequenceLecture L(int contentId, int curriculumId, DateTime date, bool done, TimeSpan? time = null, int? lectureId = null)
            => new(contentId, lectureId ?? contentId * 10, curriculumId, date, time, done);

        [Fact]
        public void Empty_ReturnsEmpty()
            => Assert.Empty(LectureSequenceCalculator.Compute(Array.Empty<SequenceLecture>()));

        [Fact]
        public void FirstLecture_IsAlwaysOpen()
        {
            var s = LectureSequenceCalculator.Compute(new[] { L(1, 1, D1, done: false) });
            Assert.False(s[1].IsLocked);
            Assert.True(s[1].CanOpen);
        }

        [Fact]
        public void SecondLecture_LockedUntilFirstCompleted()
        {
            var before = LectureSequenceCalculator.Compute(new[] { L(1, 1, D1, false), L(2, 1, D2, false), L(3, 1, D3, false) });
            Assert.False(before[1].IsLocked);
            Assert.True(before[2].IsLocked);
            Assert.True(before[3].IsLocked);
            Assert.False(before[2].CanOpen);

            var after = LectureSequenceCalculator.Compute(new[] { L(1, 1, D1, true), L(2, 1, D2, false), L(3, 1, D3, false) });
            Assert.False(after[2].IsLocked);
            Assert.True(after[3].IsLocked);
        }

        [Fact]
        public void Curricula_AreIndependent()
        {
            // منهج 1: الأولى غير مكتملة ⇒ الثانية مقفلة. منهج 2 لا يتأثر.
            var s = LectureSequenceCalculator.Compute(new[]
            {
                L(1, 1, D1, false), L(2, 1, D2, false),
                L(3, 2, D1, true), L(4, 2, D3, false)
            });
            Assert.True(s[2].IsLocked);
            Assert.False(s[3].IsLocked);
            Assert.False(s[4].IsLocked);
        }

        [Fact]
        public void OlderLecturePublishedLater_LocksOnlyLaterIncomplete_CompletedStayOpenForReview()
        {
            // أتمّ 1 و3، ثم نُشرت 2 (أقدم من 3): 2 مفتوحة، 3 مكتملة تبقى للمراجعة، 4 غير المكتملة مقفلة.
            var s = LectureSequenceCalculator.Compute(new[]
            {
                L(1, 1, D1, true), L(2, 1, D2, false), L(3, 1, D3, true), L(4, 1, D4, false)
            });
            Assert.False(s[2].IsLocked);
            Assert.True(s[3].IsLocked);
            Assert.True(s[3].IsCompleted);
            Assert.True(s[3].CanOpen);
            Assert.True(s[4].IsLocked);
            Assert.False(s[4].CanOpen);
        }

        [Fact]
        public void Order_ByDateThenScheduledTimeNullAsMidnightThenLectureId_IgnoringTimeOfDay()
        {
            // نفس اليوم: بلا وقت (00:00) أولًا، ثم 09:00، ثم 09:00 بمعرّف محاضرة أكبر. الوقت داخل Date يُتجاهل.
            var s = LectureSequenceCalculator.Compute(new[]
            {
                L(1, 1, D1.AddHours(20), false, TimeSpan.FromHours(9), lectureId: 7),
                L(2, 1, D1.AddHours(23), true, null, lectureId: 9),
                L(3, 1, D1, false, TimeSpan.FromHours(9), lectureId: 8)
            });
            Assert.False(s[2].IsLocked);   // الأولى (بلا وقت)
            Assert.False(s[1].IsLocked);   // 09:00 / 7 — السابقة مكتملة
            Assert.True(s[3].IsLocked);    // 09:00 / 8 — بعد غير مكتملة
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // بيانات InMemory مشتركة لـ OLC-S5
    // ═════════════════════════════════════════════════════════════════════
    internal static class OlcS5Data
    {
        public const int Course = 1, OtherCourse = 2;
        public const int Cur1 = 1, Cur2 = 2, CurExtra = 3;
        public const int Sec1 = 11, Sec2 = 12, SecExtra = 13;
        public const int PlatformBranch = 1, PartnerBranchOk = 2, PartnerBranchNoClause = 3;
        public const int PartnerOk = 10, PartnerNoClause = 20;
        public const int OnlineBatch = 100, OfflineBatch = 101, DeletedBatch = 102, OtherCourseBatch = 103, PartnerOkBatch = 110, PartnerNoClauseBatch = 120;
        public const int Student = 1, OtherStudent = 2, Withdrawn = 3, Inactive = 4, NoEnroll = 5;

        public static ApplicationDbContext Create()
        {
            var db = TestDbContextFactory.CreateFreshContext(out _);
            var today = DateTime.Today;

            db.Courses.AddRange(
                new Course { Id = Course, Name = "قدرات أونلاين", Description = "-" },
                new Course { Id = OtherCourse, Name = "دورة أخرى", Description = "-" });
            db.Curriculums.AddRange(
                new Curriculum { Id = Cur1, Title = "المنهج الكمي", Description = "-", CurriculumTypeName = "-" },
                new Curriculum { Id = Cur2, Title = "المنهج اللفظي", Description = "-", CurriculumTypeName = "-" },
                new Curriculum { Id = CurExtra, Title = "منهج خارج الدورة", Description = "-", CurriculumTypeName = "-" });
            // ترتيب CourseCurriculums.Id: اللفظي أولًا ثم الكمي (R12 بالمعرّف لا بالاسم)
            db.CourseCurriculums.AddRange(
                new CourseCurriculum { Id = 1, CourseId = Course, CurriculumId = Cur2 },
                new CourseCurriculum { Id = 2, CourseId = Course, CurriculumId = Cur1 });
            db.Sections.AddRange(
                new Section { Id = Sec1, Title = "محور الحساب", CurriculumId = Cur1 },
                new Section { Id = Sec2, Title = "محور التناظر", CurriculumId = Cur2 },
                new Section { Id = SecExtra, Title = "محور إضافي", CurriculumId = CurExtra });

            db.Partners.AddRange(
                new Partner { Id = PartnerOk, Name = "مدرسة بالبند", Code = "OK", PartnershipStart = today.AddYears(-1), PartnershipEnd = today.AddYears(1) },
                new Partner { Id = PartnerNoClause, Name = "مدرسة بلا بند", Code = "NO", PartnershipStart = today.AddYears(-1), PartnershipEnd = today.AddYears(1) });
            db.Branches.AddRange(
                OlcS4Data.NewBranch(PlatformBranch, "المعهد", null),
                OlcS4Data.NewBranch(PartnerBranchOk, "فرع بالبند", PartnerOk),
                OlcS4Data.NewBranch(PartnerBranchNoClause, "فرع بلا بند", PartnerNoClause));
            db.PartnerSubscriptions.AddRange(
                OlcS4Data.Contract(PartnerOk, today.AddMonths(-1), today.AddMonths(6), clause: true),
                OlcS4Data.Contract(PartnerNoClause, today.AddMonths(-1), today.AddMonths(6), clause: false));

            db.Batches.AddRange(
                new Batch { Id = OnlineBatch, Name = "دفعة أونلاين", BranchId = PlatformBranch, CourseId = Course, IsOnlineLearning = true },
                new Batch { Id = OfflineBatch, Name = "دفعة حضورية", BranchId = PlatformBranch, CourseId = Course },
                new Batch { Id = DeletedBatch, Name = "دفعة محذوفة", BranchId = PlatformBranch, CourseId = Course, IsOnlineLearning = true, IsDeleted = true },
                new Batch { Id = OtherCourseBatch, Name = "دفعة دورة أخرى", BranchId = PlatformBranch, CourseId = OtherCourse, IsOnlineLearning = true },
                new Batch { Id = PartnerOkBatch, Name = "دفعة شريك بالبند", BranchId = PartnerBranchOk, CourseId = Course, IsOnlineLearning = true },
                new Batch { Id = PartnerNoClauseBatch, Name = "دفعة شريك بلا بند", BranchId = PartnerBranchNoClause, CourseId = Course, IsOnlineLearning = true });

            db.Students.AddRange(
                NewStudent(Student, PlatformBranch),
                NewStudent(OtherStudent, PlatformBranch),
                NewStudent(Withdrawn, PlatformBranch),
                NewStudent(Inactive, PlatformBranch, active: false),
                NewStudent(NoEnroll, PlatformBranch));

            db.StudentBatchEnrollments.AddRange(
                new StudentBatchEnrollment { StudentID = Student, BatchId = OnlineBatch },
                new StudentBatchEnrollment { StudentID = OtherStudent, BatchId = OnlineBatch },
                new StudentBatchEnrollment { StudentID = Withdrawn, BatchId = OnlineBatch, Status = "Withdrawn" },
                new StudentBatchEnrollment { StudentID = Inactive, BatchId = OnlineBatch });

            db.SaveChanges();
            db.ChangeTracker.Clear();
            return db;
        }

        public static Student NewStudent(int id, int branchId, bool active = true) => new()
        {
            StudentID = id, NationalID = $"10000000{id:00}", FullName = $"طالب {id}", Gender = "ذكر", Age = 17,
            School = "-", BranchId = branchId, IsActiveForLearning = active
        };

        public static void Enroll(ApplicationDbContext db, int studentId, int batchId, string status = "Active")
        {
            db.StudentBatchEnrollments.Add(new StudentBatchEnrollment { StudentID = studentId, BatchId = batchId, Status = status });
            db.SaveChanges();
            db.ChangeTracker.Clear();
        }

        private static int _lectureSeq = 1000;

        /// <summary>محاضرة + محتوى (افتراضيًا منشور FullVideo بمدة 600ث). يُرجع معرّف المحتوى.</summary>
        public static int Content(ApplicationDbContext db, int batchId, int sectionId, DateTime date,
            LectureContentMode mode = LectureContentMode.FullVideo, LectureContentStatus status = LectureContentStatus.Published,
            TimeSpan? time = null, int items = 0, bool deleted = false, string? title = null)
        {
            var lectureId = Interlocked.Increment(ref _lectureSeq);
            db.Lecture.Add(new Lecture
            {
                Id = lectureId, Title = $"محاضرة {lectureId}", Location = "-", Date = date, ScheduledTime = time,
                InstructorId = 1, SectionId = sectionId, CourseId = Course, BatchId = batchId
            });
            var content = new LectureContent
            {
                LectureId = lectureId, ContentMode = mode, Status = status, Title = title ?? $"محتوى {lectureId}",
                DurationSeconds = mode == LectureContentMode.FullVideo ? 600 : null, IsDeleted = deleted,
                VideoProvider = mode == LectureContentMode.FullVideo ? LectureVideoProvider.YouTube : null,
                VideoExternalId = mode == LectureContentMode.FullVideo ? "dQw4w9WgXcQ" : null
            };
            for (var i = 0; i < items; i++)
                content.Items.Add(new LectureContentItem { LessonId = 500 + i, SortOrder = i, VideoProvider = LectureVideoProvider.Vimeo, VideoExternalId = "123456" + i, DurationSeconds = 100 });
            db.LectureContents.Add(content);
            db.SaveChanges();
            db.ChangeTracker.Clear();
            return content.Id;
        }

        public static void CompleteFull(ApplicationDbContext db, int contentId, int studentId, bool completed = true, double watched = 600)
        {
            db.LectureContentProgress.Add(new LectureContentProgress
            {
                LectureContentId = contentId, StudentId = studentId, WatchedSeconds = watched, MaxPositionSeconds = watched,
                DurationSeconds = 600, IsCompleted = completed, EndedSeen = completed
            });
            db.SaveChanges();
            db.ChangeTracker.Clear();
        }

        public static void CompleteItems(ApplicationDbContext db, int contentId, int studentId, int count)
        {
            var itemIds = db.LectureContentItems.Where(i => i.LectureContentId == contentId).OrderBy(i => i.SortOrder).Select(i => i.Id).Take(count).ToList();
            foreach (var id in itemIds)
                db.LectureContentItemProgress.Add(new LectureContentItemProgress { LectureContentItemId = id, StudentId = studentId, IsCompleted = true, EndedSeen = true, WatchedSeconds = 100 });
            db.SaveChanges();
            db.ChangeTracker.Clear();
        }

        public static IOnlineLectureFeatureService Feature(bool enabled)
        {
            var mock = new Mock<IOnlineLectureFeatureService>();
            mock.Setup(f => f.IsEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(enabled);
            return mock.Object;
        }

        public static StudentOnlineLearningService Learning(ApplicationDbContext db, bool enabled = true)
            => new(db, new OnlineLearningAccessService(db, Feature(enabled)), new LectureSequenceService(db));
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S5.1 — IOnlineLearningAccessService (R2/R3/R17)
    // ═════════════════════════════════════════════════════════════════════
    public class OnlineLearningAccessServiceTests
    {
        [Fact]
        public async Task FeatureOff_NoAccessAnywhere()
        {
            using var db = OlcS5Data.Create();
            var contentId = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, new DateTime(2026, 11, 1));
            var access = new OnlineLearningAccessService(db, OlcS5Data.Feature(false));

            Assert.Empty(await access.GetAccessibleBatchesAsync(OlcS5Data.Student));
            Assert.False(await access.IsEligibleAsync(OlcS5Data.Student, OlcS5Data.Course));
            Assert.Null(await access.CanViewAsync(OlcS5Data.Student, contentId));
        }

        [Theory]
        [InlineData(OlcS5Data.Student, true)]
        [InlineData(OlcS5Data.OtherStudent, true)]
        [InlineData(OlcS5Data.Withdrawn, false)]   // تسجيل غير نشط
        [InlineData(OlcS5Data.Inactive, false)]    // طالب غير نشط للتعلّم
        [InlineData(OlcS5Data.NoEnroll, false)]
        public async Task R3_ActiveEnrollmentInOnlineBatchOnly(int studentId, bool expected)
        {
            using var db = OlcS5Data.Create();
            var access = new OnlineLearningAccessService(db, OlcS5Data.Feature(true));
            Assert.Equal(expected, await access.IsEligibleAsync(studentId, OlcS5Data.Course));
        }

        [Theory]
        [InlineData(OlcS5Data.OfflineBatch, false)]
        [InlineData(OlcS5Data.DeletedBatch, false)]
        [InlineData(OlcS5Data.PartnerOkBatch, true)]          // R17: عقد الشريك يشمل البند
        [InlineData(OlcS5Data.PartnerNoClauseBatch, false)]   // R17: عقد بلا البند
        public async Task R3_R17_BatchMustBeOnline_NotDeleted_AndPartnerClause(int batchId, bool expected)
        {
            using var db = OlcS5Data.Create();
            OlcS5Data.Enroll(db, OlcS5Data.NoEnroll, batchId);
            var access = new OnlineLearningAccessService(db, OlcS5Data.Feature(true));
            Assert.Equal(expected, await access.IsEligibleAsync(OlcS5Data.NoEnroll, OlcS5Data.Course));
        }

        [Fact]
        public async Task EligibleBatch_ScopedToCourse_PrefersContextBatch()
        {
            using var db = OlcS5Data.Create();
            OlcS5Data.Enroll(db, OlcS5Data.Student, OlcS5Data.PartnerOkBatch);
            OlcS5Data.Enroll(db, OlcS5Data.Student, OlcS5Data.OtherCourseBatch);
            var access = new OnlineLearningAccessService(db, OlcS5Data.Feature(true));

            Assert.Equal(OlcS5Data.PartnerOkBatch, await access.GetEligibleBatchIdAsync(OlcS5Data.Student, OlcS5Data.Course, OlcS5Data.PartnerOkBatch));
            Assert.Equal(OlcS5Data.OnlineBatch, await access.GetEligibleBatchIdAsync(OlcS5Data.Student, OlcS5Data.Course, OlcS5Data.OfflineBatch));
            Assert.Equal(OlcS5Data.OtherCourseBatch, await access.GetEligibleBatchIdAsync(OlcS5Data.Student, OlcS5Data.OtherCourse));
            Assert.Null(await access.GetEligibleBatchIdAsync(OlcS5Data.Student, 999));
            Assert.True(await access.IsEligibleAsync(OlcS5Data.Student, null));
        }

        [Fact]
        public async Task R2_CanView_PublishedInOwnBatchOnly()
        {
            using var db = OlcS5Data.Create();
            var published = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, new DateTime(2026, 11, 1));
            var draft = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, new DateTime(2026, 11, 2), status: LectureContentStatus.Draft);
            var hidden = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, new DateTime(2026, 11, 3), status: LectureContentStatus.Hidden);
            var deleted = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, new DateTime(2026, 11, 4), deleted: true);
            var otherBatch = OlcS5Data.Content(db, OlcS5Data.PartnerOkBatch, OlcS5Data.Sec1, new DateTime(2026, 11, 1));
            var access = new OnlineLearningAccessService(db, OlcS5Data.Feature(true));

            var ok = await access.CanViewAsync(OlcS5Data.Student, published);
            Assert.NotNull(ok);
            Assert.Equal(OlcS5Data.OnlineBatch, ok!.BatchId);
            Assert.Equal(OlcS5Data.Cur1, ok.CurriculumId);

            Assert.Null(await access.CanViewAsync(OlcS5Data.Student, draft));
            Assert.Null(await access.CanViewAsync(OlcS5Data.Student, hidden));
            Assert.Null(await access.CanViewAsync(OlcS5Data.Student, deleted));
            Assert.Null(await access.CanViewAsync(OlcS5Data.Student, otherBatch));     // دفعة أخرى ⇒ 404
            Assert.Null(await access.CanViewAsync(OlcS5Data.Withdrawn, published));    // تسجيل غير نشط
            Assert.Null(await access.CanViewAsync(OlcS5Data.Student, 0));
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S5.2 — ILectureSequenceService (البيانات + القفل)
    // ═════════════════════════════════════════════════════════════════════
    public class LectureSequenceServiceTests
    {
        private static readonly DateTime D1 = new(2026, 11, 1), D2 = new(2026, 11, 2), D3 = new(2026, 11, 3);

        [Fact]
        public async Task HiddenDraftDeleted_NotInSequence()
        {
            using var db = OlcS5Data.Create();
            var first = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2, status: LectureContentStatus.Hidden);
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2, status: LectureContentStatus.Draft);
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2, deleted: true);
            var third = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D3);
            OlcS5Data.CompleteFull(db, first, OlcS5Data.Student);

            var rows = await new LectureSequenceService(db).GetForStudentAsync(OlcS5Data.Student, OlcS5Data.OnlineBatch);

            Assert.Equal(new[] { first, third }, rows.Select(r => r.LectureContentId));
            Assert.True(rows[0].IsCompleted);
            Assert.False(rows[1].IsLocked);   // المخفي/المسودة/المحذوف بينهما لا يقفل
        }

        [Fact]
        public async Task IndicatorVideos_MissingOneItem_IsIncomplete_AndLocksNext()
        {
            using var db = OlcS5Data.Create();
            var playlist = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1, mode: LectureContentMode.IndicatorVideos, items: 3);
            var next = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            OlcS5Data.CompleteItems(db, playlist, OlcS5Data.Student, 2);

            var rows = await new LectureSequenceService(db).GetForStudentAsync(OlcS5Data.Student, OlcS5Data.OnlineBatch);
            var p = rows.Single(r => r.LectureContentId == playlist);
            Assert.False(p.IsCompleted);
            Assert.Equal(2, p.CompletedItems);
            Assert.Equal(66, p.ProgressPercent);
            Assert.True(rows.Single(r => r.LectureContentId == next).IsLocked);
        }

        [Fact]
        public async Task IndicatorVideos_DeletedItemIgnored_AllActiveCompleted_UnlocksNext()
        {
            using var db = OlcS5Data.Create();
            var playlist = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1, mode: LectureContentMode.IndicatorVideos, items: 3);
            var next = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            OlcS5Data.CompleteItems(db, playlist, OlcS5Data.Student, 2);

            // حذف ناعم للعنصر الثالث (غير المكتمل) ⇒ كل العناصر النشطة مكتملة
            var last = db.LectureContentItems.Where(i => i.LectureContentId == playlist).OrderByDescending(i => i.SortOrder).First();
            last.IsDeleted = true;
            db.SaveChanges();
            db.ChangeTracker.Clear();

            var rows = await new LectureSequenceService(db).GetForStudentAsync(OlcS5Data.Student, OlcS5Data.OnlineBatch);
            var p = rows.Single(r => r.LectureContentId == playlist);
            Assert.Equal(2, p.ItemsCount);
            Assert.True(p.IsCompleted);
            Assert.Equal(100, p.ProgressPercent);
            Assert.False(rows.Single(r => r.LectureContentId == next).IsLocked);
        }

        [Fact]
        public async Task FullVideo_PercentFromServerWatchedSeconds_CappedBelow100UntilCompleted()
        {
            using var db = OlcS5Data.Create();
            var a = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            OlcS5Data.CompleteFull(db, a, OlcS5Data.Student, completed: false, watched: 900);   // أكثر من المدة لكن غير مكتمل

            var row = (await new LectureSequenceService(db).GetForStudentAsync(OlcS5Data.Student, OlcS5Data.OnlineBatch)).Single();
            Assert.False(row.IsCompleted);
            Assert.Equal(99, row.ProgressPercent);

            Assert.Equal(50, LectureSequenceService.FullPercent(false, 300, 600));
            Assert.Equal(0, LectureSequenceService.FullPercent(false, 300, null));
            Assert.Equal(100, LectureSequenceService.FullPercent(true, 0, null));
        }

        [Fact]
        public async Task OtherStudentsProgress_DoesNotCount_AndCurriculaIndependent()
        {
            using var db = OlcS5Data.Create();
            var q1 = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var q2 = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            var v1 = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec2, D3);
            var v2 = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec2, D3.AddDays(1));
            OlcS5Data.CompleteFull(db, q1, OlcS5Data.OtherStudent);
            OlcS5Data.CompleteFull(db, v1, OlcS5Data.Student);

            var rows = (await new LectureSequenceService(db).GetForStudentAsync(OlcS5Data.Student, OlcS5Data.OnlineBatch))
                .ToDictionary(r => r.LectureContentId);

            Assert.False(rows[q1].IsCompleted);
            Assert.True(rows[q2].IsLocked);
            Assert.False(rows[v2].IsLocked);
            Assert.Equal(OlcS5Data.Cur2, rows[v1].CurriculumId);
        }

        [Fact]
        public async Task OtherBatch_ReturnsNothing()
        {
            using var db = OlcS5Data.Create();
            OlcS5Data.Content(db, OlcS5Data.PartnerOkBatch, OlcS5Data.Sec1, D1);
            Assert.Empty(await new LectureSequenceService(db).GetForStudentAsync(OlcS5Data.Student, OlcS5Data.OnlineBatch));
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // OLC-S5.3 — صفحة القسم (StudentOnlineLearningService)
    // ═════════════════════════════════════════════════════════════════════
    public class StudentOnlineLearningServiceTests
    {
        private static readonly DateTime D1 = new(2026, 11, 1), D2 = new(2026, 11, 2), D3 = new(2026, 11, 3);

        [Fact]
        public async Task Index_CourseCurriculaInIdOrder_ExtraCurriculumAppended_EmptyCurriculumKept()
        {
            using var db = OlcS5Data.Create();
            var q1 = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1, title: "كمي 1");
            var q2 = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2, title: "كمي 2");
            var x = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.SecExtra, D3, title: "إضافي");
            OlcS5Data.CompleteFull(db, q1, OlcS5Data.Student);

            var vm = await OlcS5Data.Learning(db).GetIndexAsync(OlcS5Data.Student, OlcS5Data.Course, OlcS5Data.OnlineBatch);

            Assert.True(vm.IsEligible);
            Assert.Equal("قدرات أونلاين", vm.CourseTitle);
            Assert.Equal(new[] { OlcS5Data.Cur2, OlcS5Data.Cur1, OlcS5Data.CurExtra }, vm.Curricula.Select(c => c.CurriculumId));
            Assert.Empty(vm.Curricula[0].Lectures);   // اللفظي بلا محاضرات منشورة — يظهر بحالة فراغ

            var quant = vm.Curricula[1];
            Assert.Equal(1, quant.CompletedCount);
            Assert.Equal(2, quant.TotalCount);
            Assert.True(quant.Lectures[0].IsCompleted);
            Assert.False(quant.Lectures[1].IsLocked);
            Assert.True(quant.Lectures[1].IsNext);
            Assert.Equal(q2, quant.Lectures[1].LectureContentId);

            Assert.Equal(x, vm.Curricula[2].Lectures.Single().LectureContentId);
            Assert.Equal(3, vm.TotalLectures);
            Assert.Equal(33, vm.ProgressPercent);
        }

        [Fact]
        public async Task Index_LockedLecture_HasNoNextFlag_AndCompletedLaterStaysReviewable()
        {
            using var db = OlcS5Data.Create();
            var a = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var b = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D2);
            var c = OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D3);
            OlcS5Data.CompleteFull(db, c, OlcS5Data.Student);   // أكمل c قبل أن تُنشر a (ملاحظة R15)

            var lectures = (await OlcS5Data.Learning(db).GetIndexAsync(OlcS5Data.Student, OlcS5Data.Course, OlcS5Data.OnlineBatch))
                .Curricula.Single(x => x.CurriculumId == OlcS5Data.Cur1).Lectures;

            Assert.True(lectures[0].IsNext);
            Assert.True(lectures[1].IsLocked);
            Assert.False(lectures[1].IsNext);
            Assert.True(lectures[2].IsCompleted);
            Assert.False(lectures[2].IsLocked);   // المكتمل لا يتأثر — يُراجع
            Assert.Equal(new[] { a, b, c }, lectures.Select(l => l.LectureContentId));
        }

        [Fact]
        public async Task Index_NotEligible_WithHintWhenAnotherCourseIsOnline()
        {
            using var db = OlcS5Data.Create();
            OlcS5Data.Enroll(db, OlcS5Data.NoEnroll, OlcS5Data.OtherCourseBatch);

            var vm = await OlcS5Data.Learning(db).GetIndexAsync(OlcS5Data.NoEnroll, OlcS5Data.Course, OlcS5Data.OfflineBatch);
            Assert.False(vm.IsEligible);
            Assert.True(vm.HasOtherEligibleCourse);
            Assert.Empty(vm.Curricula);

            var none = await OlcS5Data.Learning(db).GetIndexAsync(OlcS5Data.Withdrawn, OlcS5Data.Course, OlcS5Data.OnlineBatch);
            Assert.False(none.IsEligible);
            Assert.False(none.HasOtherEligibleCourse);
        }

        [Fact]
        public async Task Index_FeatureOff_NotEligible()
        {
            using var db = OlcS5Data.Create();
            OlcS5Data.Content(db, OlcS5Data.OnlineBatch, OlcS5Data.Sec1, D1);
            var vm = await OlcS5Data.Learning(db, enabled: false).GetIndexAsync(OlcS5Data.Student, OlcS5Data.Course, OlcS5Data.OnlineBatch);
            Assert.False(vm.IsEligible);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    // حراس بنيوية: C.5 بلا بيانات فيديو + الـ Controller على سياق الطالب
    // ═════════════════════════════════════════════════════════════════════
    public class OnlineLearningStudentStructureTests
    {
        [Fact]
        public void MenuCacheSignal_Invalidate_ExpiresCapturedToken_AndIssuesFreshOne()
        {
            var captured = OnlineLearningMenuCacheSignal.Token;
            Assert.False(captured.HasChanged);

            OnlineLearningMenuCacheSignal.Invalidate();

            Assert.True(captured.HasChanged);
            Assert.False(OnlineLearningMenuCacheSignal.Token.HasChanged);
        }

        [Fact]
        public void MenuCacheSignal_EvictsMemoryCacheEntry()
        {
            using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
            var options = new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };
            options.ExpirationTokens.Add(OnlineLearningMenuCacheSignal.Token);
            Microsoft.Extensions.Caching.Memory.CacheExtensions.Set(cache, "olc-nav:1:1", true, options);
            Assert.True(cache.TryGetValue("olc-nav:1:1", out _));

            OnlineLearningMenuCacheSignal.Invalidate();

            Assert.False(cache.TryGetValue("olc-nav:1:1", out _));
        }

        [Fact]
        public void C5_IndexViewModels_ExposeNoVideoSourceFields()
        {
            var types = new[] { typeof(StudentOnlineLearningIndexVm), typeof(StudentOnlineCurriculumVm), typeof(StudentOnlineLectureVm) };
            var forbidden = new[] { "Video", "Url", "ExternalId", "Embed", "Provider", "Thumbnail" };

            foreach (var type in types)
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    Assert.DoesNotContain(forbidden, f => prop.Name.Contains(f, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Controller_InheritsStudentContext_AndIndexIsGetOnly()
        {
            Assert.True(typeof(StudentBaseController).IsAssignableFrom(typeof(OnlineLearningController)));

            var index = typeof(OnlineLearningController).GetMethod(nameof(OnlineLearningController.Index))!;
            Assert.NotNull(index.GetCustomAttribute<HttpGetAttribute>());
            Assert.Equal(typeof(Task<IActionResult>), index.ReturnType);

            // لا يقبل أي معرّف طالب/دفعة من الطلب
            Assert.DoesNotContain(index.GetParameters(), p => p.ParameterType == typeof(int));
        }
    }
}
