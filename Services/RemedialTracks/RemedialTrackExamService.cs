using Microsoft.EntityFrameworkCore;
using QdratNew.Data;

namespace QdratNew.Services.RemedialTracks
{
    /// <summary>RTK-S1: هيكل الخدمة فقط — تُملأ في RTK-S5.</summary>
    public sealed class RemedialTrackExamService : IRemedialTrackExamService
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
        private readonly TimeProvider _time;

        public RemedialTrackExamService(IDbContextFactory<ApplicationDbContext> dbFactory, TimeProvider time)
        {
            _dbFactory = dbFactory;
            _time = time;
        }
    }
}
