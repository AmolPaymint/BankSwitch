using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace BankSwitch.Application;

public enum OpsComponentType { Switch, Atm, Pos, Cbs, Network, Hsm, Database, Queue, ApiGateway, DrReplication }
public enum OpsHealthStatus { Healthy, Degraded, Down, Maintenance, Unknown }
public enum IncidentSeverity { Low, Medium, High, Critical, Regulatory }
public enum IncidentStatus { Open, Acknowledged, Assigned, InProgress, Resolved, Closed, Escalated }
public enum TicketLevel { L1, L2, L3, Vendor, Oem, Bank }
public enum SlaTargetType { Availability, ResponseTime, ResolutionTime, TechnicalDeclineRate, ReversalCompletion, DrRpo, DrRto, Cutover }
public enum SlaBreachStatus { WithinSla, Warning, Breached }
public enum DeclineCategory { Technical, Business, Network, Cbs, Hsm, Timeout, Format, Fraud, Limit, Unknown }
public enum DrDrillStatus { Planned, Running, Completed, Failed, EvidenceApproved }
public enum CapacityMetricType { Cpu, Memory, Disk, QueueDepth, Tps, LatencyP95, LatencyP99, DbConnections, HsmUtilization }

public sealed record OperationsHealthSnapshot(
    Guid SnapshotId,
    OpsComponentType ComponentType,
    string ComponentCode,
    OpsHealthStatus Status,
    string StatusMessage,
    decimal AvailabilityPercent,
    int CurrentTps,
    int TechnicalDeclines,
    DateTimeOffset CapturedAt,
    string AuditHash);

public sealed record SubmitHealthSnapshotRequest(
    OpsComponentType ComponentType,
    string ComponentCode,
    OpsHealthStatus Status,
    string StatusMessage,
    decimal AvailabilityPercent,
    int CurrentTps,
    int TechnicalDeclines);

public sealed record IncidentTicket(
    Guid IncidentId,
    string IncidentNumber,
    IncidentSeverity Severity,
    IncidentStatus Status,
    OpsComponentType ComponentType,
    string ComponentCode,
    string Title,
    string Description,
    TicketLevel CurrentLevel,
    string AssignedTo,
    DateTimeOffset OpenedAt,
    DateTimeOffset? AcknowledgedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt,
    string RootCause,
    string CorrectiveAction,
    string AuditHash);

public sealed record CreateIncidentRequest(
    IncidentSeverity Severity,
    OpsComponentType ComponentType,
    string ComponentCode,
    string Title,
    string Description,
    string AssignedTo);

public sealed record UpdateIncidentStatusRequest(
    Guid IncidentId,
    IncidentStatus Status,
    TicketLevel? CurrentLevel,
    string? AssignedTo,
    string? RootCause,
    string? CorrectiveAction);

public sealed record SlaPolicy(
    Guid PolicyId,
    string PolicyCode,
    SlaTargetType TargetType,
    OpsComponentType? ComponentType,
    decimal ThresholdValue,
    string Unit,
    int WarningMinutes,
    int BreachMinutes,
    TicketLevel EscalateTo,
    bool Enabled,
    DateTimeOffset CreatedAt);

public sealed record CreateSlaPolicyRequest(
    string PolicyCode,
    SlaTargetType TargetType,
    OpsComponentType? ComponentType,
    decimal ThresholdValue,
    string Unit,
    int WarningMinutes,
    int BreachMinutes,
    TicketLevel EscalateTo,
    bool Enabled);

public sealed record SlaEvaluationResult(
    Guid EvaluationId,
    Guid PolicyId,
    string PolicyCode,
    SlaBreachStatus Status,
    decimal ObservedValue,
    decimal ThresholdValue,
    string Unit,
    string Message,
    Guid? IncidentId,
    DateTimeOffset EvaluatedAt,
    string AuditHash);

public sealed record EscalationRule(
    Guid RuleId,
    IncidentSeverity Severity,
    TicketLevel FromLevel,
    TicketLevel ToLevel,
    int EscalateAfterMinutes,
    string NotifyGroup,
    bool Enabled);

public sealed record RegisterEscalationRuleRequest(
    IncidentSeverity Severity,
    TicketLevel FromLevel,
    TicketLevel ToLevel,
    int EscalateAfterMinutes,
    string NotifyGroup,
    bool Enabled);

