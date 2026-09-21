using QdratNew.Services.Interfaces;
using System;

namespace QdratNew.Services.Implementations
{
    public class TimeZoneService : ITimeZoneService
    {
        private readonly TimeZoneInfo _tzSaudi;

        public TimeZoneService()
        {
            // ✅ المنطقة الزمنية للسعودية (UTC+3)
            _tzSaudi = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
        }

        // 🕒 الوقت العالمي الثابت
        public DateTime GetNowUtc() => DateTime.UtcNow;

        // 🕒 الوقت المحلي بالسعودية
        public DateTime GetNowSaudi() =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tzSaudi);

        // 🔁 تحويل وقت UTC إلى السعودية
        public DateTime ConvertToSaudi(DateTime utcTime)
        {
            if (utcTime.Kind == DateTimeKind.Unspecified)
                utcTime = DateTime.SpecifyKind(utcTime, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, _tzSaudi);
        }

        // 🔁 تحويل وقت سعودي إلى UTC (للتخزين)
        public DateTime ConvertToUtc(DateTime saudiTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(saudiTime, _tzSaudi);
        }
    }

}
