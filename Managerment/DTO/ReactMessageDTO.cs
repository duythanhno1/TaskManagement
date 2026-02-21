using System.ComponentModel.DataAnnotations;

namespace Managerment.DTO
{
    public class ReactMessageDTO
    {
        [Required]
        public int MessageId { get; set; }

        [Required]
        [MaxLength(20)]
        [RegularExpression("^(like|love|laugh|wow|sad)$", ErrorMessage = "ReactionType must be: like, love, laugh, wow, or sad")]
        public string ReactionType { get; set; }
    }
}
