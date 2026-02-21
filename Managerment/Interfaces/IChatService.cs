using Managerment.DTO;
using Managerment.Services;

namespace Managerment.Interfaces
{
    public interface IChatService
    {
        Task<ServiceResult<object>> CreateGroupAsync(int currentUserId, CreateGroupDTO dto);
        Task<ServiceResult<List<object>>> GetMyGroupsAsync(int currentUserId);
        Task<ServiceResult<List<object>>> GetMessagesAsync(int currentUserId, int groupId, int? cursor = null, int pageSize = 50);
        Task<ServiceResult<object>> SendMessageAsync(int currentUserId, SendMessageDTO dto);
        Task<ServiceResult<object>> DeleteMessageAsync(int currentUserId, int messageId);
        Task<ServiceResult<object>> ReactToMessageAsync(int currentUserId, ReactMessageDTO dto);
        Task<ServiceResult<object>> RemoveReactionAsync(int currentUserId, int messageId);
        Task<ServiceResult<object>> MarkAsReadAsync(int currentUserId, int groupId);
    }
}
