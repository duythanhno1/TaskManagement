namespace Shared.Contracts.Events
{
    public record TaskDeletedEvent
    {
        public int TaskId { get; init; }
        public string TaskName { get; init; } = string.Empty;
        public int? AssignedToUserId { get; init; }
        public DateTime DeletedAt { get; init; }
    }
}
