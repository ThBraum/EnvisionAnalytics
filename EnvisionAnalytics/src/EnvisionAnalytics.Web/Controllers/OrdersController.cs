using EnvisionAnalytics.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace EnvisionAnalytics.Controllers
{
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<EnvisionAnalytics.Hubs.DashboardHub> _hub;

        public OrdersController(ApplicationDbContext db, Microsoft.AspNetCore.SignalR.IHubContext<EnvisionAnalytics.Hubs.DashboardHub> hub) { _db = db; _hub = hub; }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> List(DateTime? start, DateTime? end, string? status, decimal? minAmount, int page = 1, int pageSize = 25, string? sortBy = "OrderDate", string? sortDir = "desc")
        {
            var q = _db.Orders.Include(o=>o.Customer).AsQueryable();
            if (start.HasValue) q = q.Where(o => o.OrderDate >= start.Value);
            if (end.HasValue) q = q.Where(o => o.OrderDate <= end.Value);
            if (!string.IsNullOrEmpty(status)) q = q.Where(o => o.Status == status);
            if (minAmount.HasValue) q = q.Where(o => o.TotalAmount >= minAmount.Value);

            var total = await q.CountAsync();

            bool asc = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
            q = (sortBy ?? "OrderDate") switch
            {
                "TotalAmount" => asc ? q.OrderBy(o => o.TotalAmount) : q.OrderByDescending(o => o.TotalAmount),
                "Customer" => asc ? q.OrderBy(o => o.Customer!.Name) : q.OrderByDescending(o => o.Customer!.Name),
                _ => asc ? q.OrderBy(o => o.OrderDate) : q.OrderByDescending(o => o.OrderDate),
            };

            var items = await q.Skip((page-1)*pageSize).Take(pageSize).ToListAsync();

            return Json(new { total, items });
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(DateTime? start, DateTime? end, string? status, decimal? minAmount)
        {
            var q = _db.Orders.Include(o=>o.Customer).AsQueryable();
            if (start.HasValue) q = q.Where(o => o.OrderDate >= start.Value);
            if (end.HasValue) q = q.Where(o => o.OrderDate <= end.Value);
            if (!string.IsNullOrEmpty(status)) q = q.Where(o => o.Status == status);
            if (minAmount.HasValue) q = q.Where(o => o.TotalAmount >= minAmount.Value);

            var list = await q.OrderByDescending(o => o.OrderDate).ToListAsync();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("OrderId,OrderDate,Customer,Status,TotalAmount,ItemsCount,Channel");
            foreach(var o in list)
            {
                csv.AppendLine($"{o.OrderId},{o.OrderDate:o},\"{o.Customer?.Name}\",{o.Status},{o.TotalAmount},{o.ItemsCount},{o.Channel}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"orders_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
        }

        [HttpPost]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin,Analyst")]
        public async Task<IActionResult> Create([FromBody] EnvisionAnalytics.Models.CreateOrderRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email == req.CustomerEmail);
            if (customer == null)
            {
                customer = new EnvisionAnalytics.Models.Customer { CustomerId = Guid.NewGuid(), Email = req.CustomerEmail, Name = req.CustomerEmail.Split('@')[0], CreatedAt = DateTime.UtcNow };
                _db.Customers.Add(customer);
            }

            var order = new EnvisionAnalytics.Models.Order {
                OrderId = Guid.NewGuid(),
                CustomerId = customer.CustomerId,
                OrderDate = DateTime.UtcNow,
                Status = "Completed",
                Channel = req.Channel,
                Items = new List<EnvisionAnalytics.Models.OrderItem>()
            };

            decimal total = 0m;
            foreach(var it in req.Items ?? Enumerable.Empty<EnvisionAnalytics.Models.CreateOrderItem>())
            {
                var product = await _db.Products.FindAsync(it.ProductId);
                if (product == null) continue;
                var oi = new EnvisionAnalytics.Models.OrderItem { OrderItemId = Guid.NewGuid(), OrderId = order.OrderId, ProductId = product.ProductId, Quantity = it.Quantity, UnitPrice = product.Price };
                order.Items.Add(oi);
                total += oi.UnitPrice * oi.Quantity;
            }
            order.TotalAmount = Math.Round(total, 2);
            order.ItemsCount = order.Items.Sum(i => i.Quantity);

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();

            await _hub.Clients.All.SendAsync("event", new { type = "OrderCreated", orderId = order.OrderId, amount = order.TotalAmount, customer = customer.Email, timestamp = order.OrderDate });

            return CreatedAtAction(nameof(List), new { id = order.OrderId }, new { order.OrderId });
        }
    }
}
