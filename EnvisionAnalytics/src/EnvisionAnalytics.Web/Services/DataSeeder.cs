using System;
using System.Data;
using System.Linq;
using Bogus;
using EnvisionAnalytics.Data;
using EnvisionAnalytics.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EnvisionAnalytics.Services
{
    public class DataSeeder
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _um;
        private readonly RoleManager<Microsoft.AspNetCore.Identity.IdentityRole> _rm;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<EnvisionAnalytics.Hubs.DashboardHub> _hub;
        
        public DataSeeder(ApplicationDbContext db, UserManager<ApplicationUser> um, RoleManager<Microsoft.AspNetCore.Identity.IdentityRole> rm, Microsoft.AspNetCore.SignalR.IHubContext<EnvisionAnalytics.Hubs.DashboardHub> hub)
        {
            _db = db;
            _um = um;
            _rm = rm;
            _hub = hub;
        }

        public async Task SeedIfEmptyAsync()
        {
            try
            {
                await _db.Database.MigrateAsync();
            }
            catch
            {
            }

            var availableMigrations = _db.Database.GetMigrations();
            var hasAvailableMigrations = availableMigrations != null && availableMigrations.Any();
            if (!hasAvailableMigrations)
            {
                var historyExists = await TableExistsAsync("__EFMigrationsHistory");
                if (historyExists)
                {
                    Console.WriteLine("DataSeeder: found __EFMigrationsHistory but no migrations in assembly — dropping history and calling EnsureCreated.");
                    await DropTableIfExistsAsync("__EFMigrationsHistory");
                    try { await _db.Database.EnsureCreatedAsync(); } catch { }
                }
            }

            var productsTableExists = await TableExistsAsync("Products");
            if (!productsTableExists)
            {
                try { await _db.Database.MigrateAsync(); } catch { }
                try { await _db.Database.EnsureCreatedAsync(); } catch { }
                productsTableExists = await TableExistsAsync("Products");
            }

            if (!productsTableExists)
            {
                Console.WriteLine("DataSeeder: Products table not found after migrations/EnsureCreated - skipping seeding.");
                return;
            }

            // If SEED_ALWAYS is set (1/true) then clear existing data and re-seed every startup.
            var seedAlwaysEnv = Environment.GetEnvironmentVariable("SEED_ALWAYS");
            var forceSeed = !string.IsNullOrEmpty(seedAlwaysEnv) && (seedAlwaysEnv == "1" || seedAlwaysEnv.Equals("true", StringComparison.OrdinalIgnoreCase));
            if (forceSeed)
            {
                Console.WriteLine("DataSeeder: SEED_ALWAYS set — clearing existing data before seeding.");
                try
                {
                    await _db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"OrderItems\" RESTART IDENTITY CASCADE; TRUNCATE TABLE \"Orders\" RESTART IDENTITY CASCADE; TRUNCATE TABLE \"Events\" RESTART IDENTITY CASCADE; TRUNCATE TABLE \"Customers\" RESTART IDENTITY CASCADE; TRUNCATE TABLE \"Products\" RESTART IDENTITY CASCADE;");
                }
                catch
                {
                    try
                    {
                        _db.OrderItems.RemoveRange(_db.OrderItems);
                        _db.Orders.RemoveRange(_db.Orders);
                        _db.Events.RemoveRange(_db.Events);
                        _db.Customers.RemoveRange(_db.Customers);
                        _db.Products.RemoveRange(_db.Products);
                        await _db.SaveChangesAsync();
                    }
                    catch { }
                }
            }

            if (await _db.Products.AnyAsync()) return;

            // seed products
            var categories = new[] { "Electronics", "Home", "Clothing", "Books", "Outdoor" };
            var productFaker = new Faker<Product>("pt_BR")
                .RuleFor(p => p.ProductId, f => Guid.NewGuid())
                .RuleFor(p => p.Name, f => f.Commerce.ProductName())
                .RuleFor(p => p.Category, f => f.PickRandom(categories))
                .RuleFor(p => p.Brand, f => f.Company.CompanyName())
                .RuleFor(p => p.Cost, f => decimal.Parse(f.Commerce.Price(5, 80)))
                .RuleFor(p => p.Price, (f, p) => Math.Round(p.Cost * (decimal)f.Random.Double(1.2, 3.0), 2));

            var products = productFaker.Generate(50);
            _db.Products.AddRange(products);

            // customers
            var customerFaker = new Faker<Customer>("pt_BR")
                .RuleFor(c => c.CustomerId, f => Guid.NewGuid())
                .RuleFor(c => c.Name, f => f.Person.FullName)
                .RuleFor(c => c.Email, f => f.Internet.Email())
                .RuleFor(c => c.CreatedAt, f => DateTime.SpecifyKind(f.Date.Past(1), DateTimeKind.Utc));

            var customers = customerFaker.Generate(200);
            _db.Customers.AddRange(customers);

            // orders
            var rnd = new Random();
            var orders = new List<Order>();
            for (int i = 0; i < 1000; i++)
            {
                var cust = customers[rnd.Next(customers.Count)];
                var date = DateTime.UtcNow.AddDays(-rnd.Next(0, 90)).AddMinutes(-rnd.Next(0, 1440));
                var itemsCount = rnd.Next(1, 6);
                var order = new Order
                {
                    OrderId = Guid.NewGuid(),
                    CustomerId = cust.CustomerId,
                    OrderDate = date,
                    Status = rnd.NextDouble() < 0.9 ? "Completed" : "Cancelled",
                    ItemsCount = itemsCount,
                    Channel = rnd.NextDouble() < 0.7 ? "Web" : "Mobile"
                };

                var chosen = products.OrderBy(p => rnd.Next()).Take(itemsCount).ToList();
                decimal total = 0;
                foreach (var p in chosen)
                {
                    var qty = rnd.Next(1, 4);
                    var oi = new OrderItem
                    {
                        OrderItemId = Guid.NewGuid(),
                        OrderId = order.OrderId,
                        ProductId = p.ProductId,
                        Quantity = qty,
                        UnitPrice = p.Price
                    };
                    order.Items.Add(oi);
                    total += oi.UnitPrice * oi.Quantity;
                }
                order.TotalAmount = Math.Round(total, 2);
                orders.Add(order);
            }
            _db.Orders.AddRange(orders);

            // events
            var events = new List<Event>();
            foreach (var o in orders.Take(200))
            {
                events.Add(new Event { EventId = Guid.NewGuid(), Timestamp = o.OrderDate, Type = "OrderCreated", Message = $"Order {o.OrderId} created", Severity = "Info" });
            }
            _db.Events.AddRange(events);

            await _db.SaveChangesAsync();

            // ensure roles exist
            var roles = new[] { "Admin", "Analyst", "Viewer" };
            foreach (var r in roles)
            {
                if (!await _rm.RoleExistsAsync(r))
                    await _rm.CreateAsync(new Microsoft.AspNetCore.Identity.IdentityRole(r));
            }

            // seed admin user
            var adminEmail = "admin@envision.local";
            if (await _um.FindByEmailAsync(adminEmail) == null)

            // send some seeded events to hub (structured JSON)
            foreach (var ev in events.Take(20))
            {
                await _hub.Clients.All.SendAsync("event", new { type = ev.Type, timestamp = ev.Timestamp, message = ev.Message, severity = ev.Severity });
            }

            {
                var admin = new ApplicationUser { UserName = "admin", Email = adminEmail, EmailConfirmed = true };
                await _um.CreateAsync(admin, "P@ssw0rd!");
                await _um.AddToRoleAsync(admin, "Admin");
            }
        }

        private async Task<bool> TableExistsAsync(string tableName)
        {
            var conn = _db.Database.GetDbConnection();
            try
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='public' AND c.relname = @name)";
                var param = cmd.CreateParameter();
                param.ParameterName = "name";
                param.Value = tableName;
                cmd.Parameters.Add(param);

                var res = await cmd.ExecuteScalarAsync();
                return Convert.ToBoolean(res);
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        private async Task DropTableIfExistsAsync(string tableName)
        {
            var conn = _db.Database.GetDbConnection();
            try
            {
                if (conn.State != ConnectionState.Open)
                    await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"DROP TABLE IF EXISTS \"{tableName}\";";
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                if (conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }
    }
}
