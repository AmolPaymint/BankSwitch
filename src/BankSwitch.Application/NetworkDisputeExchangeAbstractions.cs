using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed record BuildNetworkDisputeFileRequest(
    DisputeExchangeNetwork Network,
    DisputeExchangeFileType FileType,
    DateOnly BusinessDate,
    IReadOnlyList<Guid> ChargebackCaseIds,
    IReadOnlyList<Guid>? DisputeIds = null,
    string OutputFileName = "");

public sealed record ImportNetworkDisputeFileRequest(
    DisputeExchangeNetwork Network,
    DisputeExchangeFileType FileType,
    DateOnly BusinessDate,
    string FileName,
    string Content,
    string SourceChannel);

public sealed record SubmitExternalOdrUdirRequest(
    DisputeExchangeNetwork Network,
    Guid? LocalDisputeId,
    Guid? ChargebackCaseId,
    string Rrn,
    string MaskedPan,
    decimal Amount,
    string CurrencyCode,
    string ComplaintCategory,
    string CustomerReference,
    string UdirTransactionId,
    string EvidenceSummary);

public interface INetworkDisputeFormatAdapter
{
    DisputeExchangeNetwork Network { get; }
    NetworkDisputeExchangeFile BuildFile(
        DisputeExchangeFileType fileType,
        DateOnly businessDate,
        IReadOnlyList<ChargebackCase> chargebackCases,
        IReadOnlyList<CustomerDispute> disputes,
        string fileName,
        string actor);

    IReadOnlyList<NetworkDisputeExchangeRecord> ParseFile(Guid fileId, DisputeExchangeFileType fileType, string content);
    NetworkDisputeValidationResult ValidateFile(NetworkDisputeExchangeFile file, IReadOnlyList<NetworkDisputeExchangeRecord> records);
}

public interface INetworkDisputeTransportGateway
{
    DisputeExchangeNetwork Network { get; }
    Task<NetworkDisputeTransportAck> TransmitAsync(NetworkDisputeExchangeFile file, CancellationToken cancellationToken = default);
    Task<NetworkDisputeTransportAck> QueryStatusAsync(string externalBatchReference, CancellationToken cancellationToken = default);
}

public interface INetworkDisputeExchangeRepository
{
    Task AddFileAsync(NetworkDisputeExchangeFile file, IReadOnlyCollection<NetworkDisputeExchangeRecord> records, CancellationToken cancellationToken = default);
    Task UpdateFileAsync(NetworkDisputeExchangeFile file, CancellationToken cancellationToken = default);
    Task<NetworkDisputeExchangeFile?> GetFileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkDisputeExchangeFile>> GetFilesAsync(DateOnly? businessDate, DisputeExchangeNetwork? network, DisputeExchangeStatus? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkDisputeExchangeRecord>> GetRecordsAsync(Guid fileId, CancellationToken cancellationToken = default);
}

public interface INetworkDisputeExchangeService
{
    Task<CmsOperationResult<NetworkDisputeExchangeFile>> BuildFileAsync(BuildNetworkDisputeFileRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkDisputeExchangeFile>> TransmitFileAsync(Guid fileId, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkDisputeExchangeFile>> ImportFileAsync(ImportNetworkDisputeFileRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NetworkDisputeExchangeFile>> SubmitExternalOdrUdirAsync(SubmitExternalOdrUdirRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkDisputeExchangeFile>> GetFilesAsync(DateOnly? businessDate, DisputeExchangeNetwork? network, DisputeExchangeStatus? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkDisputeExchangeRecord>> GetRecordsAsync(Guid fileId, CancellationToken cancellationToken = default);
}
