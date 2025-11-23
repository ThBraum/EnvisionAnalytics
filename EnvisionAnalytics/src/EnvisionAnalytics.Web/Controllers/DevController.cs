using EnvisionAnalytics.Data;
using EnvisionAnalytics.Hubs;
using EnvisionAnalytics.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EnvisionAnalytics.Controllers
{
    [ApiController]
    [Route("api/dev")]
    public class DevController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<DashboardHub> _hub;
        private readonly IWebHostEnvironment _env;

        public DevController(ApplicationDbContext db, IHubContext<DashboardHub> hub, IWebHostEnvironment env)
        {
            _db = db;
            _hub = hub;
            _env = env;
        }

        [HttpPost("create-orders")]
        public async Task<IActionResult> CreateOrders(int count = 10)
        {
            if (!_env.IsDevelopment()) return Forbid();

            var products = await _db.Products.ToListAsync();
            if (products == null || products.Count == 0) return BadRequest("No products available to create orders.");

            var customers = await _db.Customers.ToListAsync();
            var rnd = new Random();
            var created = new List<Guid>();

            for (int i = 0; i < count; i++)
            {
                var cust = customers.Count > 0 ? customers[rnd.Next(customers.Count)] : null;
                if (cust == null)
                {
                    cust = new Customer { CustomerId = Guid.NewGuid(), Name = $"dev_user_{i}", Email = $"dev+{i}@example.com", CreatedAt = DateTime.UtcNow };
                    _db.Customers.Add(cust);
                    customers.Add(cust);
                }

                var order = new Order
                {
                    OrderId = Guid.NewGuid(),
                    CustomerId = cust.CustomerId,
                    OrderDate = DateTime.UtcNow,
                    Status = "Completed",
                    Channel = "Web",
                    Items = new List<OrderItem>()
                };

                var pick = products.OrderBy(p => rnd.Next()).First();
                var qty = rnd.Next(1, 4);
                var oi = new OrderItem { OrderItemId = Guid.NewGuid(), OrderId = order.OrderId, ProductId = pick.ProductId, Quantity = qty, UnitPrice = pick.Price };
                order.Items.Add(oi);
                order.TotalAmount = Math.Round(oi.UnitPrice * oi.Quantity, 2);
                order.ItemsCount = qty;

                _db.Orders.Add(order);
                created.Add(order.OrderId);

                await _db.SaveChangesAsync();
                await _hub.Clients.All.SendAsync("event", new { type = "OrderCreated", orderId = order.OrderId, amount = order.TotalAmount, customer = cust.Email, timestamp = order.OrderDate });
            }

            return Ok(new { created = created.Count });
        }
    }
}
