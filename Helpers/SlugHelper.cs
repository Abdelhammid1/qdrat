using System.Text.RegularExpressions;

namespace QdratNew.Helpers
{
    public static class SlugHelper
    {
        public static string GenerateSlug(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Trim().ToLowerInvariant();

            // تحويل الحروف العربية (بسيط)
            text = text.Replace("أ", "a")
                       .Replace("إ", "i")
                       .Replace("آ", "a")
                       .Replace("ة", "h")
                       .Replace("ى", "a")
                       .Replace("ي", "y")
                       .Replace("و", "w");

            text = Regex.Replace(text, @"\s+", "-");
            text = Regex.Replace(text, @"[^a-z0-9\-]", "");

            return text;
        }
    }

}
