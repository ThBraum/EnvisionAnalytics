using EnvisionAnalytics.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnvisionAnalytics.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        public HomeController(ApplicationDbContext db) { _db = db; }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.UtcNow.Date;
            var revenueToday = await _db.Orders.Where(o => o.OrderDate >= today && o.Status == "Completed").SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
            var ordersToday = await _db.Orders.CountAsync(o => o.OrderDate >= today && o.Status == "Completed");
            var activeUsers = await _db.Customers.CountAsync(c => c.CreatedAt >= DateTime.UtcNow.AddDays(-7));

            ViewData["RevenueToday"] = revenueToday;
            ViewData["OrdersToday"] = ordersToday;
            ViewData["ActiveUsers"] = activeUsers;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> RevenueByDay(int days = 30)
        {
            var start = DateTime.UtcNow.Date.AddDays(-days + 1);
            var rows = await _db.Orders
                .Where(o => o.OrderDate >= start && o.Status == "Completed")
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new { Date = g.Key, Revenue = g.Sum(x => x.TotalAmount), Orders = g.Count() })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Json(rows);
        }
    }
}
