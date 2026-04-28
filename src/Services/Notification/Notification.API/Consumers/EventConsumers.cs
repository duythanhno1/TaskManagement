using MassTransit;
using Notification.API.Services;
using Shared.Contracts.Events;

namespace Notification.API.Consumers
{
    public class TaskCreatedConsumer : IConsumer<TaskCreatedEvent>
    {
        private readonly INotificationService _notifService;
        public TaskCreatedConsumer(INotificationService notifService) { _notifService = notifService; }

        public async Task Consume(ConsumeContext<TaskCreatedEvent> context)
        {
            var evt = context.Message;
            if (evt.AssignedToUserId.HasValue)
                await _notifService.CreateAndPushAsync(evt.AssignedToUserId.Value, "TaskAssigned", "New task assigned", $"You have been assigned: {evt.TaskName}", evt.TaskId.ToString());
        }
    }

    public class TaskAssignedConsumer : IConsumer<TaskAssignedEvent>
    {
        private readonly INotificationService _notifService;
        public TaskAssignedConsumer(INotificationService notifService) { _notifService = notifService; }

        public async Task Consume(ConsumeContext<TaskAssignedEvent> context)
        {
            var evt = context.Message;
            await _notifService.CreateAndPushAsync(evt.NewAssignedToUserId, "TaskAssigned", "Task assigned to you", $"You have been assigned to task: {evt.TaskName}", evt.TaskId.ToString());
        }
    }

    public class TaskDeadlineConsumer : IConsumer<TaskDeadlineEvent>
    {
        private readonly INotificationService _notifService;
        public TaskDeadlineConsumer(INotificationService notifService) { _notifService = notifService; }

        public async Task Consume(ConsumeContext<TaskDeadlineEvent> context)
        {
            var evt = context.Message;
            await _notifService.CreateAndPushAsync(evt.AssignedToUserId, "TaskDeadline", "Task sắp hết hạn", $"Task \"{evt.TaskName}\" sẽ hết hạn lúc {evt.DueDate:dd/MM/yyyy HH:mm}", evt.TaskId.ToString());
        }
    }

    public class MessageSentConsumer : IConsumer<MessageSentEvent>
    {
        private readonly INotificationService _notifService;
        public MessageSentConsumer(INotificationService notifService) { _notifService = notifService; }

        public async Task Consume(ConsumeContext<MessageSentEvent> context)
        {
            var evt = context.Message;
            if (evt.OtherMemberUserIds.Count > 0)
                await _notifService.CreateBulkAndPushAsync(evt.OtherMemberUserIds, "NewMessage", $"New message from {evt.SenderUserName}", evt.Content, evt.GroupId.ToString());
        }
    }

    public class GroupCreatedConsumer : IConsumer<GroupCreatedEvent>
    {
        private readonly INotificationService _notifService;
        public GroupCreatedConsumer(INotificationService notifService) { _notifService = notifService; }

        public async Task Consume(ConsumeContext<GroupCreatedEvent> context)
        {
            var evt = context.Message;
            if (evt.MemberUserIds.Count > 0)
                await _notifService.CreateBulkAndPushAsync(evt.MemberUserIds, "GroupInvite", "New Chat Group", $"You have been added to group: {evt.GroupName}", evt.GroupId.ToString());
        }
    }

    public class ReactionAddedConsumer : IConsumer<ReactionAddedEvent>
    {
        private readonly INotificationService _notifService;
        public ReactionAddedConsumer(INotificationService notifService) { _notifService = notifService; }

        public async Task Consume(ConsumeContext<ReactionAddedEvent> context)
        {
            var evt = context.Message;
            await _notifService.CreateAndPushAsync(evt.MessageOwnerUserId, "Reaction", $"{evt.ReactedByUserName} reacted {evt.ReactionType}", "Reacted to your message", evt.GroupId.ToString());
        }
    }
}
