using QdratNew.Entities;
using QdratNew.Services.Interfaces;

namespace QdratNew.Services.Implementations
{
    public class TimeCalculationService : ITimeCalculationService
    {
        public TimeSpan CalculateActualDuration(
            DateTime? startedAt,
            DateTime? completedAt,
            List<QuestionAttemptNew> attempts,
            int totalAllowedSeconds
        )
        {
            // 🔹 الطالب لم يبدأ
            if (startedAt == null)
                return TimeSpan.Zero;

            // 🔹 1) الطالب أنهى الاختبار رسميًا
            if (completedAt != null)
            {
                var d = completedAt.Value - startedAt.Value;

                if (d.TotalSeconds > totalAllowedSeconds)
                    d = TimeSpan.FromSeconds(totalAllowedSeconds);

                if (d.TotalSeconds < 5)
                    d = TimeSpan.FromSeconds(5);

                return d;
            }

            // 🔹 2) الطالب غادر ولم يكمل — نستخدم أول وآخر محاولة
            if (attempts != null && attempts.Any())
            {
                var first = attempts.OrderBy(a => a.AttemptedAt).First().AttemptedAt;
                var last = attempts.OrderByDescending(a => a.AttemptedAt).First().AttemptedAt;

                var d = last - first;

                if (d.TotalSeconds <= 0)
                    d = TimeSpan.FromSeconds(5);

                if (d.TotalSeconds > totalAllowedSeconds)
                    d = TimeSpan.FromSeconds(totalAllowedSeconds);

                return d;
            }

            // 🔹 3) لم يجب أي سؤال — استخدم دقيقة واحدة (قيمة منطقية)
            return TimeSpan.FromSeconds(
                Math.Min(totalAllowedSeconds, 60)
            );
        }

        public string FormatDuration(TimeSpan duration)
        {
            int m = (int)duration.TotalMinutes;
            int s = duration.Seconds;
            return $"{m} دقيقة و {s} ثانية";
        }
    }
}
