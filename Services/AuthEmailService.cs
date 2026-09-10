using System.Net.Http.Headers;
namespace PigPocket.Api.Services;

public class AuthEmailService(HttpClient client, IConfiguration configuration, ILogger<AuthEmailService> logger)
{
    public async Task SendCode(string email, string code, bool reset)
    {
        var key = configuration["Resend:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new AuthException(503, "email_unavailable", "Email delivery is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        var purpose = reset ? "Reset your password" : "Verify your email";
        request.Content = JsonContent.Create(new {
            from = configuration["Resend:From"] ?? "Piggy Pockets <onboarding@resend.dev>",
            to = new[] { email }, subject = $"{purpose} — Piggy Pockets",
            html = $"<div style='font-family:Arial;background:#fffdf6;padding:32px;color:#004c3b'><h1>Piggy Pockets</h1><h2>{purpose}</h2><p>Your code is:</p><p style='font-size:36px;letter-spacing:8px;font-weight:bold'>{code}</p><p>This code expires in 10 minutes. Do not share it.</p><p>If you did not request this, you can ignore this email.</p></div>",
            text = $"{purpose}: {code}. This code expires in 10 minutes. Do not share it. If you did not request this, ignore this email."
        });
        try
        {
            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode) return;
            logger.LogWarning("Resend rejected an authentication email with HTTP {Status}", (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning("Resend authentication email delivery failed ({Type})", ex.GetType().Name);
        }
        throw new AuthException(503, "email_unavailable", "We could not send your code. Please try again shortly.");
    }
}

public class AuthException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
