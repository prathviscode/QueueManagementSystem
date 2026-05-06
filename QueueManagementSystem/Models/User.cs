using System.ComponentModel.DataAnnotations;

namespace QueueManagementSystem.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        // FIX: Initialise to empty string so the nullable compiler is satisfied.
        // Validated in AuthController to be "Admin" or "Staff" before saving.
        public string Role { get; set; } = string.Empty; // Admin / Staff

        public int? CounterNumber { get; set; } // Staff-assigned counter
    }
}
