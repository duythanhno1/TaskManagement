using Microsoft.AspNetCore.SignalR;

namespace Managerment.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(ILogger<ChatHub> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Client gọi method này để join vào group chat room (để nhận real-time messages)
        /// </summary>
        public async Task JoinGroup(int groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{groupId}");
            _logger.LogInformation("User {ConnectionId} joined group chat_{GroupId}", Context.ConnectionId, groupId);
        }

        /// <summary>
        /// Client gọi method này để leave group chat room
        /// </summary>
        public async Task LeaveGroup(int groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{groupId}");
            _logger.LogInformation("User {ConnectionId} left group chat_{GroupId}", Context.ConnectionId, groupId);
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("ChatHub client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            _logger.LogInformation("ChatHub client disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
