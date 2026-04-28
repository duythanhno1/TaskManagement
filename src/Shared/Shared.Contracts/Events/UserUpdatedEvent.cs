namespace Shared.Contracts.Events
{
    public record UserUpdatedEvent
    {
        public int UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public DateTime UpdatedAt { get; init; }
    }
}
