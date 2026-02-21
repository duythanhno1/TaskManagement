using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Managerment.Interfaces;

namespace Managerment.Model
{
    [Table("ChatMessages")]
    public class ChatMessage : ISoftDeletable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MessageId { get; set; }

        [ForeignKey("Group")]
        public int GroupId { get; set; }

        [ForeignKey("SenderUser")]
        public int SenderUserId { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Content { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;

        // ISoftDeletable
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }

        public ChatGroup Group { get; set; }
        public User SenderUser { get; set; }
        public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
        public ICollection<MessageReadStatus> ReadStatuses { get; set; } = new List<MessageReadStatus>();
    }
}
