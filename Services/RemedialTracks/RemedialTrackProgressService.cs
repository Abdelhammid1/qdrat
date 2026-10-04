using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>RTK-S1: هيكل الخدمة فقط — تُملأ في RTK-S4/S5.</summary>
    public sealed class RemedialTrackProgressService : IRemedialTrackProgressService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;

        public RemedialTrackProgressService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider time)
        {
            _dbFactory = dbFactory;
            _time = time;
        }
    }
}
