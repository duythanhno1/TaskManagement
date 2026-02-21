using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Managerment.Model
{
    [Table("ChatGroups")]
    public class ChatGroup
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GroupId { get; set; }

        [Required]
        [MaxLength(200)]
        public string GroupName { get; set; }

        public bool IsDirectMessage { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("CreatedByUser")]
        public int CreatedByUserId { get; set; }

        public User CreatedByUser { get; set; }

        public ICollection<ChatGroupMember> Members { get; set; } = new List<ChatGroupMember>();
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
