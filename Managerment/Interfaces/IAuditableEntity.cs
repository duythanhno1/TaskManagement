namespace Managerment.Interfaces
{
    /// <summary>
    /// Marker interface for entities that support audit tracking.
    /// Entities implementing this will have their changes automatically logged.
    /// </summary>
    public interface IAuditableEntity
    {
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }
}
