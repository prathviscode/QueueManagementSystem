namespace QueueManagementSystem.Models
{
    public class Token
    {
        public int TokenId { get; set; }
        public int TokenNumber { get; set; }

        // FIX: Default values ensure non-nullable strings satisfy the compiler
        // and also provide sensible DB defaults.
        public string Department { get; set; } = "General";
        public int? CounterNumber { get; set; }
        public string Status { get; set; } = "Waiting";
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow; // FIX: Use UtcNow instead of Now for consistent time storage
    }
}
