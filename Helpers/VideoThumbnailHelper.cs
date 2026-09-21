using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QdratNew.Helpers
{
    public static class VideoThumbnailHelper
    {
        private static readonly HttpClient _http = new HttpClient();

        public static async Task<string?> GetThumbnailAsync(string videoUrl)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
                return null;

            // 🟥 YouTube
            if (videoUrl.Contains("youtube.com/watch?v=") || videoUrl.Contains("youtu.be"))
            {
                var videoId = ExtractYouTubeId(videoUrl);
                if (!string.IsNullOrEmpty(videoId))
                    return $"https://img.youtube.com/vi/{videoId}/hqdefault.jpg";
            }

            // 🟦 Vimeo
            if (videoUrl.Contains("vimeo.com"))
            {
                var vimeoId = ExtractVimeoId(videoUrl);
                if (!string.IsNullOrEmpty(vimeoId))
                {
                    try
                    {
                        var apiUrl = $"https://vimeo.com/api/v2/video/{vimeoId}.json";
                        var json = await _http.GetStringAsync(apiUrl);
                        var data = JsonDocument.Parse(json).RootElement[0];
                        return data.GetProperty("thumbnail_large").GetString();
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            return null;
        }

        private static string? ExtractYouTubeId(string url)
        {
            var match = Regex.Match(url, @"(?:v=|be/)([a-zA-Z0-9_-]{11})");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string? ExtractVimeoId(string url)
        {
            var match = Regex.Match(url, @"vimeo\.com/(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
