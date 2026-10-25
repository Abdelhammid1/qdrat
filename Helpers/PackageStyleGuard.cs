using System.Text.RegularExpressions;

namespace QdratNew.Helpers;

public static class PackageStyleGuard
{
    private static readonly Regex Hex = new(@"^#[0-9A-Fa-f]{6}\z",RegexOptions.Compiled);
    private static readonly Regex SlugRx = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z",RegexOptions.Compiled);
    private static readonly HashSet<string> ReservedSlugs =
        new(StringComparer.Ordinal) { "admin", "register", "success", "thanks", "new" };

    public static bool IsValidHex(string? v) => v != null && Hex.IsMatch(v);

    public static bool IsValidSlug(string? v) =>
        v != null && v.Length <= 160 && SlugRx.IsMatch(v) && !ReservedSlugs.Contains(v);

    /// <summary>يعيد #FFFFFF أو #1F2937 حسب سطوع الخلفية (تباين WCAG قدر الإمكان).</summary>
    public static string ReadableTextColor(string hex)
    {
        if (!IsValidHex(hex)) return "#FFFFFF";

        static double Lin(int c)
        {
            var s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);
        var lum = 0.2126 * Lin(r) + 0.7152 * Lin(g) + 0.0722 * Lin(b);
        return lum > 0.4 ? "#1F2937" : "#FFFFFF";
    }
}
