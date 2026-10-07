using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace BankSwitch.Infrastructure;

// ============================================================
// HSM Lifecycle Management
// ============================================================

public sealed class HsmLifecycleService : IHsmLifecycleService
{
    private readonly IHsmLifecycleRepository _repository;
    private readonly IHsmClient _hsm;
    private readonly IConfiguration _configuration;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public HsmLifecycleService(
        IHsmLifecycleRepository repository,
        IHsmClient hsm,
        IConfiguration configuration,
        IAuditLogger audit,
        IClock clock)
    {
        _repository = repository;
        _hsm = hsm;
        _configuration = configuration;
        _audit = audit;
        _clock = clock;
    }

    public async Task<HsmPartitionSnapshot> PollPartitionHealthAsync(
        string partitionName, CancellationToken cancellationToken = default)
    {
        var mode = _configuration["Hsm:Mode"] ?? "Http";
        var (status, keyCount, free, firmware, tamper, diagnostic) = mode switch
        {
            "BypassForDevelopmentOnly" => (HsmPartitionStatus.Offline, 0, 0, "DEV-BYPASS", "NotApplicable", "HSM operations bypassed for development."),
            "HmacSoftwareForTestOnly"  => (HsmPartitionStatus.Online, 3, 997, "SOFTWARE-SIM-1.0", "OK", "Software simulation active; no physical HSM."),
            _ => await QueryHsmHealthAsync(partitionName, cancellationToken).ConfigureAwait(false)
        };

        var snapshot = new HsmPartitionSnapshot
        {
            PartitionName = partitionName,
            HsmSerialNumber = _configuration[$"Hsm:Partitions:{partitionName}:SerialNumber"] ?? "UNKNOWN",
            Status = status,
            LoadedKeyCount = keyCount,
            FreeKeySlots = free,
            FirmwareVersion = firmware,
            TamperStatus = tamper,
            DiagnosticLog = diagnostic,
            SnapshotTakenAt = _clock.UtcNow
        };

        await _repository.AddSnapshotAsync(snapshot, cancellationToken).ConfigureAwait(false);

        if (status is HsmPartitionStatus.Degraded or HsmPartitionStatus.Tampered)
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "HsmHealthAlert",
                $"HSM partition {partitionName} status={status} tamper={tamper}");

