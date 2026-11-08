using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using QdratNew.Data;
using QdratNew.Data.Seeders;
using QdratNew.Entities;
using QdratNew.Enums;
using QdratNew.Services.Interfaces;
using QdratNew.Services.OnlineLecture;
using Xunit;

namespace QdratNew.Tests.OnlineLecture
{
    /// <summary>
    /// OLC-S1.4 — «حارس العقد»: يقرأ نموذج EF الفعلي (مزوّد SQL Server بلا اتصال) ويتحقق حرفيًا من
    /// العقد الموحَّد v1.1 (C.2/C.3): أسماء الجداول والأعمدة والـ Nullability والأطوال والقيم الافتراضية
    /// والفهارس الفريدة وسلوك الحذف وأرقام الـ Enums. أداة النقل في المشروع الجديد تطابق بالاسم حرفيًا،
    /// فأي تعديل هنا دون رفع إصدار العقد في الخطتين يجب أن يكسر هذا الاختبار.
    /// </summary>
    public class OnlineLectureContractGuardTests
    {
        // (العمود, CLR type بلا Nullable, Nullable?, MaxLength أو null)
        private sealed record Col(string Name, Type Clr, bool Nullable, int? MaxLength = null);

        private static readonly Col[] AuditColumns =
        {
            new("CreatedAt", typeof(DateTime), false),
            new("UpdatedAt", typeof(DateTime), true),
            new("CreatedBy", typeof(string), true, 450),
            new("UpdatedBy", typeof(string), true, 450),
            new("IsDeleted", typeof(bool), false),
            new("DeletedAt", typeof(DateTime), true),
        };

        private static readonly Col[] LectureContentsColumns = new Col[]
        {
            new("Id", typeof(int), false),
            new("LectureId", typeof(int), false),
            new("ContentMode", typeof(int), false),
            new("Status", typeof(int), false),
            new("Title", typeof(string), false, 200),
            new("Description", typeof(string), true, 1000),
            new("VideoProvider", typeof(int), true),
            new("VideoUrl", typeof(string), true, 500),
            new("VideoExternalId", typeof(string), true, 50),
            new("DurationSeconds", typeof(int), true),
            new("ThumbnailUrl", typeof(string), true, 500),
            new("PublishedAt", typeof(DateTime), true),
            new("PublishedByUserId", typeof(string), true, 450),
        }.Concat(AuditColumns).ToArray();

        private static readonly Col[] LectureContentItemsColumns = new Col[]
        {
            new("Id", typeof(int), false),
            new("LectureContentId", typeof(int), false),
            new("LessonId", typeof(int), false),
            new("SortOrder", typeof(int), false),
            new("Title", typeof(string), true, 200),
            new("StartSeconds", typeof(int), true),
            new("EndSeconds", typeof(int), true),
            new("SourceVideoKind", typeof(int), false),
            new("SourceVideoId", typeof(int), true),
            new("VideoProvider", typeof(int), true),
            new("VideoUrl", typeof(string), true, 500),
            new("VideoExternalId", typeof(string), true, 50),
            new("DurationSeconds", typeof(int), true),
        }.Concat(AuditColumns).ToArray();

        private static Col[] ProgressColumns(string ownerFk) => new Col[]
        {
            new("Id", typeof(int), false),
            new(ownerFk, typeof(int), false),
            new("StudentId", typeof(int), false),
            new("WatchedSeconds", typeof(double), false),
            new("MaxPositionSeconds", typeof(double), false),
            new("DurationSeconds", typeof(int), true),
            new("LastPingState", typeof(string), true, 10),
            new("FirstPingAtUtc", typeof(DateTime), true),
            new("LastPingAtUtc", typeof(DateTime), true),
            new("EndedSeen", typeof(bool), false),
            new("IsCompleted", typeof(bool), false),
            new("CompletedAtUtc", typeof(DateTime), true),
            new("CreatedAt", typeof(DateTime), false),
            new("UpdatedAt", typeof(DateTime), true),
        };

        /// <summary>نموذج التصميم الكامل (يحتفظ بالأطوال والقيم الافتراضية) بمزوّد SQL Server — لا يفتح اتصالًا.</summary>
        private static IModel BuildDesignModel()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer("Server=.;Database=OlcContractGuard_NoConnection;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;
            using var ctx = new ApplicationDbContext(options);
            return ctx.GetService<IDesignTimeModel>().Model;
        }

        private static readonly Lazy<IModel> Model = new(BuildDesignModel);

        private static IEntityType Entity<T>() =>
            Model.Value.FindEntityType(typeof(T)) ?? throw new Xunit.Sdk.XunitException($"{typeof(T).Name} غير مسجّل في ApplicationDbContext");

