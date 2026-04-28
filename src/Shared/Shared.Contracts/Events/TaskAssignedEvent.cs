namespace Shared.Contracts.Events
{
    public record TaskAssignedEvent
    {
        public int TaskId { get; init; }
        public string TaskName { get; init; } = string.Empty;
        public int? OldAssignedToUserId { get; init; }
        public int NewAssignedToUserId { get; init; }
        public string NewAssignedToUserName { get; init; } = string.Empty;
        public DateTime AssignedAt { get; init; }
    }
}
