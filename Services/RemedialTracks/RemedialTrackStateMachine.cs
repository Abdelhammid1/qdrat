using QdratNew.Enums;

namespace QdratNew.Services.RemedialTracks
{
    public readonly record struct AxisTransition(
        RemedialTrackAxisStatus NewStatus,
        int NewRound,
        bool StartRewatchRound,        // أنشئ VideoProgress للجولة 2
        bool RecordNotPassed,          // اكتب حدث AxisNotPassed
        bool UnlockNextAxis);          // افتح المحور التالي (Videos / الجولة 1)

    /// <summary>
    /// RTK-S1.4: آلة الحالة لكل (طالب × محور) — دوال نقية بلا DbContext كي تُختبر جدوليًا.
    /// أي انتقال غير مسموح يرمي InvalidOperationException (تحوّله الـ Controllers إلى 409).
    /// </summary>
    public static class RemedialTrackStateMachine
    {
        public static AxisTransition OnAllVideosCompleted(RemedialTrackAxisStatus current, int round) => current switch
        {
            RemedialTrackAxisStatus.Videos  when round == 1 => new(RemedialTrackAxisStatus.AwaitingExam101, 1, false, false, false),
            RemedialTrackAxisStatus.Rewatch when round == 2 => new(RemedialTrackAxisStatus.AwaitingExam102, 2, false, false, false),
            _ => throw new InvalidOperationException($"لا يمكن إنهاء الفيديوهات من الحالة {current} (الجولة {round}).")
        };

        public static AxisTransition OnExamSubmitted(RemedialTrackAxisStatus current, RemedialTrackExamNumber exam, bool passed)
        {
            return (current, exam, passed) switch
            {
                (RemedialTrackAxisStatus.AwaitingExam101, RemedialTrackExamNumber.Exam101, true)  => new(RemedialTrackAxisStatus.Passed, 1, false, false, true),
                (RemedialTrackAxisStatus.AwaitingExam101, RemedialTrackExamNumber.Exam101, false) => new(RemedialTrackAxisStatus.Rewatch, 2, true,  false, false),
                (RemedialTrackAxisStatus.AwaitingExam102, RemedialTrackExamNumber.Exam102, true)  => new(RemedialTrackAxisStatus.Passed, 2, false, false, true),
                (RemedialTrackAxisStatus.AwaitingExam102, RemedialTrackExamNumber.Exam102, false) => new(RemedialTrackAxisStatus.FailedBlocked, 2, false, true, false),
                _ => throw new InvalidOperationException($"لا يمكن تسليم {exam} من الحالة {current}.")
            };
        }

        // الأدمن يفتح التالي: فقط من FailedBlocked
        public static AxisTransition OnAdminOpenNext(RemedialTrackAxisStatus current) =>
            current == RemedialTrackAxisStatus.FailedBlocked
                ? new(RemedialTrackAxisStatus.FailedOpenedByAdmin, 2, false, false, true)
                : throw new InvalidOperationException("فتح المحور التالي مسموح فقط لمحور رسب فيه الطالب في الاختبارين.");

        // حالة التسجيل بعد تغيّر محور
        public static RemedialTrackEnrollmentStatus ResolveEnrollmentStatus(
            IReadOnlyList<RemedialTrackAxisStatus> axesInOrder)
        {
            if (axesInOrder.Count == 0) return RemedialTrackEnrollmentStatus.NotStarted;
            var last = axesInOrder[^1];
            var lastFinished = last is RemedialTrackAxisStatus.Passed or RemedialTrackAxisStatus.FailedBlocked or RemedialTrackAxisStatus.FailedOpenedByAdmin;
            if (!lastFinished)
                return axesInOrder.All(s => s == RemedialTrackAxisStatus.Locked)
                    ? RemedialTrackEnrollmentStatus.NotStarted
                    : RemedialTrackEnrollmentStatus.InProgress;

            var anyFailed = axesInOrder.Any(s => s is RemedialTrackAxisStatus.FailedBlocked or RemedialTrackAxisStatus.FailedOpenedByAdmin);
            return anyFailed ? RemedialTrackEnrollmentStatus.CompletedWithFailures : RemedialTrackEnrollmentStatus.Completed;
        }
    }
}