public sealed record TechnicalDeclineEvent(
    Guid DeclineId,
    string TransactionReference,
    DeclineCategory Category,
    string ComponentCode,
    string ResponseCode,
    string Reason,
    string Channel,
    decimal Amount,
    string CurrencyCode,
    DateTimeOffset OccurredAt,
    string AuditHash);

public sealed record RecordTechnicalDeclineRequest(
    string TransactionReference,
    DeclineCategory Category,
    string ComponentCode,
    string ResponseCode,
    string Reason,
    string Channel,
    decimal Amount,
    string CurrencyCode);

public sealed record RootCauseAnalysisCase(
    Guid RcaId,
    Guid IncidentId,
    string InterimReport,
    string FinalReport,
    string RootCause,
    string CorrectiveAction,
    string PreventiveAction,
    string PreparedBy,
    DateTimeOffset DueAt,
    DateTimeOffset? SubmittedAt,
    string Status,
    string AuditHash);

public sealed record SubmitRcaRequest(
    Guid IncidentId,
    string InterimReport,
    string FinalReport,
    string RootCause,
    string CorrectiveAction,
    string PreventiveAction);

public sealed record DrDrillRun(
    Guid DrillId,
    string DrillCode,
    DrDrillStatus Status,
    DateTimeOffset PlannedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int ObservedRpoMinutes,
    int ObservedRtoMinutes,
    string EvidenceFile,
    string Report,
    string AuditHash);

public sealed record ScheduleDrDrillRequest(string DrillCode, DateTimeOffset PlannedAt);
public sealed record CompleteDrDrillRequest(Guid DrillId, int ObservedRpoMinutes, int ObservedRtoMinutes, string EvidenceFile, string Report, bool Passed);

public sealed record CapacityMetric(
    Guid MetricId,
    CapacityMetricType MetricType,
    string ComponentCode,
    decimal Value,
    string Unit,
    DateTimeOffset CapturedAt,
    string AuditHash);

public sealed record RecordCapacityMetricRequest(CapacityMetricType MetricType, string ComponentCode, decimal Value, string Unit);

public sealed record RegulatoryUptimeReport(
    Guid ReportId,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal SwitchAvailability,
    decimal AtmAvailability,
    decimal PosAvailability,
    int TechnicalDeclines,
    int Incidents,
    int SlaBreaches,
    string FileName,
    string Sha256Hash,
    DateTimeOffset GeneratedAt);

public sealed record GenerateRegulatoryUptimeReportRequest(DateOnly FromDate, DateOnly ToDate, string OutputFormat);

public sealed record OperationsDashboard(
    int Components,
    int HealthyComponents,
    int OpenIncidents,
    int CriticalIncidents,
    int SlaBreaches,
    int TechnicalDeclinesToday,
    int PendingRca,
    int PlannedDrDrills,
    decimal AverageAvailability,
    DateTimeOffset GeneratedAt);

public interface IOperationsCommandCenterRepository
{
    Task SaveHealthSnapshotAsync(OperationsHealthSnapshot snapshot, CancellationToken ct);
    Task<IReadOnlyList<OperationsHealthSnapshot>> GetLatestHealthAsync(CancellationToken ct);
    Task SaveIncidentAsync(IncidentTicket incident, CancellationToken ct);
    Task<IncidentTicket?> GetIncidentAsync(Guid incidentId, CancellationToken ct);
    Task<IReadOnlyList<IncidentTicket>> GetIncidentsAsync(IncidentStatus? status, CancellationToken ct);
    Task SaveSlaPolicyAsync(SlaPolicy policy, CancellationToken ct);
    Task<IReadOnlyList<SlaPolicy>> GetSlaPoliciesAsync(CancellationToken ct);
    Task SaveSlaEvaluationAsync(SlaEvaluationResult result, CancellationToken ct);
    Task<IReadOnlyList<SlaEvaluationResult>> GetRecentSlaEvaluationsAsync(CancellationToken ct);
    Task SaveEscalationRuleAsync(EscalationRule rule, CancellationToken ct);
    Task<IReadOnlyList<EscalationRule>> GetEscalationRulesAsync(CancellationToken ct);
    Task SaveTechnicalDeclineAsync(TechnicalDeclineEvent decline, CancellationToken ct);
    Task<IReadOnlyList<TechnicalDeclineEvent>> GetTechnicalDeclinesAsync(DateOnly? businessDate, CancellationToken ct);
    Task SaveRcaAsync(RootCauseAnalysisCase rca, CancellationToken ct);
    Task<IReadOnlyList<RootCauseAnalysisCase>> GetRcaCasesAsync(CancellationToken ct);
    Task SaveDrDrillAsync(DrDrillRun run, CancellationToken ct);
    Task<DrDrillRun?> GetDrDrillAsync(Guid drillId, CancellationToken ct);
    Task<IReadOnlyList<DrDrillRun>> GetDrDrillsAsync(CancellationToken ct);
    Task SaveCapacityMetricAsync(CapacityMetric metric, CancellationToken ct);
    Task<IReadOnlyList<CapacityMetric>> GetCapacityMetricsAsync(CancellationToken ct);
    Task SaveUptimeReportAsync(RegulatoryUptimeReport report, CancellationToken ct);
    Task<IReadOnlyList<RegulatoryUptimeReport>> GetUptimeReportsAsync(CancellationToken ct);
    Task<OperationsDashboard> GetDashboardAsync(CancellationToken ct);
}

