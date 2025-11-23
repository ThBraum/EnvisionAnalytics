using EnvisionAnalytics.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnvisionAnalytics.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;
        public HomeController(ApplicationDbContext db, IConfiguration config) { _db = db; _config = config; }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.UtcNow.Date;
            var revenueToday = await _db.Orders.Where(o => o.OrderDate >= today && o.Status == "Completed").SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
            var ordersToday = await _db.Orders.CountAsync(o => o.OrderDate >= today && o.Status == "Completed");
            var activeUsers = await _db.Customers.CountAsync(c => c.CreatedAt >= DateTime.UtcNow.AddDays(-7));

            // Determine conversion based on current request culture. Default stored values assume USD.
            var culture = System.Threading.Thread.CurrentThread.CurrentCulture.Name ?? "en-US";
            var usdToBrl = _config.GetValue<decimal?>("CurrencyRates:USDToBRL") ?? 5.5m;
            var usdToArs = _config.GetValue<decimal?>("CurrencyRates:USDToARS") ?? 350m;
            decimal multiplier = 1m;
            if (culture.Equals("pt-BR", StringComparison.OrdinalIgnoreCase)) multiplier = usdToBrl;
            else if (culture.Equals("es-ES", StringComparison.OrdinalIgnoreCase)) multiplier = usdToArs;

            // Convert revenue for display according to culture
            var displayedRevenue = Math.Round(revenueToday * multiplier, 2);

            ViewData["RevenueToday"] = displayedRevenue;
            ViewData["OrdersToday"] = ordersToday;
            ViewData["ActiveUsers"] = activeUsers;

            // Expose conversion rates and culture to the view for client-side chart conversion
            ViewData["CurrentCulture"] = culture;
            ViewData["UsdToBrl"] = (double)usdToBrl;
            ViewData["UsdToArs"] = (double)usdToArs;

            return View();
        }

        [HttpGet]
        public IActionResult LearnMore()
        {
            return View("~/Views/Home/LearnMore.cshtml");
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
