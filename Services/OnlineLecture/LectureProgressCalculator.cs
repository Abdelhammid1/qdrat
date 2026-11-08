namespace QdratNew.Services.OnlineLecture
{
    /// <summary>نتيجة حساب نبضة واحدة على محتوى/عنصر أونلاين (دالة نقية).</summary>
    public readonly record struct LectureProgressComputation(
        double WatchedSeconds,
        double DurationSeconds,
        double RequiredSeconds,
        bool Ended,
        bool Completed,
        double MaxPositionSeconds);

    /// <summary>
    /// OLC-S7.1 — نسخة من <c>RemedialTrackProgressService.ComputePing</c> بلا مزوّد (لا «Other» ولا manual-done في OLC)
    /// وبثوابت R9 من <see cref="LectureWatchRules"/>. دالة نقية بلا قاعدة بيانات ولا وقت — تُنسخ حرفيًا في OLN.
    /// <list type="bullet">
    /// <item>الرصيد يُمنح عن الفترة السابقة فقط إن كانت آخر حالة مسجّلة «playing» (سقف 20ث)، ولا رصيد لأول نبضة (P5).</item>
    /// <item>أبعد موضع لا يتقدّم أكثر من الزمن المنقضي فعليًا (+3ث) — لا يُقبل موضع مزيّف (P4).</item>
    /// <item>فجوة نبضات &gt; 20ث مع تقدّم موضع مثبت تُحتسب حتى 600ث (انقطاع إنترنت) — لا يُرفض الإتمام ظلمًا.</item>
    /// <item>المدة المُدخلة من الأدمن تتقدّم على مدة العميل؛ ومدة العميل لا تنخفض بعد حفظها.</item>
    /// <item>الإتمام = ended شوهد و WatchedSeconds ≥ ceil(Duration × 80%) (R9).</item>
    /// </list>
    /// </summary>
    public static class LectureProgressCalculator
    {
        /// <summary>سقف منطقي للمشاهدة المحتسبة فوق المدة (نفس RTK).</summary>
        public const double DurationCeilingFactor = 1.05;

        /// <summary>حدود مدة العميل حين لا توجد مدة من الأدمن (5ث .. 4 ساعات).</summary>
        public const double MinClientDurationSeconds = 5;
        public const double MaxClientDurationSeconds = 4 * 3600;

        public static LectureProgressComputation Compute(
            double previousWatched, bool previousEnded, string? lastState, DateTime? lastPingAtUtc,
            DateTime nowUtc, string state,
            int? adminDuration, int? storedDuration, double clientDuration,
            double clientPosition, double previousMaxPosition)
        {
            var clientSafe = double.IsNaN(clientDuration) || double.IsInfinity(clientDuration) ? 0 : clientDuration;
            double duration = adminDuration is > 0
                ? adminDuration.Value
                : Math.Max(storedDuration ?? 0, Math.Clamp(clientSafe, MinClientDurationSeconds, MaxClientDurationSeconds));

            double credit = 0, elapsed = 0;
            if (lastState == "playing" && lastPingAtUtc.HasValue)
            {
                elapsed = Math.Max(0, (nowUtc - lastPingAtUtc.Value).TotalSeconds);
                credit = Math.Min(elapsed, LectureWatchRules.MaxCreditPerPingSeconds);
            }

            // P4: أبعد موضع لا يتقدّم أكثر من الزمن الفعلي المنقضي منذ آخر نبضة «playing» (+سماح)
            var prevMax = double.IsNaN(previousMaxPosition) || previousMaxPosition < 0 ? 0 : previousMaxPosition;
            var pos = double.IsNaN(clientPosition) || double.IsInfinity(clientPosition) ? 0 : Math.Clamp(clientPosition, 0, duration);
            var maxPos = Math.Max(prevMax, Math.Min(pos, prevMax + elapsed + LectureWatchRules.PositionToleranceSeconds));
            maxPos = Math.Min(maxPos, duration);

            // انقطاع النبضات (فجوة > 20ث) مع تقدّم موضع مثبت: نحتسب ما شوهد فعلًا بدل خسارته
            if (elapsed > LectureWatchRules.MaxCreditPerPingSeconds)
                credit = Math.Max(credit, Math.Min(Math.Min(elapsed, LectureWatchRules.MaxGapCreditSeconds), maxPos - prevMax));

            var ended = previousEnded || state == "ended";
            var watched = Math.Min(previousWatched + credit, duration * DurationCeilingFactor);
            var required = Math.Ceiling(duration * LectureWatchRules.MinWatchPercent / 100.0);
            return new LectureProgressComputation(watched, duration, required, ended, ended && watched >= required, maxPos);
        }
    }
}
