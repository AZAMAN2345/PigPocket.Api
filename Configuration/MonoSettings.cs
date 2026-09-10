namespace PigPocket.Api.Configuration;

public class MonoSettings
{
    public string BaseUrl { get; set; } = "https://api.withmono.com/";
    public string SecretKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string RedirectUrl { get; set; } = "";
}
