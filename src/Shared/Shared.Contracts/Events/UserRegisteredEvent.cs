namespace Shared.Contracts.Events
{
    public record UserRegisteredEvent
    {
        public int UserId { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Role { get; init; } = "User";
        public DateTime CreatedAt { get; init; }
    }
}
