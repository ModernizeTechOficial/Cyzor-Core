namespace Cyzor.Provisioning.Services;

public interface IAlertService
{
    Task SendRollbackAlertAsync(Guid tenantId, string domain, string reason);
}

public class AlertService : IAlertService
{
    private readonly HttpClient _httpClient;
    private readonly string? _webhookUrl;
    private readonly string? _webhookSecret;

    public AlertService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        _webhookUrl = Environment.GetEnvironmentVariable("ALERT_WEBHOOK_URL");
        _webhookSecret = Environment.GetEnvironmentVariable("ALERT_WEBHOOK_SECRET");
    }

    public async Task SendRollbackAlertAsync(Guid tenantId, string domain, string reason)
    {
        var timestamp = DateTime.UtcNow.ToString("O");
        var message = $"[ALERT] Rollback executed for tenant {domain} (ID: {tenantId}) at {timestamp}. Reason: {reason}";
        
        Console.WriteLine(message);
        Console.WriteLine($"[ALERT] Log message written to systemd journal");

        // Send to webhook if configured
        if (!string.IsNullOrEmpty(_webhookUrl))
        {
            try
            {
                await SendWebhookAsync(tenantId, domain, reason, timestamp);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ALERT] Warning: Failed to send webhook: {ex.Message}");
            }
        }
    }

    private async Task SendWebhookAsync(Guid tenantId, string domain, string reason, string timestamp)
    {
        var payload = new
        {
            EventType = "Rollback",
            TenantId = tenantId,
            Domain = domain,
            Reason = reason,
            Timestamp = timestamp,
            Severity = "high"
        };

        var jsonContent = System.Text.Json.JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

        // Add optional HMAC signature
        if (!string.IsNullOrEmpty(_webhookSecret))
        {
            var signature = ComputeHmacSha256(jsonContent, _webhookSecret);
            content.Headers.Add("X-Webhook-Signature", $"sha256={signature}");
        }

        try
        {
            Console.WriteLine($"[ALERT] Sending webhook to {_webhookUrl}");
            var response = await _httpClient.PostAsync(_webhookUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ALERT] ✓ Webhook sent successfully (status {response.StatusCode})");
            }
            else
            {
                Console.WriteLine($"[ALERT] ✗ Webhook failed (status {response.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ALERT] ✗ Webhook error: {ex.Message}");
            throw;
        }
    }

    private string ComputeHmacSha256(string message, string secret)
    {
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(message));
        return System.Convert.ToHexString(hash).ToLower();
    }
}
