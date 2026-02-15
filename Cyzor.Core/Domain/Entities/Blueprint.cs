namespace Cyzor.Core.Domain.Entities;

public class Blueprint
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Repository { get; private set; } = default!;
    public string InstallScript { get; private set; } = default!;
    public int RequiredCpu { get; private set; }
    public int RequiredMemoryMb { get; private set; }

    public Blueprint() { }

    public Blueprint(string name, string repository, string installScript)
    {
        Id = Guid.NewGuid();
        Name = name;
        Repository = repository;
        InstallScript = installScript;
    }
}
