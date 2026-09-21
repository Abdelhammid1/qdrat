using QdratNew.Entities;

namespace QdratNew.Services.Interfaces
{
    public interface ITimeCalculationService
    {
        /// <summary>
        /// يحسب الزمن الفعلي الذي استغرقه الطالب في أي اختبار.
        /// </summary>
        TimeSpan CalculateActualDuration(
            DateTime? startedAt,
            DateTime? completedAt,
            List<QuestionAttemptNew> attempts,
            int totalAllowedSeconds
        );

        /// <summary>
        /// يعيد الزمن بصيغة جاهزة للعرض "10 دقيقة و 30 ثانية"
        /// </summary>
        string FormatDuration(TimeSpan duration);
    }
}
