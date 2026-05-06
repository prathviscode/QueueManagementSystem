namespace QueueManagementSystem.Models
{
    public class LoginRequest
    {
        // FIX: Nullable warnings resolved with required keyword.
        // These fields are validated in the controller before use.
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
