using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Managerment.Model
{
    [Table("MessageReactions")]
    public class MessageReaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ReactionId { get; set; }

        [ForeignKey("Message")]
        public int MessageId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(20)]
        public string ReactionType { get; set; } // like, love, laugh, wow, sad

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ChatMessage Message { get; set; }
        public User User { get; set; }
    }
}
