namespace EnvisionAnalytics.Models
{
    public class Event
    {
        public Guid EventId { get; set; }
        public DateTime Timestamp { get; set; }
        public string Type { get; set; } = null!;
        public string Message { get; set; } = null!;
        public string Severity { get; set; } = "Info";
    }
}
