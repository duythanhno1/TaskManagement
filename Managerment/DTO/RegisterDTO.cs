using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Managerment.DTO
{
    public class RegisterDTO
    {
        [Required]
        public string FullName {  get; set; }

        [Required]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        [Required]
        public string PhoneNumber {  get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public string Role { get; set; } = "User";
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
