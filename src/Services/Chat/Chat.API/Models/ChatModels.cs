using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Shared.Contracts.Interfaces;

namespace Chat.API.Models
{
    [Table("ChatGroups")]
    public class ChatGroup : ISoftDeletable
    {
        [Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int GroupId { get; set; }
        [Required][MaxLength(200)]
        public string GroupName { get; set; } = string.Empty;
        public bool IsDirectMessage { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int CreatedByUserId { get; set; }
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public ICollection<ChatGroupMember> Members { get; set; } = new List<ChatGroupMember>();
        public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    [Table("ChatGroupMembers")]
    public class ChatGroupMember
    {
        [Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int UserId { get; set; }
        [MaxLength(100)]
        public string? UserFullName { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.Now;
        public ChatGroup Group { get; set; } = null!;
    }

    [Table("ChatMessages")]
    public class ChatMessage : ISoftDeletable
    {
        [Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int MessageId { get; set; }
        public int GroupId { get; set; }
        public int SenderUserId { get; set; }
        [MaxLength(100)]
        public string? SenderUserName { get; set; }
        [Required][MaxLength(4000)]
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.Now;
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
        public ChatGroup Group { get; set; } = null!;
        public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
        public ICollection<MessageReadStatus> ReadStatuses { get; set; } = new List<MessageReadStatus>();
    }

    [Table("MessageReactions")]
    public class MessageReaction
    {
        [Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int UserId { get; set; }
        [MaxLength(50)]
        public string ReactionType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public ChatMessage Message { get; set; } = null!;
    }

    [Table("MessageReadStatuses")]
    public class MessageReadStatus
    {
        [Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int UserId { get; set; }
        public DateTime ReadAt { get; set; } = DateTime.Now;
        public ChatMessage Message { get; set; } = null!;
    }
}
