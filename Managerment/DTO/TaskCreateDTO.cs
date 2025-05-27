using System.ComponentModel.DataAnnotations;

namespace Managerment.DTO
{
    public class TaskCreateDTO
    {
        [Required]
        [MaxLength(200)]
        public string TaskName { get; set; }

        [MaxLength(1000)]
        public string Description { get; set; }

        public int? AssignedTo { get; set; } // User ID
    }
}
