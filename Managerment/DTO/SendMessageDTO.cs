using System.ComponentModel.DataAnnotations;

namespace Managerment.DTO
{
    public class SendMessageDTO
    {
        [Required]
        public int GroupId { get; set; }

        [Required]
        [MaxLength(4000)]
        public string Content { get; set; }
    }
}
