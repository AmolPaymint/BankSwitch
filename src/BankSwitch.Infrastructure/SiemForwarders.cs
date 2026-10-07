using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

// ============================================================
// Splunk HTTP Event Collector (HEC)
// ============================================================

/// <summary>
/// Forwards security events to Splunk via the HTTP Event Collector (HEC) API.
/// Batches multiple events in a single HTTP POST to reduce connection overhead.
/// Requires: <c>Siem:SplunkHecUrl</c> and <c>Siem:SplunkHecToken</c> in config.
/// </summary>
public sealed class SplunkHecSiemForwarder : ISiemForwarder
{
    private readonly HttpClient _http;
    private readonly string _hecUrl;
    private readonly ILogger<SplunkHecSiemForwarder> _logger;

    public string ForwarderName => "Splunk HEC";

    public SplunkHecSiemForwarder(string hecUrl, string hecToken, ILogger<SplunkHecSiemForwarder> logger)
    {
        _hecUrl = hecUrl;
        _logger = logger;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Splunk {hecToken}");
    }

    public async Task<SiemForwardResult> ForwardAsync(SiemSecurityEvent evt, CancellationToken cancellationToken = default)
        => await ForwardBatchAsync(new[] { evt }, cancellationToken).ConfigureAwait(false);

    public async Task<SiemForwardResult> ForwardBatchAsync(IReadOnlyCollection<SiemSecurityEvent> events, CancellationToken cancellationToken = default)
    {
        try
        {
            // Splunk HEC batch format: newline-delimited JSON objects
            var sb = new StringBuilder();
            foreach (var evt in events)
            {
                var obj = new { time = evt.CreatedAt.ToUnixTimeSeconds(), source = "bankswitch", sourcetype = "bankswitch:security", @event = BuildSplunkEvent(evt) };
                sb.AppendLine(JsonSerializer.Serialize(obj));
            }

            var response = await _http.PostAsync(_hecUrl,
                new StringContent(sb.ToString(), Encoding.UTF8, "application/json"),
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Splunk HEC returned {StatusCode} for {Count} events.", (int)response.StatusCode, events.Count);

            return new SiemForwardResult(response.IsSuccessStatusCode, (int)response.StatusCode, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Splunk HEC delivery failed for batch of {Count} events.", events.Count);
            return new SiemForwardResult(false, 0, ex.Message);
        }
    }

    private static object BuildSplunkEvent(SiemSecurityEvent evt) => new
    {
        event_type = evt.EventType,
        severity = evt.Severity.ToString(),
        resource_id = evt.EntityReference,
        description = evt.Message,
        payload = evt.PayloadJson,
        occurred_at = evt.CreatedAt
    };
}

// ============================================================
// Microsoft Sentinel (Log Analytics Workspace Data Collector API)
// ============================================================

/// <summary>
/// Forwards security events to Microsoft Sentinel via the Log Analytics
/// Data Collector API (HTTP Data Collector API v1).
/// Requires: <c>Siem:SentinelWorkspaceId</c>, <c>Siem:SentinelSharedKey</c>, <c>Siem:SentinelLogType</c>.
/// </summary>
public sealed class SentinelSiemForwarder : ISiemForwarder
{
    private readonly HttpClient _http;
    private readonly string _workspaceId;
    private readonly string _sharedKey;
    private readonly string _logType;
    private readonly ILogger<SentinelSiemForwarder> _logger;

    public string ForwarderName => "Microsoft Sentinel";

    public SentinelSiemForwarder(string workspaceId, string sharedKey, string logType, ILogger<SentinelSiemForwarder> logger)
    {
        _workspaceId = workspaceId;
        _sharedKey = sharedKey;
        _logType = string.IsNullOrWhiteSpace(logType) ? "BankSwitchSecurity" : logType;
        _logger = logger;
        _http = new HttpClient();
    }

    public async Task<SiemForwardResult> ForwardAsync(SiemSecurityEvent evt, CancellationToken cancellationToken = default)
        => await ForwardBatchAsync(new[] { evt }, cancellationToken).ConfigureAwait(false);

    public async Task<SiemForwardResult> ForwardBatchAsync(IReadOnlyCollection<SiemSecurityEvent> events, CancellationToken cancellationToken = default)
    {
        try
        {
            var body = JsonSerializer.Serialize(events.Select(evt => new
            {
                EventType = evt.EventType,
                Severity = evt.Severity.ToString(),
                ResourceId = evt.EntityReference,
                Description = evt.Message,
                OccurredAt = evt.CreatedAt
            }));

            var dateString = DateTime.UtcNow.ToString("R");
            var signature = BuildSentinelSignature(body, dateString);

            var url = $"https://{_workspaceId}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01";
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.TryAddWithoutValidation("Authorization", $"SharedKey {_workspaceId}:{signature}");
            request.Headers.TryAddWithoutValidation("Log-Type", _logType);
            request.Headers.TryAddWithoutValidation("x-ms-date", dateString);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Sentinel returned {StatusCode} for batch of {Count} events.", (int)response.StatusCode, events.Count);

            return new SiemForwardResult(response.IsSuccessStatusCode, (int)response.StatusCode, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sentinel delivery failed.");
            return new SiemForwardResult(false, 0, ex.Message);
        }
    }

    private string BuildSentinelSignature(string body, string dateString)
    {
        var stringToHash = $"POST\n{Encoding.UTF8.GetByteCount(body)}\napplication/json\nx-ms-date:{dateString}\n/api/logs";
        var keyBytes = Convert.FromBase64String(_sharedKey);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToHash)));
    }
}

