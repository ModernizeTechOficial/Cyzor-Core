using System.Diagnostics;

namespace Cyzor.Infrastructure.Services.Command;

public class LocalCommandExecutor : ICommandExecutor
{
    public async Task<string> ExecuteAsync(string command, string? workingDirectory = null)
    {
        Console.WriteLine($"[LOCAL] Executing: {command}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"-c \"{command}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.WriteLine("[LOCAL] Failed to start process (null process)");
                return string.Empty;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(error))
                Console.WriteLine($"[LOCAL] Error: {error}");

            Console.WriteLine($"[LOCAL] Output: {output}");

            return output;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOCAL] Exception while executing command: {ex.Message}");
            return $"ERROR: {ex.Message}";
        }
    }
}