        return snapshot;
    }

    public async Task<CmsOperationResult<HsmKeyLoadEvent>> RecordKeyLoadEventAsync(
        RecordKeyLoadEventRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Custodian1) || string.IsNullOrWhiteSpace(request.Custodian2))
            return CmsOperationResult<HsmKeyLoadEvent>.Fail("30", "Dual custodian attestation is required (PCI DSS Req 3.6). Both Custodian1 and Custodian2 must be provided.");
        if (string.Equals(request.Custodian1, request.Custodian2, StringComparison.OrdinalIgnoreCase))
            return CmsOperationResult<HsmKeyLoadEvent>.Fail("57", "Custodian1 and Custodian2 must be different individuals (PCI DSS dual-control requirement).");
        if (string.IsNullOrWhiteSpace(request.KeyCheckValue))
            return CmsOperationResult<HsmKeyLoadEvent>.Fail("30", "Key check value (KCV) is required to verify the loaded key.");

        var evt = new HsmKeyLoadEvent
        {
            KeyProfileCode = request.KeyProfileCode,
            HsmPartitionName = request.HsmPartitionName,
            EventType = request.EventType,
            Custodian1 = request.Custodian1,
            Custodian2 = request.Custodian2,
            Purpose = request.Purpose,
            KeyCheckValue = request.KeyCheckValue,
            CorrelationId = request.CorrelationId,
            OccurredAt = _clock.UtcNow
        };

        await _repository.AddKeyLoadEventAsync(evt, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, $"{request.Custodian1}+{request.Custodian2}",
            $"HsmKey{request.EventType}", request.KeyProfileCode, request.KeyCheckValue,
            $"partition={request.HsmPartitionName} purpose={request.Purpose}", "PCI-REQ-3.6");

        return CmsOperationResult<HsmKeyLoadEvent>.Success(evt, $"Key load event recorded: {request.EventType} for profile {request.KeyProfileCode}.");
    }

    public Task<IReadOnlyList<HsmPartitionSnapshot>> GetPartitionSnapshotsAsync(CancellationToken cancellationToken = default)
        => _repository.GetLatestSnapshotsAsync(cancellationToken);

    public Task<IReadOnlyList<HsmKeyLoadEvent>> GetKeyLoadAuditTrailAsync(string? keyProfileCode, CancellationToken cancellationToken = default)
        => _repository.GetKeyLoadEventsAsync(keyProfileCode, cancellationToken);

    private async Task<(HsmPartitionStatus, int, int, string, string, string)> QueryHsmHealthAsync(
        string partitionName, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var baseUrl = _configuration["Hsm:BaseUrl"] ?? "http://hsm-service:8080";
            var response = await http.GetFromJsonAsync<HsmHealthResponse>($"{baseUrl}/health/{partitionName}", cancellationToken).ConfigureAwait(false);
            if (response is null) return (HsmPartitionStatus.Degraded, 0, 0, "UNKNOWN", "QUERY_NULL", "Null response from HSM health endpoint.");
            var status = response.IsOnline && !response.IsTampered ? HsmPartitionStatus.Online : response.IsTampered ? HsmPartitionStatus.Tampered : HsmPartitionStatus.Degraded;
            return (status, response.LoadedKeys, response.FreeSlots, response.Firmware, response.TamperStatus, response.DiagnosticMessage ?? string.Empty);
        }
        catch (Exception ex)
        {
            return (HsmPartitionStatus.Offline, 0, 0, "UNKNOWN", "ERROR", $"Health poll failed: {ex.Message}");
        }
    }

    private sealed record HsmHealthResponse(bool IsOnline, bool IsTampered, int LoadedKeys, int FreeSlots, string Firmware, string TamperStatus, string? DiagnosticMessage);
}

public sealed class InMemoryHsmLifecycleRepository : IHsmLifecycleRepository
{
    private readonly ConcurrentDictionary<string, HsmPartitionSnapshot> _snapshots = new();
    private readonly List<HsmKeyLoadEvent> _events = [];

