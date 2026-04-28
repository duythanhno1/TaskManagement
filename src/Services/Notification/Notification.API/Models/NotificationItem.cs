using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Notification.API.Models
{
    [Table("Notifications")]
    public class NotificationItem
    {
        [Key][DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int NotificationId { get; set; }
        public int UserId { get; set; }
        [Required][MaxLength(50)]
        public string Type { get; set; } = string.Empty;
        [Required][MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string? Content { get; set; }
        [MaxLength(100)]
        public string? ReferenceId { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
