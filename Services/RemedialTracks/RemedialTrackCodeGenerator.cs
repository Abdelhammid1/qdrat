using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>
    /// RTK-S1.5: مولّد كود الخطة (RTK-yyyy-0000) والرقم المرجعي (CSPRNG — D6).
    /// المولّد لا يحفظ شيئًا: عند تعارض الفهرس الفريد على RemedialTracks.Code يعيد المستدعي (BuilderService في S2)
    /// استدعاء NextTrackCodeAsync حتى MaxCodeRetries مرات. نفس نمط QuestionReviewTaskService.NextCodeAsync.
    /// </summary>
    public sealed class RemedialTrackCodeGenerator : IRemedialTrackCodeGenerator
    {
        public const int MaxCodeRetries = 5;
        private const string Prefix = "RTK-";

        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;

        public RemedialTrackCodeGenerator(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider time)
        {
            _dbFactory = dbFactory;
            _time = time;
        }

        public async Task<string> NextTrackCodeAsync(CancellationToken ct = default)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var prefix = $"{Prefix}{_time.GetUtcNow().Year}-";

            var last = await db.RemedialTracks.AsNoTracking()
                .Where(t => t.Code.StartsWith(prefix))
                .OrderByDescending(t => t.Code)
                .Select(t => t.Code)
                .FirstOrDefaultAsync(ct);

            var n = 1;
            if (last is not null && int.TryParse(last.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var lastNumber))
                n = lastNumber + 1;

            return $"{prefix}{n.ToString("0000", CultureInfo.InvariantCulture)}";
        }

        public string NewAccessCode()
            => RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
    }
}
