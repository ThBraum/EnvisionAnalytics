using System.ComponentModel.DataAnnotations;

namespace EnvisionAnalytics.Models
{
    public class CreateOrderRequest
    {
        [Required]
        public string CustomerEmail { get; set; } = null!;

        public string? Channel { get; set; } = "Web";

        [Required]
        public List<CreateOrderItem>? Items { get; set; }
    }

    public class CreateOrderItem
    {
        [Required]
        public Guid ProductId { get; set; }
        [Range(1, 1000)]
        public int Quantity { get; set; }
    }
}
