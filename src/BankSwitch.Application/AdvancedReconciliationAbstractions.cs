using System;
using System.Collections.Generic;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed record ImportNetworkReconciliationFileRequest(
    NetworkReconciliationFormat Format,
    SettlementNetwork Network,
    DateOnly BusinessDate,
    string FileName,
    string FileContent,
    string SourceChannel);

public sealed record AttachAtmEvidenceRequest(
    string Rrn,
    string Stan,
    string TerminalId,
    DateOnly BusinessDate,
    string EvidenceType,
    string FileName,
    string Base64Content,
    string CapturedBy,
    string ExtractedText = "");

public sealed record CreateC3RRunRequest(
    string TerminalId,
    DateOnly BusinessDate,
    decimal OpeningBalance,
    decimal LoadAmount,
    decimal DispensedAmount,
    decimal DepositedAmount,
    decimal CashBroughtBackAmount,
    decimal PhysicalClosingBalance,
    IReadOnlyList<Guid>? EvidenceIds = null);

public sealed record SubmitOdrUdirCaseRequest(
    OdrUdirNetwork Network,
    Guid? LocalDisputeId,
    Guid? ChargebackCaseId,
    string Rrn,
    string MaskedPan,
    decimal Amount,
    string CurrencyCode,
    string ComplaintCategory,
    string UdirTransactionId);

public sealed record OdrUdirNetworkAck(string ExternalCaseReference, string ResponseCode, string ResponseMessage, OdrUdirCaseStatus Status);

public interface INetworkReconciliationFormatParser
{
    NetworkReconciliationFormat Format { get; }
    Task<IReadOnlyList<NetworkReconciliationRecord>> ParseAsync(Guid fileId, SettlementNetwork network, DateOnly businessDate, string fileContent, CancellationToken cancellationToken = default);
}

public interface IAdvancedReconciliationRepository
{
    Task AddNetworkFileAsync(NetworkReconciliationFile file, IReadOnlyCollection<NetworkReconciliationRecord> records, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkReconciliationFile>> GetNetworkFilesAsync(DateOnly? businessDate, SettlementNetwork? network, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkReconciliationRecord>> GetNetworkRecordsAsync(Guid fileId, CancellationToken cancellationToken = default);
    Task AddEvidenceAsync(AtmEvidenceItem evidence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AtmEvidenceItem>> GetEvidenceAsync(string? rrn, string? terminalId, DateOnly? businessDate, CancellationToken cancellationToken = default);
    Task AddC3RRunAsync(C3RAtmReconciliationRun run, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<C3RAtmReconciliationRun>> GetC3RRunsAsync(DateOnly? businessDate, string? terminalId, CancellationToken cancellationToken = default);
    Task UpdateC3RRunAsync(C3RAtmReconciliationRun run, CancellationToken cancellationToken = default);
    Task AddOdrUdirCaseAsync(OdrUdirCase c, CancellationToken cancellationToken = default);
    Task<OdrUdirCase?> GetOdrUdirCaseAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OdrUdirCase>> GetOdrUdirCasesAsync(OdrUdirCaseStatus? status, OdrUdirNetwork? network, CancellationToken cancellationToken = default);
    Task UpdateOdrUdirCaseAsync(OdrUdirCase c, CancellationToken cancellationToken = default);
}

public interface IOdrUdirGateway
{
    OdrUdirNetwork Network { get; }
    Task<OdrUdirNetworkAck> SubmitAsync(OdrUdirCase c, IReadOnlyList<AtmEvidenceItem> evidence, CancellationToken cancellationToken = default);
    Task<OdrUdirNetworkAck> SubmitEvidenceAsync(OdrUdirCase c, IReadOnlyList<AtmEvidenceItem> evidence, CancellationToken cancellationToken = default);
}

public interface IAdvancedReconciliationService
{
    Task<CmsOperationResult<NetworkReconciliationFile>> ImportNetworkFileAsync(ImportNetworkReconciliationFileRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkReconciliationFile>> GetNetworkFilesAsync(DateOnly? businessDate, SettlementNetwork? network, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NetworkReconciliationRecord>> GetNetworkRecordsAsync(Guid fileId, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<AtmEvidenceItem>> AttachAtmEvidenceAsync(AttachAtmEvidenceRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AtmEvidenceItem>> GetAtmEvidenceAsync(string? rrn, string? terminalId, DateOnly? businessDate, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<C3RAtmReconciliationRun>> CreateC3RRunAsync(CreateC3RRunRequest request, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<C3RAtmReconciliationRun>> GetC3RRunsAsync(DateOnly? businessDate, string? terminalId, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<OdrUdirCase>> SubmitOdrUdirCaseAsync(SubmitOdrUdirCaseRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<OdrUdirCase>> SubmitOdrUdirEvidenceAsync(Guid caseId, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OdrUdirCase>> GetOdrUdirCasesAsync(OdrUdirCaseStatus? status, OdrUdirNetwork? network, CancellationToken cancellationToken = default);
}
