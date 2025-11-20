using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;
using EnvisionAnalytics.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

namespace EnvisionAnalytics.Services
{
    public class OrdersMonitorService : BackgroundService
    {
        private readonly IServiceProvider _prov;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<EnvisionAnalytics.Hubs.DashboardHub> _hub;

        public OrdersMonitorService(IServiceProvider prov, Microsoft.AspNetCore.SignalR.IHubContext<EnvisionAnalytics.Hubs.DashboardHub> hub)
        {
            _prov = prov;
            _hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _prov.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var since = DateTime.UtcNow.AddMinutes(-10);
                    var count = await db.Orders.CountAsync(o => o.OrderDate >= since && o.Status == "Completed", stoppingToken);
                    await _hub.Clients.All.SendAsync("ordersLast10m", count, cancellationToken: stoppingToken);
                }
                catch (Exception)
                {
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
