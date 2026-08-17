using System.Text.Json;

namespace RemoteTickets.Infrastructure.Setup;

/// <summary>Persists the installation master-database connection string used by the tenant catalog.</summary>
public interface ISetupConfigurationStore
{
    /// <summary>Gets the configured master-database connection string.</summary>
    /// <returns>The configured connection string, or <see langword="null"/> when setup has not stored one.</returns>
    string? GetMasterConnectionString();

    /// <summary>Validates and persists the master-database connection string.</summary>
    /// <param name="connectionString">The SQL Server connection string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed task when persistence succeeds.</returns>
    Task SetMasterConnectionStringAsync(string connectionString, CancellationToken cancellationToken);
}

/// <summary>Stores setup configuration in an application-local file until a dedicated secrets store is configured.</summary>
public sealed class SetupConfigurationStore : ISetupConfigurationStore
{
    private readonly string _path = Path.Combine(AppContext.BaseDirectory, "data", "setup.json");

    /// <inheritdoc />
    public string? GetMasterConnectionString()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        try
        {
            State? state = JsonSerializer.Deserialize<State>(File.ReadAllText(_path));
            return state?.MasterConnectionString;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SetMasterConnectionStringAsync(string connectionString, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A master database connection string is required.", nameof(connectionString));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string json = JsonSerializer.Serialize(new State(connectionString));
        await File.WriteAllTextAsync(_path, json, cancellationToken);
    }

    private sealed record State(string MasterConnectionString);
}
