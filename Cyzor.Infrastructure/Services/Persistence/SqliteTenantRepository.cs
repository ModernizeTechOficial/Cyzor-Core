using System.Data;
using System.Threading;
using Microsoft.Data.Sqlite;
using Cyzor.Core.Domain.Entities;
using Cyzor.Core.Domain.Interfaces;

namespace Cyzor.Infrastructure.Services.Persistence;

public class SqliteTenantRepository : ITenantRepository
{
    private readonly string _dbPath;

    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SqliteTenantRepository()
    {
        _dbPath = "/var/www/cyzor_dotnet/tenants.db";
        _initialized = false;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS tenants (
            id TEXT PRIMARY KEY,
            domain TEXT NOT NULL,
            state TEXT NOT NULL,
            port INTEGER NULL,
            created_at TEXT NOT NULL,
            updated_at TEXT NULL
        );";
            await cmd.ExecuteNonQueryAsync();
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task CreateAsync(TenantRecord tenant)
    {
        await EnsureInitializedAsync();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO tenants (id,domain,state,port,created_at,updated_at) VALUES ($id,$domain,$state,$port,$created,$updated);";
        cmd.Parameters.AddWithValue("$id", tenant.Id.ToString());
        cmd.Parameters.AddWithValue("$domain", tenant.Domain);
        cmd.Parameters.AddWithValue("$state", tenant.State);
        cmd.Parameters.AddWithValue("$port", tenant.Port.HasValue ? (object)tenant.Port.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$created", tenant.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$updated", tenant.UpdatedAt.HasValue ? (object)tenant.UpdatedAt.Value.ToString("o") : DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateStateAsync(Guid id, string state)
    {
        await EnsureInitializedAsync();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tenants SET state=$state, updated_at=$updated WHERE id=$id;";
        cmd.Parameters.AddWithValue("$state", state);
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$id", id.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SetPortAsync(Guid id, int port)
    {
        await EnsureInitializedAsync();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE tenants SET port=$port, updated_at=$updated WHERE id=$id;";
        cmd.Parameters.AddWithValue("$port", port);
        cmd.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$id", id.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<TenantRecord?> GetByIdAsync(Guid id)
    {
        await EnsureInitializedAsync();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id,domain,state,port,created_at,updated_at FROM tenants WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        using var rdr = await cmd.ExecuteReaderAsync();
        if (!await rdr.ReadAsync()) return null;
        return Map(rdr);
    }

    public async Task<TenantRecord?> GetByDomainAsync(string domain)
    {
        await EnsureInitializedAsync();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id,domain,state,port,created_at,updated_at FROM tenants WHERE domain=$domain;";
        cmd.Parameters.AddWithValue("$domain", domain);
        using var rdr = await cmd.ExecuteReaderAsync();
        if (!await rdr.ReadAsync()) return null;
        return Map(rdr);
    }

    private TenantRecord Map(SqliteDataReader rdr)
    {
        var tr = new TenantRecord();
        tr.Id = Guid.Parse(rdr.GetString(0));
        tr.Domain = rdr.GetString(1);
        tr.State = rdr.GetString(2);
        tr.Port = rdr.IsDBNull(3) ? null : rdr.GetInt32(3);
        tr.CreatedAt = DateTime.Parse(rdr.GetString(4));
        tr.UpdatedAt = rdr.IsDBNull(5) ? null : DateTime.Parse(rdr.GetString(5));
        return tr;
    }
}
