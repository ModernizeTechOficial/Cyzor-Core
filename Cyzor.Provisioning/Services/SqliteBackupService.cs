using Cyzor.Infrastructure.Services.Command;

namespace Cyzor.Provisioning.Services;

public interface IBackupService : IHostedService
{
    Task PerformBackupAsync();
}

public class SqliteBackupService : IBackupService
{
    private readonly ICommandExecutor _executor;
    private readonly ILogger<SqliteBackupService> _logger;
    private Timer? _backupTimer;
    private const string DbPath = "/var/www/cyzor_dotnet/tenants.db";
    private const string BackupDir = "/var/www/cyzor_dotnet/backups";
    private const int BackupIntervalMinutes = 60; // Backup every hour
    private const int RetentionDays = 7; // Keep backups for 7 days

    public SqliteBackupService(ICommandExecutor executor, ILogger<SqliteBackupService> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"[BACKUP] Starting SQLite auto-backup service (interval: {BackupIntervalMinutes} minutes)");
        
        // Ensure backup directory exists
        try
        {
            _executor.ExecuteAsync($"mkdir -p {BackupDir}").Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BACKUP] Warning: Could not create backup directory: {ex.Message}");
        }

        // Start timer
        _backupTimer = new Timer(
            async _ => await PerformBackupAsync(),
            null,
            TimeSpan.FromMinutes(5), // First backup after 5 minutes
            TimeSpan.FromMinutes(BackupIntervalMinutes)
        );

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("[BACKUP] Stopping backup service");
        _backupTimer?.Dispose();
        return Task.CompletedTask;
    }

    public async Task PerformBackupAsync()
    {
        try
        {
            Console.WriteLine("[BACKUP] Starting backup of SQLite database");
            
            // Create timestamped backup
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupFile = $"{BackupDir}/tenants_db_{timestamp}.db";
            
            // Copy database file
            await _executor.ExecuteAsync($"cp {DbPath} {backupFile}");
            Console.WriteLine($"[BACKUP] ✓ Database backed up to {backupFile}");
            
            // Compress backup
            var compressedFile = $"{backupFile}.gz";
            await _executor.ExecuteAsync($"gzip {backupFile}");
            Console.WriteLine($"[BACKUP] ✓ Compressed to {compressedFile}");
            
            // Clean old backups (older than RetentionDays)
            await CleanOldBackupsAsync();
            
            Console.WriteLine("[BACKUP] Backup completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BACKUP] ✗ Backup failed: {ex.Message}");
        }
    }

    private async Task CleanOldBackupsAsync()
    {
        try
        {
            Console.WriteLine($"[BACKUP] Cleaning backups older than {RetentionDays} days");
            var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);
            var findCommand = $"find {BackupDir} -name 'tenants_db_*.db.gz' -mtime +{RetentionDays} -delete";
            await _executor.ExecuteAsync(findCommand);
            Console.WriteLine($"[BACKUP] ✓ Old backups cleaned");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BACKUP] Warning: Could not clean old backups: {ex.Message}");
        }
    }
}
