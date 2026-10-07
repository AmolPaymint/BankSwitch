using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Infrastructure;

public sealed class TcpIsoSinkClient : ISinkClient
{
    private readonly ILogger<TcpIsoSinkClient> _logger;
    private readonly IConfiguration _configuration;
    private readonly SinkCircuitBreaker _circuitBreaker;
    private readonly ISecretProvider _secrets;
    private readonly Iso8583AsciiBitmapFormatter _formatter;

    public TcpIsoSinkClient(ILogger<TcpIsoSinkClient> logger, IConfiguration configuration, SinkCircuitBreaker circuitBreaker, ISecretProvider secrets, Iso8583AsciiBitmapFormatter formatter)
    {
        _logger = logger;
        _configuration = configuration;
        _circuitBreaker = circuitBreaker;
        _secrets = secrets;
        _formatter = formatter;
    }

    public async Task<IsoMessage> SendAsync(IsoMessage message, SinkNode sinkNode, CancellationToken cancellationToken = default)
    {
        if (!_circuitBreaker.CanSend(sinkNode.NodeId, out var reason))
        {
            _logger.LogWarning("Circuit breaker blocked sink {Sink}; reason={Reason}; correlation={CorrelationId}", sinkNode.NodeId, reason, message.CorrelationId);
            return ResponseBuilder.Decline(message, "91");
        }

        var bypass = string.Equals(_configuration["Sink:BypassForDevelopmentOnly"], "true", StringComparison.OrdinalIgnoreCase);
        if (bypass)
        {
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
            _circuitBreaker.RecordSuccess(sinkNode.NodeId);
            return message.CloneResponse(message.Mti is "0400" or "0420" or "0421" ? "0430" : "0210", "00");
        }

        try
        {
            var timeoutSeconds = _configuration.GetValue("Sink:TimeoutSeconds", 60);
            var headerBytes = _configuration.GetValue("Sink:HeaderBytes", 2);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            using var tcp = new TcpClient();
            await tcp.ConnectAsync(sinkNode.Host, sinkNode.Port, timeoutCts.Token).ConfigureAwait(false);
            tcp.NoDelay = true;
            await using var stream = await CreateSinkStreamAsync(tcp, sinkNode, timeoutCts.Token).ConfigureAwait(false);

            var payload = _formatter.Format(message);
            if (payload.Length > sinkNode.Limits.MaxMessageBytes) throw new InvalidDataException($"Outbound message exceeds sink max message size {sinkNode.Limits.MaxMessageBytes}.");
            await IsoTcpFrameCodec.WriteFrameAsync(stream, payload, headerBytes, timeoutCts.Token).ConfigureAwait(false);

            var responseFrame = await IsoTcpFrameCodec.ReadFrameAsync(stream, headerBytes, sinkNode.Limits.MaxMessageBytes, sinkNode.Limits.IdleTimeout, timeoutCts.Token).ConfigureAwait(false);
            var response = _formatter.Parse(responseFrame);
            response.CorrelationId = message.CorrelationId;
            _circuitBreaker.RecordSuccess(sinkNode.NodeId);
            return response;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _circuitBreaker.RecordFailure(sinkNode.NodeId, "Timeout");
            _logger.LogError(ex, "Sink timeout for {Sink}; correlation={CorrelationId}", sinkNode.NodeId, message.CorrelationId);
            return ResponseBuilder.Decline(message, "68");
        }
        catch (Exception ex)
        {
            _circuitBreaker.RecordFailure(sinkNode.NodeId, ex.Message);
            _logger.LogError(ex, "Sink send failed for {Sink}; correlation={CorrelationId}", sinkNode.NodeId, message.CorrelationId);
            return ResponseBuilder.Decline(message, "91");
        }
    }

