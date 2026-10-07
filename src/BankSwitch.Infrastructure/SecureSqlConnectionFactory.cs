using BankSwitch.Application;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

public sealed class SecureSqlConnectionFactory
{
    private readonly string _connectionString;
    private readonly DatabasePerformanceOptions _perf;
    private readonly ILogger<SecureSqlConnectionFactory>? _logger;

    /// <summary>Default SQL command timeout in seconds — applied per-command by callers.</summary>
    public int CommandTimeout => _perf.CommandTimeoutSeconds;

    public SecureSqlConnectionFactory(
        IConfiguration configuration,
        DatabasePerformanceOptions? performanceOptions = null,
        ILogger<SecureSqlConnectionFactory>? logger = null)
    {
        var raw = Environment.GetEnvironmentVariable("SWITCHDB_CONNECTION_STRING")
            ?? configuration.GetConnectionString("SwitchDb")
            ?? throw new InvalidOperationException("Connection string 'SwitchDb' is required.");
        ValidateConnectionString(raw);

        _perf = performanceOptions ?? new DatabasePerformanceOptions();
        _logger = logger;

        // B5: Apply connection pool tuning to the connection string at construction time.
        // ADO.NET uses a global pool per unique connection string, so settings applied here
        // govern the pool created by the first Open() call.
        var builder = new SqlConnectionStringBuilder(raw)
        {
            Pooling = true,
            MinPoolSize = _perf.MinPoolSize,
            MaxPoolSize = _perf.MaxPoolSize,
            ConnectTimeout = _perf.ConnectionTimeoutSeconds,
            MultipleActiveResultSets = _perf.MultipleActiveResultSets
        };
        _connectionString = builder.ConnectionString;

        _logger?.LogInformation(
            "SQL connection pool configured: MinPool={Min} MaxPool={Max} ConnTimeout={ConnTimeout}s CmdTimeout={CmdTimeout}s",
            _perf.MinPoolSize, _perf.MaxPoolSize, _perf.ConnectionTimeoutSeconds, _perf.CommandTimeoutSeconds);
    }

    /// <summary>Returns a new (pooled) SqlConnection. Caller must Open() and Dispose().</summary>
    public SqlConnection Create() => new(_connectionString);

    /// <summary>Opens a connection and warns when it takes longer than the slow-open threshold (pool exhaustion indicator).</summary>
    public async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = Create();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();
            if (sw.ElapsedMilliseconds > _perf.SlowConnectionThresholdMs)
                _logger?.LogWarning(
                    "SQL connection open latency {ElapsedMs}ms exceeds threshold {ThresholdMs}ms. Pool may be exhausted (Max={MaxPool}). Consider increasing Database:MaxPoolSize.",
                    sw.ElapsedMilliseconds, _perf.SlowConnectionThresholdMs, _perf.MaxPoolSize);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public static void ValidateConnectionString(string connectionString)
    {
        if (connectionString.Contains("${VAULT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolve vault placeholders before starting or supply SWITCHDB_CONNECTION_STRING.");

        var builder = new SqlConnectionStringBuilder(connectionString);
        var userId = Convert.ToString(builder["User ID"]);
        if (string.Equals(userId, "sa", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production connection strings must not use the SQL Server sa account.");

        var password = Convert.ToString(builder["Password"]);
        if (string.Equals(password, "redmond", StringComparison.OrdinalIgnoreCase) || string.Equals(password, "password", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Sample/default SQL passwords are prohibited.");

        var encrypt = Convert.ToString(builder["Encrypt"]);
        if (string.IsNullOrWhiteSpace(encrypt) || string.Equals(encrypt, "False", StringComparison.OrdinalIgnoreCase) || string.Equals(encrypt, "No", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("SQL Server TLS is mandatory. Set Encrypt=True or Encrypt=Mandatory.");

        var trustServerCertificate = Convert.ToString(builder["TrustServerCertificate"]);
        if (string.Equals(trustServerCertificate, "True", StringComparison.OrdinalIgnoreCase) || string.Equals(trustServerCertificate, "Yes", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("TrustServerCertificate=False is required for production SQL TLS.");
    }
}
