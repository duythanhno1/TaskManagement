using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Managerment.Model
{
    [Table("ChatGroupMembers")]
    public class ChatGroupMember
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey("Group")]
        public int GroupId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.Now;

        public ChatGroup Group { get; set; }
        public User User { get; set; }
    }
}
