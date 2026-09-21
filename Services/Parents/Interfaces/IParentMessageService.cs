using System.Collections.Generic;
using System.Threading.Tasks;
using QdratNew.ViewModels.Parents;

namespace QdratNew.Services.Parents.Interfaces
{
    public interface IParentMessageService
    {
        Task<ParentMessagesListViewModel> GetMessagesAsync(string parentUserId, int? studentId);
        Task<ParentMessageViewModel?> GetMessageDetailsAsync(int messageId, int parentId);
        Task CreateMessageAsync(string parentUserId, ParentMessageCreateViewModel model);
    }
}
