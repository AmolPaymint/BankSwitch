using BankSwitch.Domain;

namespace BankSwitch.Application;

public interface IOperationalControlService
{
    Task<CmsOperationResult<AgencyProfile>> OnboardAgencyAsync(OnboardAgencyRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<AgencyProfile>> AdjustAgencyCreditAsync(AdjustAgencyCreditRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CorporateProfile>> OnboardCorporateAsync(OnboardCorporateRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CorporateDepartment>> CreateCorporateDepartmentAsync(CreateCorporateDepartmentRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CorporateEmployee>> CreateCorporateEmployeeAsync(CreateCorporateEmployeeRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CorporateBudget>> CreateCorporateBudgetAsync(CreateCorporateBudgetRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CardStockBatch>> AllocateCardStockAsync(AllocateCardStockRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<BulkIssueCardsResult>> BulkIssueCardsAsync(BulkIssueCardsRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<AdvancedLimitRule>> CreateAdvancedLimitRuleAsync(CreateAdvancedLimitRuleRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<BankSwitch.Domain.RiskRule>> CreateRiskRuleAsync(CreateRiskRuleRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<NotificationMessage>> QueueNotificationAsync(QueueNotificationRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<StatementResult>> GenerateStatementAsync(GenerateStatementRequest request, CancellationToken cancellationToken = default);
    Task<RiskELDecision> EvaluateRiskAsync(RiskEvaluationContext context, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<bool>> ValidateAdvancedLimitsAsync(AdvancedLimitEvaluationContext context, CancellationToken cancellationToken = default);
}

public interface IOperationalControlRepository
{
    Task AddAgencyAsync(AgencyProfile agency, CancellationToken cancellationToken = default);
    Task UpdateAgencyAsync(AgencyProfile agency, CancellationToken cancellationToken = default);
    Task<AgencyProfile?> GetAgencyAsync(Guid agencyId, CancellationToken cancellationToken = default);
    Task<AgencyProfile?> GetAgencyByCodeAsync(string agencyCode, CancellationToken cancellationToken = default);
    Task AddAgencyCreditLedgerEntryAsync(AgencyCreditLedgerEntry entry, CancellationToken cancellationToken = default);

    Task AddCorporateAsync(CorporateProfile corporate, CancellationToken cancellationToken = default);
    Task UpdateCorporateAsync(CorporateProfile corporate, CancellationToken cancellationToken = default);
    Task<CorporateProfile?> GetCorporateAsync(Guid corporateId, CancellationToken cancellationToken = default);
    Task<CorporateProfile?> GetCorporateByCodeAsync(string corporateCode, CancellationToken cancellationToken = default);

    Task AddDepartmentAsync(CorporateDepartment department, CancellationToken cancellationToken = default);
    Task<CorporateDepartment?> GetDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default);
    Task<CorporateDepartment?> GetDepartmentByCodeAsync(Guid corporateId, string departmentCode, CancellationToken cancellationToken = default);

    Task AddEmployeeAsync(CorporateEmployee employee, CancellationToken cancellationToken = default);
    Task<CorporateEmployee?> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken = default);
    Task<CorporateEmployee?> GetEmployeeByNumberAsync(Guid corporateId, string employeeNumber, CancellationToken cancellationToken = default);

    Task AddCorporateBudgetAsync(CorporateBudget budget, CancellationToken cancellationToken = default);
    Task<CorporateBudget?> GetCorporateBudgetAsync(Guid budgetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CorporateBudget>> GetActiveBudgetsAsync(Guid corporateId, Guid? departmentId, Guid? employeeId, DateOnly businessDate, CancellationToken cancellationToken = default);

    Task AddCardStockBatchAsync(CardStockBatch batch, CancellationToken cancellationToken = default);
    Task UpdateCardStockBatchAsync(CardStockBatch batch, CancellationToken cancellationToken = default);
    Task<CardStockBatch?> GetCardStockBatchByReferenceAsync(string batchReference, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CardStockBatch>> GetCardStockBatchesAsync(Guid productId, CardOwnerType? ownerType, Guid? ownerId, CancellationToken cancellationToken = default);

    Task AddAdvancedLimitRuleAsync(AdvancedLimitRule rule, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdvancedLimitRule>> GetActiveAdvancedLimitRulesAsync(CancellationToken cancellationToken = default);

    Task AddRiskRuleAsync(BankSwitch.Domain.RiskRule rule, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankSwitch.Domain.RiskRule>> GetActiveRiskRulesAsync(CancellationToken cancellationToken = default);

    Task AddNotificationAsync(NotificationMessage notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationMessage>> GetPendingNotificationsAsync(int take, CancellationToken cancellationToken = default);
    Task UpdateNotificationAsync(NotificationMessage notification, CancellationToken cancellationToken = default);

    Task AddStatementAsync(StatementDocument statement, IReadOnlyCollection<StatementLine> lines, CancellationToken cancellationToken = default);
    Task<StatementDocument?> GetStatementAsync(Guid statementId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StatementLine>> GetStatementLinesAsync(Guid statementId, CancellationToken cancellationToken = default);
}

public interface INotificationDispatcher
{
    Task DispatchPendingAsync(int take = 100, CancellationToken cancellationToken = default);
}

public sealed record OnboardAgencyRequest(
    string AgencyCode,
    string Name,
    string? ParentAgencyCode,
    string ContactName,
    string MobileNumber,
    string Email,
    string CountryCode,
    AgencyCreditMode CreditMode,
    decimal CreditLimit,
    string CommissionProfileCode,
    string SettlementAccountNumber);

public sealed record AdjustAgencyCreditRequest(
    string AgencyCode,
    CreditLedgerDirection Direction,
    decimal Amount,
    string Reference,
    string Narrative,
    string CorrelationId);

public sealed record OnboardCorporateRequest(
    string CorporateCode,
    string Name,
    string RegistrationNumber,
    string ContactName,
    string MobileNumber,
    string Email,
    string CurrencyCode,
    decimal OpeningFundingBalance,
    string RiskRating);

public sealed record CreateCorporateDepartmentRequest(
    string CorporateCode,
    string DepartmentCode,
    string Name,
    string CostCenterCode);

public sealed record CreateCorporateEmployeeRequest(
    string CorporateCode,
    string? DepartmentCode,
    string EmployeeNumber,
    string FullName,
    string MobileNumber,
    string Email);

public sealed record CreateCorporateBudgetRequest(
    string CorporateCode,
    string? DepartmentCode,
    string? EmployeeNumber,
    string BudgetCode,
    string CurrencyCode,
    decimal BudgetAmount,
    DateOnly PeriodStart,
    DateOnly PeriodEnd);

public sealed record AllocateCardStockRequest(
    string ProductCode,
    string BatchReference,
    CardOwnerType OwnerType,
    string? OwnerCode,
    int Quantity);

public sealed record BulkIssueCardItem(
    string CustomerNumber,
    string? EmployeeNumber,
    string? DepartmentCode,
    PrepaidCardKind CardKind);

public sealed record BulkIssueCardsRequest(
    string ProductCode,
    string? AgencyCode,
    string? CorporateCode,
    string? BatchReference,
    IReadOnlyCollection<BulkIssueCardItem> Cards,
    string CorrelationId);

public sealed record BulkIssueCardsResult(
    int RequestedCount,
    int SuccessCount,
    int FailureCount,
    IReadOnlyCollection<IssueCardResult> IssuedCards,
    IReadOnlyCollection<BulkIssueFailure> Failures);

public sealed record BulkIssueFailure(string CustomerNumber, string ResponseCode, string Message);

public sealed record CreateAdvancedLimitRuleRequest(
    string RuleCode,
    string Name,
    LimitScope Scope,
    Guid? ScopeId,
    string TransactionTypeCode,
    string ChannelCode,
    string CurrencyCode,
    LimitPeriod Period,
    decimal AmountLimit,
    int CountLimit,
    int Priority);

public sealed record CreateRiskRuleRequest(
    string RuleCode,
    string Name,
    RiskRuleType RuleType,
    string MatchValue,
    RiskRuleAction Action,
    string ResponseCode,
    decimal? AmountThreshold,
    int Priority,
    string AlertTemplateCode);

public sealed record QueueNotificationRequest(
    NotificationChannel Channel,
    string Recipient,
    string TemplateCode,
    string Subject,
    string PayloadJson,
    string Reference,
    string CorrelationId);

public sealed record GenerateStatementRequest(
    StatementOwnerType OwnerType,
    Guid OwnerId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string CurrencyCode);

public sealed record StatementResult(StatementDocument Statement, IReadOnlyCollection<StatementLine> Lines);

public sealed record RiskEvaluationContext(
    PrepaidCard Card,
    CustomerProfile Customer,
    CardProduct Product,
    WalletAccount Wallet,
    CmsAuthorizationRequest AuthorizationRequest);

public sealed record RiskELDecision(bool IsAllowed, string ResponseCode, string Message, RiskAction Action,BankSwitch.Domain.RiskRule? MatchedRule)
{
    public static RiskELDecision Allow() => new(true, "00", "Allowed", RiskAction.Approve, null);
    public static RiskELDecision Block(string responseCode, string message, RiskAction action, BankSwitch.Domain.RiskRule rule) => new(false, responseCode, message, action, rule);
}

public sealed record AdvancedLimitEvaluationContext(
    PrepaidCard Card,
    CustomerProfile Customer,
    CardProduct Product,
    WalletAccount Wallet,
    CmsAuthorizationRequest AuthorizationRequest,
    decimal Amount);
