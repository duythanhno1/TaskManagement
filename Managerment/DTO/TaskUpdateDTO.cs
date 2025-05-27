using System.ComponentModel.DataAnnotations;

namespace Managerment.DTO
{
    public class TaskUpdateDTO
    {
        [Required]
        public int TaskId { get; set; }

        [MaxLength(200)]
        public string TaskName { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        public int? AssignedTo { get; set; }

        [Required]
        [EnumDataType(typeof(Managerment.Model.TaskStatus))]
        public string Status { get; set; } 
    }
}
