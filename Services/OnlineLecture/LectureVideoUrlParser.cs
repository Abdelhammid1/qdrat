using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using QdratNew.Enums;

namespace QdratNew.Services.OnlineLecture
{
    /// <summary>
    /// OLC-S2.2 — محلّل روابط فيديو المحاضرات وفق القاعدة R8 من العقد v1.1 (دالة نقية تُنسخ منطقيًا في المشروع الجديد):
    /// <list type="bullet">
    /// <item>https فقط، والمضيف من قائمة ثابتة: youtube.com, www.youtube.com, youtu.be, vimeo.com, player.vimeo.com.</item>
    /// <item>المعرّف يُستخرج على الخادم ويُتحقق منه: YouTube <c>^[A-Za-z0-9_-]{11}$</c>، Vimeo <c>^\d{6,12}$</c>.</item>
    /// <item>رابط التضمين يبنيه الخادم من المعرّف فقط عبر <see cref="BuildEmbedUrl"/> — لا يُمرَّر الرابط الخام إلى iframe أبدًا.</item>
    /// </list>
    /// مستقل عن <c>RemedialTrackVideoUrlParser</c> عمدًا (لا تعديل على RTK، وأرقام المزوّد هنا 3/4 لا 1/2/9).
    /// </summary>
    public static class LectureVideoUrlParser
    {
        public const int MaxUrlLength = 500;

