using Microsoft.AspNetCore.SignalR;

namespace Chat.API.Hubs
{
    public class ChatHub : Hub
    {
        private readonly ILogger<ChatHub> _logger;
        public ChatHub(ILogger<ChatHub> logger) { _logger = logger; }

        public async Task JoinGroup(int groupId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{groupId}");
            _logger.LogInformation("User {ConnectionId} joined chat_{GroupId}", Context.ConnectionId, groupId);
        }

        public async Task LeaveGroup(int groupId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{groupId}");
            _logger.LogInformation("User {ConnectionId} left chat_{GroupId}", Context.ConnectionId, groupId);
        }

        public override async Task OnConnectedAsync() { _logger.LogInformation("ChatHub connected: {ConnectionId}", Context.ConnectionId); await base.OnConnectedAsync(); }
        public override async Task OnDisconnectedAsync(Exception? exception) { _logger.LogInformation("ChatHub disconnected: {ConnectionId}", Context.ConnectionId); await base.OnDisconnectedAsync(exception); }
    }
}
