namespace Cyzor.Provisioning.Services;

public interface IApiAuthenticationService
{
    bool ValidateApiKey(string? providedKey);
}

public class ApiAuthenticationService : IApiAuthenticationService
{
    private readonly string _validApiKey;

    public ApiAuthenticationService(string apiKey)
    {
        _validApiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
    }

    public bool ValidateApiKey(string? providedKey)
    {
        if (string.IsNullOrWhiteSpace(providedKey))
            return false;

        // Use constant-time comparison to prevent timing attacks
        return CryptographicEquals(_validApiKey, providedKey);
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a == null || b == null) return false;
        if (a.Length != b.Length) return false;

        int result = 0;
        for (int i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}
