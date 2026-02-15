namespace Cyzor.Core.Domain.Interfaces;

public interface IBlueprintInstaller
{
    Task InstallAsync(Guid instanceId);
}
