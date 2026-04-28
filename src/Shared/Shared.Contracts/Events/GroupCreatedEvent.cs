namespace Shared.Contracts.Events
{
    public record GroupCreatedEvent
    {
        public int GroupId { get; init; }
        public string GroupName { get; init; } = string.Empty;
        public int CreatedByUserId { get; init; }
        public List<int> MemberUserIds { get; init; } = new();
        public DateTime CreatedAt { get; init; }
    }
}
