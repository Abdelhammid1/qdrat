using QdratNew.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QdratNew.Services.Interfaces
{
    public interface IRemedialSessionService
    {
        Task<List<RemedialSession>> GetAllAsync();
        Task<RemedialSession?> GetByIdAsync(int id);
        Task<bool> CreateAsync(RemedialSession session);
        Task<bool> UpdateAsync(RemedialSession session);
        Task<bool> DeleteAsync(int id);
    }
}