// ============================================================
// Generic Webhook (PagerDuty / Opsgenie / Slack compatible)
// ============================================================

/// <summary>
/// Forwards security events to a generic JSON webhook endpoint.
/// Compatible with PagerDuty Events API v2, Opsgenie Alerts API,
/// and Slack incoming webhooks by adjusting the payload shape via config.
/// </summary>
public sealed class WebhookSiemForwarder : ISiemForwarder
{
    private readonly HttpClient _http;
    private readonly string _webhookUrl;
    private readonly ILogger<WebhookSiemForwarder> _logger;

    public string ForwarderName => "Webhook";

    public WebhookSiemForwarder(string webhookUrl, string? authHeader, ILogger<WebhookSiemForwarder> logger)
    {
        _webhookUrl = webhookUrl;
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        if (!string.IsNullOrWhiteSpace(authHeader))
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
    }

    public async Task<SiemForwardResult> ForwardAsync(SiemSecurityEvent evt, CancellationToken cancellationToken = default)
        => await ForwardBatchAsync(new[] { evt }, cancellationToken).ConfigureAwait(false);

    public async Task<SiemForwardResult> ForwardBatchAsync(IReadOnlyCollection<SiemSecurityEvent> events, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                source = "bankswitch",
                count = events.Count,
                events = events.Select(e => new { e.EventType, Severity = e.Severity.ToString(), e.EntityReference, e.Message, e.CreatedAt })
            });
            var response = await _http.PostAsync(_webhookUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"),
                cancellationToken).ConfigureAwait(false);
            return new SiemForwardResult(response.IsSuccessStatusCode, (int)response.StatusCode, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook SIEM delivery failed for {Count} events.", events.Count);
            return new SiemForwardResult(false, 0, ex.Message);
        }
    }
}

// ============================================================
// Log-only forwarder (development / test)
// ============================================================

/// <summary>
/// Writes SIEM events to structured ILogger output only. Used in development,
/// unit tests, and when no SIEM endpoint is configured.
/// </summary>
public sealed class LogOnlySiemForwarder : ISiemForwarder
{
    private readonly ILogger<LogOnlySiemForwarder> _logger;
    public string ForwarderName => "LogOnly";

    public LogOnlySiemForwarder(ILogger<LogOnlySiemForwarder> logger) => _logger = logger;

    public Task<SiemForwardResult> ForwardAsync(SiemSecurityEvent evt, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SIEM[{Severity}] EventType={EventType} Resource={ResourceId} Description={Description} OccurredAt={OccurredAt}",
            evt.Severity, evt.EventType, evt.EntityReference, evt.Message, evt.CreatedAt);
        return Task.FromResult(new SiemForwardResult(true, 200, string.Empty));
    }

    public Task<SiemForwardResult> ForwardBatchAsync(IReadOnlyCollection<SiemSecurityEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var evt in events)
        {
            _logger.LogInformation("SIEM[{Severity}] EventType={EventType} Resource={ResourceId} Description={Description}",
                evt.Severity, evt.EventType, evt.EntityReference, evt.Message);
        }
        return Task.FromResult(new SiemForwardResult(true, 200, string.Empty));
    }
}
