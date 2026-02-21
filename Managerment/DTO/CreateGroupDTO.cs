using System.ComponentModel.DataAnnotations;

namespace Managerment.DTO
{
    public class CreateGroupDTO
    {
        [Required]
        [MaxLength(200)]
        public string GroupName { get; set; }

        [Required]
        public List<int> MemberUserIds { get; set; }

        public bool IsDirectMessage { get; set; } = false;
    }
}
