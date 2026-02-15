namespace Cyzor.Core.Domain.Enums;

public enum LifecycleState
{
    Requested = 0,
    Validating = 1,
    AllocatingResources = 2,
    CreatingEnvironment = 3,
    InstallingBlueprint = 4,
    ConfiguringDomain = 5,
    HealthChecking = 6,
    Finalizing = 7,
    Running = 8,
    RollingBack = 50,
    Failed = 99
}
