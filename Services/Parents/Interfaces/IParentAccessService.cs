using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Interfaces
{
    public interface IParentAccessService
    {
        Task<bool> CanAccessStudentAsync(string parentUserId, int studentId);
        Task<int?> GetCurrentParentIdAsync(string userId);
    }
}
