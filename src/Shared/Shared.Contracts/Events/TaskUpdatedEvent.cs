namespace Shared.Contracts.Events
{
    public record TaskUpdatedEvent
    {
        public int TaskId { get; init; }
        public string TaskName { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public int? AssignedToUserId { get; init; }
        public DateTime UpdatedAt { get; init; }
    }
}
