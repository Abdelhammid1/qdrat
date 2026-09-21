using System.Threading.Tasks;
using QdratNew.ViewModels.Parents;

namespace QdratNew.Services.Parents.Interfaces
{
    public interface IInstitutePlanFollowUpService
    {
        Task<ParentInstitutePlanFollowUpViewModel> GetFollowUpAsync(string parentUserId, int studentId);
    }
}