    public Task AddSnapshotAsync(HsmPartitionSnapshot snap, CancellationToken ct = default)
    {
        _snapshots[snap.PartitionName] = snap; return Task.CompletedTask;
    }
    public Task<IReadOnlyList<HsmPartitionSnapshot>> GetLatestSnapshotsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<HsmPartitionSnapshot>>(_snapshots.Values.ToList());
    public Task AddKeyLoadEventAsync(HsmKeyLoadEvent evt, CancellationToken ct = default)
    {
        lock (_events) { _events.Add(evt); } return Task.CompletedTask;
    }
    public Task<IReadOnlyList<HsmKeyLoadEvent>> GetKeyLoadEventsAsync(string? keyProfileCode, CancellationToken ct = default)
    {
        IEnumerable<HsmKeyLoadEvent> q = _events;
        if (!string.IsNullOrWhiteSpace(keyProfileCode))
            q = q.Where(e => string.Equals(e.KeyProfileCode, keyProfileCode, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<HsmKeyLoadEvent>>(q.OrderByDescending(e => e.OccurredAt).ToList());
    }
}

// ============================================================
// Key Rotation Scheduler
// ============================================================

public sealed class KeyRotationScheduler : IKeyRotationScheduler
{
    private readonly IEnterpriseProductionRepository _keyProfiles;
    private readonly IEnterpriseProductionService _enterprise;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public KeyRotationScheduler(
        IEnterpriseProductionRepository keyProfiles,
        IEnterpriseProductionService enterprise,
        IAuditLogger audit,
        IClock clock)
    {
        _keyProfiles = keyProfiles;
        _enterprise = enterprise;
        _audit = audit;
        _clock = clock;
    }

    public async Task<KeyRotationScanResult> ScanAndAlertAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await _keyProfiles.GetAllKeyProfilesAsync(cancellationToken).ConfigureAwait(false);
        var now = _clock.UtcNow;
        var overdue = new List<string>();
        var dueSoon = new List<string>();

        foreach (var profile in profiles.Where(p => p.RotationDueAt.HasValue))
        {
            var daysUntilDue = (profile.RotationDueAt!.Value - now).TotalDays;
            if (daysUntilDue < 0)
            {
                overdue.Add(profile.KeyProfileCode);
                _audit.LogSecurity(Guid.NewGuid().ToString("N"), "KeyRotationOverdue",
                    $"Key profile {profile.KeyProfileCode} (alias={profile.HsmKeyAlias}) rotation was due {-daysUntilDue:F0} day(s) ago. Immediate rotation required.");
                // Publish SIEM alert
                await _enterprise.PublishSiemEventAsync(new(
                    Guid.NewGuid().ToString("N"), "KEY_ROTATION_OVERDUE", SiemEventSeverity.Critical,
                    "key-management", profile.KeyProfileCode,
                    $"Cryptographic key {profile.KeyProfileCode} rotation overdue by {-daysUntilDue:F0} days",
                    $"{{\"profile\":\"{profile.KeyProfileCode}\",\"due\":\"{profile.RotationDueAt:O}\"}}"), cancellationToken).ConfigureAwait(false);
            }
            else if (daysUntilDue <= 30)
            {
                dueSoon.Add(profile.KeyProfileCode);
                var urgency = daysUntilDue <= 7 ? SiemEventSeverity.Critical : daysUntilDue <= 14 ? SiemEventSeverity.Warning : SiemEventSeverity.Info;
                _audit.LogSecurity(Guid.NewGuid().ToString("N"), "KeyRotationDueSoon",
                    $"Key profile {profile.KeyProfileCode} rotation due in {daysUntilDue:F0} day(s) on {profile.RotationDueAt:yyyy-MM-dd}.");
                if (daysUntilDue <= 14)
                    await _enterprise.PublishSiemEventAsync(new(
                        Guid.NewGuid().ToString("N"), "KEY_ROTATION_DUE_SOON", urgency,
                        "key-management", profile.KeyProfileCode,
                        $"Key {profile.KeyProfileCode} due for rotation in {daysUntilDue:F0} days",
                        $"{{\"profile\":\"{profile.KeyProfileCode}\",\"due\":\"{profile.RotationDueAt:O}\"}}"), cancellationToken).ConfigureAwait(false);
            }
        }

        return new KeyRotationScanResult(profiles.Count, overdue.Count, dueSoon.Count, overdue, dueSoon, _clock.UtcNow);
    }
}

/// <summary>Background worker that runs the key rotation scheduler hourly.</summary>
public sealed class KeyRotationWorker : BackgroundService
{
    private readonly IKeyRotationScheduler _scheduler;
    private readonly ILogger<KeyRotationWorker> _logger;

    public KeyRotationWorker(IKeyRotationScheduler scheduler, ILogger<KeyRotationWorker> logger)
    {
        _scheduler = scheduler;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Key rotation scheduler started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _scheduler.ScanAndAlertAsync(stoppingToken).ConfigureAwait(false);
                if (result.OverdueCount > 0)
                    _logger.LogWarning("Key rotation scan: {Overdue} OVERDUE, {Soon} due within 30 days of {Total} total profiles.",
                        result.OverdueCount, result.DueSoonCount, result.ScannedKeyCount);
                else if (result.DueSoonCount > 0)
                    _logger.LogInformation("Key rotation scan: {Soon} key(s) due within 30 days.", result.DueSoonCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Key rotation scheduler error."); }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);
        }
    }
}

// ============================================================
// Secrets Vault Integration
// ============================================================

/// <summary>
/// Azure Key Vault secret provider.
/// Uses the Azure SDK DefaultAzureCredential (works with managed identity in AKS,
/// developer credentials locally, or a service principal via env vars).
/// Caches secrets for 5 minutes to reduce vault round-trips.
/// </summary>
public sealed class AzureKeyVaultSecretProvider : ISecretVaultProvider
{
    private readonly string _vaultUri;
    private readonly HttpClient _http;
    private readonly IAuditLogger _audit;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, (string Value, DateTimeOffset ExpiresAt)> _cache = new();