        private static readonly Regex YouTubeId = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex VimeoId = new("^[0-9]{6,12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>المضيفات المسموحة حرفيًا (R8).</summary>
        public static readonly IReadOnlyCollection<string> AllowedHosts = new[]
        {
            "youtube.com", "www.youtube.com", "youtu.be", "vimeo.com", "player.vimeo.com"
        };

        public static bool TryParse(string? url, out LectureVideoProvider provider, out string externalId)
            => TryParse(url, out provider, out externalId, out _);

        /// <summary>يحلّل الرابط ويعيد المزوّد والمعرّف، أو رسالة خطأ عربية واضحة.</summary>
        public static bool TryParse(string? url, out LectureVideoProvider provider, out string externalId, out string? error)
        {
            provider = default;
            externalId = string.Empty;

            var text = url?.Trim();
            if (string.IsNullOrEmpty(text))
                return Fail("رابط الفيديو مطلوب.", out error);
            if (text.Length > MaxUrlLength)
                return Fail($"رابط الفيديو أطول من {MaxUrlLength} حرفًا.", out error);
            if (text.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
                return Fail("رابط الفيديو لا يجوز أن يحتوي مسافات أو رموزًا غير مرئية.", out error);
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
                return Fail("رابط الفيديو غير صالح.", out error);
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
                return Fail("يُقبل رابط https فقط.", out error);
            if (!string.IsNullOrEmpty(uri.UserInfo))
                return Fail("رابط الفيديو لا يجوز أن يحتوي بيانات دخول.", out error);
            if (!uri.IsDefaultPort)
                return Fail("رابط الفيديو لا يجوز أن يحدد منفذًا.", out error);

            var host = uri.Host.ToLowerInvariant();
            if (!AllowedHosts.Contains(host))
                return Fail("يُقبل فقط رابط YouTube أو Vimeo (youtube.com، youtu.be، vimeo.com، player.vimeo.com).", out error);

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string? id;

            switch (host)
            {
                case "youtube.com":
                case "www.youtube.com":
                    id = YouTubeIdFromPath(uri, segments);
                    if (id is null || !YouTubeId.IsMatch(id))
                        return Fail("رابط يوتيوب غير صالح. استخدم رابط الفيديو المباشر (watch?v=… أو youtu.be/…).", out error);
                    provider = LectureVideoProvider.YouTube;
                    break;

                case "youtu.be":
                    id = segments.Length == 1 ? segments[0] : null;
                    if (id is null || !YouTubeId.IsMatch(id))
                        return Fail("رابط يوتيوب المختصر غير صالح.", out error);
                    provider = LectureVideoProvider.YouTube;
                    break;

                case "vimeo.com":
                    // vimeo.com/{id} فقط. الروابط الخاصة vimeo.com/{id}/{hash} تتطلب رمزًا لا يحفظه العقد (VideoExternalId أرقام فقط).
                    if (segments.Length == 2 && VimeoId.IsMatch(segments[0]))
                        return Fail(PrivateVimeoMessage, out error);
                    id = segments.Length == 1 ? segments[0] : null;
                    if (id is null || !VimeoId.IsMatch(id))
                        return Fail("رابط فيميو غير صالح. استخدم الصيغة vimeo.com/123456789.", out error);
                    provider = LectureVideoProvider.Vimeo;
                    break;

                default: // player.vimeo.com
                    if (segments.Length != 2 || !segments[0].Equals("video", StringComparison.OrdinalIgnoreCase) || !VimeoId.IsMatch(segments[1]))
                        return Fail("رابط مشغّل فيميو غير صالح. الصيغة: player.vimeo.com/video/123456789.", out error);
                    if (QueryHelpers.ParseQuery(uri.Query).ContainsKey("h"))
                        return Fail(PrivateVimeoMessage, out error);
                    id = segments[1];
                    provider = LectureVideoProvider.Vimeo;
                    break;
            }

            externalId = id!;
            error = null;
            return true;
        }

        /// <summary>الرابط القانوني الذي يُخزَّن في VideoUrl بعد التحقق (يُبنى من المعرّف لا من النص المُدخل).</summary>
        public static string BuildCanonicalUrl(LectureVideoProvider provider, string externalId)
        {
            EnsureValidId(provider, externalId);
            return provider == LectureVideoProvider.YouTube
                ? $"https://www.youtube.com/watch?v={externalId}"
                : $"https://vimeo.com/{externalId}";
        }

        /// <summary>رابط التضمين الآمن (R8) — يرمي استثناء إن كان المزوّد أو المعرّف غير صالح.</summary>
        public static string BuildEmbedUrl(LectureVideoProvider provider, string externalId)
        {
            EnsureValidId(provider, externalId);
            return provider switch
            {
                LectureVideoProvider.YouTube => $"https://www.youtube-nocookie.com/embed/{externalId}?enablejsapi=1&rel=0",
                LectureVideoProvider.Vimeo => $"https://player.vimeo.com/video/{externalId}",
                _ => throw new ArgumentOutOfRangeException(nameof(provider))
            };
        }

        /// <summary>نسخة لا ترمي — للعرض (تعيد null عند بيانات غير صالحة مخزَّنة سابقًا).</summary>
        public static string? TryBuildEmbedUrl(LectureVideoProvider? provider, string? externalId)
            => provider.HasValue && IsValidId(provider.Value, externalId)
                ? BuildEmbedUrl(provider.Value, externalId!)
                : null;

        public static bool IsValidId(LectureVideoProvider provider, string? externalId)
            => !string.IsNullOrEmpty(externalId) && provider switch
            {
                LectureVideoProvider.YouTube => YouTubeId.IsMatch(externalId),
                LectureVideoProvider.Vimeo => VimeoId.IsMatch(externalId),
                _ => false
            };

        private const string PrivateVimeoMessage =
            "روابط Vimeo الخاصة (التي تحتوي رمز h أو جزءًا بعد رقم الفيديو) غير مدعومة. اجعل الفيديو «مخفيًا عن Vimeo» مع تقييد التضمين لنطاق المنصة، ثم استخدم الرابط vimeo.com/رقم_الفيديو.";

        private static string? YouTubeIdFromPath(Uri uri, string[] segments)
        {
            if (segments.Length == 1 && segments[0].Equals("watch", StringComparison.OrdinalIgnoreCase))
            {
                var query = QueryHelpers.ParseQuery(uri.Query);
                return query.TryGetValue("v", out var v) && v.Count == 1 ? v[0] : null;
            }

            if (segments.Length == 2 &&
                (segments[0].Equals("embed", StringComparison.OrdinalIgnoreCase) ||
                 segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase) ||
                 segments[0].Equals("live", StringComparison.OrdinalIgnoreCase)))
                return segments[1];

            return null;
        }

        private static void EnsureValidId(LectureVideoProvider provider, string externalId)
        {
            if (!Enum.IsDefined(provider))
                throw new ArgumentOutOfRangeException(nameof(provider));
            if (!IsValidId(provider, externalId))
                throw new ArgumentException("معرّف الفيديو غير صالح لهذا المزوّد.", nameof(externalId));
        }

        private static bool Fail(string message, out string? error)
        {
            error = message;
            return false;
        }
    }
}
