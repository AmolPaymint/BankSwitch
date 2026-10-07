using BankSwitch.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BankSwitch.Infrastructure;

public sealed class SecurePostgresConnectionFactory
{
    private readonly string _connectionString;
    private readonly DatabasePerformanceOptions _perf;
    private readonly ILogger<SecurePostgresConnectionFactory>? _logger;

    /// <summary>
    /// Default PostgreSQL command timeout in seconds.
    /// Applied per-command by callers.
    /// </summary>
    public int CommandTimeout => _perf.CommandTimeoutSeconds;

    public SecurePostgresConnectionFactory(
        IConfiguration configuration,
        DatabasePerformanceOptions? performanceOptions = null,
        ILogger<SecurePostgresConnectionFactory>? logger = null)
    {
        var raw =
            Environment.GetEnvironmentVariable("SWITCHDB_CONNECTION_STRING")
            ?? configuration.GetConnectionString("SwitchDb")
            ?? throw new InvalidOperationException(
                "Connection string 'SwitchDb' is required.");

        ValidateConnectionString(raw);

        _perf = performanceOptions ?? new DatabasePerformanceOptions();
        _logger = logger;

        // PostgreSQL/Npgsql connection pooling.
        // Npgsql manages a connection pool for each unique connection string.
        var builder = new NpgsqlConnectionStringBuilder(raw)
        {
            Pooling = true,
            MinPoolSize = _perf.MinPoolSize,
            MaxPoolSize = _perf.MaxPoolSize,
            Timeout = _perf.ConnectionTimeoutSeconds
        };

        _connectionString = builder.ConnectionString;

        _logger?.LogInformation(
            "PostgreSQL connection pool configured: MinPool={Min} MaxPool={Max} ConnTimeout={ConnTimeout}s CmdTimeout={CmdTimeout}s",
            _perf.MinPoolSize,
            _perf.MaxPoolSize,
            _perf.ConnectionTimeoutSeconds,
            _perf.CommandTimeoutSeconds);
    }

    /// <summary>
    /// Returns a new pooled NpgsqlConnection.
    /// Caller must OpenAsync() and Dispose().
    /// </summary>
    public NpgsqlConnection Create()
    {
        return new NpgsqlConnection(_connectionString);
    }

    /// <summary>
    /// Opens a PostgreSQL connection and warns when opening
    /// takes longer than the configured slow-open threshold.
    /// </summary>
    public async Task<NpgsqlConnection> OpenAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = Create();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await connection
                .OpenAsync(cancellationToken)
                .ConfigureAwait(false);

            sw.Stop();

            if (sw.ElapsedMilliseconds > _perf.SlowConnectionThresholdMs)
            {
                _logger?.LogWarning(
                    "PostgreSQL connection open latency {ElapsedMs}ms exceeds threshold {ThresholdMs}ms. Connection pool may be exhausted (Max={MaxPool}). Consider increasing Database:MaxPoolSize.",
                    sw.ElapsedMilliseconds,
                    _perf.SlowConnectionThresholdMs,
                    _perf.MaxPoolSize);
            }
            return connection;
        }
        catch
        {
            await connection
                .DisposeAsync()
                .ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Validates the PostgreSQL connection string.
    /// </summary>
    public static void ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL connection string cannot be empty.");
        }

        if (connectionString.Contains(
                "${VAULT",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Resolve vault placeholders before starting or supply SWITCHDB_CONNECTION_STRING.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);

       // var userId = Convert.ToString(builder["User ID"]);
        var userId = Convert.ToString(builder["Username"]);
        if (string.Equals(builder.Username, "sa", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production connection strings must not use the Postgress SQL Server postgres account.");

        var password = builder.Password;

        if (string.Equals(
                password,
                "redmond",
                StringComparison.OrdinalIgnoreCase)
            ||
            string.Equals(
                password,
                "password",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Sample/default PostgreSQL passwords are prohibited.");
        }

        // PostgreSQL SSL/TLS validation.
        //
        // For production, use certificate validation.
        // VerifyFull is preferred when PostgreSQL certificates/DNS
        // are configured correctly.
       /* if (builder.SslMode == SslMode.Disable)
        {
            throw new InvalidOperationException(
                "PostgreSQL TLS is mandatory. Configure SSL Mode=Require or VerifyFull.");
        }

        if (builder.SslMode == SslMode.Allow
            || builder.SslMode == SslMode.Prefer)
        {
            throw new InvalidOperationException(
                "PostgreSQL TLS must be explicitly enabled. Use SSL Mode=Require or VerifyFull.");
        }*/
    }
}
