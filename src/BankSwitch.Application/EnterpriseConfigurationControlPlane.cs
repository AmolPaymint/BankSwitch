using System.Text.Json;

namespace BankSwitch.Application;

public enum ConfigurationSensitivity { Informational, Operational, Sensitive, Critical }
public enum ConfigurationReloadPolicy { HotReload, ConnectionRestart, ServiceRestart, NodeRestart, ClusterRestart }
public enum ConfigurationChangeState { Draft, Submitted, Approved, Rejected, Scheduled, Applied, Failed, RolledBack, Cancelled }
public enum ConfigurationValueType { String, Integer, Decimal, Boolean, Json, Enum, Uri, Duration, SecretReference, CertificateReference }

public sealed record ConfigurationDomain(string Code, string Name, string Description, int DisplayOrder, bool Enabled);

public sealed record ConfigurationDefinition(
    Guid Id,
    string DomainCode,
    string Key,
    string DisplayName,
    string Description,
    ConfigurationValueType ValueType,
    string? DefaultValue,
    string? AllowedValuesJson,
    decimal? MinimumValue,
    decimal? MaximumValue,
    ConfigurationSensitivity Sensitivity,
    ConfigurationReloadPolicy ReloadPolicy,
    bool RequiresApproval,
    bool IsSecret,
    bool IsSensitive,
    bool ProductionLocked,
    string? ValidationPattern,
    string? ValidationExpression,
    bool Enabled);

public sealed record ConfigurationValue(
    Guid Id,
    Guid DefinitionId,
    string Environment,
    string InstitutionScope,
    string Value,
    long Version,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string UpdatedBy,
    DateTimeOffset UpdatedAt,
    byte[]? RowVersion = null);

public sealed record ConfigurationChangeItem(
    Guid Id,
    Guid ChangeRequestId,
    Guid DefinitionId,
    string DomainCode,
    string Key,
    string? OldValue,
    string NewValue,
    bool IsSecretReference,
    ConfigurationReloadPolicy ReloadPolicy);

public sealed record ConfigurationChangeRequest(
    Guid Id,
    string CorrelationId,
    string Environment,
    string InstitutionScope,
    string Maker,
    string? Checker,
    string Reason,
    string TicketReference,
    DateTimeOffset? EffectiveAt,
    ConfigurationChangeState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? AppliedAt,
    string? RejectionReason,
    IReadOnlyCollection<ConfigurationChangeItem> Items);

public sealed record ConfigurationHistoryEntry(
    long Version,
    Guid DefinitionId,
    string DomainCode,
    string Key,
    string Environment,
    string InstitutionScope,
    string? OldValue,
    string NewValue,
    string ChangedBy,
    Guid? ChangeRequestId,
    DateTimeOffset ChangedAt,
    string Reason,
    string Hash);

public sealed record ConfigurationSnapshot(
    Guid Id,
    string Name,
    string Environment,
    string InstitutionScope,
    long Version,
    string Checksum,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    string PayloadJson);

public sealed record ConfigurationValidationIssue(string Code, string Message, string Severity, string? Key = null);
public sealed record ConfigurationValidationResult(bool IsValid, IReadOnlyCollection<ConfigurationValidationIssue> Issues);

public sealed record ConfigurationDeploymentResult(
    Guid DeploymentId,
    Guid ChangeRequestId,
    bool Success,
    string Status,
    ConfigurationReloadPolicy HighestReloadPolicy,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string Message,
    bool RolledBack);

public sealed record ConfigurationDiagnostic(string Component, string Status, double? LatencyMs, DateTimeOffset CheckedAt, string? Message);

public sealed record FeatureFlagDefinition(
    Guid Id,
    string Key,
    string Description,
    bool Enabled,
    string Environment,
    string InstitutionScope,
    int RolloutPercentage,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string UpdatedBy,
    DateTimeOffset UpdatedAt);

public sealed record CertificateInventoryItem(
    Guid Id,
    string Name,
    string Purpose,
    string Environment,
    string Subject,
    string Issuer,
    string Thumbprint,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    string SecretReference,
    string Status);

