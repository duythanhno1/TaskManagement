using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Managerment.Model
{
    [Table("audit_logs")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AuditLogId { get; set; }

        [Required, MaxLength(100)]
        public string EntityName { get; set; } = string.Empty;

        public string EntityId { get; set; } = string.Empty;

        [Required, MaxLength(20)]
        public string Action { get; set; } = string.Empty; // Create, Update, Delete

        public string? OldValues { get; set; } // JSON

        public string? NewValues { get; set; } // JSON

        public string? ChangedColumns { get; set; } // JSON array

        public int? UserId { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Navigation
        public User? User { get; set; }
    }
}
