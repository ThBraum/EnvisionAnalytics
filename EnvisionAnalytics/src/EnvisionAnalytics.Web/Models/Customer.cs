namespace EnvisionAnalytics.Models
{
    public class Customer
    {
        public Guid CustomerId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
