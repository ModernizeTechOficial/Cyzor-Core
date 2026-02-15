using System.Text.RegularExpressions;
using Cyzor.Provisioning.DTOs;

namespace Cyzor.Provisioning.Services;

public interface IProvisioningRequestValidator
{
    (bool IsValid, string? ErrorMessage) Validate(ProvisioningRequest request);
}

public class ProvisioningRequestValidator : IProvisioningRequestValidator
{
    private static readonly Regex DomainRegex = new(
        @"^(?!-)[a-zA-Z0-9-]{1,63}(?<!-)(\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
        RegexOptions.Compiled
    );

    private static readonly HashSet<string> ValidAppTypes = new() { "node", "python", "static" };

    public (bool IsValid, string? ErrorMessage) Validate(ProvisioningRequest request)
    {
        if (request == null)
            return (false, "Request cannot be null");

        if (string.IsNullOrWhiteSpace(request.Domain))
            return (false, "Domain is required and cannot be empty");

        if (request.Domain.Length > 253)
            return (false, "Domain cannot exceed 253 characters");

        if (!DomainRegex.IsMatch(request.Domain))
            return (false, "Domain format is invalid. Must be a valid FQDN");

        if (string.IsNullOrWhiteSpace(request.AppType))
            request.AppType = "node";

        if (!ValidAppTypes.Contains(request.AppType.ToLower()))
            return (false, $"AppType '{request.AppType}' is not supported. Valid types: {string.Join(", ", ValidAppTypes)}");

        return (true, null);
    }
}
