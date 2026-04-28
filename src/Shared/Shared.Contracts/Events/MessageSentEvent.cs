namespace Shared.Contracts.Events
{
    public record MessageSentEvent
    {
        public int MessageId { get; init; }
        public int GroupId { get; init; }
        public string GroupName { get; init; } = string.Empty;
        public int SenderUserId { get; init; }
        public string SenderUserName { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public List<int> OtherMemberUserIds { get; init; } = new();
        public DateTime SentAt { get; init; }
    }
}
