namespace Cyzor.Provisioning.DTOs;

public class ProvisioningRequest
{
    public string Domain { get; set; } = default!;
    public string AppType { get; set; } = "node";
}

public class ProvisioningResponse
{
    public Guid InstanceId { get; set; }
    public string Domain { get; set; } = default!;
    public string Status { get; set; } = "Queued";
}
