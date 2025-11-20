namespace EnvisionAnalytics.Models
{
    public class Order
    {
        public Guid OrderId { get; set; }
        public Guid CustomerId { get; set; }
        public Customer? Customer { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = "New";
        public decimal TotalAmount { get; set; }
        public int ItemsCount { get; set; }
        public string? Channel { get; set; }
        public List<OrderItem> Items { get; set; } = new();
    }
}
