using EnvisionAnalytics.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EnvisionAnalytics.Controllers
{
    [Route("api/products")]
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public ProductsController(ApplicationDbContext db) { _db = db; }

        [HttpGet("random")]
        public async Task<IActionResult> Random()
        {
            var p = await _db.Products.OrderBy(x => Guid.NewGuid()).Select(x => new { x.ProductId, x.Name }).FirstOrDefaultAsync();
            if (p == null) return NotFound();
            return Json(p);
        }

        [HttpGet("top/{count:int?}")]
        public async Task<IActionResult> Top(int count = 10)
        {
            var list = await _db.Products.Take(count).Select(x => new { x.ProductId, x.Name, x.Category, x.Price }).ToListAsync();
            return Json(list);
        }
    }
}
