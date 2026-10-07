using BankSwitch.Domain;

namespace BankSwitch.Application;

// V34: Card Network Acquiring Certification Simulator.
// Provides Visa/Mastercard/RuPay-NPCI simulators, scheme test cases, POS certification packs,
// EMV/contactless checklists, message/host validation, settlement/reversal/chargeback test flows
// and evidence reports for pre-certification readiness. This is a certification lab boundary;
// official scheme certification still requires scheme specifications, keys and network sign-off.

public enum AcquiringCertificationScheme
{
    Visa,
    Mastercard,
    Rupay,
    NpciNfs
}

public enum CertificationTestCategory
{
    Authorization,
    Reversal,
    Settlement,
    Chargeback,
    Emv,
    Contactless,
    Offline,
    CashAtPos,
    TipAdjustment,
    NetworkManagement,
    NegativeValidation,
    Performance
}

public enum CertificationTestStatus
{
    Draft,
    Ready,
    Running,
    Passed,
    Failed,
    Waived,
    Blocked
}

public enum CertificationSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum EmvChecklistStatus
{
    NotStarted,
    InProgress,
    Passed,
    Failed,
    NotApplicable
}

public enum CertificationFlowKind
{
    Purchase,
    Refund,
    Void,
    Reversal,
    LateReversal,
    PartialReversal,
    PreAuthorization,
    Completion,
    CashAtPos,
    TipAdjustment,
    ContactlessOnline,
    ContactlessOffline,
    SettlementPresentment,
    Chargeback,
    Representment,
    Arbitration
}

public sealed record CertificationTestCase(
    Guid Id,
    string TestCaseCode,
    AcquiringCertificationScheme Scheme,
    CertificationTestCategory Category,
    CertificationFlowKind FlowKind,
    string Title,
    string Description,
    IReadOnlyDictionary<string, string> InputFields,
    IReadOnlyDictionary<string, string> ExpectedFields,
    string ExpectedResponseCode,
    CertificationSeverity Severity,
    bool IsMandatory,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateCertificationTestCaseRequest(
    string TestCaseCode,
    AcquiringCertificationScheme Scheme,
    CertificationTestCategory Category,
    CertificationFlowKind FlowKind,
    string Title,
    string Description,
    IReadOnlyDictionary<string, string> InputFields,
    IReadOnlyDictionary<string, string> ExpectedFields,
    string ExpectedResponseCode,
    CertificationSeverity Severity,
    bool IsMandatory,
    string CorrelationId);

public sealed record CertificationPack(
    Guid Id,
    string PackCode,
    AcquiringCertificationScheme Scheme,
    string TerminalModel,
    string PosProtocol,
    string Version,
    IReadOnlyList<Guid> TestCaseIds,
    CertificationTestStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string CorrelationId);

public sealed record CreateCertificationPackRequest(
    string PackCode,
    AcquiringCertificationScheme Scheme,
    string TerminalModel,
    string PosProtocol,
    string Version,
    IReadOnlyList<Guid> TestCaseIds,
    string CorrelationId);

public sealed record AcquirerMessageValidationRequest(
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    string Mti,
    IReadOnlyDictionary<string, string> Fields,
    string RawMessage,
    string CorrelationId);

public sealed record ValidationFinding(
    string Code,
    CertificationSeverity Severity,
    string Field,
    string Message);

public sealed record AcquirerMessageValidationResult(
    Guid Id,
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    string Mti,
    bool IsValid,
    IReadOnlyList<ValidationFinding> Findings,
    string MessageHash,
    DateTimeOffset ValidatedAt,
    string CorrelationId);

public sealed record HostResponseValidationRequest(
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    string RequestMti,
    string ResponseMti,
    IReadOnlyDictionary<string, string> RequestFields,
    IReadOnlyDictionary<string, string> ResponseFields,
    string ExpectedResponseCode,
    string CorrelationId);

public sealed record HostResponseValidationResult(
    Guid Id,
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    bool IsValid,
    IReadOnlyList<ValidationFinding> Findings,
    DateTimeOffset ValidatedAt,
    string CorrelationId);

public sealed record SchemeSimulationRequest(
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    string Mti,
    IReadOnlyDictionary<string, string> Fields,
    string ScenarioCode,
    string CorrelationId);

public sealed record SchemeSimulationResponse(
    Guid Id,
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    string ResponseMti,
    string ResponseCode,
    string AuthorizationCode,
    string NetworkReference,
    IReadOnlyDictionary<string, string> ResponseFields,
    string SimulatorTrace,
    DateTimeOffset SimulatedAt,
    string CorrelationId);

public sealed record CertificationRun(
    Guid Id,
    Guid PackId,
    string PackCode,
    AcquiringCertificationScheme Scheme,
    CertificationTestStatus Status,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int BlockedTests,
    string EvidenceHash,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Actor,
    string CorrelationId);

public sealed record StartCertificationRunRequest(
    Guid PackId,
    string CorrelationId);

public sealed record CertificationTestResult(
    Guid Id,
    Guid RunId,
    Guid TestCaseId,
    string TestCaseCode,
    CertificationTestStatus Status,
    string ResponseCode,
    IReadOnlyList<ValidationFinding> Findings,
    string RequestHash,
    string ResponseHash,
    string Trace,
    DateTimeOffset ExecutedAt);

public sealed record EmvContactlessChecklistItem(
    Guid Id,
    AcquiringCertificationScheme Scheme,
    string TerminalModel,
    string KernelType,
    string RequirementCode,
    string RequirementText,
    EmvChecklistStatus Status,
    string EvidenceReference,
    string Remarks,
    DateTimeOffset UpdatedAt,
    string CorrelationId);

public sealed record UpsertEmvContactlessChecklistItemRequest(
    Guid? Id,
    AcquiringCertificationScheme Scheme,
    string TerminalModel,
    string KernelType,
    string RequirementCode,
    string RequirementText,
    EmvChecklistStatus Status,
    string EvidenceReference,
    string Remarks,
    string CorrelationId);

public sealed record CertificationFlowTestRequest(
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    string Stan,
    string Rrn,
    decimal Amount,
    string CurrencyCode,
    string MerchantId,
    string TerminalId,
    string PanMasked,
    string CorrelationId);

public sealed record CertificationFlowTestResult(
    Guid Id,
    AcquiringCertificationScheme Scheme,
    CertificationFlowKind FlowKind,
    string Stan,
    string Rrn,
    string ResponseCode,
    string NetworkReference,
    string SettlementReference,
    string ChargebackReference,
    CertificationTestStatus Status,
    IReadOnlyList<ValidationFinding> Findings,
    DateTimeOffset ExecutedAt,
    string CorrelationId);

public sealed record CertificationEvidenceReport(
    Guid Id,
    Guid RunId,
    string ReportFormat,
    string ReportBody,
    string ReportHash,
    DateTimeOffset GeneratedAt,
    string CorrelationId);

public interface IAcquiringSchemeSimulator
{
    AcquiringCertificationScheme Scheme { get; }
    Task<SchemeSimulationResponse> SimulateAsync(SchemeSimulationRequest request, CancellationToken ct = default);
}

public interface IAcquiringCertificationRepository
{
    Task AddTestCaseAsync(CertificationTestCase testCase, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationTestCase>> GetTestCasesAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default);
    Task<CertificationTestCase?> GetTestCaseAsync(Guid id, CancellationToken ct = default);
    Task AddPackAsync(CertificationPack pack, CancellationToken ct = default);
    Task<CertificationPack?> GetPackAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationPack>> GetPacksAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default);
    Task AddRunAsync(CertificationRun run, CancellationToken ct = default);
    Task UpdateRunAsync(CertificationRun run, CancellationToken ct = default);
    Task<CertificationRun?> GetRunAsync(Guid id, CancellationToken ct = default);
    Task AddTestResultAsync(CertificationTestResult result, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default);
    Task AddMessageValidationAsync(AcquirerMessageValidationResult result, CancellationToken ct = default);
    Task AddHostValidationAsync(HostResponseValidationResult result, CancellationToken ct = default);
    Task UpsertChecklistItemAsync(EmvContactlessChecklistItem item, CancellationToken ct = default);
    Task<IReadOnlyList<EmvContactlessChecklistItem>> GetChecklistAsync(AcquiringCertificationScheme? scheme, string? terminalModel, CancellationToken ct = default);
    Task AddFlowResultAsync(CertificationFlowTestResult result, CancellationToken ct = default);
    Task AddEvidenceReportAsync(CertificationEvidenceReport report, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default);
}

