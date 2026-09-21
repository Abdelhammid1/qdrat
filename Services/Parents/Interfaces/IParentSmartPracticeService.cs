using System.Threading.Tasks;
using QdratNew.ViewModels.Parents;

namespace QdratNew.Services.Parents.Interfaces
{
    public interface IParentSmartPracticeService
    {
        Task<ParentSmartPracticeCreateViewModel> BuildCreateModelAsync(string parentUserId, int? studentId);
        Task<ParentSmartPracticeResultViewModel> CreateSmartPracticeAsync(string parentUserId, ParentSmartPracticeCreateViewModel model);
        Task<bool> CanCreatePracticeTodayAsync(int parentId, int studentId);
        Task<ParentSmartPracticeDetailsViewModel?> GetDetailsAsync(int requestId, int parentId);
    }
}
