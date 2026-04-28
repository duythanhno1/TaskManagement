using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Contracts.Interfaces;

namespace Task.API.Models
{
    public enum TaskItemStatus
    {
        Todo,
        InProgress,
        Completed
    }

    [Table("TaskItems")]
    public class TaskItem : ISoftDeletable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TaskId { get; set; }

        [Required]
        [MaxLength(200)]
        public string TaskName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        public int? AssignedTo { get; set; }

        /// <summary>
        /// Denormalized from Auth Service — updated via UserUpdatedEvent
        /// </summary>
        [MaxLength(100)]
        public string? AssignedToUserName { get; set; }

        public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? DueDate { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Soft Delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
