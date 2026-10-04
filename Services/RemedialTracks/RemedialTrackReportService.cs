using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>RTK-S1: هيكل الخدمة فقط — تُملأ في RTK-S6.</summary>
    public sealed class RemedialTrackReportService : IRemedialTrackReportService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;

        public RemedialTrackReportService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider time)
        {
            _dbFactory = dbFactory;
            _time = time;
        }
    }
}
