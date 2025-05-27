using System.ComponentModel.DataAnnotations;

namespace Managerment.DTO
{
    public class TaskAssignDTO
    {
        [Required]
        public int TaskId { get; set; }

        [Required]
        public int NewAssignedToUserId { get; set; }
    }
}
