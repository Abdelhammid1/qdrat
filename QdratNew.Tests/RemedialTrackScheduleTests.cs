using QdratNew.Enums;
using QdratNew.Services.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>جدولة المحاور على الأيام: دوال نقية (التحقق، الضغط، المحور المستحق).</summary>
    public class RemedialTrackScheduleTests
    {
        private static readonly DateTime Publish = new(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData(new[] { 1 }, true)]
        [InlineData(new[] { 1, 1, 2, 3, 3 }, true)]
        [InlineData(new[] { 2, 3 }, false)]       // لا يبدأ من 1
        [InlineData(new[] { 1, 3 }, false)]       // يوم فارغ
        [InlineData(new[] { 1, 2, 1 }, false)]    // تناقص
        [InlineData(new[] { 1, 61 }, false)]      // خارج النطاق
        [InlineData(new[] { 0, 1 }, false)]
        public void Validate_Rules(int[] days, bool ok)
            => Assert.Equal(ok, RemedialTrackSchedule.Validate(days).Count == 0);

        [Fact]
        public void Compact_RemovesEmptyDays()
            => Assert.Equal(new[] { 1, 1, 2, 3 }, RemedialTrackSchedule.Compact(new[] { 1, 1, 3, 5 }));

        [Fact]
        public void ReleaseAt_Day1IsPublishTime_DayNAddsDays()
        {
            Assert.Equal(Publish, RemedialTrackSchedule.ReleaseAtUtc(Publish, 1));
            Assert.Equal(Publish.AddDays(2), RemedialTrackSchedule.ReleaseAtUtc(Publish, 3));
        }

        private static RemedialTrackSchedule.AxisRow Row(int order, RemedialTrackAxisStatus st, int day)
            => new(order * 10, order, st, day);

        [Fact]
        public void FindDue_NextAxisOpensOnlyAfterPreviousFinishedAndDayArrived()
        {
            var rows = new[]
            {
                Row(1, RemedialTrackAxisStatus.Passed, 1),
                Row(2, RemedialTrackAxisStatus.Locked, 2),
                Row(3, RemedialTrackAxisStatus.Locked, 3)
            };

            Assert.Null(RemedialTrackSchedule.FindDue(rows, Publish, Publish.AddHours(5)));            // اليوم 2 لم يحن
            var due = RemedialTrackSchedule.FindDue(rows, Publish, Publish.AddDays(1));
            Assert.Equal(2, due!.Value.Order);
            Assert.Equal(2, RemedialTrackSchedule.FindDue(rows, Publish, Publish.AddDays(9))!.Value.Order); // لا يقفز للثالث
        }

        [Theory]
        [InlineData(RemedialTrackAxisStatus.Videos)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam101)]
        [InlineData(RemedialTrackAxisStatus.Rewatch)]
        [InlineData(RemedialTrackAxisStatus.FailedBlocked)]     // بانتظار الإدارة — لا فتح تلقائي
        public void FindDue_PreviousNotFinished_ReturnsNull(RemedialTrackAxisStatus prev)
        {
            var rows = new[] { Row(1, prev, 1), Row(2, RemedialTrackAxisStatus.Locked, 2) };
            Assert.Null(RemedialTrackSchedule.FindDue(rows, Publish, Publish.AddDays(30)));
        }

        [Fact]
        public void FindDue_AdminOpenedPrevious_CountsAsFinished()
        {
            var rows = new[] { Row(1, RemedialTrackAxisStatus.FailedOpenedByAdmin, 1), Row(2, RemedialTrackAxisStatus.Locked, 2) };
            Assert.NotNull(RemedialTrackSchedule.FindDue(rows, Publish, Publish.AddDays(1)));
        }
    }
}
