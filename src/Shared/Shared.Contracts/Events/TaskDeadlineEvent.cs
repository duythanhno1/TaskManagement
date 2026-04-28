namespace Shared.Contracts.Events
{
    public record TaskDeadlineEvent
    {
        public int TaskId { get; init; }
        public string TaskName { get; init; } = string.Empty;
        public int AssignedToUserId { get; init; }
        public DateTime DueDate { get; init; }
    }
}
