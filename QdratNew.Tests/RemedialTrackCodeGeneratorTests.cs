using Microsoft.EntityFrameworkCore;
using QdratNew.Data;
using QdratNew.Entities;
using QdratNew.Services.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>RTK-S1.5: مولّد كود الخطة والرقم المرجعي.</summary>
    public class RemedialTrackCodeGeneratorTests
    {
        private sealed class FixedTime : TimeProvider
        {
            private readonly DateTimeOffset _now;
            public FixedTime(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
        }

        private static RemedialTrackCodeGenerator Create(string db, DateTimeOffset now)
            => new(new TestDbContextFactory(db), new FixedTime(now));

        private static RemedialTrack Track(string code) => new()
        {
            Code = code,
            Title = "t",
            CurriculumId = 1,
            CreatedByUserId = "u"
        };

        [Fact]
        public void NewAccessCode_IsAlwaysSixDigits()
        {
            var gen = Create(Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);

            for (var i = 0; i < 2000; i++)
            {
                var code = gen.NewAccessCode();
                Assert.Equal(6, code.Length);
                Assert.True(int.TryParse(code, out var n));
                Assert.InRange(n, 100000, 999999);
            }
        }

        [Fact]
        public async Task NextTrackCode_FirstOfYear_IsOne()
        {
            var gen = Create(Guid.NewGuid().ToString(), new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero));

            Assert.Equal("RTK-2026-0001", await gen.NextTrackCodeAsync());
        }

        [Fact]
        public async Task NextTrackCode_IncrementsWithinYear_AndIgnoresOtherYears()
        {
            var name = Guid.NewGuid().ToString();
            var factory = new TestDbContextFactory(name);
            await using (var db = factory.CreateDbContext())
            {
                db.RemedialTracks.AddRange(Track("RTK-2026-0001"), Track("RTK-2026-0009"), Track("RTK-2025-0500"));
                await db.SaveChangesAsync();
            }

            var gen = new RemedialTrackCodeGenerator(factory, new FixedTime(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.Equal("RTK-2026-0010", await gen.NextTrackCodeAsync());

            var genNextYear = new RemedialTrackCodeGenerator(factory, new FixedTime(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            Assert.Equal("RTK-2027-0001", await genNextYear.NextTrackCodeAsync());
        }
    }
}
