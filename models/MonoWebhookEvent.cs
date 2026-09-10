namespace PigPocket.Api.Models;

public class MonoWebhookEvent
{
    public Guid Id { get; set; }
    public string EventId { get; set; } = "";
    public string Event { get; set; } = "";
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
