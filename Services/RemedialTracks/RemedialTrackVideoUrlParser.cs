using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using QdratNew.Enums;

namespace QdratNew.Services.RemedialTracks
{
    public readonly record struct VideoUrlParseResult(
        bool IsValid,
        string? Error,
        RemedialTrackVideoProvider Provider,
        string? ExternalId,
        string? NormalizedUrl)
    {
        /// <summary>D5: المنصات غير YouTube/Vimeo تحتاج مدة يدوية.</summary>
        public bool RequiresDuration => IsValid && Provider == RemedialTrackVideoProvider.Other;
    }

    /// <summary>
    /// RTK-S2.3: محلل روابط الفيديو (دالة نقية). يملأ Provider و ExternalId ويرفض كل ما ليس https عاديًا.
    /// عند العرض يُبنى رابط الـ embed من ExternalId عبر <see cref="BuildEmbedUrl"/> ولا يُستخدم النص المُدخل خامًا في src.
    /// </summary>
    public static class RemedialTrackVideoUrlParser
    {
        public const int MaxUrlLength = 500;
        public const int MinOtherDurationSeconds = 10;

        private static readonly Regex YouTubeId = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex VimeoId = new("^[0-9]{6,12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex VimeoHash = new("^[A-Za-z0-9]{6,20}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static VideoUrlParseResult Parse(string? raw)
        {
            var text = raw?.Trim();
            if (string.IsNullOrEmpty(text))
                return Invalid("رابط الفيديو مطلوب.");
            if (text.Length > MaxUrlLength)
                return Invalid($"رابط الفيديو أطول من {MaxUrlLength} حرفًا.");
            if (text.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
                return Invalid("رابط الفيديو لا يجوز أن يحتوي مسافات أو رموزًا غير مرئية.");

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
                return Invalid("رابط الفيديو غير صالح.");
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
                return Invalid("يُقبل رابط https فقط.");
            if (!string.IsNullOrEmpty(uri.UserInfo))
                return Invalid("رابط الفيديو لا يجوز أن يحتوي بيانات دخول.");
            if (uri.HostNameType != UriHostNameType.Dns)
                return Invalid("لا يُقبل رابط بعنوان IP.");

            var host = uri.Host.ToLowerInvariant();
            if (host.StartsWith("www.", StringComparison.Ordinal))
                host = host[4..];
            if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal) || !host.Contains('.'))
                return Invalid("اسم النطاق غير صالح.");

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            switch (host)
            {
                case "youtube.com":
                case "m.youtube.com":
                    return ParseYouTubeWatchStyle(uri, segments);
                case "youtu.be":
                    return segments.Length >= 1 ? YouTube(segments[0]) : Invalid("رابط يوتيوب غير صالح.");
                case "vimeo.com":
                    return ParseVimeoPublic(segments);
                case "player.vimeo.com":
                    return ParseVimeoPlayer(uri, segments);
                default:
                    return new VideoUrlParseResult(true, null, RemedialTrackVideoProvider.Other, null, uri.AbsoluteUri);
            }
        }

        /// <summary>رابط الـ embed الآمن. يعيد null إن لم يُدعم المزوّد أو كان المعرّف غير صالح.</summary>
        public static string? BuildEmbedUrl(RemedialTrackVideoProvider provider, string? externalId)
        {
            if (string.IsNullOrEmpty(externalId)) return null;

            switch (provider)
            {
                case RemedialTrackVideoProvider.YouTube when YouTubeId.IsMatch(externalId):
                    return $"https://www.youtube-nocookie.com/embed/{externalId}";

                case RemedialTrackVideoProvider.Vimeo:
                    var parts = externalId.Split(':');
                    if (parts.Length is < 1 or > 2 || !VimeoId.IsMatch(parts[0])) return null;
                    if (parts.Length == 2)
                        return VimeoHash.IsMatch(parts[1])
                            ? $"https://player.vimeo.com/video/{parts[0]}?h={parts[1]}"
                            : null;
                    return $"https://player.vimeo.com/video/{parts[0]}";

                default:
                    return null;
            }
        }

        private static VideoUrlParseResult ParseYouTubeWatchStyle(Uri uri, string[] segments)
        {
            if (segments.Length == 1 && segments[0].Equals("watch", StringComparison.OrdinalIgnoreCase))
            {
                var query = QueryHelpers.ParseQuery(uri.Query);
                return query.TryGetValue("v", out var v) && v.Count == 1
                    ? YouTube(v[0])
                    : Invalid("رابط يوتيوب غير صالح.");
            }

            if (segments.Length == 2 &&
                (segments[0].Equals("embed", StringComparison.OrdinalIgnoreCase) ||
                 segments[0].Equals("shorts", StringComparison.OrdinalIgnoreCase)))
                return YouTube(segments[1]);

            return Invalid("رابط يوتيوب غير مدعوم. استخدم رابط فيديو مباشر.");
        }

        private static VideoUrlParseResult YouTube(string? id)
            => id is not null && YouTubeId.IsMatch(id)
                ? new VideoUrlParseResult(true, null, RemedialTrackVideoProvider.YouTube, id, $"https://www.youtube.com/watch?v={id}")
                : Invalid("معرّف فيديو يوتيوب غير صالح.");

        private static VideoUrlParseResult ParseVimeoPublic(string[] segments)
        {
            if (segments.Length is < 1 or > 2 || !VimeoId.IsMatch(segments[0]))
                return Invalid("رابط فيميو غير صالح.");

            if (segments.Length == 2)
                return VimeoHash.IsMatch(segments[1])
                    ? Vimeo(segments[0], segments[1])
                    : Invalid("رابط فيميو غير صالح.");

            return Vimeo(segments[0], null);
        }

        private static VideoUrlParseResult ParseVimeoPlayer(Uri uri, string[] segments)
        {
            if (segments.Length != 2 || !segments[0].Equals("video", StringComparison.OrdinalIgnoreCase) || !VimeoId.IsMatch(segments[1]))
                return Invalid("رابط فيميو غير صالح.");

            var query = QueryHelpers.ParseQuery(uri.Query);
            if (query.TryGetValue("h", out var h) && h.Count == 1 && !string.IsNullOrEmpty(h[0]))
                return VimeoHash.IsMatch(h[0]!) ? Vimeo(segments[1], h[0]) : Invalid("رابط فيميو غير صالح.");

            return Vimeo(segments[1], null);
        }

        private static VideoUrlParseResult Vimeo(string id, string? hash)
            => hash is null
                ? new VideoUrlParseResult(true, null, RemedialTrackVideoProvider.Vimeo, id, $"https://vimeo.com/{id}")
                : new VideoUrlParseResult(true, null, RemedialTrackVideoProvider.Vimeo, $"{id}:{hash}", $"https://vimeo.com/{id}/{hash}");

        private static VideoUrlParseResult Invalid(string error)
            => new(false, error, default, null, null);
    }
}