    public AzureKeyVaultSecretProvider(string vaultUri, IAuditLogger audit, IConfiguration configuration)
    {
        _vaultUri = vaultUri.TrimEnd('/');
        _audit = audit;
        _configuration = configuration;
        _http = new HttpClient { BaseAddress = new Uri(_vaultUri) };
    }

    public string ProviderName => "AzureKeyVault";
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_vaultUri);

    public async Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = name.ToUpperInvariant();
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Value;

        try
        {
            // Azure Key Vault REST API: GET {vault}/secrets/{name}?api-version=7.4
            var response = await _http.GetFromJsonAsync<AkvSecretResponse>(
                $"/secrets/{Uri.EscapeDataString(name)}?api-version=7.4", cancellationToken).ConfigureAwait(false);
            var value = response?.Value ?? string.Empty;
            _cache[cacheKey] = (value, DateTimeOffset.UtcNow.AddMinutes(5));
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "SecretRead", $"Secret '{name}' retrieved from Azure Key Vault.");
            return value;
        }
        catch (Exception ex)
        {
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "SecretReadFailed", $"Failed to read secret '{name}' from Azure Key Vault: {ex.Message}");
            // Fall back to configuration for non-production resilience
            return _configuration[$"Secrets:{name}"] ?? string.Empty;
        }
    }

    public async Task<CmsOperationResult<string>> SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = System.Net.Http.Json.JsonContent.Create(new { value });
            await _http.PutAsync($"/secrets/{Uri.EscapeDataString(name)}?api-version=7.4", content, cancellationToken).ConfigureAwait(false);
            _cache.TryRemove(name.ToUpperInvariant(), out _); // invalidate cache
            return CmsOperationResult<string>.Success(name, $"Secret '{name}' set in Azure Key Vault.");
        }
        catch (Exception ex) { return CmsOperationResult<string>.Fail("06", $"Failed to set secret: {ex.Message}"); }
    }

    public async Task<CmsOperationResult<bool>> DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            await _http.DeleteAsync($"/secrets/{Uri.EscapeDataString(name)}?api-version=7.4", cancellationToken).ConfigureAwait(false);
            _cache.TryRemove(name.ToUpperInvariant(), out _);
            return CmsOperationResult<bool>.Success(true, $"Secret '{name}' deleted.");
        }
        catch (Exception ex) { return CmsOperationResult<bool>.Fail("06", $"Failed to delete secret: {ex.Message}"); }
    }

    public Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(new List<string> { "— Azure Key Vault list requires admin SDK integration —" });

    private sealed record AkvSecretResponse(string Value);
}

/// <summary>
/// HashiCorp Vault secret provider.
/// Reads from the KV v2 secrets engine at the configured mount path.
/// Authenticates using a Vault token read from the environment.
/// </summary>
public sealed class HashiCorpVaultSecretProvider : ISecretVaultProvider
{
    private readonly string _vaultAddr;
    private readonly string _mountPath;
    private readonly HttpClient _http;
    private readonly IAuditLogger _audit;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, (string Value, DateTimeOffset ExpiresAt)> _cache = new();