public interface IOperationsCommandCenterService
{
    Task<OperationsDashboard> GetDashboardAsync(CancellationToken ct);
    Task<IReadOnlyList<OperationsHealthSnapshot>> GetHealthAsync(CancellationToken ct);
    Task<CmsOperationResult<OperationsHealthSnapshot>> SubmitHealthSnapshotAsync(SubmitHealthSnapshotRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<IncidentTicket>> GetIncidentsAsync(IncidentStatus? status, CancellationToken ct);
    Task<CmsOperationResult<IncidentTicket>> CreateIncidentAsync(CreateIncidentRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<IncidentTicket>> UpdateIncidentStatusAsync(UpdateIncidentStatusRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<SlaPolicy>> CreateSlaPolicyAsync(CreateSlaPolicyRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<SlaPolicy>> GetSlaPoliciesAsync(CancellationToken ct);
    Task<IReadOnlyList<SlaEvaluationResult>> EvaluateSlaAsync(string actor, CancellationToken ct);
    Task<CmsOperationResult<EscalationRule>> RegisterEscalationRuleAsync(RegisterEscalationRuleRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<TechnicalDeclineEvent>> RecordTechnicalDeclineAsync(RecordTechnicalDeclineRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<TechnicalDeclineEvent>> GetTechnicalDeclinesAsync(DateOnly? businessDate, CancellationToken ct);
    Task<CmsOperationResult<RootCauseAnalysisCase>> SubmitRcaAsync(SubmitRcaRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<RootCauseAnalysisCase>> GetRcaCasesAsync(CancellationToken ct);
    Task<CmsOperationResult<DrDrillRun>> ScheduleDrDrillAsync(ScheduleDrDrillRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<DrDrillRun>> CompleteDrDrillAsync(CompleteDrDrillRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<DrDrillRun>> GetDrDrillsAsync(CancellationToken ct);
    Task<CmsOperationResult<CapacityMetric>> RecordCapacityMetricAsync(RecordCapacityMetricRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<CapacityMetric>> GetCapacityMetricsAsync(CancellationToken ct);
    Task<CmsOperationResult<RegulatoryUptimeReport>> GenerateRegulatoryUptimeReportAsync(GenerateRegulatoryUptimeReportRequest request, string actor, CancellationToken ct);
    Task<IReadOnlyList<RegulatoryUptimeReport>> GetRegulatoryUptimeReportsAsync(CancellationToken ct);
}

public sealed class OperationsCommandCenterService : IOperationsCommandCenterService
{
    private readonly IOperationsCommandCenterRepository _repository;

    public OperationsCommandCenterService(IOperationsCommandCenterRepository repository) => _repository = repository;

    public Task<OperationsDashboard> GetDashboardAsync(CancellationToken ct) => _repository.GetDashboardAsync(ct);
    public Task<IReadOnlyList<OperationsHealthSnapshot>> GetHealthAsync(CancellationToken ct) => _repository.GetLatestHealthAsync(ct);
    public Task<IReadOnlyList<IncidentTicket>> GetIncidentsAsync(IncidentStatus? status, CancellationToken ct) => _repository.GetIncidentsAsync(status, ct);
    public Task<IReadOnlyList<SlaPolicy>> GetSlaPoliciesAsync(CancellationToken ct) => _repository.GetSlaPoliciesAsync(ct);
    public Task<IReadOnlyList<TechnicalDeclineEvent>> GetTechnicalDeclinesAsync(DateOnly? businessDate, CancellationToken ct) => _repository.GetTechnicalDeclinesAsync(businessDate, ct);
    public Task<IReadOnlyList<RootCauseAnalysisCase>> GetRcaCasesAsync(CancellationToken ct) => _repository.GetRcaCasesAsync(ct);
    public Task<IReadOnlyList<DrDrillRun>> GetDrDrillsAsync(CancellationToken ct) => _repository.GetDrDrillsAsync(ct);
    public Task<IReadOnlyList<CapacityMetric>> GetCapacityMetricsAsync(CancellationToken ct) => _repository.GetCapacityMetricsAsync(ct);
    public Task<IReadOnlyList<RegulatoryUptimeReport>> GetRegulatoryUptimeReportsAsync(CancellationToken ct) => _repository.GetUptimeReportsAsync(ct);

    public async Task<CmsOperationResult<OperationsHealthSnapshot>> SubmitHealthSnapshotAsync(SubmitHealthSnapshotRequest request, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ComponentCode)) return CmsOperationResult<OperationsHealthSnapshot>.Fail("12", "Component code is required");
        var snapshot = new OperationsHealthSnapshot(Guid.NewGuid(), request.ComponentType, request.ComponentCode.Trim(), request.Status, request.StatusMessage ?? string.Empty, request.AvailabilityPercent, request.CurrentTps, request.TechnicalDeclines, DateTimeOffset.UtcNow, Hash($"HEALTH|{request.ComponentCode}|{request.Status}|{actor}"));
        await _repository.SaveHealthSnapshotAsync(snapshot, ct).ConfigureAwait(false);
        return CmsOperationResult<OperationsHealthSnapshot>.Success(snapshot, "Health snapshot captured");
    }

    public async Task<CmsOperationResult<IncidentTicket>> CreateIncidentAsync(CreateIncidentRequest request, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return CmsOperationResult<IncidentTicket>.Fail("12", "Incident title is required");
        var now = DateTimeOffset.UtcNow;
        var incident = new IncidentTicket(Guid.NewGuid(), $"INC-{now:yyyyMMddHHmmss}-{RandomNumberGenerator.GetInt32(1000, 9999)}", request.Severity, IncidentStatus.Open, request.ComponentType, request.ComponentCode, request.Title, request.Description ?? string.Empty, TicketLevel.L1, request.AssignedTo ?? string.Empty, now, null, null, null, string.Empty, string.Empty, Hash($"INC|{request.Title}|{request.Severity}|{actor}|{now:O}"));
        await _repository.SaveIncidentAsync(incident, ct).ConfigureAwait(false);
        return CmsOperationResult<IncidentTicket>.Success(incident, "Incident opened");
    }

    public async Task<CmsOperationResult<IncidentTicket>> UpdateIncidentStatusAsync(UpdateIncidentStatusRequest request, string actor, CancellationToken ct)
    {
        var existing = await _repository.GetIncidentAsync(request.IncidentId, ct).ConfigureAwait(false);
        if (existing is null) return CmsOperationResult<IncidentTicket>.Fail("14", "Incident not found");
        var now = DateTimeOffset.UtcNow;
        var updated = existing with
        {
            Status = request.Status,
            CurrentLevel = request.CurrentLevel ?? existing.CurrentLevel,
            AssignedTo = request.AssignedTo ?? existing.AssignedTo,
            AcknowledgedAt = request.Status == IncidentStatus.Acknowledged && existing.AcknowledgedAt is null ? now : existing.AcknowledgedAt,
            ResolvedAt = request.Status == IncidentStatus.Resolved && existing.ResolvedAt is null ? now : existing.ResolvedAt,
            ClosedAt = request.Status == IncidentStatus.Closed && existing.ClosedAt is null ? now : existing.ClosedAt,
            RootCause = request.RootCause ?? existing.RootCause,
            CorrectiveAction = request.CorrectiveAction ?? existing.CorrectiveAction,
            AuditHash = Hash($"INC-UPDATE|{existing.IncidentId}|{request.Status}|{actor}|{now:O}")
        };
        await _repository.SaveIncidentAsync(updated, ct).ConfigureAwait(false);
        return CmsOperationResult<IncidentTicket>.Success(updated, "Incident updated");
    }

    public async Task<CmsOperationResult<SlaPolicy>> CreateSlaPolicyAsync(CreateSlaPolicyRequest request, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PolicyCode)) return CmsOperationResult<SlaPolicy>.Fail("12", "Policy code is required");
        var policy = new SlaPolicy(Guid.NewGuid(), request.PolicyCode.Trim(), request.TargetType, request.ComponentType, request.ThresholdValue, request.Unit, request.WarningMinutes, request.BreachMinutes, request.EscalateTo, request.Enabled, DateTimeOffset.UtcNow);
        await _repository.SaveSlaPolicyAsync(policy, ct).ConfigureAwait(false);
        return CmsOperationResult<SlaPolicy>.Success(policy, "SLA policy created");
    }

    public async Task<IReadOnlyList<SlaEvaluationResult>> EvaluateSlaAsync(string actor, CancellationToken ct)
    {
        var policies = await _repository.GetSlaPoliciesAsync(ct).ConfigureAwait(false);
        var health = await _repository.GetLatestHealthAsync(ct).ConfigureAwait(false);
        var declines = await _repository.GetTechnicalDeclinesAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct).ConfigureAwait(false);
        var results = new List<SlaEvaluationResult>();
        foreach (var policy in policies.Where(p => p.Enabled))
        {
            decimal observed = policy.TargetType switch
            {
                SlaTargetType.Availability => health.Where(h => !policy.ComponentType.HasValue || h.ComponentType == policy.ComponentType.Value).DefaultIfEmpty().Average(h => h?.AvailabilityPercent ?? 100m),
                SlaTargetType.TechnicalDeclineRate => declines.Count,
                SlaTargetType.ResponseTime => 0m,
                SlaTargetType.DrRpo => 0m,
                SlaTargetType.DrRto => 0m,
                _ => 0m
            };
            var breach = policy.TargetType == SlaTargetType.Availability ? observed < policy.ThresholdValue : observed > policy.ThresholdValue;
            var warning = !breach && policy.TargetType == SlaTargetType.Availability ? observed < policy.ThresholdValue + 0.05m : observed > policy.ThresholdValue * 0.8m;
            Guid? incidentId = null;
            if (breach)
            {
                var incident = (await CreateIncidentAsync(new CreateIncidentRequest(IncidentSeverity.High, policy.ComponentType ?? OpsComponentType.Switch, policy.PolicyCode, $"SLA breached: {policy.PolicyCode}", $"Observed {observed} {policy.Unit}; threshold {policy.ThresholdValue} {policy.Unit}", policy.EscalateTo.ToString()), actor, ct).ConfigureAwait(false)).Value;
                incidentId = incident?.IncidentId;
            }
            var result = new SlaEvaluationResult(Guid.NewGuid(), policy.PolicyId, policy.PolicyCode, breach ? SlaBreachStatus.Breached : warning ? SlaBreachStatus.Warning : SlaBreachStatus.WithinSla, observed, policy.ThresholdValue, policy.Unit, breach ? "SLA breach detected" : warning ? "SLA warning threshold reached" : "Within SLA", incidentId, DateTimeOffset.UtcNow, Hash($"SLA|{policy.PolicyCode}|{observed}|{actor}"));
            await _repository.SaveSlaEvaluationAsync(result, ct).ConfigureAwait(false);
            results.Add(result);
        }
        return results;
    }

    public async Task<CmsOperationResult<EscalationRule>> RegisterEscalationRuleAsync(RegisterEscalationRuleRequest request, string actor, CancellationToken ct)
    {
        var rule = new EscalationRule(Guid.NewGuid(), request.Severity, request.FromLevel, request.ToLevel, request.EscalateAfterMinutes, request.NotifyGroup, request.Enabled);
        await _repository.SaveEscalationRuleAsync(rule, ct).ConfigureAwait(false);
        return CmsOperationResult<EscalationRule>.Success(rule, "Escalation rule registered");
    }

    public async Task<CmsOperationResult<TechnicalDeclineEvent>> RecordTechnicalDeclineAsync(RecordTechnicalDeclineRequest request, string actor, CancellationToken ct)
    {
        var decline = new TechnicalDeclineEvent(Guid.NewGuid(), request.TransactionReference, request.Category, request.ComponentCode, request.ResponseCode, request.Reason, request.Channel, request.Amount, request.CurrencyCode, DateTimeOffset.UtcNow, Hash($"DECLINE|{request.TransactionReference}|{request.ResponseCode}|{actor}"));
        await _repository.SaveTechnicalDeclineAsync(decline, ct).ConfigureAwait(false);
        return CmsOperationResult<TechnicalDeclineEvent>.Success(decline, "Technical decline recorded");
    }

    public async Task<CmsOperationResult<RootCauseAnalysisCase>> SubmitRcaAsync(SubmitRcaRequest request, string actor, CancellationToken ct)
    {
        var incident = await _repository.GetIncidentAsync(request.IncidentId, ct).ConfigureAwait(false);
        if (incident is null) return CmsOperationResult<RootCauseAnalysisCase>.Fail("14", "Incident not found");
        var rca = new RootCauseAnalysisCase(Guid.NewGuid(), request.IncidentId, request.InterimReport, request.FinalReport, request.RootCause, request.CorrectiveAction, request.PreventiveAction, actor, incident.OpenedAt.AddDays(7), DateTimeOffset.UtcNow, "Submitted", Hash($"RCA|{request.IncidentId}|{request.RootCause}|{actor}"));
        await _repository.SaveRcaAsync(rca, ct).ConfigureAwait(false);
        await UpdateIncidentStatusAsync(new UpdateIncidentStatusRequest(request.IncidentId, IncidentStatus.Resolved, null, null, request.RootCause, request.CorrectiveAction), actor, ct).ConfigureAwait(false);
        return CmsOperationResult<RootCauseAnalysisCase>.Success(rca, "RCA submitted");
    }

    public async Task<CmsOperationResult<DrDrillRun>> ScheduleDrDrillAsync(ScheduleDrDrillRequest request, string actor, CancellationToken ct)
    {
        var run = new DrDrillRun(Guid.NewGuid(), request.DrillCode, DrDrillStatus.Planned, request.PlannedAt, null, null, 0, 0, string.Empty, string.Empty, Hash($"DR|{request.DrillCode}|{actor}"));
        await _repository.SaveDrDrillAsync(run, ct).ConfigureAwait(false);
        return CmsOperationResult<DrDrillRun>.Success(run, "DR drill scheduled");
    }

    public async Task<CmsOperationResult<DrDrillRun>> CompleteDrDrillAsync(CompleteDrDrillRequest request, string actor, CancellationToken ct)
    {
        var existing = await _repository.GetDrDrillAsync(request.DrillId, ct).ConfigureAwait(false);
        if (existing is null) return CmsOperationResult<DrDrillRun>.Fail("14", "DR drill not found");
        var now = DateTimeOffset.UtcNow;
        var updated = existing with { Status = request.Passed ? DrDrillStatus.Completed : DrDrillStatus.Failed, StartedAt = existing.StartedAt ?? now.AddMinutes(-request.ObservedRtoMinutes), CompletedAt = now, ObservedRpoMinutes = request.ObservedRpoMinutes, ObservedRtoMinutes = request.ObservedRtoMinutes, EvidenceFile = request.EvidenceFile, Report = request.Report, AuditHash = Hash($"DR-COMPLETE|{request.DrillId}|{request.Passed}|{actor}|{now:O}") };
        await _repository.SaveDrDrillAsync(updated, ct).ConfigureAwait(false);
        return CmsOperationResult<DrDrillRun>.Success(updated, "DR drill completed");
    }

    public async Task<CmsOperationResult<CapacityMetric>> RecordCapacityMetricAsync(RecordCapacityMetricRequest request, string actor, CancellationToken ct)
    {
        var metric = new CapacityMetric(Guid.NewGuid(), request.MetricType, request.ComponentCode, request.Value, request.Unit, DateTimeOffset.UtcNow, Hash($"CAP|{request.MetricType}|{request.ComponentCode}|{request.Value}|{actor}"));
        await _repository.SaveCapacityMetricAsync(metric, ct).ConfigureAwait(false);
        return CmsOperationResult<CapacityMetric>.Success(metric, "Capacity metric recorded");
    }

    public async Task<CmsOperationResult<RegulatoryUptimeReport>> GenerateRegulatoryUptimeReportAsync(GenerateRegulatoryUptimeReportRequest request, string actor, CancellationToken ct)
    {
        var health = await _repository.GetLatestHealthAsync(ct).ConfigureAwait(false);
        var incidents = await _repository.GetIncidentsAsync(null, ct).ConfigureAwait(false);
        var sla = await _repository.GetRecentSlaEvaluationsAsync(ct).ConfigureAwait(false);
        var declines = await _repository.GetTechnicalDeclinesAsync(null, ct).ConfigureAwait(false);
        decimal Avg(OpsComponentType type) => health.Where(h => h.ComponentType == type).DefaultIfEmpty().Average(h => h?.AvailabilityPercent ?? 100m);
        var payload = $"from={request.FromDate};to={request.ToDate};switch={Avg(OpsComponentType.Switch):F4};atm={Avg(OpsComponentType.Atm):F4};pos={Avg(OpsComponentType.Pos):F4};declines={declines.Count};incidents={incidents.Count}";
        var hash = Hash(payload);
        var report = new RegulatoryUptimeReport(Guid.NewGuid(), request.FromDate, request.ToDate, Avg(OpsComponentType.Switch), Avg(OpsComponentType.Atm), Avg(OpsComponentType.Pos), declines.Count, incidents.Count, sla.Count(s => s.Status == SlaBreachStatus.Breached), $"regulatory-uptime-{request.FromDate:yyyyMMdd}-{request.ToDate:yyyyMMdd}.{(string.IsNullOrWhiteSpace(request.OutputFormat) ? "json" : request.OutputFormat.ToLowerInvariant())}", hash, DateTimeOffset.UtcNow);
        await _repository.SaveUptimeReportAsync(report, ct).ConfigureAwait(false);
        return CmsOperationResult<RegulatoryUptimeReport>.Success(report, "Regulatory uptime report generated");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed class InMemoryOperationsCommandCenterRepository : IOperationsCommandCenterRepository
{
    private readonly ConcurrentDictionary<string, OperationsHealthSnapshot> _health = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, IncidentTicket> _incidents = new();
    private readonly ConcurrentDictionary<Guid, SlaPolicy> _slaPolicies = new();
    private readonly ConcurrentQueue<SlaEvaluationResult> _slaEvaluations = new();
    private readonly ConcurrentDictionary<Guid, EscalationRule> _rules = new();
    private readonly ConcurrentDictionary<Guid, TechnicalDeclineEvent> _declines = new();
    private readonly ConcurrentDictionary<Guid, RootCauseAnalysisCase> _rcas = new();
    private readonly ConcurrentDictionary<Guid, DrDrillRun> _drills = new();
    private readonly ConcurrentQueue<CapacityMetric> _capacity = new();
    private readonly ConcurrentDictionary<Guid, RegulatoryUptimeReport> _reports = new();

    public Task SaveHealthSnapshotAsync(OperationsHealthSnapshot snapshot, CancellationToken ct) { _health[$"{snapshot.ComponentType}:{snapshot.ComponentCode}"] = snapshot; return Task.CompletedTask; }
    public Task<IReadOnlyList<OperationsHealthSnapshot>> GetLatestHealthAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<OperationsHealthSnapshot>>(_health.Values.OrderBy(v => v.ComponentType).ThenBy(v => v.ComponentCode).ToList());
    public Task SaveIncidentAsync(IncidentTicket incident, CancellationToken ct) { _incidents[incident.IncidentId] = incident; return Task.CompletedTask; }
    public Task<IncidentTicket?> GetIncidentAsync(Guid incidentId, CancellationToken ct) => Task.FromResult(_incidents.TryGetValue(incidentId, out var i) ? i : null);
    public Task<IReadOnlyList<IncidentTicket>> GetIncidentsAsync(IncidentStatus? status, CancellationToken ct) => Task.FromResult<IReadOnlyList<IncidentTicket>>(_incidents.Values.Where(i => !status.HasValue || i.Status == status.Value).OrderByDescending(i => i.OpenedAt).ToList());
    public Task SaveSlaPolicyAsync(SlaPolicy policy, CancellationToken ct) { _slaPolicies[policy.PolicyId] = policy; return Task.CompletedTask; }
    public Task<IReadOnlyList<SlaPolicy>> GetSlaPoliciesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SlaPolicy>>(_slaPolicies.Values.OrderBy(p => p.PolicyCode).ToList());
    public Task SaveSlaEvaluationAsync(SlaEvaluationResult result, CancellationToken ct) { _slaEvaluations.Enqueue(result); return Task.CompletedTask; }
    public Task<IReadOnlyList<SlaEvaluationResult>> GetRecentSlaEvaluationsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SlaEvaluationResult>>(_slaEvaluations.OrderByDescending(e => e.EvaluatedAt).Take(200).ToList());
    public Task SaveEscalationRuleAsync(EscalationRule rule, CancellationToken ct) { _rules[rule.RuleId] = rule; return Task.CompletedTask; }
    public Task<IReadOnlyList<EscalationRule>> GetEscalationRulesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<EscalationRule>>(_rules.Values.ToList());
    public Task SaveTechnicalDeclineAsync(TechnicalDeclineEvent decline, CancellationToken ct) { _declines[decline.DeclineId] = decline; return Task.CompletedTask; }
    public Task<IReadOnlyList<TechnicalDeclineEvent>> GetTechnicalDeclinesAsync(DateOnly? businessDate, CancellationToken ct) => Task.FromResult<IReadOnlyList<TechnicalDeclineEvent>>(_declines.Values.Where(d => !businessDate.HasValue || DateOnly.FromDateTime(d.OccurredAt.UtcDateTime) == businessDate.Value).OrderByDescending(d => d.OccurredAt).ToList());
    public Task SaveRcaAsync(RootCauseAnalysisCase rca, CancellationToken ct) { _rcas[rca.RcaId] = rca; return Task.CompletedTask; }
    public Task<IReadOnlyList<RootCauseAnalysisCase>> GetRcaCasesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RootCauseAnalysisCase>>(_rcas.Values.OrderByDescending(r => r.DueAt).ToList());
    public Task SaveDrDrillAsync(DrDrillRun run, CancellationToken ct) { _drills[run.DrillId] = run; return Task.CompletedTask; }
    public Task<DrDrillRun?> GetDrDrillAsync(Guid drillId, CancellationToken ct) => Task.FromResult(_drills.TryGetValue(drillId, out var d) ? d : null);
    public Task<IReadOnlyList<DrDrillRun>> GetDrDrillsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<DrDrillRun>>(_drills.Values.OrderByDescending(d => d.PlannedAt).ToList());
    public Task SaveCapacityMetricAsync(CapacityMetric metric, CancellationToken ct) { _capacity.Enqueue(metric); return Task.CompletedTask; }
    public Task<IReadOnlyList<CapacityMetric>> GetCapacityMetricsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CapacityMetric>>(_capacity.OrderByDescending(m => m.CapturedAt).Take(500).ToList());
    public Task SaveUptimeReportAsync(RegulatoryUptimeReport report, CancellationToken ct) { _reports[report.ReportId] = report; return Task.CompletedTask; }
    public Task<IReadOnlyList<RegulatoryUptimeReport>> GetUptimeReportsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<RegulatoryUptimeReport>>(_reports.Values.OrderByDescending(r => r.GeneratedAt).ToList());
    public Task<OperationsDashboard> GetDashboardAsync(CancellationToken ct)
    {
        var health = _health.Values.ToList();
        var incidents = _incidents.Values.ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dashboard = new OperationsDashboard(
            health.Count,
            health.Count(h => h.Status == OpsHealthStatus.Healthy),
            incidents.Count(i => i.Status is IncidentStatus.Open or IncidentStatus.Acknowledged or IncidentStatus.Assigned or IncidentStatus.InProgress or IncidentStatus.Escalated),
            incidents.Count(i => (i.Severity == IncidentSeverity.Critical || i.Severity == IncidentSeverity.Regulatory) && i.Status != IncidentStatus.Closed),
            _slaEvaluations.Count(e => e.Status == SlaBreachStatus.Breached),
            _declines.Values.Count(d => DateOnly.FromDateTime(d.OccurredAt.UtcDateTime) == today),
            _rcas.Values.Count(r => !string.Equals(r.Status, "Submitted", StringComparison.OrdinalIgnoreCase)),
            _drills.Values.Count(d => d.Status == DrDrillStatus.Planned),
            health.Count == 0 ? 100m : health.Average(h => h.AvailabilityPercent),
            DateTimeOffset.UtcNow);
        return Task.FromResult(dashboard);
    }
}
