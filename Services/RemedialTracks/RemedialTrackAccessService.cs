using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>RTK-S1: هيكل الخدمة فقط — تُملأ في RTK-S3/S4.</summary>
    public sealed class RemedialTrackAccessService : IRemedialTrackAccessService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;

        public RemedialTrackAccessService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider time)
        {
            _dbFactory = dbFactory;
            _time = time;
        }
    }
}
