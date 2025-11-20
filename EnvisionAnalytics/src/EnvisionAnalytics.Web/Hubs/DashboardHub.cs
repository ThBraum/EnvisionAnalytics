using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace EnvisionAnalytics.Hubs
{
    public class DashboardHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> Connections = new();

        public override Task OnConnectedAsync()
        {
            Connections[Context.ConnectionId] = Context.User?.Identity?.Name ?? "anonymous";
            Clients.All.SendAsync("usersOnline", Connections.Count);
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            Connections.TryRemove(Context.ConnectionId, out _);
            Clients.All.SendAsync("usersOnline", Connections.Count);
            return base.OnDisconnectedAsync(exception);
        }
    }
}