    private async Task<Stream> CreateSinkStreamAsync(TcpClient tcp, SinkNode sinkNode, CancellationToken cancellationToken)
    {
        var networkStream = tcp.GetStream();
        if (!sinkNode.Security.RequireMtls && !_configuration.GetValue("Sink:UseTls", false)) return networkStream;

        var remoteThumbprint = string.Empty;
        var ssl = new SslStream(networkStream, leaveInnerStreamOpen: false, (_, certificate, _, errors) =>
        {
            if (certificate is null) return false;
            remoteThumbprint = certificate.GetCertHashString();
            if (errors != SslPolicyErrors.None && _configuration.GetValue("Sink:AllowInvalidServerCertificateForDevelopmentOnly", false) == false) return false;
            if (!string.IsNullOrWhiteSpace(sinkNode.Security.CertificateThumbprint))
            {
                return string.Equals(NormalizeThumbprint(sinkNode.Security.CertificateThumbprint), NormalizeThumbprint(remoteThumbprint), StringComparison.OrdinalIgnoreCase);
            }
            return true;
        });

        var clientCertificates = new X509CertificateCollection();
        var certPath = _configuration["Sink:ClientCertificatePath"];
        if (!string.IsNullOrWhiteSpace(certPath))
        {
            var secretName = _configuration["Sink:ClientCertificatePasswordSecretName"] ?? "SinkClientCertificatePassword";
            var password = await TryGetSecretAsync(secretName, cancellationToken).ConfigureAwait(false);
            clientCertificates.Add(new X509Certificate2(certPath, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet));
        }

        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = sinkNode.Host,
            ClientCertificates = clientCertificates,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.Online
        }, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(remoteThumbprint))
        {
            var remoteIp = GetRemoteIp(tcp);
            var policy = NodeSecurityPolicy.ValidateSinkConnection(sinkNode, remoteIp, remoteThumbprint, NodeSecurityPolicy.IsPrivateAddress(remoteIp));
            if (!policy.IsAllowed) throw new AuthenticationException(policy.Reason);
        }
        return ssl;
    }

    private async Task<string> TryGetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        try { return await _secrets.GetSecretAsync(secretName, cancellationToken).ConfigureAwait(false); }
        catch { return string.Empty; }
    }

    private static string GetRemoteIp(TcpClient tcp) => tcp.Client.RemoteEndPoint is IPEndPoint ip ? ip.Address.ToString() : string.Empty;
    private static string NormalizeThumbprint(string value) => value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Trim();
}

public sealed class SinkCircuitBreaker
{
    private readonly ConcurrentDictionary<string, CircuitState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly CircuitBreakerOptions _options;

    public SinkCircuitBreaker(CircuitBreakerOptions options) => _options = options;

    public bool CanSend(string sinkNodeId, out string reason)
    {
        reason = string.Empty;
        var state = _states.GetOrAdd(sinkNodeId, _ => new CircuitState());
        lock (state)
        {
            if (state.OpenUntil > DateTimeOffset.UtcNow)
            {
                reason = state.LastReason;
                return false;
            }
            return true;
        }
    }

    public void RecordSuccess(string sinkNodeId)
    {
        var state = _states.GetOrAdd(sinkNodeId, _ => new CircuitState());
        lock (state)
        {
            state.FailureCount = 0;
            state.OpenUntil = DateTimeOffset.MinValue;
            state.LastReason = string.Empty;
        }
    }

    public void RecordFailure(string sinkNodeId, string reason)
    {
        var state = _states.GetOrAdd(sinkNodeId, _ => new CircuitState());
        lock (state)
        {
            state.FailureCount++;
            state.LastReason = reason;
            if (state.FailureCount >= _options.FailuresBeforeOpen)
            {
                state.OpenUntil = DateTimeOffset.UtcNow.Add(_options.OpenDuration);
            }
        }
    }

    private sealed class CircuitState
    {
        public int FailureCount { get; set; }
        public DateTimeOffset OpenUntil { get; set; }
        public string LastReason { get; set; } = string.Empty;
    }
}

public sealed record CircuitBreakerOptions
{
    public int FailuresBeforeOpen { get; init; } = 3;
    public TimeSpan OpenDuration { get; init; } = TimeSpan.FromSeconds(30);
}