public interface IAcquiringCertificationService
{
    Task<CmsOperationResult<CertificationTestCase>> CreateTestCaseAsync(CreateCertificationTestCaseRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationTestCase>> GetTestCasesAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default);
    Task<CmsOperationResult<CertificationPack>> CreatePackAsync(CreateCertificationPackRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationPack>> GetPacksAsync(AcquiringCertificationScheme? scheme, CancellationToken ct = default);
    Task<CmsOperationResult<AcquirerMessageValidationResult>> ValidateAcquirerMessageAsync(AcquirerMessageValidationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<HostResponseValidationResult>> ValidateHostResponseAsync(HostResponseValidationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<SchemeSimulationResponse>> SimulateSchemeAsync(SchemeSimulationRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<CertificationRun>> RunCertificationPackAsync(StartCertificationRunRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationTestResult>> GetRunResultsAsync(Guid runId, CancellationToken ct = default);
    Task<CmsOperationResult<EmvContactlessChecklistItem>> UpsertChecklistItemAsync(UpsertEmvContactlessChecklistItemRequest request, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<EmvContactlessChecklistItem>> GetChecklistAsync(AcquiringCertificationScheme? scheme, string? terminalModel, CancellationToken ct = default);
    Task<CmsOperationResult<CertificationFlowTestResult>> ExecuteFlowTestAsync(CertificationFlowTestRequest request, string actor, CancellationToken ct = default);
    Task<CmsOperationResult<CertificationEvidenceReport>> GenerateEvidenceReportAsync(Guid runId, string format, string correlationId, string actor, CancellationToken ct = default);
    Task<IReadOnlyList<CertificationEvidenceReport>> GetEvidenceReportsAsync(Guid? runId, CancellationToken ct = default);
}
