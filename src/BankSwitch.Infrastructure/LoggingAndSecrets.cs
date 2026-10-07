using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

public sealed class ConfigurationSecretProvider : ISecretProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationSecretProvider(IConfiguration configuration) => _configuration = configuration;

    public Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var value = _configuration[$"Secrets:{name}"] ?? Environment.GetEnvironmentVariable(name) ?? Environment.GetEnvironmentVariable(name.Replace(':', '_'));
        if (!string.IsNullOrWhiteSpace(value) && value.StartsWith("${VAULT:", StringComparison.OrdinalIgnoreCase) && value.EndsWith("}", StringComparison.Ordinal))
        {
            var vaultName = value[8..^1];
            value = Environment.GetEnvironmentVariable(vaultName) ?? Environment.GetEnvironmentVariable(vaultName.Replace(':', '_'));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Secret '{name}' was not supplied by the vault/secret provider.");
        }
        return Task.FromResult(value);
    }
}

public sealed class StructuredAuditLogger : IAuditLogger
{
    private readonly ILogger<StructuredAuditLogger> _logger;

    public StructuredAuditLogger(ILogger<StructuredAuditLogger> logger) => _logger = logger;

    public void LogTransaction(TransactionLog log)
    {
        _logger.LogInformation(
            "TransactionLog CorrelationId={CorrelationId} Source={Source} Sink={Sink} MTI={Mti} MaskedPAN={MaskedPan} STAN={Stan} RRN={Rrn} Amount={Amount} Currency={Currency} ResponseCode={ResponseCode} LatencyMs={LatencyMs} Route={Route} Scheme={Scheme} Fee={Fee} ReversalState={ReversalState} MacStatus={MacStatus}",
            log.CorrelationId,
            log.SourceNodeId,
            log.SinkNodeId,
            log.Mti,
            log.MaskedPan,
            log.Stan,
            log.Rrn,
            log.Amount,
            log.CurrencyCode,
            log.ResponseCode,
            log.LatencyMilliseconds,
            log.RouteUsed,
            log.SchemeUsed,
            log.FeeApplied,
            log.ReversalState,
            log.MacValidationStatus);
    }

    public void LogSecurity(string correlationId, string eventName, string message) =>
        _logger.LogWarning("SecurityLog CorrelationId={CorrelationId} Event={Event} Message={Message}", correlationId, eventName, CardholderDataProtector.RedactLogText(message));

    public void LogAdminAudit(string correlationId, string actor, string action, string oldValue, string newValue, string reason, string ticketReference) =>
        _logger.LogInformation("AdminAudit CorrelationId={CorrelationId} Actor={Actor} Action={Action} OldValue={OldValue} NewValue={NewValue} Reason={Reason} Ticket={Ticket}", correlationId, actor, action, CardholderDataProtector.RedactLogText(oldValue), CardholderDataProtector.RedactLogText(newValue), CardholderDataProtector.RedactLogText(reason), ticketReference);

    public void LogSystem(string correlationId, string eventName, Exception? exception = null)
    {
        if (exception is null)
        {
            _logger.LogInformation("SystemLog CorrelationId={CorrelationId} Event={Event}", correlationId, CardholderDataProtector.RedactLogText(eventName));
            return;
        }
        _logger.LogError(exception, "SystemLog CorrelationId={CorrelationId} Event={Event}", correlationId, CardholderDataProtector.RedactLogText(eventName));
    }

    public void LogReconciliation(string correlationId, string settlementProfile, string status, string details) =>
        _logger.LogInformation("ReconciliationLog CorrelationId={CorrelationId} SettlementProfile={SettlementProfile} Status={Status} Details={Details}", correlationId, settlementProfile, status, CardholderDataProtector.RedactLogText(details));
}

/// <summary>
/// Configuration-backed ISecretVaultProvider for dev/test.
/// Wraps the existing ConfigurationSecretProvider with the vault management interface.
/// In production, replace with AzureKeyVaultSecretProvider or HashiCorpVaultSecretProvider.
/// </summary>
public sealed class ConfigurationSecretVaultProvider : ISecretVaultProvider
{
    private readonly IConfiguration _configuration;
    public ConfigurationSecretVaultProvider(IConfiguration configuration) => _configuration = configuration;
    public string ProviderName => "Configuration";
    public bool IsAvailable => true;
    public Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(_configuration[$"Secrets:{name}"] ?? string.Empty);
    public Task<CmsOperationResult<string>> SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
        => Task.FromResult(CmsOperationResult<string>.Fail("57", "Configuration provider is read-only. Use Azure Key Vault or HashiCorp Vault in production."));
    public Task<CmsOperationResult<bool>> DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(CmsOperationResult<bool>.Fail("57", "Configuration provider is read-only."));
    public Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(new List<string> { "— Configuration secrets are not enumerable. Use appsettings.json or environment variables. —" });
}
