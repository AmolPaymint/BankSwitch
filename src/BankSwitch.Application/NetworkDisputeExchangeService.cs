using System.Security.Cryptography;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class NetworkDisputeExchangeService : INetworkDisputeExchangeService
{
    private readonly INetworkDisputeExchangeRepository _repo;
    private readonly IChargebackService _chargebacks;
    private readonly IDisputeService _disputes;
    private readonly IReadOnlyDictionary<DisputeExchangeNetwork, INetworkDisputeFormatAdapter> _adapters;
    private readonly IReadOnlyDictionary<DisputeExchangeNetwork, INetworkDisputeTransportGateway> _gateways;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public NetworkDisputeExchangeService(
        INetworkDisputeExchangeRepository repo,
        IChargebackService chargebacks,
        IDisputeService disputes,
        IEnumerable<INetworkDisputeFormatAdapter> adapters,
        IEnumerable<INetworkDisputeTransportGateway> gateways,
        IAuditLogger audit,
        IClock clock)
    {
        _repo = repo;
        _chargebacks = chargebacks;
        _disputes = disputes;
        _adapters = adapters.GroupBy(a => a.Network).ToDictionary(g => g.Key, g => g.First());
        _gateways = gateways.GroupBy(g => g.Network).ToDictionary(g => g.Key, g => g.First());
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<NetworkDisputeExchangeFile>> BuildFileAsync(BuildNetworkDisputeFileRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (!_adapters.TryGetValue(request.Network, out var adapter)) return Fail($"No dispute file adapter registered for {request.Network}.");
        if (request.ChargebackCaseIds.Count == 0 && (request.DisputeIds is null || request.DisputeIds.Count == 0)) return Fail("At least one chargeback case or dispute is required.");

        var cbCases = new List<ChargebackCase>();
        foreach (var id in request.ChargebackCaseIds.Distinct())
        {
            var result = await _chargebacks.GetCaseAsync(id, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || result.Value is null) return Fail($"Chargeback case not found: {id}");
            cbCases.Add(result.Value);
        }

        var disputes = new List<CustomerDispute>();
        foreach (var id in (request.DisputeIds ?? Array.Empty<Guid>()).Distinct())
        {
            var result = await _disputes.GetDisputeAsync(id, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess || result.Value is null) return Fail($"Dispute not found: {id}");
            disputes.Add(result.Value);
        }

        var file = adapter.BuildFile(request.FileType, request.BusinessDate, cbCases, disputes, request.OutputFileName, actor);
        var records = adapter.ParseFile(file.Id, request.FileType, file.Content);
        var validation = adapter.ValidateFile(file, records);
        if (!validation.IsValid) return Fail("Dispute exchange validation failed: " + string.Join("; ", validation.Errors));

        await _repo.AddFileAsync(file, records, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(file.FileName, actor, "NetworkDisputeFileBuilt", string.Empty, $"network={file.Network} type={file.FileType} records={file.RecordCount} sha256={file.ContentSha256}", string.Empty, string.Empty);
        return Ok(file, $"{file.Network} {file.FileType} file generated and validated.");
    }

    public async Task<CmsOperationResult<NetworkDisputeExchangeFile>> TransmitFileAsync(Guid fileId, string actor, CancellationToken cancellationToken = default)
    {
        var file = await _repo.GetFileAsync(fileId, cancellationToken).ConfigureAwait(false);
        if (file is null) return Fail("Dispute exchange file not found.");
        if (!_gateways.TryGetValue(file.Network, out var gateway)) return Fail($"No dispute transport gateway registered for {file.Network}.");
        if (file.Direction != DisputeExchangeDirection.Outbound) return Fail("Only outbound files can be transmitted.");
        if (file.Status is DisputeExchangeStatus.Acknowledged or DisputeExchangeStatus.Transmitted) return Ok(file, "File already transmitted.");

        var queued = file with { Status = DisputeExchangeStatus.Queued };
        await _repo.UpdateFileAsync(queued, cancellationToken).ConfigureAwait(false);

        var ack = await gateway.TransmitAsync(queued, cancellationToken).ConfigureAwait(false);
        var transmitted = queued with
        {
            Status = ack.Status,
            ExternalBatchReference = ack.ExternalBatchReference,
            TransportReference = ack.TransportReference,
            NetworkAckCode = ack.AckCode,
            NetworkAckMessage = ack.AckMessage,
            TransmittedAt = _clock.UtcNow,
            AcknowledgedAt = ack.Status == DisputeExchangeStatus.Acknowledged ? _clock.UtcNow : null
        };
        await _repo.UpdateFileAsync(transmitted, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(file.FileName, actor, "NetworkDisputeFileTransmitted", file.Status.ToString(), transmitted.Status.ToString(), $"batch={ack.ExternalBatchReference} tx={ack.TransportReference} code={ack.AckCode}", string.Empty);
        return Ok(transmitted, ack.AckMessage);
    }

    public async Task<CmsOperationResult<NetworkDisputeExchangeFile>> ImportFileAsync(ImportNetworkDisputeFileRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (!_adapters.TryGetValue(request.Network, out var adapter)) return Fail($"No dispute file adapter registered for {request.Network}.");
        if (string.IsNullOrWhiteSpace(request.Content)) return Fail("Inbound file content is required.");

        var file = new NetworkDisputeExchangeFile
        {
            Network = request.Network,
            Direction = DisputeExchangeDirection.Inbound,
            FileType = request.FileType,
            Status = DisputeExchangeStatus.Imported,
            BusinessDate = request.BusinessDate,
            FileName = request.FileName,
            Content = request.Content,
            ContentSha256 = Sha256(request.Content),
            CreatedAt = _clock.UtcNow,
            CreatedBy = actor
        };
        var records = adapter.ParseFile(file.Id, request.FileType, request.Content);
        file = file with { RecordCount = records.Count };
        var validation = adapter.ValidateFile(file, records);
        file = validation.IsValid ? file with { Status = DisputeExchangeStatus.Applied } : file with { Status = DisputeExchangeStatus.Failed, NetworkAckCode = "96", NetworkAckMessage = string.Join("; ", validation.Errors) };

        await _repo.AddFileAsync(file, records, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(file.FileName, actor, "NetworkDisputeFileImported", string.Empty, $"network={file.Network} type={file.FileType} records={file.RecordCount} status={file.Status} sha256={file.ContentSha256}", request.SourceChannel, string.Empty);
        return validation.IsValid ? Ok(file, "Inbound dispute exchange file imported and applied.") : FailResult(file, "Inbound file imported with validation errors: " + file.NetworkAckMessage);
    }

    public async Task<CmsOperationResult<NetworkDisputeExchangeFile>> SubmitExternalOdrUdirAsync(SubmitExternalOdrUdirRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (request.Network is not (DisputeExchangeNetwork.RbiOdr or DisputeExchangeNetwork.NpciUdir or DisputeExchangeNetwork.NpciRupay or DisputeExchangeNetwork.NpciNfs))
            return Fail("External ODR/UDIR submission must use RbiOdr, NpciUdir, NpciRupay or NpciNfs.");
        if (!_adapters.TryGetValue(request.Network, out var adapter)) return Fail($"No ODR/UDIR adapter registered for {request.Network}.");
        if (!_gateways.TryGetValue(request.Network, out var gateway)) return Fail($"No ODR/UDIR transport gateway registered for {request.Network}.");

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);
        var virtualDispute = new CustomerDispute
        {
            Id = request.LocalDisputeId ?? Guid.NewGuid(),
            DisputeReference = string.IsNullOrWhiteSpace(request.UdirTransactionId) ? $"ODR-{Guid.NewGuid().ToString("N")[..8]}" : request.UdirTransactionId,
            CustomerNumber = request.CustomerReference,
            Rrn = request.Rrn,
            MaskedPan = request.MaskedPan,
            DisputedAmount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            TransactionDate = today,
            CustomerStatement = request.EvidenceSummary,
            DisputeType = DisputeType.Other,
            Channel = request.Network.ToString()
        };

        var fileType = request.Network == DisputeExchangeNetwork.RbiOdr ? DisputeExchangeFileType.OdrComplaint : DisputeExchangeFileType.UdirComplaint;
        var file = adapter.BuildFile(fileType, today, Array.Empty<ChargebackCase>(), new[] { virtualDispute }, string.Empty, actor);
        var records = adapter.ParseFile(file.Id, fileType, file.Content)
            .Select(r => r with { LocalDisputeId = request.LocalDisputeId, LocalChargebackCaseId = request.ChargebackCaseId, UdirTransactionId = request.UdirTransactionId })
            .ToList();
        var validation = adapter.ValidateFile(file, records);
        if (!validation.IsValid) return Fail("ODR/UDIR validation failed: " + string.Join("; ", validation.Errors));

        await _repo.AddFileAsync(file, records, cancellationToken).ConfigureAwait(false);
        var ack = await gateway.TransmitAsync(file, cancellationToken).ConfigureAwait(false);
        var submitted = file with
        {
            Status = ack.Status,
            ExternalBatchReference = ack.ExternalBatchReference,
            TransportReference = ack.TransportReference,
            NetworkAckCode = ack.AckCode,
            NetworkAckMessage = ack.AckMessage,
            TransmittedAt = _clock.UtcNow,
            AcknowledgedAt = ack.Status == DisputeExchangeStatus.Acknowledged ? _clock.UtcNow : null
        };
        await _repo.UpdateFileAsync(submitted, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(submitted.FileName, actor, "ExternalOdrUdirSubmitted", string.Empty, $"network={submitted.Network} rrn={request.Rrn} batch={ack.ExternalBatchReference} code={ack.AckCode}", request.CustomerReference, string.Empty);
        return Ok(submitted, ack.AckMessage);
    }

    public Task<IReadOnlyList<NetworkDisputeExchangeFile>> GetFilesAsync(DateOnly? businessDate, DisputeExchangeNetwork? network, DisputeExchangeStatus? status, CancellationToken cancellationToken = default)
        => _repo.GetFilesAsync(businessDate, network, status, cancellationToken);

    public Task<IReadOnlyList<NetworkDisputeExchangeRecord>> GetRecordsAsync(Guid fileId, CancellationToken cancellationToken = default)
        => _repo.GetRecordsAsync(fileId, cancellationToken);

    private static string Sha256(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static CmsOperationResult<NetworkDisputeExchangeFile> Ok(NetworkDisputeExchangeFile file, string message) => CmsOperationResult<NetworkDisputeExchangeFile>.Success(file, message);
    private static CmsOperationResult<NetworkDisputeExchangeFile> Fail(string message) => CmsOperationResult<NetworkDisputeExchangeFile>.Fail("96", message);
    private static CmsOperationResult<NetworkDisputeExchangeFile> FailResult(NetworkDisputeExchangeFile file, string message) => CmsOperationResult<NetworkDisputeExchangeFile>.Fail("96", message);
}
