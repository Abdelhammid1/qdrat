using QdratNew.Enums;
using QdratNew.Services.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>RTK-S1.4: اختبارات جدولية لآلة الحالة النقية.</summary>
    public class RemedialTrackStateMachineTests
    {
        private const RemedialTrackAxisStatus Videos = RemedialTrackAxisStatus.Videos;
        private const RemedialTrackAxisStatus Rewatch = RemedialTrackAxisStatus.Rewatch;
        private const RemedialTrackAxisStatus Await101 = RemedialTrackAxisStatus.AwaitingExam101;
        private const RemedialTrackAxisStatus Await102 = RemedialTrackAxisStatus.AwaitingExam102;
        private const RemedialTrackAxisStatus Passed = RemedialTrackAxisStatus.Passed;
        private const RemedialTrackAxisStatus Blocked = RemedialTrackAxisStatus.FailedBlocked;
        private const RemedialTrackAxisStatus Opened = RemedialTrackAxisStatus.FailedOpenedByAdmin;
        private const RemedialTrackAxisStatus Locked = RemedialTrackAxisStatus.Locked;

        // ===== OnAllVideosCompleted =====
        [Theory]
        [InlineData(RemedialTrackAxisStatus.Videos, 1, RemedialTrackAxisStatus.AwaitingExam101, 1)]
        [InlineData(RemedialTrackAxisStatus.Rewatch, 2, RemedialTrackAxisStatus.AwaitingExam102, 2)]
        public void OnAllVideosCompleted_ValidRows(RemedialTrackAxisStatus current, int round, RemedialTrackAxisStatus expected, int expectedRound)
        {
            var t = RemedialTrackStateMachine.OnAllVideosCompleted(current, round);

            Assert.Equal(expected, t.NewStatus);
            Assert.Equal(expectedRound, t.NewRound);
            Assert.False(t.StartRewatchRound);
            Assert.False(t.RecordNotPassed);
            Assert.False(t.UnlockNextAxis);
        }

        [Theory]
        [InlineData(RemedialTrackAxisStatus.Locked, 1)]
        [InlineData(RemedialTrackAxisStatus.Videos, 2)]      // جولة خاطئة
        [InlineData(RemedialTrackAxisStatus.Rewatch, 1)]     // جولة خاطئة
        [InlineData(RemedialTrackAxisStatus.AwaitingExam101, 1)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam102, 2)]
        [InlineData(RemedialTrackAxisStatus.Passed, 1)]
        [InlineData(RemedialTrackAxisStatus.FailedBlocked, 2)]
        [InlineData(RemedialTrackAxisStatus.FailedOpenedByAdmin, 2)]
        public void OnAllVideosCompleted_InvalidRows_Throw(RemedialTrackAxisStatus current, int round)
        {
            Assert.Throws<InvalidOperationException>(() => RemedialTrackStateMachine.OnAllVideosCompleted(current, round));
        }

        // ===== OnExamSubmitted =====
        [Theory]
        // (الحالة، الاختبار، نجح؟) → (الحالة الجديدة، الجولة، جولة إعادة، عدم اجتياز، فتح التالي)
        [InlineData(RemedialTrackAxisStatus.AwaitingExam101, RemedialTrackExamNumber.Exam101, true,  RemedialTrackAxisStatus.Passed,        1, false, false, true)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam101, RemedialTrackExamNumber.Exam101, false, RemedialTrackAxisStatus.Rewatch,       2, true,  false, false)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam102, RemedialTrackExamNumber.Exam102, true,  RemedialTrackAxisStatus.Passed,        2, false, false, true)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam102, RemedialTrackExamNumber.Exam102, false, RemedialTrackAxisStatus.FailedBlocked, 2, false, true,  false)]
        public void OnExamSubmitted_ValidRows(
            RemedialTrackAxisStatus current, RemedialTrackExamNumber exam, bool passed,
            RemedialTrackAxisStatus expected, int expectedRound, bool rewatch, bool notPassed, bool unlockNext)
        {
            var t = RemedialTrackStateMachine.OnExamSubmitted(current, exam, passed);

            Assert.Equal(expected, t.NewStatus);
            Assert.Equal(expectedRound, t.NewRound);
            Assert.Equal(rewatch, t.StartRewatchRound);
            Assert.Equal(notPassed, t.RecordNotPassed);
            Assert.Equal(unlockNext, t.UnlockNextAxis);
        }

        [Theory]
        // اختبار لا يطابق الحالة
        [InlineData(RemedialTrackAxisStatus.AwaitingExam101, RemedialTrackExamNumber.Exam102, true)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam101, RemedialTrackExamNumber.Exam102, false)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam102, RemedialTrackExamNumber.Exam101, true)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam102, RemedialTrackExamNumber.Exam101, false)]
        // حالات لا يُسلَّم منها اختبار
        [InlineData(RemedialTrackAxisStatus.Locked, RemedialTrackExamNumber.Exam101, true)]
        [InlineData(RemedialTrackAxisStatus.Videos, RemedialTrackExamNumber.Exam101, true)]
        [InlineData(RemedialTrackAxisStatus.Rewatch, RemedialTrackExamNumber.Exam102, true)]
        [InlineData(RemedialTrackAxisStatus.Passed, RemedialTrackExamNumber.Exam101, true)]
        [InlineData(RemedialTrackAxisStatus.Passed, RemedialTrackExamNumber.Exam102, false)]
        [InlineData(RemedialTrackAxisStatus.FailedBlocked, RemedialTrackExamNumber.Exam102, false)]
        [InlineData(RemedialTrackAxisStatus.FailedOpenedByAdmin, RemedialTrackExamNumber.Exam102, true)]
        public void OnExamSubmitted_InvalidRows_Throw(RemedialTrackAxisStatus current, RemedialTrackExamNumber exam, bool passed)
        {
            Assert.Throws<InvalidOperationException>(() => RemedialTrackStateMachine.OnExamSubmitted(current, exam, passed));
        }

        // ===== OnAdminOpenNext =====
        [Fact]
        public void OnAdminOpenNext_FromFailedBlocked_OpensNext()
        {
            var t = RemedialTrackStateMachine.OnAdminOpenNext(Blocked);

            Assert.Equal(Opened, t.NewStatus);
            Assert.Equal(2, t.NewRound);
            Assert.False(t.StartRewatchRound);
            Assert.False(t.RecordNotPassed);
            Assert.True(t.UnlockNextAxis);
        }

        [Theory]
        [InlineData(RemedialTrackAxisStatus.Locked)]
        [InlineData(RemedialTrackAxisStatus.Videos)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam101)]
        [InlineData(RemedialTrackAxisStatus.Rewatch)]
        [InlineData(RemedialTrackAxisStatus.AwaitingExam102)]
        [InlineData(RemedialTrackAxisStatus.Passed)]
        [InlineData(RemedialTrackAxisStatus.FailedOpenedByAdmin)]
        public void OnAdminOpenNext_FromAnyOtherStatus_Throws(RemedialTrackAxisStatus current)
        {
            Assert.Throws<InvalidOperationException>(() => RemedialTrackStateMachine.OnAdminOpenNext(current));
        }

        // ===== ResolveEnrollmentStatus =====
        public static IEnumerable<object[]> EnrollmentCases()
        {
            yield return new object[] { Array.Empty<RemedialTrackAxisStatus>(), RemedialTrackEnrollmentStatus.NotStarted };
            // لم تبدأ: كل المحاور مغلقة
            yield return new object[] { new[] { Locked, Locked }, RemedialTrackEnrollmentStatus.NotStarted };
            // قيد التنفيذ
            yield return new object[] { new[] { Videos, Locked }, RemedialTrackEnrollmentStatus.InProgress };
            yield return new object[] { new[] { Passed, Await101 }, RemedialTrackEnrollmentStatus.InProgress };
            yield return new object[] { new[] { Passed, Passed, Rewatch }, RemedialTrackEnrollmentStatus.InProgress };
            // محور وسط فشل ثم فُتح التالي، والأخير لم ينتهِ
            yield return new object[] { new[] { Opened, Await102 }, RemedialTrackEnrollmentStatus.InProgress };
            // محور وسط FailedBlocked والأخير Locked → ما زال قيد التنفيذ (بانتظار الإدارة)
            yield return new object[] { new[] { Blocked, Locked }, RemedialTrackEnrollmentStatus.InProgress };
            // كل المحاور Passed
            yield return new object[] { new[] { Passed }, RemedialTrackEnrollmentStatus.Completed };
            yield return new object[] { new[] { Passed, Passed, Passed }, RemedialTrackEnrollmentStatus.Completed };
            // آخر محور FailedBlocked
            yield return new object[] { new[] { Passed, Blocked }, RemedialTrackEnrollmentStatus.CompletedWithFailures };
            yield return new object[] { new[] { Blocked }, RemedialTrackEnrollmentStatus.CompletedWithFailures };
            // محور وسط FailedOpenedByAdmin ثم آخر Passed
            yield return new object[] { new[] { Passed, Opened, Passed }, RemedialTrackEnrollmentStatus.CompletedWithFailures };
            // آخر محور FailedOpenedByAdmin
            yield return new object[] { new[] { Passed, Opened }, RemedialTrackEnrollmentStatus.CompletedWithFailures };
        }

        [Theory]
        [MemberData(nameof(EnrollmentCases))]
        public void ResolveEnrollmentStatus_Table(RemedialTrackAxisStatus[] axes, RemedialTrackEnrollmentStatus expected)
        {
            Assert.Equal(expected, RemedialTrackStateMachine.ResolveEnrollmentStatus(axes));
        }

        // ===== مسار كامل عبر الآلة =====
        [Fact]
        public void FullPath_FailBoth_Block_AdminOpens_ThenNextPasses()
        {
            var a1 = RemedialTrackStateMachine.OnAllVideosCompleted(Videos, 1);
            var a2 = RemedialTrackStateMachine.OnExamSubmitted(a1.NewStatus, RemedialTrackExamNumber.Exam101, false);
            Assert.True(a2.StartRewatchRound);
            var a3 = RemedialTrackStateMachine.OnAllVideosCompleted(a2.NewStatus, a2.NewRound);
            var a4 = RemedialTrackStateMachine.OnExamSubmitted(a3.NewStatus, RemedialTrackExamNumber.Exam102, false);
            Assert.Equal(Blocked, a4.NewStatus);
            Assert.True(a4.RecordNotPassed);

            var a5 = RemedialTrackStateMachine.OnAdminOpenNext(a4.NewStatus);
            Assert.True(a5.UnlockNextAxis);

            // المحور التالي يُفتح Videos/جولة 1 ثم ينجح من 101
            var b1 = RemedialTrackStateMachine.OnAllVideosCompleted(Videos, 1);
            var b2 = RemedialTrackStateMachine.OnExamSubmitted(b1.NewStatus, RemedialTrackExamNumber.Exam101, true);
            Assert.Equal(Passed, b2.NewStatus);

            Assert.Equal(
                RemedialTrackEnrollmentStatus.CompletedWithFailures,
                RemedialTrackStateMachine.ResolveEnrollmentStatus(new[] { a5.NewStatus, b2.NewStatus }));
        }
    }
}
