using Microsoft.AspNetCore.SignalR;

namespace Notification.API.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;
        public NotificationHub(ILogger<NotificationHub> logger) { _logger = logger; }
        public override async Task OnConnectedAsync() { _logger.LogInformation("NotificationHub connected: {ConnectionId}", Context.ConnectionId); await base.OnConnectedAsync(); }
        public override async Task OnDisconnectedAsync(Exception? exception) { _logger.LogInformation("NotificationHub disconnected: {ConnectionId}", Context.ConnectionId); await base.OnDisconnectedAsync(exception); }
    }
}