        private static void AssertExactColumns(IEntityType entity, string expectedTable, Col[] expected)
        {
            Assert.Equal(expectedTable, entity.GetTableName());
            var table = StoreObjectIdentifier.Table(expectedTable, entity.GetSchema());

            var actualColumns = entity.GetProperties().Select(p => p.GetColumnName(table)).OrderBy(n => n).ToArray();
            Assert.Equal(expected.Select(c => c.Name).OrderBy(n => n).ToArray(), actualColumns);

            foreach (var col in expected)
            {
                var p = entity.GetProperties().Single(x => x.GetColumnName(table) == col.Name);
                var providerClr = p.GetTypeMapping().Converter?.ProviderClrType ?? p.ClrType;
                var clr = Nullable.GetUnderlyingType(providerClr) ?? providerClr;
                Assert.True(col.Clr == clr, $"{expectedTable}.{col.Name}: النوع {clr.Name} بدل {col.Clr.Name}");
                Assert.True(col.Nullable == p.IsColumnNullable(table), $"{expectedTable}.{col.Name}: Nullable={p.IsColumnNullable(table)} بدل {col.Nullable}");
                Assert.True(col.MaxLength == p.GetMaxLength(), $"{expectedTable}.{col.Name}: الطول {p.GetMaxLength()} بدل {col.MaxLength}");
            }
        }

        private static void AssertUniqueIndex(IEntityType entity, bool unique, params string[] props)
        {
            var idx = entity.GetIndexes().SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(props));
            Assert.True(idx != null, $"{entity.GetTableName()}: الفهرس ({string.Join(", ", props)}) غير موجود");
            Assert.True(unique == idx!.IsUnique, $"{entity.GetTableName()}: الفهرس ({string.Join(", ", props)}) IsUnique={idx.IsUnique}");
            Assert.Null(idx.GetFilter()); // لا فلتر — الفهرس الفريد يجب أن يتصرف بنفس الطريقة في SQL Server وPostgreSQL
        }

        private static void AssertFk(IEntityType entity, string fkProp, Type principal, DeleteBehavior onDelete)
        {
            var fk = entity.GetForeignKeys().SingleOrDefault(f => f.Properties.Count == 1 && f.Properties[0].Name == fkProp);
            Assert.True(fk != null, $"{entity.GetTableName()}.{fkProp}: لا يوجد FK");
            Assert.Equal(principal, fk!.PrincipalEntityType.ClrType);
            Assert.Equal(onDelete, fk.DeleteBehavior);
        }

        [Fact]
        public void LectureContents_MatchesContract()
        {
            var e = Entity<LectureContent>();
            AssertExactColumns(e, "LectureContents", LectureContentsColumns);
            AssertUniqueIndex(e, true, "LectureId");
            AssertFk(e, "LectureId", typeof(Lecture), DeleteBehavior.Restrict);
            Assert.Equal(1, Convert.ToInt32(e.FindProperty("Status")!.GetDefaultValue()));
            Assert.Equal(false, e.FindProperty("IsDeleted")!.GetDefaultValue());
        }

        [Fact]
        public void LectureContentItems_MatchesContract()
        {
            var e = Entity<LectureContentItem>();
            AssertExactColumns(e, "LectureContentItems", LectureContentItemsColumns);
            AssertUniqueIndex(e, true, "LectureContentId", "LessonId");
            AssertUniqueIndex(e, false, "LectureContentId", "SortOrder");
            AssertFk(e, "LectureContentId", typeof(LectureContent), DeleteBehavior.Cascade);
            AssertFk(e, "LessonId", typeof(Lesson), DeleteBehavior.Restrict);
            Assert.Equal(0, Convert.ToInt32(e.FindProperty("SortOrder")!.GetDefaultValue()));
            Assert.Equal(0, Convert.ToInt32(e.FindProperty("SourceVideoKind")!.GetDefaultValue()));
            Assert.Equal(false, e.FindProperty("IsDeleted")!.GetDefaultValue());

            // SourceVideoId بلا FK عمدًا (العقد C.2)
            Assert.DoesNotContain(e.GetForeignKeys(), f => f.Properties.Any(p => p.Name == "SourceVideoId"));
        }

        [Fact]
        public void LectureContentProgress_MatchesContract()
        {
            var e = Entity<LectureContentProgress>();
            AssertExactColumns(e, "LectureContentProgress", ProgressColumns("LectureContentId"));
            AssertUniqueIndex(e, true, "LectureContentId", "StudentId");
            AssertFk(e, "LectureContentId", typeof(LectureContent), DeleteBehavior.Cascade);
            AssertFk(e, "StudentId", typeof(Student), DeleteBehavior.Restrict);
        }

        [Fact]
        public void LectureContentItemProgress_MatchesContract()
        {
            var e = Entity<LectureContentItemProgress>();
            AssertExactColumns(e, "LectureContentItemProgress", ProgressColumns("LectureContentItemId"));
            AssertUniqueIndex(e, true, "LectureContentItemId", "StudentId");
            AssertFk(e, "LectureContentItemId", typeof(LectureContentItem), DeleteBehavior.Cascade);
            AssertFk(e, "StudentId", typeof(Student), DeleteBehavior.Restrict);
        }

