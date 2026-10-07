using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using BankSwitch.Application;
using BankSwitch.Domain;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BankSwitch.Engine;

public sealed class IsoTcpGatewayHostedService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ISecretProvider _secrets;
    private readonly INodeRepository _nodes;
    private readonly TransactionProcessor _processor;
    private readonly IHsmClient _hsm;
    private readonly Application.Iso8583AsciiBitmapFormatter _formatter;
    private readonly IReplayCache _replayCache;
    private readonly INodeRateLimiter _rateLimiter;
    private readonly ILogger<IsoTcpGatewayHostedService> _logger;
    private TcpListener? _listener;
    private X509Certificate2? _serverCertificate;

    public IsoTcpGatewayHostedService(
        IConfiguration configuration,
        ISecretProvider secrets,
        INodeRepository nodes,
        TransactionProcessor processor,
        IHsmClient hsm,
        Application.Iso8583AsciiBitmapFormatter formatter,
        IReplayCache replayCache,
        INodeRateLimiter rateLimiter,
        ILogger<IsoTcpGatewayHostedService> logger)
    {
        _configuration = configuration;
        _secrets = secrets;
        _nodes = nodes;
        _processor = processor;
        _hsm = hsm;
        _formatter = formatter;
        _replayCache = replayCache;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("SourceGateway:Enabled", true))
        {
            _logger.LogWarning("ISO source TCP gateway is disabled by configuration.");
            return;
        }

        var listenIp = _configuration["SourceGateway:ListenIp"] ?? "0.0.0.0";
        var port = _configuration.GetValue("SourceGateway:Port", 5050);
        var maxConcurrent = Math.Max(1, _configuration.GetValue("SourceGateway:MaxConcurrentConnections", 512));

        // Pre-load server certificate once so it is disposed cleanly on shutdown (fixes BUG-008).
        if (_configuration.GetValue("SourceGateway:RequireMutualTls", false))
        {
            var certPath = _configuration["SourceGateway:ServerCertificatePath"]
                ?? throw new InvalidOperationException("SourceGateway:ServerCertificatePath is required when mTLS is enabled.");
            var secretName = _configuration["SourceGateway:ServerCertificatePasswordSecretName"] ?? "SourceGatewayServerCertificatePassword";
            var password = await TryGetSecretAsync(secretName, stoppingToken).ConfigureAwait(false);
            _serverCertificate = new X509Certificate2(certPath, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
        }

        var ip = IPAddress.Parse(listenIp);
        _listener = new TcpListener(ip, port);
        _listener.Start(backlog: maxConcurrent);
        _logger.LogInformation("ISO source TCP gateway listening on {Ip}:{Port}", listenIp, port);

        using var semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var tcp = await _listener.AcceptTcpClientAsync(stoppingToken).ConfigureAwait(false);
                await semaphore.WaitAsync(stoppingToken).ConfigureAwait(false);
                _ = Task.Run(async () =>
                {
                    try { await HandleClientAsync(tcp, stoppingToken).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                    catch (Exception ex) { _logger.LogError(ex, "Unhandled source gateway client error."); }
                    finally
                    {
                        tcp.Dispose();
                        semaphore.Release();
                    }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _listener?.Stop();
        _serverCertificate?.Dispose();
        _serverCertificate = null;
        return base.StopAsync(cancellationToken);
    }

    private async Task HandleClientAsync(TcpClient tcp, CancellationToken stoppingToken)
    {
        tcp.NoDelay = true;
        var remoteIp = tcp.Client.RemoteEndPoint is IPEndPoint ep ? ep.Address.ToString() : string.Empty;
        await using var stream = await CreateSourceStreamAsync(tcp, stoppingToken).ConfigureAwait(false);
        var sourceNodeId = _configuration["SourceGateway:DefaultSourceNodeId"] ?? throw new InvalidOperationException("SourceGateway:DefaultSourceNodeId is required.");
        var sourceNode = await _nodes.GetSourceNodeAsync(sourceNodeId, stoppingToken).ConfigureAwait(false) ?? throw new InvalidOperationException($"Source node {sourceNodeId} not found.");
        var headerBytes = _configuration.GetValue("SourceGateway:HeaderBytes", 2);
        var replayTtl = TimeSpan.FromSeconds(_configuration.GetValue("SourceGateway:ReplayTtlSeconds", 300));
        var clientCertThumbprint = stream is SslStream ssl && ssl.RemoteCertificate is not null ? ssl.RemoteCertificate.GetCertHashString() : null;
        var policy = NodeSecurityPolicy.ValidateSourceConnection(sourceNode, remoteIp, clientCertThumbprint, NodeSecurityPolicy.IsPrivateAddress(remoteIp));
        if (!policy.IsAllowed)
        {
            _logger.LogWarning("Rejected source connection. Node={Node} RemoteIp={RemoteIp} Reason={Reason}", sourceNode.NodeId, remoteIp, policy.Reason);
            return;
        }

        while (!stoppingToken.IsCancellationRequested && tcp.Connected)
        {
            var frame = await IsoTcpFrameCodec.ReadFrameAsync(stream, headerBytes, sourceNode.Limits.MaxMessageBytes, sourceNode.Limits.IdleTimeout, stoppingToken).ConfigureAwait(false);
            var request = _formatter.Parse(frame);
            if (!_rateLimiter.TryAcquire(sourceNode.NodeId, sourceNode.Limits.TpsLimit, out var throttleReason))
            {
                await WriteDeclineAsync(stream, request, sourceNode, "91", headerBytes, stoppingToken).ConfigureAwait(false);
                _logger.LogWarning("TPS throttle for source {Source}. {Reason}", sourceNode.NodeId, throttleReason);
                continue;
            }

            var replayKey = BuildReplayKey(sourceNode.NodeId, request);
            if (!_replayCache.TryAccept(replayKey, replayTtl))
            {
                await WriteDeclineAsync(stream, request, sourceNode, "94", headerBytes, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var response = await _processor.ProcessAsync(request, sourceNode.NodeId, stoppingToken).ConfigureAwait(false);
            var sourceMac = await _hsm.GenerateMacForSourceAsync(response, sourceNode, stoppingToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(sourceMac)) response.SetField(64, sourceMac);
            await IsoTcpFrameCodec.WriteFrameAsync(stream, _formatter.Format(response), headerBytes, stoppingToken).ConfigureAwait(false);
        }
    }

    private string BuildReplayKey(string nodeId, IsoMessage request)
    {
        throw new NotImplementedException();
    }


    private async Task<Stream> CreateSourceStreamAsync(TcpClient tcp, CancellationToken cancellationToken)
    {
        var networkStream = tcp.GetStream();
        if (_serverCertificate is null) return networkStream;  // mTLS not enabled; certificate was not pre-loaded.

        var ssl = new SslStream(networkStream, leaveInnerStreamOpen: false, (_, certificate, _, errors) =>
        {
            if (certificate is null) return false;
            return errors == SslPolicyErrors.None || _configuration.GetValue("SourceGateway:AllowInvalidClientCertificateForDevelopmentOnly", false);
        });
        await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = _serverCertificate,  // reuse pre-loaded instance; disposed in StopAsync
            ClientCertificateRequired = true,
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CertificateRevocationCheckMode = X509RevocationMode.Online
        }, cancellationToken).ConfigureAwait(false);
        return ssl;
    }

    private async Task<string> TryGetSecretAsync(string secretName, CancellationToken cancellationToken)
    {
        try { return await _secrets.GetSecretAsync(secretName, cancellationToken).ConfigureAwait(false); }
        catch { return string.Empty; }
    }

    private async Task WriteDeclineAsync(Stream stream, IsoMessage request, SourceNode sourceNode, string responseCode, int headerBytes, CancellationToken cancellationToken)
    {
        var response = ResponseBuilder.Decline(request, responseCode);
        var sourceMac = await _hsm.GenerateMacForSourceAsync(response, sourceNode, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(sourceMac)) response.SetField(64, sourceMac);
        await IsoTcpFrameCodec.WriteFrameAsync(stream, _formatter.Format(response), headerBytes, cancellationToken).ConfigureAwait(false);
    }
}