public sealed record SecretReferenceInfo(
    Guid Id,
    string Name,
    string Provider,
    string Reference,
    string Environment,
    string Version,
    DateTimeOffset? LastRotatedAt,
    DateTimeOffset? ExpiresAt,
    string Status);

public sealed record CreateConfigurationChangeRequest(
    string Environment,
    string InstitutionScope,
    string Reason,
    string TicketReference,
    DateTimeOffset? EffectiveAt,
    IReadOnlyCollection<CreateConfigurationChangeItem> Items);

public sealed record CreateConfigurationChangeItem(string DomainCode, string Key, string NewValue);
public sealed record RejectConfigurationChangeRequest(string Reason);
public sealed record CreateConfigurationSnapshotRequest(string Name, string Environment, string InstitutionScope);
public sealed record RestoreConfigurationSnapshotRequest(string Reason, string TicketReference);

public interface IEnterpriseConfigurationRepository
{
    Task<IReadOnlyCollection<ConfigurationDomain>> GetDomainsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationDefinition>> GetDefinitionsAsync(string? domainCode = null, CancellationToken cancellationToken = default);
    Task<ConfigurationDefinition?> GetDefinitionAsync(string domainCode, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationValue>> GetValuesAsync(string environment, string institutionScope, string? domainCode = null, CancellationToken cancellationToken = default);
    Task<ConfigurationValue?> GetValueAsync(Guid definitionId, string environment, string institutionScope, CancellationToken cancellationToken = default);

    Task<ConfigurationChangeRequest> CreateChangeRequestAsync(ConfigurationChangeRequest request, CancellationToken cancellationToken = default);
    Task<ConfigurationChangeRequest?> GetChangeRequestAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationChangeRequest>> GetChangeRequestsAsync(ConfigurationChangeState? state = null, CancellationToken cancellationToken = default);
    Task UpdateChangeRequestStateAsync(Guid id, ConfigurationChangeState state, string? checker, string? rejectionReason, DateTimeOffset? submittedAt, DateTimeOffset? approvedAt, DateTimeOffset? appliedAt, CancellationToken cancellationToken = default);

    Task<long> ApplyValuesAsync(ConfigurationChangeRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationHistoryEntry>> GetHistoryAsync(string environment, string institutionScope, string? domainCode = null, int take = 250, CancellationToken cancellationToken = default);

    Task<ConfigurationSnapshot> CreateSnapshotAsync(ConfigurationSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<ConfigurationSnapshot?> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationSnapshot>> GetSnapshotsAsync(string environment, string institutionScope, CancellationToken cancellationToken = default);
    Task<long> RestoreSnapshotAsync(ConfigurationSnapshot snapshot, string actor, string reason, Guid changeRequestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<FeatureFlagDefinition>> GetFeatureFlagsAsync(string environment, string institutionScope, CancellationToken cancellationToken = default);
    Task UpsertFeatureFlagAsync(FeatureFlagDefinition featureFlag, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CertificateInventoryItem>> GetCertificatesAsync(string environment, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SecretReferenceInfo>> GetSecretReferencesAsync(string environment, CancellationToken cancellationToken = default);
    Task RecordDeploymentAsync(ConfigurationDeploymentResult result, CancellationToken cancellationToken = default);
}


public sealed record ConfigurationRuntimeApplicationItem(
    string DomainCode,
    string Key,
    ConfigurationReloadPolicy ReloadPolicy,
    bool AppliedImmediately,
    bool RestartRequired,
    string Status,
    string Message);

public sealed record ConfigurationRuntimeApplicationResult(
    bool Success,
    IReadOnlyCollection<ConfigurationRuntimeApplicationItem> Items,
    string Message);

public sealed record ConfigurationCompletenessItem(
    string DomainCode,
    string Key,
    ConfigurationValueType ValueType,
    ConfigurationSensitivity Sensitivity,
    ConfigurationReloadPolicy ReloadPolicy,
    bool FrontendControlSupported,
    bool ValidationSupported,
    bool MakerCheckerEnforced,
    bool SecretSafe,
    bool RuntimeHandlerAvailable,
    string RuntimeBehavior,
    string Status);

public sealed record ConfigurationCompletenessReport(
    int DomainCount,
    int DefinitionCount,
    int CompleteCount,
    int PartialCount,
    int GapCount,
    decimal CompletionPercent,
    IReadOnlyCollection<ConfigurationCompletenessItem> Items);

public interface IConfigurationRuntimeApplicator
{
    Task<ConfigurationRuntimeApplicationResult> ApplyAsync(ConfigurationChangeRequest request, CancellationToken cancellationToken = default);
    Task<ConfigurationRuntimeApplicationResult> ValidateRuntimeAsync(ConfigurationChangeRequest request, CancellationToken cancellationToken = default);
}

public interface IConfigurationCompletenessService
{
    Task<ConfigurationCompletenessReport> AssessAsync(CancellationToken cancellationToken = default);
}

public interface IConfigurationRuntimeProbe
{
    Task<IReadOnlyCollection<ConfigurationDiagnostic>> ProbeAsync(CancellationToken cancellationToken = default);
}

public interface IEnterpriseConfigurationControlPlane
{
    Task<IReadOnlyCollection<ConfigurationDomain>> GetDomainsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationDefinition>> GetDefinitionsAsync(string? domainCode = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationValue>> GetValuesAsync(string environment, string institutionScope, string? domainCode = null, CancellationToken cancellationToken = default);
    Task<ConfigurationValidationResult> ValidateAsync(CreateConfigurationChangeRequest request, CancellationToken cancellationToken = default);
    Task<ConfigurationChangeRequest> DraftAsync(CreateConfigurationChangeRequest request, string maker, CancellationToken cancellationToken = default);
    Task<ConfigurationChangeRequest> SubmitAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    Task<ConfigurationChangeRequest> ApproveAsync(Guid id, string checker, CancellationToken cancellationToken = default);
    Task<ConfigurationChangeRequest> RejectAsync(Guid id, string checker, string reason, CancellationToken cancellationToken = default);
    Task<ConfigurationDeploymentResult> ApplyAsync(Guid id, string actor, CancellationToken cancellationToken = default);
    Task<ConfigurationDeploymentResult> RollbackAsync(Guid id, string actor, string reason, CancellationToken cancellationToken = default);
    Task<ConfigurationChangeRequest?> GetChangeRequestAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationChangeRequest>> GetChangeRequestsAsync(ConfigurationChangeState? state = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationHistoryEntry>> GetHistoryAsync(string environment, string institutionScope, string? domainCode = null, int take = 250, CancellationToken cancellationToken = default);
    Task<ConfigurationSnapshot> CreateSnapshotAsync(CreateConfigurationSnapshotRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationSnapshot>> GetSnapshotsAsync(string environment, string institutionScope, CancellationToken cancellationToken = default);
    Task<ConfigurationDeploymentResult> RestoreSnapshotAsync(Guid snapshotId, RestoreConfigurationSnapshotRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ConfigurationDiagnostic>> GetDiagnosticsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<FeatureFlagDefinition>> GetFeatureFlagsAsync(string environment, string institutionScope, CancellationToken cancellationToken = default);
    Task UpsertFeatureFlagAsync(FeatureFlagDefinition featureFlag, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CertificateInventoryItem>> GetCertificatesAsync(string environment, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SecretReferenceInfo>> GetSecretReferencesAsync(string environment, CancellationToken cancellationToken = default);
}

public sealed class EnterpriseConfigurationControlPlane : IEnterpriseConfigurationControlPlane
{
    private readonly IEnterpriseConfigurationRepository _repository;
    private readonly IConfigurationRuntimeProbe _runtimeProbe;
    private readonly IAuditLogger _audit;
    private readonly IConfigurationRuntimeApplicator _runtimeApplicator;

    public EnterpriseConfigurationControlPlane(IEnterpriseConfigurationRepository repository, IConfigurationRuntimeProbe runtimeProbe, IConfigurationRuntimeApplicator runtimeApplicator, IAuditLogger audit)
    {
        _repository = repository;
        _runtimeProbe = runtimeProbe;
        _runtimeApplicator = runtimeApplicator;
        _audit = audit;
    }

    public Task<IReadOnlyCollection<ConfigurationDomain>> GetDomainsAsync(CancellationToken cancellationToken = default) => _repository.GetDomainsAsync(cancellationToken);
    public Task<IReadOnlyCollection<ConfigurationDefinition>> GetDefinitionsAsync(string? domainCode = null, CancellationToken cancellationToken = default) => _repository.GetDefinitionsAsync(domainCode, cancellationToken);
    public Task<IReadOnlyCollection<ConfigurationValue>> GetValuesAsync(string environment, string institutionScope, string? domainCode = null, CancellationToken cancellationToken = default) => _repository.GetValuesAsync(environment, institutionScope, domainCode, cancellationToken);
    public Task<ConfigurationChangeRequest?> GetChangeRequestAsync(Guid id, CancellationToken cancellationToken = default) => _repository.GetChangeRequestAsync(id, cancellationToken);
    public Task<IReadOnlyCollection<ConfigurationChangeRequest>> GetChangeRequestsAsync(ConfigurationChangeState? state = null, CancellationToken cancellationToken = default) => _repository.GetChangeRequestsAsync(state, cancellationToken);
    public Task<IReadOnlyCollection<ConfigurationHistoryEntry>> GetHistoryAsync(string environment, string institutionScope, string? domainCode = null, int take = 250, CancellationToken cancellationToken = default) => _repository.GetHistoryAsync(environment, institutionScope, domainCode, take, cancellationToken);
    public Task<IReadOnlyCollection<ConfigurationDiagnostic>> GetDiagnosticsAsync(CancellationToken cancellationToken = default) => _runtimeProbe.ProbeAsync(cancellationToken);
    public Task<IReadOnlyCollection<FeatureFlagDefinition>> GetFeatureFlagsAsync(string environment, string institutionScope, CancellationToken cancellationToken = default) => _repository.GetFeatureFlagsAsync(environment, institutionScope, cancellationToken);
    public Task<IReadOnlyCollection<CertificateInventoryItem>> GetCertificatesAsync(string environment, CancellationToken cancellationToken = default) => _repository.GetCertificatesAsync(environment, cancellationToken);
    public Task<IReadOnlyCollection<SecretReferenceInfo>> GetSecretReferencesAsync(string environment, CancellationToken cancellationToken = default) => _repository.GetSecretReferencesAsync(environment, cancellationToken);

    public async Task<ConfigurationValidationResult> ValidateAsync(CreateConfigurationChangeRequest request, CancellationToken cancellationToken = default)
    {
        var issues = new List<ConfigurationValidationIssue>();
        if (string.IsNullOrWhiteSpace(request.Environment)) issues.Add(new("ENV_REQUIRED", "Environment is required.", "Error"));
        if (string.IsNullOrWhiteSpace(request.InstitutionScope)) issues.Add(new("SCOPE_REQUIRED", "Institution scope is required.", "Error"));
        if (request.Items.Count == 0) issues.Add(new("ITEM_REQUIRED", "At least one setting must be changed.", "Error"));

        foreach (var item in request.Items)
        {
            var def = await _repository.GetDefinitionAsync(item.DomainCode, item.Key, cancellationToken).ConfigureAwait(false);
            if (def is null) { issues.Add(new("UNKNOWN_SETTING", $"Unknown setting {item.DomainCode}:{item.Key}.", "Error", item.Key)); continue; }
            if (!def.Enabled) issues.Add(new("SETTING_DISABLED", $"Setting {item.Key} is disabled.", "Error", item.Key));
            if (def.IsSecret && def.ValueType != ConfigurationValueType.SecretReference)
                issues.Add(new("SECRET_VALUE_PROHIBITED", $"Secret setting {item.Key} must use a secret reference and can never contain a secret value.", "Error", item.Key));
            ValidateValue(def, item.NewValue, issues);
            ValidateProductionGuard(request.Environment, item, issues);
        }
        return new(issues.All(x => !string.Equals(x.Severity, "Error", StringComparison.OrdinalIgnoreCase)), issues);
    }

    public async Task<ConfigurationChangeRequest> DraftAsync(CreateConfigurationChangeRequest request, string maker, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(request, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid) throw new InvalidOperationException(string.Join(" | ", validation.Issues.Select(x => x.Message)));

        var items = new List<ConfigurationChangeItem>();
        foreach (var item in request.Items)
        {
            var def = await _repository.GetDefinitionAsync(item.DomainCode, item.Key, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Unknown setting {item.DomainCode}:{item.Key}");
            var current = await _repository.GetValueAsync(def.Id, request.Environment, request.InstitutionScope, cancellationToken).ConfigureAwait(false);
            items.Add(new(Guid.NewGuid(), Guid.Empty, def.Id, item.DomainCode, item.Key, current?.Value, item.NewValue, def.IsSecret, def.ReloadPolicy));
        }
        var id = Guid.NewGuid();
        items = items.Select(x => x with { ChangeRequestId = id }).ToList();
        var now = DateTimeOffset.UtcNow;
        var change = new ConfigurationChangeRequest(id, Guid.NewGuid().ToString("N"), request.Environment, request.InstitutionScope, maker, null, request.Reason, request.TicketReference, request.EffectiveAt, ConfigurationChangeState.Draft, now, now, null, null, null, null, items);
        var saved = await _repository.CreateChangeRequestAsync(change, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(saved.CorrelationId, maker, "ConfigControlPlaneDraft", "", JsonSerializer.Serialize(request.Items), request.Reason, request.TicketReference);
        return saved;
    }

    public async Task<ConfigurationChangeRequest> SubmitAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var change = await RequireAsync(id, cancellationToken).ConfigureAwait(false);
        if (change.State != ConfigurationChangeState.Draft) throw new InvalidOperationException("Only draft changes can be submitted.");
        if (!string.Equals(change.Maker, actor, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Only the maker can submit this change.");
        await _repository.UpdateChangeRequestStateAsync(id, ConfigurationChangeState.Submitted, null, null, DateTimeOffset.UtcNow, null, null, cancellationToken).ConfigureAwait(false);
        return (await RequireAsync(id, cancellationToken).ConfigureAwait(false));
    }

    public async Task<ConfigurationChangeRequest> ApproveAsync(Guid id, string checker, CancellationToken cancellationToken = default)
    {
        var change = await RequireAsync(id, cancellationToken).ConfigureAwait(false);
        if (change.State != ConfigurationChangeState.Submitted) throw new InvalidOperationException("Only submitted changes can be approved.");
        if (string.Equals(change.Maker, checker, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Maker cannot approve their own configuration change.");
        var next = change.EffectiveAt.HasValue && change.EffectiveAt > DateTimeOffset.UtcNow ? ConfigurationChangeState.Scheduled : ConfigurationChangeState.Approved;
        await _repository.UpdateChangeRequestStateAsync(id, next, checker, null, change.SubmittedAt, DateTimeOffset.UtcNow, null, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(change.CorrelationId, checker, "ConfigControlPlaneApprove", "", id.ToString(), change.Reason, change.TicketReference);
        return await RequireAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConfigurationChangeRequest> RejectAsync(Guid id, string checker, string reason, CancellationToken cancellationToken = default)
    {
        var change = await RequireAsync(id, cancellationToken).ConfigureAwait(false);
        if (change.State != ConfigurationChangeState.Submitted) throw new InvalidOperationException("Only submitted changes can be rejected.");
        if (string.Equals(change.Maker, checker, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Maker cannot reject their own configuration change as checker.");
        await _repository.UpdateChangeRequestStateAsync(id, ConfigurationChangeState.Rejected, checker, reason, change.SubmittedAt, null, null, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(change.CorrelationId, checker, "ConfigControlPlaneReject", "", id.ToString(), reason, change.TicketReference);
        return await RequireAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ConfigurationDeploymentResult> ApplyAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var change = await RequireAsync(id, cancellationToken).ConfigureAwait(false);
        if (change.State is not (ConfigurationChangeState.Approved or ConfigurationChangeState.Scheduled)) throw new InvalidOperationException("Change must be approved before it can be applied.");
        if (change.State == ConfigurationChangeState.Scheduled && change.EffectiveAt > DateTimeOffset.UtcNow) throw new InvalidOperationException("Scheduled change has not reached its effective time.");
        var started = DateTimeOffset.UtcNow;
        var highest = change.Items.Count == 0 ? ConfigurationReloadPolicy.HotReload : (ConfigurationReloadPolicy)change.Items.Max(x => (int)x.ReloadPolicy);
        try
        {
            var runtimeValidation = await _runtimeApplicator.ValidateRuntimeAsync(change, cancellationToken).ConfigureAwait(false);
            if (!runtimeValidation.Success) throw new InvalidOperationException(runtimeValidation.Message);
            await _repository.ApplyValuesAsync(change, actor, cancellationToken).ConfigureAwait(false);
            var runtimeApply = await _runtimeApplicator.ApplyAsync(change, cancellationToken).ConfigureAwait(false);
            if (!runtimeApply.Success) throw new InvalidOperationException(runtimeApply.Message);
            await _repository.UpdateChangeRequestStateAsync(id, ConfigurationChangeState.Applied, change.Checker, null, change.SubmittedAt, change.ApprovedAt, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            var runtimeSummary = string.Join("; ", runtimeApply.Items.Select(x => $"{x.DomainCode}:{x.Key}={x.Status}"));
            var result = new ConfigurationDeploymentResult(Guid.NewGuid(), id, true, "Applied", highest, started, DateTimeOffset.UtcNow, $"Configuration persisted and runtime application validated. {runtimeSummary}", false);
            await _repository.RecordDeploymentAsync(result, cancellationToken).ConfigureAwait(false);
            _audit.LogAdminAudit(change.CorrelationId, actor, "ConfigControlPlaneApply", "", id.ToString(), change.Reason, change.TicketReference);
            return result;
        }
        catch (Exception ex)
        {
            await _repository.UpdateChangeRequestStateAsync(id, ConfigurationChangeState.Failed, change.Checker, ex.Message, change.SubmittedAt, change.ApprovedAt, null, cancellationToken).ConfigureAwait(false);
            var result = new ConfigurationDeploymentResult(Guid.NewGuid(), id, false, "Failed", highest, started, DateTimeOffset.UtcNow, ex.Message, false);
            await _repository.RecordDeploymentAsync(result, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<ConfigurationDeploymentResult> RollbackAsync(Guid id, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var change = await RequireAsync(id, cancellationToken).ConfigureAwait(false);
        if (change.State != ConfigurationChangeState.Applied) throw new InvalidOperationException("Only applied changes can be rolled back.");

        var rollbackId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var reverseItems = change.Items.Select(x => new ConfigurationChangeItem(
            Guid.NewGuid(), rollbackId, x.DefinitionId, x.DomainCode, x.Key, x.NewValue, x.OldValue ?? string.Empty, x.IsSecretReference, x.ReloadPolicy)).ToArray();
        var rollback = new ConfigurationChangeRequest(
            rollbackId, Guid.NewGuid().ToString("N"), change.Environment, change.InstitutionScope, "SYSTEM_ROLLBACK", actor,
            $"Rollback of {id}: {reason}", change.TicketReference, null, ConfigurationChangeState.Approved, now, now, now, now, null, null, reverseItems);
        await _repository.CreateChangeRequestAsync(rollback, cancellationToken).ConfigureAwait(false);

        var started = DateTimeOffset.UtcNow;
        var highest = reverseItems.Length == 0 ? ConfigurationReloadPolicy.HotReload : (ConfigurationReloadPolicy)reverseItems.Max(x => (int)x.ReloadPolicy);
        var runtimeValidation = await _runtimeApplicator.ValidateRuntimeAsync(rollback, cancellationToken).ConfigureAwait(false);
        if (!runtimeValidation.Success) throw new InvalidOperationException(runtimeValidation.Message);
        await _repository.ApplyValuesAsync(rollback, actor, cancellationToken).ConfigureAwait(false);
        var runtimeApply = await _runtimeApplicator.ApplyAsync(rollback, cancellationToken).ConfigureAwait(false);
        if (!runtimeApply.Success) throw new InvalidOperationException(runtimeApply.Message);
        await _repository.UpdateChangeRequestStateAsync(rollbackId, ConfigurationChangeState.Applied, actor, null, now, now, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        await _repository.UpdateChangeRequestStateAsync(id, ConfigurationChangeState.RolledBack, change.Checker, reason, change.SubmittedAt, change.ApprovedAt, change.AppliedAt, cancellationToken).ConfigureAwait(false);

        var result = new ConfigurationDeploymentResult(Guid.NewGuid(), rollbackId, true, "RollbackApplied", highest, started, DateTimeOffset.UtcNow, $"Rollback applied through controlled request {rollbackId}.", true);
        await _repository.RecordDeploymentAsync(result, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(change.CorrelationId, actor, "ConfigControlPlaneRollback", id.ToString(), rollbackId.ToString(), reason, change.TicketReference);
        return result;
    }

    public async Task<ConfigurationSnapshot> CreateSnapshotAsync(CreateConfigurationSnapshotRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var values = await _repository.GetValuesAsync(request.Environment, request.InstitutionScope, null, cancellationToken).ConfigureAwait(false);
        var payload = JsonSerializer.Serialize(values.OrderBy(x => x.DefinitionId));
        var checksum = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var version = values.Count == 0 ? 0 : values.Max(x => x.Version);
        return await _repository.CreateSnapshotAsync(new(Guid.NewGuid(), request.Name, request.Environment, request.InstitutionScope, version, checksum, actor, DateTimeOffset.UtcNow, payload), cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyCollection<ConfigurationSnapshot>> GetSnapshotsAsync(string environment, string institutionScope, CancellationToken cancellationToken = default) => _repository.GetSnapshotsAsync(environment, institutionScope, cancellationToken);

    public async Task<ConfigurationDeploymentResult> RestoreSnapshotAsync(Guid snapshotId, RestoreConfigurationSnapshotRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var snapshot = await _repository.GetSnapshotAsync(snapshotId, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Configuration snapshot not found.");
        var id = Guid.NewGuid();
        var started = DateTimeOffset.UtcNow;
        await _repository.RestoreSnapshotAsync(snapshot, actor, request.Reason, id, cancellationToken).ConfigureAwait(false);
        var result = new ConfigurationDeploymentResult(Guid.NewGuid(), id, true, "SnapshotRestored", ConfigurationReloadPolicy.ClusterRestart, started, DateTimeOffset.UtcNow, $"Snapshot {snapshot.Name} restored.", false);
        await _repository.RecordDeploymentAsync(result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public async Task UpsertFeatureFlagAsync(FeatureFlagDefinition featureFlag, string actor, CancellationToken cancellationToken = default)
    {
        if (featureFlag.RolloutPercentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(featureFlag.RolloutPercentage));
        await _repository.UpsertFeatureFlagAsync(featureFlag with { UpdatedBy = actor, UpdatedAt = DateTimeOffset.UtcNow }, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "FeatureFlagUpsert", featureFlag.Key, featureFlag.Enabled.ToString(), "Feature flag update", "CONFIG-CONTROL-PLANE");
    }

    private async Task<ConfigurationChangeRequest> RequireAsync(Guid id, CancellationToken cancellationToken)
        => await _repository.GetChangeRequestAsync(id, cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException("Configuration change request not found.");

    private static void ValidateValue(ConfigurationDefinition def, string value, List<ConfigurationValidationIssue> issues)
    {
        bool parsed;
        decimal numeric;
        switch (def.ValueType)
        {
            case ConfigurationValueType.Integer:
                parsed = long.TryParse(value, out var n); numeric = n;
                if (!parsed) issues.Add(new("TYPE_INTEGER", $"{def.Key} must be an integer.", "Error", def.Key));
                else ValidateRange(def, numeric, issues); break;
            case ConfigurationValueType.Decimal:
                parsed = decimal.TryParse(value, out numeric);
                if (!parsed) issues.Add(new("TYPE_DECIMAL", $"{def.Key} must be numeric.", "Error", def.Key));
                else ValidateRange(def, numeric, issues); break;
            case ConfigurationValueType.Boolean:
                if (!bool.TryParse(value, out _)) issues.Add(new("TYPE_BOOLEAN", $"{def.Key} must be true or false.", "Error", def.Key)); break;
            case ConfigurationValueType.Uri:
                if (!Uri.TryCreate(value, UriKind.Absolute, out _)) issues.Add(new("TYPE_URI", $"{def.Key} must be an absolute URI.", "Error", def.Key)); break;
            case ConfigurationValueType.Duration:
                if (!TimeSpan.TryParse(value, out _)) issues.Add(new("TYPE_DURATION", $"{def.Key} must be a valid duration (hh:mm:ss).", "Error", def.Key)); break;
            case ConfigurationValueType.SecretReference:
                if (!(value.StartsWith("vault://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("hsm://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("kv://", StringComparison.OrdinalIgnoreCase)))
                    issues.Add(new("SECRET_REFERENCE_FORMAT", $"{def.Key} must be a vault://, kv:// or hsm:// reference.", "Error", def.Key));
                break;
            case ConfigurationValueType.CertificateReference:
                if (string.IsNullOrWhiteSpace(value) || value.Length < 3) issues.Add(new("CERT_REFERENCE_FORMAT", $"{def.Key} must contain a certificate inventory reference.", "Error", def.Key)); break;
            case ConfigurationValueType.Json:
                try { JsonDocument.Parse(value).Dispose(); } catch { issues.Add(new("TYPE_JSON", $"{def.Key} must contain valid JSON.", "Error", def.Key)); } break;
        }
        if (!string.IsNullOrWhiteSpace(def.ValidationPattern))
        {
            try
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(value, def.ValidationPattern))
                    issues.Add(new("VALUE_PATTERN", $"{def.Key} does not match the required format.", "Error", def.Key));
            }
            catch { issues.Add(new("DEFINITION_PATTERN_INVALID", $"Validation pattern for {def.Key} is invalid.", "Error", def.Key)); }
        }
        if (!string.IsNullOrWhiteSpace(def.AllowedValuesJson))
        {
            try
            {
                var allowed = JsonSerializer.Deserialize<string[]>(def.AllowedValuesJson) ?? Array.Empty<string>();
                if (allowed.Length > 0 && !allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
                    issues.Add(new("VALUE_NOT_ALLOWED", $"{def.Key} must be one of: {string.Join(", ", allowed)}.", "Error", def.Key));
            }
            catch { issues.Add(new("DEFINITION_INVALID", $"Allowed-values metadata for {def.Key} is invalid.", "Error", def.Key)); }
        }
    }

    private static void ValidateRange(ConfigurationDefinition def, decimal value, List<ConfigurationValidationIssue> issues)
    {
        if (def.MinimumValue.HasValue && value < def.MinimumValue.Value) issues.Add(new("VALUE_MIN", $"{def.Key} must be >= {def.MinimumValue}.", "Error", def.Key));
        if (def.MaximumValue.HasValue && value > def.MaximumValue.Value) issues.Add(new("VALUE_MAX", $"{def.Key} must be <= {def.MaximumValue}.", "Error", def.Key));
    }

    private static void ValidateProductionGuard(string environment, CreateConfigurationChangeItem item, List<ConfigurationValidationIssue> issues)
    {
        if (!string.Equals(environment, "PROD", StringComparison.OrdinalIgnoreCase) && !string.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase)) return;
        var key = item.Key.ToLowerInvariant();
        var value = item.NewValue.Trim();
        if (key.Contains("repository") && string.Equals(value, "InMemory", StringComparison.OrdinalIgnoreCase)) issues.Add(new("PROD_INMEMORY_BLOCKED", "Production cannot use an InMemory repository.", "Error", item.Key));
        if (key.Contains("hsm") && (value.Contains("mock", StringComparison.OrdinalIgnoreCase) || value.Contains("bypass", StringComparison.OrdinalIgnoreCase))) issues.Add(new("PROD_HSM_BYPASS_BLOCKED", "Production cannot use mock/bypass HSM configuration.", "Error", item.Key));
        if (key.Contains("tls") && string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) issues.Add(new("PROD_TLS_REQUIRED", "TLS cannot be disabled in production.", "Error", item.Key));
        if (key.Contains("authentication") && string.Equals(value, "disabled", StringComparison.OrdinalIgnoreCase)) issues.Add(new("PROD_AUTH_REQUIRED", "Authentication cannot be disabled in production.", "Error", item.Key));
    }
}