        [Fact]
        public void Batches_HasOnlineLearningColumns()
        {
            var e = Entity<Batch>();
            Assert.Equal("Batches", e.GetTableName());
            var table = StoreObjectIdentifier.Table("Batches", e.GetSchema());

            var flag = e.GetProperties().Single(p => p.GetColumnName(table) == "IsOnlineLearning");
            Assert.Equal(typeof(bool), flag.ClrType);
            Assert.False(flag.IsColumnNullable(table));
            Assert.Equal(false, flag.GetDefaultValue());

            var enabledAt = e.GetProperties().Single(p => p.GetColumnName(table) == "OnlineLearningEnabledAt");
            Assert.Equal(typeof(DateTime?), enabledAt.ClrType);
            Assert.True(enabledAt.IsColumnNullable(table));
        }

        [Fact]
        public void Enums_MatchContractNumbers()
        {
            Assert.Equal(new[] { 1, 2 }, Enum.GetValues<LectureContentMode>().Select(v => (int)v));
            Assert.Equal(1, (int)LectureContentMode.FullVideo);
            Assert.Equal(2, (int)LectureContentMode.IndicatorVideos);

            Assert.Equal(new[] { 1, 2, 3 }, Enum.GetValues<LectureContentStatus>().Select(v => (int)v));
            Assert.Equal(1, (int)LectureContentStatus.Draft);
            Assert.Equal(2, (int)LectureContentStatus.Published);
            Assert.Equal(3, (int)LectureContentStatus.Hidden);

            Assert.Equal(new[] { 3, 4 }, Enum.GetValues<LectureVideoProvider>().Select(v => (int)v));
            Assert.Equal(3, (int)LectureVideoProvider.YouTube);
            Assert.Equal(4, (int)LectureVideoProvider.Vimeo);

            Assert.Equal(new[] { 0, 1, 2 }, Enum.GetValues<LectureContentSourceKind>().Select(v => (int)v));
            Assert.Equal(0, (int)LectureContentSourceKind.Manual);
            Assert.Equal(1, (int)LectureContentSourceKind.RemedialVideo);
            Assert.Equal(2, (int)LectureContentSourceKind.LessonExplanationVideo);
        }

        [Fact]
        public void EnumColumns_AreStoredAsInt()
        {
            void AssertInt<T>(string prop)
            {
                var p = Entity<T>().FindProperty(prop)!;
                var provider = p.GetTypeMapping().Converter?.ProviderClrType ?? p.ClrType;
                Assert.Equal(typeof(int), Nullable.GetUnderlyingType(provider) ?? provider);
                Assert.Equal("int", p.GetColumnType());
            }

            AssertInt<LectureContent>(nameof(LectureContent.ContentMode));
            AssertInt<LectureContent>(nameof(LectureContent.Status));
            AssertInt<LectureContent>(nameof(LectureContent.VideoProvider));
            AssertInt<LectureContentItem>(nameof(LectureContentItem.SourceVideoKind));
            AssertInt<LectureContentItem>(nameof(LectureContentItem.VideoProvider));
        }
    }

    /// <summary>OLC-S1.3 — مفتاح الميزة: مغلق افتراضيًا، والقيمة الصريحة true فقط تفعّله.</summary>
    public class OnlineLectureFeatureServiceTests
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("false", false)]
        [InlineData("False", false)]
        [InlineData("garbage", false)]
        [InlineData("1", false)]
        [InlineData("true", true)]
        [InlineData(" True ", true)]
        public void Parse_OnlyExplicitTrueEnables(string? value, bool expected)
            => Assert.Equal(expected, OnlineLectureFeatureService.Parse(value));

        [Fact]
        public async Task IsEnabledAsync_MissingSetting_IsDisabled_AndCached()
        {
            var settings = new Mock<ISystemSettingService>();
            settings.Setup(s => s.GetAsync(OnlineLectureFeatureService.SettingKey)).ReturnsAsync((string?)null);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var sut = new OnlineLectureFeatureService(settings.Object, cache);

            Assert.False(await sut.IsEnabledAsync());
            Assert.False(await sut.IsEnabledAsync());
            settings.Verify(s => s.GetAsync(OnlineLectureFeatureService.SettingKey), Times.Once);
        }

        [Fact]
        public async Task IsEnabledAsync_ExplicitTrue_IsEnabled()
        {
            var settings = new Mock<ISystemSettingService>();
            settings.Setup(s => s.GetAsync(OnlineLectureFeatureService.SettingKey)).ReturnsAsync("true");
            using var cache = new MemoryCache(new MemoryCacheOptions());

            Assert.True(await new OnlineLectureFeatureService(settings.Object, cache).IsEnabledAsync());
        }

        [Fact]
        public void Seeder_AddsKeyAsFalse_AndIsIdempotent()
        {
            using var ctx = TestDbContextFactory.CreateFreshContext(out _);
            SystemSettingsSeeder.Seed(ctx);
            SystemSettingsSeeder.Seed(ctx);

            var rows = ctx.SystemSettings.Where(s => s.Key == OnlineLectureFeatureService.SettingKey).ToList();
            Assert.Single(rows);
            Assert.Equal("false", rows[0].Value);
        }
    }
}
