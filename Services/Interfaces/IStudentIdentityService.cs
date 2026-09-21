// Services/Interfaces/IStudentIdentityService.cs
using System.Security.Claims;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IStudentIdentityService
    {
        Task<int> GetCurrentStudentIdAsync(ClaimsPrincipal user);
    }
}
