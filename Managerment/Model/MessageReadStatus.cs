using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Managerment.Model
{
    [Table("MessageReadStatuses")]
    public class MessageReadStatus
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [ForeignKey("Message")]
        public int MessageId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public DateTime ReadAt { get; set; } = DateTime.Now;

        public ChatMessage Message { get; set; }
        public User User { get; set; }
    }
}
