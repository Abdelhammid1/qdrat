using System.Threading.Tasks;

namespace QdratNew.Services.Parents.Interfaces
{
    public interface IParentNotificationService
    {
        Task SendParentNotificationAsync(int parentId, int studentId, string message, string? targetUrl = null);
    }
}
