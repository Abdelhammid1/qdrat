using QdratNew.Enums;
using QdratNew.Services.RemedialTracks;
using Xunit;

namespace QdratNew.Tests
{
    /// <summary>RTK-S2.3: محلل روابط الفيديو (نقي — بلا اتصال بأي منصة).</summary>
    public class RemedialTrackVideoUrlParserTests
    {
        private const string YtId = "dQw4w9WgXcQ";

        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=30s")]
        [InlineData("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42")]
        [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
        [InlineData("  https://www.youtube.com/watch?v=dQw4w9WgXcQ  ")]
        public void YouTube_ValidForms_ExtractId(string url)
        {
            var r = RemedialTrackVideoUrlParser.Parse(url);

            Assert.True(r.IsValid, r.Error);
            Assert.Equal(RemedialTrackVideoProvider.YouTube, r.Provider);
            Assert.Equal(YtId, r.ExternalId);
            Assert.Equal($"https://www.youtube.com/watch?v={YtId}", r.NormalizedUrl);
            Assert.False(r.RequiresDuration);
        }

        [Theory]
        [InlineData("https://vimeo.com/123456789", "123456789")]
        [InlineData("https://www.vimeo.com/123456789", "123456789")]
        [InlineData("https://vimeo.com/123456789/abcdef1234", "123456789:abcdef1234")]
        [InlineData("https://player.vimeo.com/video/123456789", "123456789")]
        [InlineData("https://player.vimeo.com/video/123456789?h=abcdef1234", "123456789:abcdef1234")]
        public void Vimeo_ValidForms_ExtractId(string url, string expectedId)
        {
            var r = RemedialTrackVideoUrlParser.Parse(url);

            Assert.True(r.IsValid, r.Error);
            Assert.Equal(RemedialTrackVideoProvider.Vimeo, r.Provider);
            Assert.Equal(expectedId, r.ExternalId);
        }

        [Fact]
        public void OtherPlatform_IsValid_ButRequiresDuration()
        {
            var r = RemedialTrackVideoUrlParser.Parse("https://videos.example.com/lesson/12");

            Assert.True(r.IsValid);
            Assert.Equal(RemedialTrackVideoProvider.Other, r.Provider);
            Assert.Null(r.ExternalId);
            Assert.True(r.RequiresDuration);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a url")]
        [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]                 // http
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
        [InlineData("file:///C:/Windows/win.ini")]
        [InlineData("ftp://example.com/video.mp4")]
        [InlineData("https://127.0.0.1/video")]
        [InlineData("https://192.168.1.10/video")]
        [InlineData("https://[::1]/video")]
        [InlineData("https://localhost/video")]
        [InlineData("https://intranet/video")]                                       // اسم بلا نقطة
        [InlineData("https://user:pass@www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/watch?v=short")]                        // معرّف قصير
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQtoolong")]           // معرّف طويل
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXc<")]                  // رمز غير مسموح
        [InlineData("https://www.youtube.com/watch")]                                // بلا v
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&v=aaaaaaaaaaa")]    // v مكررة
        [InlineData("https://www.youtube.com/playlist?list=PL1234567890")]
        [InlineData("https://www.youtube.com/@channel")]
        [InlineData("https://youtu.be/")]
        [InlineData("https://vimeo.com/abc")]
        [InlineData("https://vimeo.com/123")]                                        // معرّف فيميو قصير
        [InlineData("https://vimeo.com/123456789/bad-hash!")]
        [InlineData("https://player.vimeo.com/video/123456789?h=<script>")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ\" onload=\"x")]     // محاولة كسر attribute
        public void Malicious_Or_Malformed_AreRejected(string? url)
        {
            var r = RemedialTrackVideoUrlParser.Parse(url);

            Assert.False(r.IsValid);
            Assert.False(string.IsNullOrWhiteSpace(r.Error));
            Assert.Null(r.NormalizedUrl);
        }

        [Fact]
        public void TooLongUrl_IsRejected()
        {
            var url = "https://videos.example.com/" + new string('a', RemedialTrackVideoUrlParser.MaxUrlLength);

            Assert.False(RemedialTrackVideoUrlParser.Parse(url).IsValid);
        }

        [Fact]
        public void EmbedUrl_IsBuiltFromId_ForYouTubeAndVimeo()
        {
            Assert.Equal($"https://www.youtube-nocookie.com/embed/{YtId}",
                RemedialTrackVideoUrlParser.BuildEmbedUrl(RemedialTrackVideoProvider.YouTube, YtId));
            Assert.Equal("https://player.vimeo.com/video/123456789",
                RemedialTrackVideoUrlParser.BuildEmbedUrl(RemedialTrackVideoProvider.Vimeo, "123456789"));
            Assert.Equal("https://player.vimeo.com/video/123456789?h=abcdef1234",
                RemedialTrackVideoUrlParser.BuildEmbedUrl(RemedialTrackVideoProvider.Vimeo, "123456789:abcdef1234"));
        }

        [Theory]
        [InlineData(RemedialTrackVideoProvider.Other, "anything")]
        [InlineData(RemedialTrackVideoProvider.YouTube, "bad id\"><script>")]
        [InlineData(RemedialTrackVideoProvider.YouTube, null)]
        [InlineData(RemedialTrackVideoProvider.Vimeo, "12:abc")]
        [InlineData(RemedialTrackVideoProvider.Vimeo, "123456789:bad!")]
        public void EmbedUrl_IsNull_ForUnsafeInput(RemedialTrackVideoProvider provider, string? id)
            => Assert.Null(RemedialTrackVideoUrlParser.BuildEmbedUrl(provider, id));
    }
}
