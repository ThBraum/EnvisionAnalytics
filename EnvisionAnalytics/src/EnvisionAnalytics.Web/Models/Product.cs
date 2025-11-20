namespace EnvisionAnalytics.Models
{
    public class Product
    {
        public Guid ProductId { get; set; }
        public string Name { get; set; } = null!;
        public string Category { get; set; } = null!;
        public string? Brand { get; set; }
        public decimal Cost { get; set; }
        public decimal Price { get; set; }
    }
}
