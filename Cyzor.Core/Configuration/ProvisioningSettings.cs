namespace Cyzor.Core.Configuration;

public class ProvisioningSettings
{
    public string Host { get; set; } = default!;
    public string User { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string AppType { get; set; } = "node"; // node ou dotnet
    public string BuildsBasePath { get; set; } = "/var/www/builds";
    public string AppsBasePath { get; set; } = "/var/www";
}
