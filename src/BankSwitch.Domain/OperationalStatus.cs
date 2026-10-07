namespace BankSwitch.Domain;

public enum OperationalStatus { Draft, Active, Suspended, Closed }

public enum AgencyCreditMode
{
    Prefunded,
    PostpaidCredit
}

public enum CreditLedgerDirection
{
    Credit,
    Debit,
    Reserve,
    Release
}

public enum CardOwnerType { Issuer, Customer, Agency, Corporate, CorporateEmployee }

public enum CardStockStatus { Draft, Available, Reserved, Exhausted, Suspended, Closed }

public enum LimitPeriod
{
    PerTransaction,
    Daily,
    Weekly,
    Monthly
}

public enum LimitScope
{
    Program,
    Product,
    Customer,
    Card,
    Agency,
    Corporate,
    Department,
    Employee
}

public enum RiskRuleType { MerchantCategoryBlock, MerchantCountryBlock, ChannelBlock, CurrencyBlock, AmountThreshold, CustomerRiskRating, AgencyStatus, CorporateStatus }

public enum RiskAction { Approve, Alert, StepUp, Hold, Decline, BlockCard }
public enum RiskRuleAction {  Approve,Alert,Allow,Hold, StepUp, Review, Decline, BlockCard, BlockMerchant, AlertOnly }
public enum RiskRuleCategory { Velocity, Amount, GeoLocation, Mcc, Currency, Device, Merchant, Account, CardPresent, Contactless, Ecommerce, Aml, Sanctions, Manual }
public enum NotificationChannel{Sms,Email,Push,Webhook, Whatsapp, Ivrs}
public enum NotificationStatus{Queued,Pending,Sent,Failed,Suppressed}

public enum StatementOwnerType
{
    Customer,
    Card,
    Agency,
    Corporate,
    Department,
    Employee
}

public enum StatementStatus
{
    Generated,
    Published,
    Archived
}

public sealed record AgencyProfile : Entity
{
    public string AgencyCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid? ParentAgencyId { get; init; }
    public string ContactName { get; init; } = string.Empty;
    public string MobileNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public AgencyCreditMode CreditMode { get; init; } = AgencyCreditMode.Prefunded;
    public decimal CreditLimit { get; init; }
    public decimal AvailableCredit { get; init; }
    public decimal UsedCredit { get; init; }
    public decimal ReservedCredit { get; init; }
    public string CommissionProfileCode { get; init; } = string.Empty;
    public string SettlementAccountNumber { get; init; } = string.Empty;
    public OperationalStatus Status { get; init; } = OperationalStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AgencyCreditLedgerEntry : Entity
{
    public Guid AgencyId { get; init; }
    public CreditLedgerDirection Direction { get; init; }
    public decimal Amount { get; init; }
    public decimal AvailableCreditAfter { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Narrative { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CorporateProfile : Entity
{
    public string CorporateCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string RegistrationNumber { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;
    public string MobileNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public string RiskRating { get; init; } = "LOW";
    public decimal FundingBalance { get; init; }
    public decimal AvailableFundingBalance { get; init; }
    public OperationalStatus Status { get; init; } = OperationalStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CorporateDepartment : Entity
{
    public Guid CorporateId { get; init; }
    public string DepartmentCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CostCenterCode { get; init; } = string.Empty;
    public OperationalStatus Status { get; init; } = OperationalStatus.Active;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CorporateEmployee : Entity
{
    public Guid CorporateId { get; init; }
    public Guid? DepartmentId { get; init; }
    public string EmployeeNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string MobileNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public OperationalStatus Status { get; init; } = OperationalStatus.Active;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CorporateBudget : Entity
{
    public Guid CorporateId { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? EmployeeId { get; init; }
    public string BudgetCode { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BudgetAmount { get; init; }
    public decimal AvailableAmount { get; init; }
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public OperationalStatus Status { get; init; } = OperationalStatus.Active;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CardStockBatch : Entity
{
    public Guid ProductId { get; init; }
    public string BatchReference { get; init; } = string.Empty;
    public CardOwnerType OwnerType { get; init; } = CardOwnerType.Issuer;
    public Guid? OwnerId { get; init; }
    public int Quantity { get; init; }
    public int AvailableQuantity { get; init; }
    public int ReservedQuantity { get; init; }
    public int IssuedQuantity { get; init; }
    public CardStockStatus Status { get; init; } = CardStockStatus.Available;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AdvancedLimitRule : Entity
{
    public string RuleCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public LimitScope Scope { get; init; } = LimitScope.Product;
    public Guid? ScopeId { get; init; }
    public string TransactionTypeCode { get; init; } = string.Empty;
    public string ChannelCode { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public LimitPeriod Period { get; init; } = LimitPeriod.Daily;
    public decimal AmountLimit { get; init; }
    public int CountLimit { get; init; }
    public int Priority { get; init; } = 100;
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record RiskRule : Entity
{
    public  Guid RuleId ;
    public  string RuleCode { get; init; } = string.Empty;
    public  RiskRuleCategory Category { get; init; }
    public  string Description  { get; init; } = string.Empty;
    public  string Expression  { get; init; } = string.Empty;
    public  int Score  { get; init; } 
    public  RiskRuleAction Action  { get; init; } 
    public  bool Enabled { get; init; } 
    public  int Priority { get; init; } 
    public  DateTimeOffset UpdatedAt { get; init; } 
    public  string UpdatedBy { get; init; } 
    public  string AuditHash  { get; init; } 
    public  string ResponseCode  { get; init; } 
    public  string Name { get; init; } 
    public  RiskRuleType RuleType  { get; init; } 
    public  string MatchValue  { get; init; } 
    public  decimal? AmountThreshold  { get; init; } 
    public  string AlertTemplateCode { get; init; } 
    public  DateTimeOffset CreatedAt  { get; init; } 
    // public string RuleCode { get; init; } = string.Empty;
    // public string Name { get; init; } = string.Empty;
    // public RiskRuleType RuleType { get; init; }
    // public string MatchValue { get; init; } = string.Empty;
    // public RiskAction Action { get; init; } = RiskAction.Decline;
    // public string ResponseCode { get; init; } = "59";
    // public decimal? AmountThreshold { get; init; }
    // public int Priority { get; init; } = 100;
    // public bool IsActive { get; init; } = true;
    // public string AlertTemplateCode { get; init; } = "RISK_ALERT";
    // public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record NotificationMessage : Entity
{
    public NotificationChannel Channel { get; init; }
    public string Recipient { get; init; } = string.Empty;
    public string TemplateCode { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string CorrelationId { get; init; } = string.Empty;
    public NotificationStatus Status { get; init; } = NotificationStatus.Pending;
    public int Attempts { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; init; }
}

public sealed record StatementDocument : Entity
{
    public StatementOwnerType OwnerType { get; init; }
    public Guid OwnerId { get; init; }
    public string StatementNumber { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public DateOnly PeriodStart { get; init; }
    public DateOnly PeriodEnd { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal ClosingBalance { get; init; }
    public decimal DebitTotal { get; init; }
    public decimal CreditTotal { get; init; }
    public int TransactionCount { get; init; }
    public StatementStatus Status { get; init; } = StatementStatus.Generated;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record StatementLine : Entity
{
    public Guid StatementId { get; init; }
    public DateTimeOffset TransactionDate { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Narrative { get; init; } = string.Empty;
    public LedgerEntryDirection Direction { get; init; }
    public decimal Amount { get; init; }
    public decimal BalanceAfter { get; init; }
}
