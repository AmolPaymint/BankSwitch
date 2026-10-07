using BankSwitch.Application;
using Microsoft.Extensions.Configuration;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryConfigurationRuntimeProbe : IConfigurationRuntimeProbe
{
    private readonly IConfiguration _configuration;
    public InMemoryConfigurationRuntimeProbe(IConfiguration configuration) => _configuration = configuration;

    public Task<IReadOnlyCollection<ConfigurationDiagnostic>> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        IReadOnlyCollection<ConfigurationDiagnostic> values = new[]
        {
            new ConfigurationDiagnostic("Repository Provider", "Simulation", null, now, _configuration["Repository:Provider"] ?? "InMemory"),
            new ConfigurationDiagnostic("Control Plane", "Healthy", null, now, "InMemory test/simulation repository active. Not permitted in Production.")
        };
        return Task.FromResult(values);
    }
}
