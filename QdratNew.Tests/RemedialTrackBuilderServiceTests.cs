using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.RemedialTracks;
using QdratNew.ViewModels.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>RTK-S2: خدمة بناء الخطة على InMemory (الفهارس الفريدة المُصفّاة لا تُفرض هنا — تُغطّى في RTK-S7.3).</summary>
    public class RemedialTrackBuilderServiceTests
    {
        private sealed class FixedTime : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        }

        private static readonly RemedialTrackActor Actor = new("admin-1", "مدير");

        private sealed class Fixture
        {
            public TestDbContextFactory Factory { get; }
            public RemedialTrackBuilderService Sut { get; }
            public int CurriculumId { get; private set; }
            public int OtherCurriculumId { get; private set; }
            public int[] SectionIds { get; private set; } = Array.Empty<int>();
            public int OtherSectionId { get; private set; }
            public int GoodModelA { get; private set; }
            public int GoodModelB { get; private set; }
            public int BadAnswerModel { get; private set; }
            public int EmptyModel { get; private set; }
            public int HomeworkModel { get; private set; }
            public int ArchivedModel { get; private set; }
            public int OtherCurriculumModel { get; private set; }

            public Fixture()
            {
                Factory = new TestDbContextFactory(Guid.NewGuid().ToString());
                Sut = new RemedialTrackBuilderService(Factory, new FixedTime(), new RemedialTrackCodeGenerator(Factory, new FixedTime()));
            }

            public async Task SeedAsync()
            {
                await using var db = Factory.CreateDbContext();

                var cur = new Curriculum { Title = "قدرات", Description = "d", CurriculumTypeName = "t" };
                var other = new Curriculum { Title = "تحصيلي", Description = "d", CurriculumTypeName = "t" };
                db.Curriculums.AddRange(cur, other);
                await db.SaveChangesAsync();
                CurriculumId = cur.Id;
                OtherCurriculumId = other.Id;

                var s1 = new Section { Title = "كمي", CurriculumId = cur.Id };
                var s2 = new Section { Title = "لفظي", CurriculumId = cur.Id };
                var s3 = new Section { Title = "هندسة", CurriculumId = cur.Id };
                var so = new Section { Title = "فيزياء", CurriculumId = other.Id };
                db.Sections.AddRange(s1, s2, s3, so);
                await db.SaveChangesAsync();
                SectionIds = new[] { s1.Id, s2.Id, s3.Id };
                OtherSectionId = so.Id;

                GoodModelA = await AddModelAsync(db, "M1", ProfessionalModelType.Exam, false, null, good: 3, bad: 0);
                GoodModelB = await AddModelAsync(db, "M2", ProfessionalModelType.Exam, false, null, good: 2, bad: 0);
                BadAnswerModel = await AddModelAsync(db, "M3", ProfessionalModelType.Exam, false, null, good: 2, bad: 1);
                EmptyModel = await AddModelAsync(db, "M4", ProfessionalModelType.Exam, false, null, good: 0, bad: 0);
                HomeworkModel = await AddModelAsync(db, "H1", ProfessionalModelType.Homework, false, null, good: 2, bad: 0);
                ArchivedModel = await AddModelAsync(db, "M5", ProfessionalModelType.Exam, true, null, good: 2, bad: 0);
                OtherCurriculumModel = await AddModelAsync(db, "M6", ProfessionalModelType.Exam, false, other.Id, good: 2, bad: 0);
            }

            private static async Task<int> AddModelAsync(ApplicationDbContext db, string title, ProfessionalModelType type,
                bool archived, int? curriculumId, int good, int bad)
            {
                var model = new ProfessionalModel
                {
                    Title = title,
                    Description = "d",
                    CreatedBy = "seed",
                    CreatedAt = DateTime.UtcNow,
                    ModelType = type,
                    IsArchived = archived,
                    CurriculumId = curriculumId
                };
                db.ProfessionalModels.Add(model);
                await db.SaveChangesAsync();

                var order = 1;
                for (var i = 0; i < good + bad; i++)
                {
                    var q = new Question
                    {
                        CurriculumId = 1,
                        LessonId = 1,
                        ReferenceNumber = $"Q-{title}-{i}",
                        CorrectAnswer = i < good ? "A" : null
                    };
                    db.Questions.Add(q);
                    db.ProfessionalModelQuestions.Add(new ProfessionalModelQuestion { ModelId = model.Id, QuestionId = q.Id, OrderNumber = order++ });
                }
                await db.SaveChangesAsync();
                return model.Id;
            }

            public async Task<int> CreateTrackAsync()
            {
                var r = await Sut.CreateAsync(new CreateRemedialTrackInput { Title = "خطة", CurriculumId = CurriculumId }, Actor);
                Assert.True(r.Success, r.Message);
                return (int)r.Data!;
            }

            public async Task<int> AddAxisAsync(int trackId, int sectionIndex)
            {
                var r = await Sut.AddAxisAsync(new AddRemedialAxisInput
                {
                    TrackId = trackId,
                    SectionId = SectionIds[sectionIndex],
                    Exam101ModelId = GoodModelA,
                    Exam102ModelId = GoodModelB,
                    ExamDurationMinutes = 30
                });
                Assert.True(r.Success, r.Message);
                return (int)r.Data!;
            }

            public async Task<int> AddVideoAsync(int axisId, string title, string url = "https://youtu.be/dQw4w9WgXcQ", int? duration = null)
            {
                var r = await Sut.AddVideoAsync(new AddRemedialVideoInput { AxisId = axisId, Title = title, Url = url, DurationSeconds = duration });
                Assert.True(r.Success, r.Message);
                return (int)r.Data!;
            }

            public async Task LockAsync(int trackId)
            {
                await using var db = Factory.CreateDbContext();
                var t = await db.RemedialTracks.FirstAsync(x => x.Id == trackId);
                t.IsStructureLocked = true;
                await db.SaveChangesAsync();
            }
        }

        private static async Task<Fixture> NewAsync()
        {
            var f = new Fixture();
            await f.SeedAsync();
            return f;
        }

        // ---------------- جدول الأيام ----------------

        [Fact]
        public async Task Schedule_NewAxisInheritsLastDay_SaveValidatesAndPersists()
        {
            var f = await NewAsync();
            var t = await f.CreateTrackAsync();
            var a1 = await f.AddAxisAsync(t, 0);
            var a2 = await f.AddAxisAsync(t, 1);
            var a3 = await f.AddAxisAsync(t, 2);

            // يوم فارغ (1، 3) مرفوض
            var gap = await f.Sut.SaveScheduleAsync(new SaveRemedialScheduleInput
            {
                TrackId = t,
                Items = { new() { AxisId = a1, Day = 1 }, new() { AxisId = a2, Day = 3 }, new() { AxisId = a3, Day = 3 } }
            });
            Assert.False(gap.Success);

            // محور ناقص مرفوض
            var missing = await f.Sut.SaveScheduleAsync(new SaveRemedialScheduleInput
            {
                TrackId = t,
                Items = { new() { AxisId = a1, Day = 1 }, new() { AxisId = a2, Day = 2 } }
            });
            Assert.False(missing.Success);

            var ok = await f.Sut.SaveScheduleAsync(new SaveRemedialScheduleInput
            {
                TrackId = t,
                Items = { new() { AxisId = a1, Day = 1 }, new() { AxisId = a2, Day = 2 }, new() { AxisId = a3, Day = 2 } }
            });
            Assert.True(ok.Success, ok.Message);

            await using var db = f.Factory.CreateDbContext();
            var days = await db.RemedialTrackAxes.Where(a => a.TrackId == t).OrderBy(a => a.Order).Select(a => a.ReleaseDay).ToListAsync();
            Assert.Equal(new[] { 1, 2, 2 }, days);
        }

        [Fact]
        public async Task Schedule_RemoveAxis_CompactsDays_AndLockedStructureRejectsSave()
        {
            var f = await NewAsync();
            var t = await f.CreateTrackAsync();
            var a1 = await f.AddAxisAsync(t, 0);
            var a2 = await f.AddAxisAsync(t, 1);
            var a3 = await f.AddAxisAsync(t, 2);
            Assert.True((await f.Sut.SaveScheduleAsync(new SaveRemedialScheduleInput
            {
                TrackId = t,
                Items = { new() { AxisId = a1, Day = 1 }, new() { AxisId = a2, Day = 2 }, new() { AxisId = a3, Day = 3 } }
            })).Success);

            Assert.True((await f.Sut.RemoveAxisAsync(a2)).Success);   // اليوم 2 يصبح فارغًا → يُضغط

            await using (var db = f.Factory.CreateDbContext())
            {
                var days = await db.RemedialTrackAxes.Where(a => a.TrackId == t).OrderBy(a => a.Order).Select(a => a.ReleaseDay).ToListAsync();
                Assert.Equal(new[] { 1, 2 }, days);
            }

            await f.LockAsync(t);
            var locked = await f.Sut.SaveScheduleAsync(new SaveRemedialScheduleInput
            {
                TrackId = t,
                Items = { new() { AxisId = a1, Day = 1 }, new() { AxisId = a3, Day = 1 } }
            });
            Assert.False(locked.Success);
        }

        // ---------------- الإنشاء والترويسة ----------------

        [Fact]
        public async Task Create_SetsDefaults_Code_AndDraft()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();

            await using var db = f.Factory.CreateDbContext();
            var t = await db.RemedialTracks.SingleAsync(x => x.Id == id);
            Assert.Equal("RTK-2026-0001", t.Code);
            Assert.Equal(RemedialTrackStatus.Draft, t.Status);
            Assert.Equal(60, t.PassPercent);
            Assert.Equal(90, t.MinWatchPercent);
            Assert.False(t.IsStructureLocked);
            Assert.Equal("admin-1", t.CreatedByUserId);
        }

        [Fact]
        public async Task Create_SecondTrack_GetsNextCode()
        {
            var f = await NewAsync();
            await f.CreateTrackAsync();
            var id2 = await f.CreateTrackAsync();

            await using var db = f.Factory.CreateDbContext();
            Assert.Equal("RTK-2026-0002", (await db.RemedialTracks.SingleAsync(x => x.Id == id2)).Code);
        }

        [Fact]
        public async Task Create_RejectsUnknownCurriculum_AndBlankTitle()
        {
            var f = await NewAsync();

            var noCur = await f.Sut.CreateAsync(new CreateRemedialTrackInput { Title = "x", CurriculumId = 9999 }, Actor);
            var noTitle = await f.Sut.CreateAsync(new CreateRemedialTrackInput { Title = "  ", CurriculumId = f.CurriculumId }, Actor);

            Assert.False(noCur.Success);
            Assert.False(noTitle.Success);
        }

        [Fact]
        public async Task EditHeader_BeforeLock_ChangesAll()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();

            var r = await f.Sut.EditHeaderAsync(new EditRemedialTrackHeaderInput
            {
                Id = id, Title = "جديد", Description = "وصف",
                CurriculumId = f.OtherCurriculumId, PassPercent = 70, MinWatchPercent = 80
            });

            Assert.True(r.Success, r.Message);
            await using var db = f.Factory.CreateDbContext();
            var t = await db.RemedialTracks.SingleAsync();
            Assert.Equal("جديد", t.Title);
            Assert.Equal(f.OtherCurriculumId, t.CurriculumId);
            Assert.Equal(70, t.PassPercent);
            Assert.Equal(80, t.MinWatchPercent);
        }

        [Fact]
        public async Task EditHeader_AfterLock_AllowsNameOnly_AndRejectsStructuralFields()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            await f.LockAsync(id);

            var nameOnly = await f.Sut.EditHeaderAsync(new EditRemedialTrackHeaderInput { Id = id, Title = "اسم جديد", Description = "d" });
            var passChange = await f.Sut.EditHeaderAsync(new EditRemedialTrackHeaderInput { Id = id, Title = "اسم جديد", PassPercent = 75 });
            var sameValues = await f.Sut.EditHeaderAsync(new EditRemedialTrackHeaderInput { Id = id, Title = "ثالث", PassPercent = 60, CurriculumId = f.CurriculumId });

            Assert.True(nameOnly.Success);
            Assert.False(passChange.Success);
            Assert.True(sameValues.Success);   // قيم مطابقة للحالية ليست تغييرًا
        }

        [Fact]
        public async Task EditHeader_CurriculumChange_RejectedWhenAxesExist()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            await f.AddAxisAsync(id, 0);

            var r = await f.Sut.EditHeaderAsync(new EditRemedialTrackHeaderInput { Id = id, Title = "x", CurriculumId = f.OtherCurriculumId });

            Assert.False(r.Success);
        }

        // ---------------- المحاور ----------------

        [Fact]
        public async Task AddAxis_AssignsSequentialOrder_AndRejectsDuplicateSection()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();

            await f.AddAxisAsync(id, 0);
            await f.AddAxisAsync(id, 1);
            var dup = await f.Sut.AddAxisAsync(new AddRemedialAxisInput
            {
                TrackId = id, SectionId = f.SectionIds[0], Exam101ModelId = f.GoodModelA, Exam102ModelId = f.GoodModelB, ExamDurationMinutes = 30
            });

            Assert.False(dup.Success);
            await using var db = f.Factory.CreateDbContext();
            var orders = await db.RemedialTrackAxes.OrderBy(a => a.Order).Select(a => a.Order).ToListAsync();
            Assert.Equal(new[] { 1, 2 }, orders);
        }

        [Fact]
        public async Task AddAxis_RejectsSectionOfAnotherCurriculum()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();

            var r = await f.Sut.AddAxisAsync(new AddRemedialAxisInput
            {
                TrackId = id, SectionId = f.OtherSectionId, Exam101ModelId = f.GoodModelA, Exam102ModelId = f.GoodModelB, ExamDurationMinutes = 30
            });

            Assert.False(r.Success);
        }

        [Fact]
        public async Task MoveAxis_SwapsOrders_AndBlocksAtEdges()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var a1 = await f.AddAxisAsync(id, 0);
            var a2 = await f.AddAxisAsync(id, 1);
            var a3 = await f.AddAxisAsync(id, 2);

            Assert.False((await f.Sut.MoveAxisAsync(a1, -1)).Success);   // أعلى القائمة
            Assert.True((await f.Sut.MoveAxisAsync(a1, 1)).Success);
            Assert.False((await f.Sut.MoveAxisAsync(a3, 1)).Success);    // أسفل القائمة

            await using var db = f.Factory.CreateDbContext();
            var ordered = await db.RemedialTrackAxes.OrderBy(a => a.Order).Select(a => new { a.Id, a.Order }).ToListAsync();
            Assert.Equal(new[] { a2, a1, a3 }, ordered.Select(x => x.Id).ToArray());
            Assert.Equal(new[] { 1, 2, 3 }, ordered.Select(x => x.Order).ToArray());
        }

        [Fact]
        public async Task RemoveAxis_RenumbersRest_AndDeletesItsVideos()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var a1 = await f.AddAxisAsync(id, 0);
            var a2 = await f.AddAxisAsync(id, 1);
            var a3 = await f.AddAxisAsync(id, 2);
            await f.AddVideoAsync(a1, "v");

            var r = await f.Sut.RemoveAxisAsync(a1);

            Assert.True(r.Success, r.Message);
            await using var db = f.Factory.CreateDbContext();
            var rest = await db.RemedialTrackAxes.OrderBy(a => a.Order).Select(a => new { a.Id, a.Order }).ToListAsync();
            Assert.Equal(new[] { a2, a3 }, rest.Select(x => x.Id).ToArray());
            Assert.Equal(new[] { 1, 2 }, rest.Select(x => x.Order).ToArray());
            Assert.Empty(await db.RemedialTrackVideos.ToListAsync());
        }

        [Fact]
        public async Task StructuralOperations_AreBlockedAfterLock_D12()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var a1 = await f.AddAxisAsync(id, 0);
            var a2 = await f.AddAxisAsync(id, 1);
            var v1 = await f.AddVideoAsync(a1, "v1");
            await f.AddVideoAsync(a1, "v2");
            await f.LockAsync(id);

            Assert.False((await f.Sut.AddAxisAsync(new AddRemedialAxisInput { TrackId = id, SectionId = f.SectionIds[2], Exam101ModelId = f.GoodModelA, Exam102ModelId = f.GoodModelB, ExamDurationMinutes = 30 })).Success);
            Assert.False((await f.Sut.MoveAxisAsync(a2, -1)).Success);
            Assert.False((await f.Sut.RemoveAxisAsync(a2)).Success);
            Assert.False((await f.Sut.SaveAxisExamsAsync(new SaveRemedialAxisExamsInput { AxisId = a1, Exam101ModelId = f.GoodModelB, Exam102ModelId = f.GoodModelA, ExamDurationMinutes = 20 })).Success);
            Assert.False((await f.Sut.AddVideoAsync(new AddRemedialVideoInput { AxisId = a1, Title = "x", Url = "https://youtu.be/dQw4w9WgXcQ" })).Success);
            Assert.False((await f.Sut.MoveVideoAsync(v1, 1)).Success);
            Assert.False((await f.Sut.RemoveVideoAsync(v1)).Success);

            // تصحيح الفيديو مسموح بعد القفل
            var edit = await f.Sut.EditVideoAsync(new EditRemedialVideoInput { VideoId = v1, Title = "مصحّح", Url = "https://youtu.be/aaaaaaaaaaa" });
            Assert.True(edit.Success, edit.Message);
        }

        // ---------------- النماذج ----------------

        [Fact]
        public async Task SaveAxisExams_RejectsInvalidModels()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var axis = await f.AddAxisAsync(id, 0);

            async Task<RemedialTrackResult> Save(int m101, int m102, int minutes = 30)
                => await f.Sut.SaveAxisExamsAsync(new SaveRemedialAxisExamsInput { AxisId = axis, Exam101ModelId = m101, Exam102ModelId = m102, ExamDurationMinutes = minutes });

            Assert.False((await Save(f.GoodModelA, f.GoodModelA)).Success);          // نفس النموذج
            Assert.False((await Save(f.GoodModelA, f.BadAnswerModel)).Success);      // سؤال بلا إجابة
            Assert.False((await Save(f.GoodModelA, f.EmptyModel)).Success);          // بلا أسئلة
            Assert.False((await Save(f.GoodModelA, f.HomeworkModel)).Success);       // واجب لا اختبار
            Assert.False((await Save(f.GoodModelA, f.ArchivedModel)).Success);       // مؤرشف
            Assert.False((await Save(f.GoodModelA, 99999)).Success);                 // غير موجود
            Assert.False((await Save(f.GoodModelA, f.GoodModelB, 4)).Success);       // مدة قصيرة
            Assert.False((await Save(f.GoodModelA, f.GoodModelB, 181)).Success);     // مدة طويلة
        }

        [Fact]
        public async Task SaveAxisExams_OtherCurriculumModel_IsWarningNotError()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var axis = await f.AddAxisAsync(id, 0);

            var r = await f.Sut.SaveAxisExamsAsync(new SaveRemedialAxisExamsInput
            {
                AxisId = axis, Exam101ModelId = f.GoodModelA, Exam102ModelId = f.OtherCurriculumModel, ExamDurationMinutes = 25
            });

            Assert.True(r.Success, r.Message);
            Assert.NotNull(r.Warnings);
            Assert.Single(r.Warnings!);
        }

        // ---------------- الفيديوهات ----------------

        [Fact]
        public async Task AddVideo_ParsesProvider_AndStoresNormalizedUrl()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var axis = await f.AddAxisAsync(id, 0);

            var yt = await f.AddVideoAsync(axis, "yt", "https://youtu.be/dQw4w9WgXcQ?t=9");
            var vm = await f.AddVideoAsync(axis, "vm", "https://vimeo.com/123456789");
            var other = await f.AddVideoAsync(axis, "other", "https://videos.example.com/x", 120);

            await using var db = f.Factory.CreateDbContext();
            var rows = await db.RemedialTrackVideos.OrderBy(v => v.Order).ToListAsync();
            Assert.Equal(new[] { yt, vm, other }, rows.Select(v => v.Id).ToArray());
            Assert.Equal(new[] { 1, 2, 3 }, rows.Select(v => v.Order).ToArray());
            Assert.Equal(RemedialTrackVideoProvider.YouTube, rows[0].Provider);
            Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", rows[0].Url);
            Assert.Equal("dQw4w9WgXcQ", rows[0].ExternalId);
            Assert.Equal(RemedialTrackVideoProvider.Vimeo, rows[1].Provider);
            Assert.Equal(RemedialTrackVideoProvider.Other, rows[2].Provider);
            Assert.Equal(120, rows[2].DurationSeconds);
        }

        [Fact]
        public async Task AddVideo_OtherPlatformWithoutDuration_AndBadUrl_AreRejected()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var axis = await f.AddAxisAsync(id, 0);

            var noDuration = await f.Sut.AddVideoAsync(new AddRemedialVideoInput { AxisId = axis, Title = "x", Url = "https://videos.example.com/x" });
            var shortDuration = await f.Sut.AddVideoAsync(new AddRemedialVideoInput { AxisId = axis, Title = "x", Url = "https://videos.example.com/x", DurationSeconds = 5 });
            var badUrl = await f.Sut.AddVideoAsync(new AddRemedialVideoInput { AxisId = axis, Title = "x", Url = "javascript:alert(1)" });

            Assert.False(noDuration.Success);
            Assert.False(shortDuration.Success);
            Assert.False(badUrl.Success);
        }

        [Fact]
        public async Task MoveVideo_And_RemoveVideo_KeepOrdersContiguous()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var axis = await f.AddAxisAsync(id, 0);
            var v1 = await f.AddVideoAsync(axis, "1");
            var v2 = await f.AddVideoAsync(axis, "2");
            var v3 = await f.AddVideoAsync(axis, "3");

            Assert.True((await f.Sut.MoveVideoAsync(v3, -1)).Success);   // 1,3,2
            Assert.True((await f.Sut.RemoveVideoAsync(v1)).Success);     // 3,2

            await using var db = f.Factory.CreateDbContext();
            var rows = await db.RemedialTrackVideos.OrderBy(v => v.Order).ToListAsync();
            Assert.Equal(new[] { v3, v2 }, rows.Select(v => v.Id).ToArray());
            Assert.Equal(new[] { 1, 2 }, rows.Select(v => v.Order).ToArray());
        }

        // ---------------- MarkReady / Archive / Duplicate ----------------

        [Fact]
        public async Task MarkReady_FailsWithReasons_WhenIncomplete()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();

            var empty = await f.Sut.MarkReadyAsync(id);
            Assert.False(empty.Success);
            Assert.NotEmpty((IReadOnlyList<string>)empty.Data!);

            await f.AddAxisAsync(id, 0);   // محور بلا فيديو
            var noVideo = await f.Sut.MarkReadyAsync(id);
            Assert.False(noVideo.Success);
            Assert.Contains(((IReadOnlyList<string>)noVideo.Data!), s => s.Contains("فيديو"));
        }

        [Fact]
        public async Task MarkReady_Succeeds_ForTwoAxesThreeVideosEach_AndStructureChangeReturnsToDraft()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var a1 = await f.AddAxisAsync(id, 0);
            var a2 = await f.AddAxisAsync(id, 1);
            foreach (var a in new[] { a1, a2 })
                for (var i = 1; i <= 3; i++)
                    await f.AddVideoAsync(a, $"v{i}");

            var ready = await f.Sut.MarkReadyAsync(id);
            Assert.True(ready.Success, ready.Message);
            await using (var db = f.Factory.CreateDbContext())
                Assert.Equal(RemedialTrackStatus.Ready, (await db.RemedialTracks.SingleAsync()).Status);

            // تعديل بنيوي على خطة Ready غير مقفلة يعيدها Draft
            await f.AddVideoAsync(a1, "extra");
            await using (var db = f.Factory.CreateDbContext())
                Assert.Equal(RemedialTrackStatus.Draft, (await db.RemedialTracks.SingleAsync()).Status);
        }

        [Fact]
        public async Task MarkReady_FailsWhenModelBecameInvalid_AfterAssignment()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var a1 = await f.AddAxisAsync(id, 0);
            await f.AddVideoAsync(a1, "v");

            await using (var db = f.Factory.CreateDbContext())
            {
                var model = await db.ProfessionalModels.SingleAsync(m => m.Id == f.GoodModelA);
                model.IsArchived = true;
                await db.SaveChangesAsync();
            }

            var r = await f.Sut.MarkReadyAsync(id);

            Assert.False(r.Success);
            Assert.Contains((IReadOnlyList<string>)r.Data!, s => s.Contains("101"));
        }

        [Fact]
        public async Task Archive_BlockedByActivePublication_ThenSucceedsAndLocksEditing()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();

            await using (var db = f.Factory.CreateDbContext())
            {
                db.RemedialTrackPublications.Add(new RemedialTrackPublication
                {
                    TrackId = id, BatchId = 1, Scope = RemedialTrackPublicationScope.WholeBatch,
                    Mode = RemedialTrackDeliveryMode.Online, PublishAtUtc = DateTime.UtcNow,
                    Status = RemedialTrackPublicationStatus.Active, CreatedByUserId = "u", CreatedAtUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            Assert.False((await f.Sut.ArchiveAsync(id)).Success);

            await using (var db = f.Factory.CreateDbContext())
            {
                var p = await db.RemedialTrackPublications.SingleAsync();
                p.Status = RemedialTrackPublicationStatus.Cancelled;
                await db.SaveChangesAsync();
            }

            Assert.True((await f.Sut.ArchiveAsync(id)).Success);
            Assert.False((await f.Sut.EditHeaderAsync(new EditRemedialTrackHeaderInput { Id = id, Title = "x" })).Success);
        }

        [Fact]
        public async Task Duplicate_CopiesAxesVideosAndModels_AsUnlockedDraftWithNewCode()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var a1 = await f.AddAxisAsync(id, 0);
            var a2 = await f.AddAxisAsync(id, 1);
            await f.AddVideoAsync(a1, "v1");
            await f.AddVideoAsync(a1, "v2");
            await f.AddVideoAsync(a2, "v3");
            await f.LockAsync(id);

            var r = await f.Sut.DuplicateAsync(id, Actor);

            Assert.True(r.Success, r.Message);
            var copyId = (int)r.Data!;
            Assert.NotEqual(id, copyId);

            await using var db = f.Factory.CreateDbContext();
            var copy = await db.RemedialTracks.SingleAsync(t => t.Id == copyId);
            Assert.Equal("RTK-2026-0002", copy.Code);
            Assert.Equal(RemedialTrackStatus.Draft, copy.Status);
            Assert.False(copy.IsStructureLocked);
            Assert.StartsWith("نسخة من", copy.Title);

            var axes = await db.RemedialTrackAxes.Where(a => a.TrackId == copyId).OrderBy(a => a.Order).ToListAsync();
            Assert.Equal(2, axes.Count);
            Assert.All(axes, a => Assert.Equal(f.GoodModelA, a.Exam101ModelId));
            Assert.Equal(new[] { 2, 1 }, axes.Select(a => db.RemedialTrackVideos.Count(v => v.AxisId == a.Id)).ToArray());
            Assert.Equal(3, await db.RemedialTrackVideos.CountAsync(v => v.AxisId == axes[0].Id || v.AxisId == axes[1].Id));

            // الأصل لم يتغيّر
            Assert.Equal(2, await db.RemedialTrackAxes.CountAsync(a => a.TrackId == id));
            Assert.True((await db.RemedialTracks.SingleAsync(t => t.Id == id)).IsStructureLocked);
        }

        // ---------------- القراءة ----------------

        [Fact]
        public async Task GetBuilder_ReturnsAxesWithVideosAndAvailableSections()
        {
            var f = await NewAsync();
            var id = await f.CreateTrackAsync();
            var a1 = await f.AddAxisAsync(id, 0);
            await f.AddVideoAsync(a1, "v1");
            await f.AddVideoAsync(a1, "v2", "https://videos.example.com/x", 60);

            var vm = await f.Sut.GetBuilderAsync(id);

            Assert.NotNull(vm);
            Assert.Single(vm!.Axes);
            Assert.Equal(2, vm.Axes[0].Videos.Count);
            Assert.NotNull(vm.Axes[0].Videos[0].EmbedUrl);   // YouTube
            Assert.Null(vm.Axes[0].Videos[1].EmbedUrl);      // منصة أخرى: لا iframe في الأدمن
            Assert.Equal(2, vm.AvailableSections.Count);     // المحور المضاف مستبعد
            Assert.DoesNotContain(vm.AvailableSections, s => s.Id == f.SectionIds[0]);
            Assert.DoesNotContain(vm.ExamModels, m => m.Id == f.HomeworkModel || m.Id == f.ArchivedModel);
            Assert.Null(await f.Sut.GetBuilderAsync(99999));
        }

        [Fact]
        public async Task GetIndex_FiltersAndPages()
        {
            var f = await NewAsync();
            await f.CreateTrackAsync();
            var id2 = await f.CreateTrackAsync();
            await f.Sut.ArchiveAsync(id2);

            var all = await f.Sut.GetIndexAsync(new RemedialTrackIndexFilter());
            var archived = await f.Sut.GetIndexAsync(new RemedialTrackIndexFilter { Status = RemedialTrackStatus.Archived });
            var bySearch = await f.Sut.GetIndexAsync(new RemedialTrackIndexFilter { Search = "0002" });
            var wrongCur = await f.Sut.GetIndexAsync(new RemedialTrackIndexFilter { CurriculumId = f.OtherCurriculumId });

            Assert.Equal(2, all.Total);
            Assert.Single(archived.Items);
            Assert.Single(bySearch.Items);
            Assert.Empty(wrongCur.Items);
        }
    }
}
