using System;

namespace QdratNew.Helpers
{
    public static class TimeHelper
    {
        /// <summary>
        /// يحوّل TimeSpan إلى تنسيق 12 ساعة (ص/م)
        /// </summary>
        public static string FormatTimeWithAmPm(TimeSpan time)
        {
            var dateTime = DateTime.Today.Add(time);
            var formatted = dateTime.ToString("hh:mm tt", new System.Globalization.CultureInfo("en-US"));
            return formatted.Replace("AM", "ص").Replace("PM", "م");
        }
    }
}
