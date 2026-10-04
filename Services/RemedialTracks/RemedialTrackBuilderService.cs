using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>RTK-S1: هيكل الخدمة فقط — تُملأ في RTK-S2.</summary>
    public sealed class RemedialTrackBuilderService : IRemedialTrackBuilderService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;

        public RemedialTrackBuilderService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider time)
        {
            _dbFactory = dbFactory;
            _time = time;
        }
    }
}
