using System.Text.RegularExpressions;

namespace QdratNew.Helpers
{
    public static class HtmlNumberHelper
    {
        /// <summary>
        /// يحافظ على جميع وسوم HTML كما هي (span، class، underline، colors…)
        /// مع تحويل الأرقام فقط إلى صيغة هندية إذا كان السؤال كمياً.
        /// </summary>
        public static string ConvertHtmlPreservingTags(string input, bool isQuantitative)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            string output = input;

            // تحويل الأرقام فقط دون المساس بالـ HTML
            if (isQuantitative)
            {
                output = Regex.Replace(output, @"\d+", match =>
                {
                    return ToIndicNumbers(match.Value);
                });
            }

            return output;
        }

        /// <summary>
        /// تحويل الأرقام العربية إلى أرقام هندية.
        /// </summary>
        private static string ToIndicNumbers(string number)
        {
            return number
                .Replace("0", "٠")
                .Replace("1", "١")
                .Replace("2", "٢")
                .Replace("3", "٣")
                .Replace("4", "٤")
                .Replace("5", "٥")
                .Replace("6", "٦")
                .Replace("7", "٧")
                .Replace("8", "٨")
                .Replace("9", "٩");
        }
    }
}
