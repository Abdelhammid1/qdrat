using QdratNew.Enums;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S11.1: توصيات تربوية لتقرير ولي الأمر — دالة نقية قابلة للاختبار.
    /// القواعد: بلا وعود، بلا مقارنة بين الطلاب، ولا إشارة لبيانات طالب آخر.
    /// </summary>
    public static class RemedialTrackRecommendations
    {
        /// <summary>نسبة المشاهدة الدنيا (من مدة الفيديوهات) قبل اعتبار المشاهدة «أقل من المطلوب».</summary>
        public const double LowWatchRatio = 0.8;

        public static string For(RemedialTrackPathKind kind, int? scorePercent, int passMarkPercent, int watchedMinutes, int videoMinutes)
        {
            var low = videoMinutes > 0 && watchedMinutes < videoMinutes * LowWatchRatio;
            var vsMark = scorePercent.HasValue && passMarkPercent > 0
                ? $" (نتيجته {scorePercent.Value}% مقابل حد الاجتياز {passMarkPercent}%)"
                : string.Empty;

            return kind switch
            {
                RemedialTrackPathKind.PassedFirstExam =>
                    "اجتاز الطالب المحور من الاختبار الأول؛ نوصي بالانتقال للمحور التالي مع الحفاظ على نفس وتيرة المذاكرة.",
                RemedialTrackPathKind.PassedAfterRewatch =>
                    "اجتاز الطالب بعد إعادة المشاهدة؛ نوصي بمراجعة الأفكار الأساسية لهذا المحور قبل الاختبار النهائي وتدريب إضافي على الأسئلة المشابهة.",
                RemedialTrackPathKind.FailedBoth =>
                    low
                        ? $"لم يجتز الطالب المحور في الاختبارين{vsMark} وكانت مدة مشاهدته أقل من المطلوب؛ نوصي بإعادة مشاهدة المحور كاملًا بتركيز وبمتابعة مباشرة من ولي الأمر."
                        : $"لم يجتز الطالب المحور في الاختبارين{vsMark} رغم المشاهدة؛ نوصي بجلسة مراجعة مع المعلم لتحديد نقاط الضعف قبل الانتقال.",
                _ => string.Empty
            };
        }

        /// <summary>يحدّد المسار من حالة المحور ونتيجتي الاختبارين.</summary>
        public static RemedialTrackPathKind PathOf(RemedialTrackAxisStatus status, double? exam101, double? exam102) => status switch
        {
            RemedialTrackAxisStatus.Locked => RemedialTrackPathKind.NotStarted,
            RemedialTrackAxisStatus.Passed => exam102.HasValue ? RemedialTrackPathKind.PassedAfterRewatch : RemedialTrackPathKind.PassedFirstExam,
            RemedialTrackAxisStatus.FailedBlocked or RemedialTrackAxisStatus.FailedOpenedByAdmin => RemedialTrackPathKind.FailedBoth,
            _ => RemedialTrackPathKind.InProgress
        };
    }
}
