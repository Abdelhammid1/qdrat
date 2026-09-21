using System.Collections.Generic;
using QdratNew.ViewModels.Dashboard;

namespace QdratNew.Services.AdminDashboard
{
    public interface IAdminLiveStudentTracker
    {
        void Track(int studentId, string userId, string path, string pageTitle);

        void MarkOffline(int studentId);

        List<LiveStudentSnapshot> GetSnapshots();

        LiveStudentSnapshot? GetSnapshotByStudentId(int studentId);
    }
}