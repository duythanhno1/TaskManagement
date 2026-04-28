using Microsoft.AspNetCore.SignalR;

namespace Task.API.Hubs
{
    public class TaskHub : Hub
    {
        private readonly ILogger<TaskHub> _logger;
        public TaskHub(ILogger<TaskHub> logger) { _logger = logger; }

        public async System.Threading.Tasks.Task SendTaskUpdate(int taskId, string taskName, string description, int? assignedTo, string status)
        {
            if (string.IsNullOrEmpty(taskName) || string.IsNullOrEmpty(status)) return;
            await Clients.All.SendAsync("ReceiveTaskUpdate", taskId, taskName, description, assignedTo, status);
        }

        public override async System.Threading.Tasks.Task OnConnectedAsync()
        {
            _logger.LogInformation("TaskHub connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async System.Threading.Tasks.Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("TaskHub disconnected: {ConnectionId}", Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
