using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace Managerment.Model
{
    public enum TaskStatus
    {
        Todo,
        InProgress,
        Completed
    }

    [Table("TaskItems")]
    public class TaskItem
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

        public DateTime? UpdatedAt { get; set; }

        public User AssignedToUser { get; set; } 
    }
}
