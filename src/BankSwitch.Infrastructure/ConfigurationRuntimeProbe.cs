using BankSwitch.Application;
using Microsoft.Extensions.Configuration;

namespace BankSwitch.Infrastructure;

public sealed class ConfigurationRuntimeProbe : IConfigurationRuntimeProbe
{
    //private readonly SecureSqlConnectionFactory _sql;
    private readonly SecurePostgresConnectionFactory _sql;
    private readonly IConfiguration _configuration;public ConfigurationRuntimeProbe(SecurePostgresConnectionFactory sql, IConfiguration configuration)
    {
        _sql = sql;
        _configuration = configuration;
    }

  /*  public ConfigurationRuntimeProbe(SecureSqlConnectionFactory sql, IConfiguration configuration)
    {
        _sql = sql;
        _configuration = configuration;
    }*/

    public async Task<IReadOnlyCollection<ConfigurationDiagnostic>> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ConfigurationDiagnostic>();
        var now = DateTimeOffset.UtcNow;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var cn = await _sql.OpenAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();
            results.Add(new("SQL Server", "Healthy", sw.Elapsed.TotalMilliseconds, now, "Enterprise configuration repository reachable."));
        }
        catch (Exception ex)
        {
            sw.Stop();
            results.Add(new("SQL Server", "Unhealthy", sw.Elapsed.TotalMilliseconds, now, ex.Message));
        }

        var repositoryProvider = _configuration["Repository:Provider"] ?? "InMemory";
        results.Add(new("Repository Provider", string.Equals(repositoryProvider, "SqlServer", StringComparison.OrdinalIgnoreCase) ? "Healthy" : "Warning", null, now, repositoryProvider));
        var hsm = _configuration["Hsm:Mode"] ?? "Http";
        results.Add(new("HSM Mode", hsm.Contains("Bypass", StringComparison.OrdinalIgnoreCase) ? "Warning" : "Healthy", null, now, hsm));
        var sensitive = _configuration["SensitiveData:Mode"] ?? "AesGcm";
        results.Add(new("Sensitive Data", string.Equals(sensitive, "AesGcm", StringComparison.OrdinalIgnoreCase) ? "Healthy" : "Warning", null, now, sensitive));
        results.Add(new("Control Plane", "Healthy", null, now, "v44.1 enterprise configuration control plane active."));
        return results;
    }
}
