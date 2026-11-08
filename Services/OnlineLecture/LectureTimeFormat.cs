using System.Globalization;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S2 — تحويل أوقات الفيديو بين ما يكتبه الأدمن والثواني المخزّنة (دالة نقية).
    /// المقبول: <c>ss</c> (ثوانٍ فقط)، <c>mm:ss</c>، <c>hh:mm:ss</c> — بأرقام لاتينية أو هندية.
    /// تُستخدم لمدة الفيديو الكامل (S2) ولتوقيتات الفصول (S3).
    /// </summary>
    public static class LectureTimeFormat
    {
        /// <summary>الحد الأعلى المعقول لمدة محاضرة مسجّلة (12 ساعة).</summary>
        public const int MaxSeconds = 12 * 60 * 60;

        public static bool TryParse(string? text, out int seconds)
        {
            seconds = 0;
            var value = NormalizeDigits(text?.Trim());
            if (string.IsNullOrEmpty(value))
                return false;

            var parts = value.Split(':');
            if (parts.Length is < 1 or > 3)
                return false;

            var numbers = new int[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 6 ||
                    !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
                    return false;
            }

            long total;
            switch (numbers.Length)
            {
                case 1:
                    total = numbers[0];
                    break;
                case 2:
                    if (numbers[1] > 59) return false;
                    total = numbers[0] * 60L + numbers[1];
                    break;
                default:
                    if (numbers[1] > 59 || numbers[2] > 59) return false;
                    total = numbers[0] * 3600L + numbers[1] * 60L + numbers[2];
                    break;
            }

            if (total > MaxSeconds)
                return false;

            seconds = (int)total;
            return true;
        }

        /// <summary>صيغة العرض: <c>m:ss</c> تحت الساعة، و<c>h:mm:ss</c> فوقها.</summary>
        public static string Format(int? seconds)
        {
            if (!seconds.HasValue || seconds.Value < 0)
                return string.Empty;

            var t = TimeSpan.FromSeconds(seconds.Value);
            return t.TotalHours >= 1
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", t.Minutes, t.Seconds);
        }

        private static string? NormalizeDigits(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var chars = value.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if (c >= '٠' && c <= '٩') chars[i] = (char)('0' + (c - '٠'));       // ٠-٩
                else if (c >= '۰' && c <= '۹') chars[i] = (char)('0' + (c - '۰'));  // ۰-۹
                else if (c == '：') chars[i] = ':';   // نقطتان بعرض كامل
            }
            return new string(chars);
        }
    }
}
