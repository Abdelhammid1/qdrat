using System.Threading.Tasks;
using QdratNew.ViewModels.Parents;

namespace QdratNew.Services.Parents.Interfaces
{
    public interface IParentDashboardService
    {
        Task<ParentDashboardViewModel> GetDashboardAsync(string parentUserId, int? selectedStudentId);
    }
}
