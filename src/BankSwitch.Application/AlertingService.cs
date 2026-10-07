using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class AlertingService : IAlertingService
{
    private readonly IAlertingRepository _repository;
    private readonly IMetricCollector _metrics;
    private readonly ISiemForwarder _siemForwarder;
    private readonly AlertingOptions _options;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public AlertingService(
        IAlertingRepository repository,
        IMetricCollector metrics,
        ISiemForwarder siemForwarder,
        AlertingOptions options,
        IAuditLogger audit,
        IClock clock)
    {
        _repository = repository;
        _metrics = metrics;
        _siemForwarder = siemForwarder;
        _options = options;
        _audit = audit;
        _clock = clock;
    }

    // ---------------------------------------------------------------
    // Rule management
    // ---------------------------------------------------------------

    public async Task<CmsOperationResult<AlertRule>> SaveAlertRuleAsync(AlertRuleInput input, Guid? id, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return CmsOperationResult<AlertRule>.Fail("30", "Alert rule name is required.");
        if (input.ThresholdValue < 0) return CmsOperationResult<AlertRule>.Fail("30", "Threshold must be non-negative.");
        if (input.EvaluationWindowMinutes < 1) return CmsOperationResult<AlertRule>.Fail("30", "Evaluation window must be at least 1 minute.");

        AlertRule? existing = id.HasValue ? await _repository.GetRuleAsync(id.Value, cancellationToken).ConfigureAwait(false) : null;

        var rule = new AlertRule
        {
            Id = id ?? Guid.NewGuid(),
            Name = input.Name.Trim(),
            RuleType = input.RuleType,
            Severity = input.Severity,
            ThresholdValue = input.ThresholdValue,
            EvaluationWindow = TimeSpan.FromMinutes(input.EvaluationWindowMinutes),
            MinimumSamples = Math.Max(1, input.MinimumSamples),
            SuppressionWindow = TimeSpan.FromMinutes(input.SuppressionWindowMinutes),
            NodeIdFilter = input.NodeIdFilter?.Trim() ?? string.Empty,
            IsActive = input.IsActive,
            CreatedAt = existing?.CreatedAt ?? _clock.UtcNow
        };

        if (existing is null)
        {
            await _repository.AddRuleAsync(rule, cancellationToken).ConfigureAwait(false);
            _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "CreateAlertRule", string.Empty, rule.Name, $"type={rule.RuleType} threshold={rule.ThresholdValue}", string.Empty);
        }
        else
        {
            await _repository.UpdateRuleAsync(rule, cancellationToken).ConfigureAwait(false);
            _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "UpdateAlertRule", existing.Name, rule.Name, $"type={rule.RuleType} threshold={rule.ThresholdValue}", string.Empty);
        }

        return CmsOperationResult<AlertRule>.Success(rule);
    }

    public Task<IReadOnlyList<AlertRule>> GetAlertRulesAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllRulesAsync(cancellationToken);

    public Task<AlertRule?> GetAlertRuleAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetRuleAsync(id, cancellationToken);

    // ---------------------------------------------------------------
    // Event lifecycle
    // ---------------------------------------------------------------

    public Task<IReadOnlyList<AlertEvent>> GetAlertEventsAsync(AlertEventFilter filter, CancellationToken cancellationToken = default)
        => _repository.GetEventsAsync(filter, cancellationToken);

    public async Task<CmsOperationResult<AlertEvent>> AcknowledgeAlertAsync(Guid eventId, string actor, CancellationToken cancellationToken = default)
    {
        var evt = await _repository.GetEventAsync(eventId, cancellationToken).ConfigureAwait(false);
        if (evt is null) return CmsOperationResult<AlertEvent>.Fail("25", "Alert event not found.");
        if (evt.Status != AlertStatus.Active) return CmsOperationResult<AlertEvent>.Success(evt, "Alert is not active.");

        var updated = evt with { Status = AlertStatus.Acknowledged, AcknowledgedAt = _clock.UtcNow, AcknowledgedBy = actor };
        await _repository.UpdateEventAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "AcknowledgeAlert", AlertStatus.Active.ToString(), AlertStatus.Acknowledged.ToString(), $"Rule={evt.RuleName}", string.Empty);
        return CmsOperationResult<AlertEvent>.Success(updated);
    }

    public async Task<CmsOperationResult<AlertEvent>> ResolveAlertAsync(Guid eventId, string actor, CancellationToken cancellationToken = default)
    {
        var evt = await _repository.GetEventAsync(eventId, cancellationToken).ConfigureAwait(false);
        if (evt is null) return CmsOperationResult<AlertEvent>.Fail("25", "Alert event not found.");

        var updated = evt with { Status = AlertStatus.Resolved, ResolvedAt = _clock.UtcNow };
        await _repository.UpdateEventAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "ResolveAlert", evt.Status.ToString(), AlertStatus.Resolved.ToString(), $"Rule={evt.RuleName}", string.Empty);
        return CmsOperationResult<AlertEvent>.Success(updated);
    }

    // ---------------------------------------------------------------
    // Threshold evaluation loop
    // ---------------------------------------------------------------

    public async Task<IReadOnlyList<AlertEvent>> EvaluateAndFireAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _repository.GetActiveRulesAsync(cancellationToken).ConfigureAwait(false);
        var fired = new List<AlertEvent>();

        foreach (var rule in rules)
        {
            try
            {
                var breach = EvaluateRule(rule);
                if (breach is null) continue;

                // Check suppression: do not re-fire if still within the suppression window
                var latest = await _repository.GetLatestEventForRuleAsync(rule.Id, cancellationToken).ConfigureAwait(false);
                if (latest is not null && latest.Status == AlertStatus.Active
                    && _clock.UtcNow - latest.FiredAt < rule.SuppressionWindow)
                    continue;

                var alertEvent = new AlertEvent
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    RuleType = rule.RuleType,
                    Severity = rule.Severity,
                    Status = AlertStatus.Active,
                    Title = breach.Title,
                    Detail = breach.Detail,
                    NodeId = breach.NodeId,
                    ObservedValue = breach.ObservedValue,
                    ThresholdValue = rule.ThresholdValue,
                    FiredAt = _clock.UtcNow
                };
                await _repository.AddEventAsync(alertEvent, cancellationToken).ConfigureAwait(false);
                fired.Add(alertEvent);

                _audit.LogSystem(alertEvent.Id.ToString("N"), $"ALERT [{rule.Severity}] {alertEvent.Title}: {alertEvent.Detail}");

                // Forward to SIEM if configured
                if (_options.ForwardAlertsToSiem)
                {
                    await ForwardAlertToSiemAsync(alertEvent, cancellationToken).ConfigureAwait(false);
                }

                // Fire webhook if configured
                if (!string.IsNullOrWhiteSpace(_options.WebhookUrl))
                {
                    await FireWebhookAsync(alertEvent, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _audit.LogSystem(rule.Id.ToString("N"), $"Alert evaluation error for rule '{rule.Name}': {ex.Message}", ex);
            }
        }

        return fired;
    }

    // ---------------------------------------------------------------
    // Per-rule evaluation logic
    // ---------------------------------------------------------------

    private AlertBreach? EvaluateRule(AlertRule rule)
    {
        var now = _clock.UtcNow;
        var since = now - rule.EvaluationWindow;
        var nodeId = string.IsNullOrWhiteSpace(rule.NodeIdFilter) ? null : rule.NodeIdFilter;

        return rule.RuleType switch
        {
            AlertRuleType.DeclineRateThreshold => EvaluateDeclineRate(rule, since, nodeId),
            AlertRuleType.LatencyThreshold => EvaluateLatency(rule, since, nodeId),
            AlertRuleType.TpsUnderflow => EvaluateTpsUnderflow(rule, since, nodeId),
            AlertRuleType.QueueDepthThreshold => EvaluateQueueDepth(rule, since),
            AlertRuleType.ResponseCodeSpike => EvaluateResponseCodeSpike(rule, since, nodeId),
            _ => null
        };
    }

    private AlertBreach? EvaluateDeclineRate(AlertRule rule, DateTimeOffset since, string? nodeId)
    {
        var total = _metrics.Query(MetricNames.TransactionCount, nodeId, since);
        if (total.Count < rule.MinimumSamples) return null;
        var declined = _metrics.Query(MetricNames.DeclineCount, nodeId, since);
        var rate = (double)declined.Count / total.Count * 100d;
        if (rate < rule.ThresholdValue) return null;
        return new AlertBreach($"Decline rate {rate:F1}% exceeds threshold {rule.ThresholdValue}%",
            $"Decline rate over {rule.EvaluationWindow.TotalMinutes:0}min window: {declined.Count}/{total.Count} transactions declined. Node={nodeId ?? "ALL"}",
            nodeId ?? string.Empty, rate);
    }

    private AlertBreach? EvaluateLatency(AlertRule rule, DateTimeOffset since, string? nodeId)
    {
        var samples = _metrics.Query(MetricNames.LatencyMs, nodeId, since);
        if (samples.Count < rule.MinimumSamples) return null;
        var avg = samples.Average(s => s.Value);
        if (avg < rule.ThresholdValue) return null;
        return new AlertBreach($"Average latency {avg:F0}ms exceeds threshold {rule.ThresholdValue}ms",
            $"Average response latency over {rule.EvaluationWindow.TotalMinutes:0}min: {avg:F0}ms. Node={nodeId ?? "ALL"}",
            nodeId ?? string.Empty, avg);
    }

    private AlertBreach? EvaluateTpsUnderflow(AlertRule rule, DateTimeOffset since, string? nodeId)
    {
        var tps = _metrics.Rate(MetricNames.TransactionCount, nodeId, since, rule.EvaluationWindow);
        if (tps >= rule.ThresholdValue) return null;
        // Only alert if there were any transactions recently (avoids firing on idle systems)
        var recentAny = _metrics.Query(MetricNames.TransactionCount, nodeId, _clock.UtcNow.AddHours(-1));
        if (recentAny.Count == 0) return null;
        return new AlertBreach($"TPS {tps:F2} below minimum threshold {rule.ThresholdValue}",
            $"Transaction rate dropped below {rule.ThresholdValue} TPS over last {rule.EvaluationWindow.TotalMinutes:0}min. Possible sink outage. Node={nodeId ?? "ALL"}",
            nodeId ?? string.Empty, tps);
    }

    private AlertBreach? EvaluateQueueDepth(AlertRule rule, DateTimeOffset since)
    {
        var samples = _metrics.Query(MetricNames.QueueDepth, null, since);
        if (!samples.Any()) return null;
        var latest = samples.Last().Value;
        if (latest < rule.ThresholdValue) return null;
        return new AlertBreach($"Queue depth {latest:F0} exceeds threshold {rule.ThresholdValue}",
            $"Transaction processing queue has {latest:F0} pending messages. Consumer may be falling behind.", string.Empty, latest);
    }

    private AlertBreach? EvaluateResponseCodeSpike(AlertRule rule, DateTimeOffset since, string? nodeId)
    {
        // Detect any response code spike using decline count as proxy
        var declined = _metrics.Query(MetricNames.DeclineCount, nodeId, since);
        if (declined.Count < rule.ThresholdValue) return null;
        return new AlertBreach($"Decline spike: {declined.Count} declines in {rule.EvaluationWindow.TotalMinutes:0}min",
            $"{declined.Count} transaction declines detected over {rule.EvaluationWindow.TotalMinutes:0}min window. Node={nodeId ?? "ALL"}",
            nodeId ?? string.Empty, declined.Count);
    }

    private async Task ForwardAlertToSiemAsync(AlertEvent alert, CancellationToken cancellationToken)
    {
        try
        {
            // Wrap alert as a SIEM event for forwarding
            var siemEvent = new SiemSecurityEvent
            {
                CorrelationId = alert.Id.ToString("N"),
                EventType = $"ALERT_{alert.RuleType}",
                Severity = alert.Severity switch
                {
                    AlertSeverity.Critical => SiemEventSeverity.Critical,
                    AlertSeverity.Error => SiemEventSeverity.Error,
                    AlertSeverity.Warning => SiemEventSeverity.Warning,
                    _ => SiemEventSeverity.Info
                },
                EntityReference = alert.NodeId,
                Message = $"{alert.Title}: {alert.Detail}",
                PayloadJson = $"{{\"ruleId\":\"{alert.RuleId}\",\"observed\":{alert.ObservedValue},\"threshold\":{alert.ThresholdValue}}}",
                CreatedAt = alert.FiredAt,
                DeliveryStatus = SiemDeliveryStatus.Pending
            };

            var result = await _siemForwarder.ForwardAsync(siemEvent, cancellationToken).ConfigureAwait(false);
            var updatedAlert = alert with { ForwardedToSiem = result.IsSuccess };
            await _repository.UpdateEventAsync(updatedAlert, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _audit.LogSystem(alert.Id.ToString("N"), $"SIEM forwarding failed for alert {alert.Title}: {ex.Message}", ex);
        }
    }

    private async Task FireWebhookAsync(AlertEvent alert, CancellationToken cancellationToken)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(_options.WebhookTimeoutSeconds) };
            if (!string.IsNullOrWhiteSpace(_options.WebhookAuthHeader))
                http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", _options.WebhookAuthHeader);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                severity = alert.Severity.ToString(),
                title = alert.Title,
                detail = alert.Detail,
                nodeId = alert.NodeId,
                observedValue = alert.ObservedValue,
                thresholdValue = alert.ThresholdValue,
                firedAt = alert.FiredAt,
                ruleId = alert.RuleId,
                ruleName = alert.RuleName
            });

            var response = await http.PostAsync(_options.WebhookUrl,
                new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
                cancellationToken).ConfigureAwait(false);

            var updatedAlert = alert with { WebhookResponseCode = (int)response.StatusCode };
            await _repository.UpdateEventAsync(updatedAlert, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _audit.LogSystem(alert.Id.ToString("N"), $"Webhook delivery failed for alert {alert.Title}: {ex.Message}", ex);
        }
    }

    private sealed record AlertBreach(string Title, string Detail, string NodeId, double ObservedValue);
}
