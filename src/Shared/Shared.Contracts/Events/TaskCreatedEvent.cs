namespace Shared.Contracts.Events
{
    public record TaskCreatedEvent
    {
        public int TaskId { get; init; }
        public string TaskName { get; init; } = string.Empty;
        public string? Description { get; init; }
        public int? AssignedToUserId { get; init; }
        public string? AssignedToUserName { get; init; }
        public string Status { get; init; } = "Todo";
        public DateTime CreatedAt { get; init; }
    }
}
