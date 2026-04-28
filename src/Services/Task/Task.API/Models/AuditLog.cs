using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Task.API.Models
{
    [Table("AuditLogs")]
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AuditLogId { get; set; }

        [MaxLength(100)]
        public string EntityName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string EntityId { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Action { get; set; } = string.Empty;

        public int? UserId { get; set; }

        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public string? ChangedColumns { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
