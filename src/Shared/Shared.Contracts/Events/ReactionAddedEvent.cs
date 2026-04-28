namespace Shared.Contracts.Events
{
    public record ReactionAddedEvent
    {
        public int MessageId { get; init; }
        public int GroupId { get; init; }
        public int ReactedByUserId { get; init; }
        public string ReactedByUserName { get; init; } = string.Empty;
        public string ReactionType { get; init; } = string.Empty;
        public int MessageOwnerUserId { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