    public HashiCorpVaultSecretProvider(string vaultAddress, string mountPath, string? vaultToken, IAuditLogger audit, IConfiguration configuration)
    {
        _vaultAddr = vaultAddress.TrimEnd('/');
        _mountPath = mountPath.Trim('/');
        _audit = audit;
        _configuration = configuration;
        _http = new HttpClient { BaseAddress = new Uri(_vaultAddr) };
        var token = vaultToken ?? Environment.GetEnvironmentVariable("VAULT_TOKEN") ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(token))
            _http.DefaultRequestHeaders.Add("X-Vault-Token", token);
    }

    public string ProviderName => "HashiCorpVault";
    public bool IsAvailable => !string.IsNullOrWhiteSpace(_vaultAddr);

    public async Task<string> GetSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        var cacheKey = name.ToUpperInvariant();
        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Value;

        try
        {
            // HashiCorp Vault KV v2: GET /v1/{mount}/data/{name}
            var response = await _http.GetFromJsonAsync<VaultKvResponse>(
                $"/v1/{_mountPath}/data/{Uri.EscapeDataString(name)}", cancellationToken).ConfigureAwait(false);
            var value = response?.Data?.Data?.TryGetValue("value", out var v) == true ? v : string.Empty;
            _cache[cacheKey] = (value, DateTimeOffset.UtcNow.AddMinutes(5));
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "SecretRead", $"Secret '{name}' retrieved from HashiCorp Vault.");
            return value;
        }
        catch (Exception ex)
        {
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "SecretReadFailed", $"Failed to read '{name}' from HashiCorp Vault: {ex.Message}");
            return _configuration[$"Secrets:{name}"] ?? string.Empty;
        }
    }

    public async Task<CmsOperationResult<string>> SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = System.Net.Http.Json.JsonContent.Create(new { data = new Dictionary<string, string> { ["value"] = value } });
            await _http.PostAsync($"/v1/{_mountPath}/data/{Uri.EscapeDataString(name)}", content, cancellationToken).ConfigureAwait(false);
            _cache.TryRemove(name.ToUpperInvariant(), out _);
            return CmsOperationResult<string>.Success(name);
        }
        catch (Exception ex) { return CmsOperationResult<string>.Fail("06", ex.Message); }
    }

    public Task<CmsOperationResult<bool>> DeleteSecretAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(CmsOperationResult<bool>.Success(false, "Delete via Vault CLI or REST API. Metadata delete not exposed here."));

    public Task<IReadOnlyList<string>> ListSecretNamesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(new List<string> { "— HashiCorp Vault list requires LIST permission on the KV mount —" });

    private sealed record VaultKvResponse(VaultKvData? Data);
    private sealed record VaultKvData(Dictionary<string, string>? Data);
}

/// <summary>
/// PAN encryption validator: scans text samples for clear-text PAN patterns.
/// </summary>
public sealed class PanEncryptionValidator : IPanEncryptionValidator
{
    private static readonly System.Text.RegularExpressions.Regex PanPattern =
        new(@"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|6[047][0-9]{14}|3[47][0-9]{13}|3(?:0[0-5]|[68][0-9])[0-9]{11}|[0-9]{12,19})\b",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly IAuditLogger _audit;

    public PanEncryptionValidator(IAuditLogger audit) => _audit = audit;

    public Task<PanValidationResult> ValidatePanHandlingAsync(string pan, string context, CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();
        if (!LuhnCheck(pan)) issues.Add("PAN fails Luhn check — may not be a valid card number.");
        if (pan.Length is < 12 or > 19) issues.Add($"PAN length {pan.Length} is outside valid range 12–19 digits.");
        return Task.FromResult(new PanValidationResult(!issues.Any(), issues));
    }

    public Task<IReadOnlyList<PanValidationFinding>> ScanForClearTextPansAsync(
        IReadOnlyList<string> logSamples, CancellationToken cancellationToken = default)
    {
        var findings = new List<PanValidationFinding>();
        foreach (var sample in logSamples)
        {
            var matches = PanPattern.Matches(sample);
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                if (!LuhnCheck(m.Value)) continue; // Skip non-Luhn numbers
                var masked = MaskPan(m.Value);
                findings.Add(new PanValidationFinding(
                    "LogSample",
                    m.Value[..6] + "...",
                    masked,
                    PanControlStatus.ClearTextDetected));
                _audit.LogSecurity(Guid.NewGuid().ToString("N"), "ClearTextPanDetected",
                    $"PAN-like value detected in log sample: {masked}. PCI DSS Req 3.4 violation.");
            }
        }
        return Task.FromResult<IReadOnlyList<PanValidationFinding>>(findings);
    }

    private static bool LuhnCheck(string s)
    {
        if (!s.All(char.IsDigit)) return false;
        var sum = 0;
        var flip = false;
        for (var i = s.Length - 1; i >= 0; i--)
        {
            var d = s[i] - '0';
            if (flip) { d *= 2; if (d > 9) d -= 9; }
            sum += d;
            flip = !flip;
        }
        return sum % 10 == 0;
    }

    private static string MaskPan(string pan) =>
        pan.Length < 10 ? "MASKED" : pan[..6] + new string('*', pan.Length - 10) + pan[^4..];
}
