namespace Cyzor.Infrastructure.Services.Command;

public interface ICommandExecutor
{
    Task<string> ExecuteAsync(string command, string? workingDirectory = null);
}
