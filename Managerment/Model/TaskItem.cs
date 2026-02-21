using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Managerment.Interfaces;

namespace Managerment.Model
{
    public enum TaskStatus
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
        public string TaskName { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        [ForeignKey("AssignedToUser")]
        public int? AssignedTo { get; set; } 

        public TaskStatus Status { get; set; } = TaskStatus.Todo;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? DueDate { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Soft Delete
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public User AssignedToUser { get; set; } 
    }
}
