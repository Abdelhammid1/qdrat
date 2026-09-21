using QdratNew.ViewModels.Partner;

namespace QdratNew.Services.Partner.Dashboard
{
    public interface IPartnerDashboardSnapshotService
    {
        PartnerDashboardSnapshotVM GetSnapshot(int partnerId);
        void Invalidate(int partnerId);
    }
}
