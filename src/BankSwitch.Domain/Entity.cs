namespace BankSwitch.Domain;

public abstract record Entity(Guid Id)
{
    protected Entity() : this(Guid.NewGuid()) { }
}

public sealed record SourceNode : Entity
{
    public string NodeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public NodeSecurityProfile Security { get; init; } = new();
    public NodeLimits Limits { get; init; } = new();
    public IReadOnlySet<string> PermittedMtis { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> PermittedChannels { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedBinRanges { get; init; } = new HashSet<string>();
    public string KeyProfile { get; init; } = string.Empty;
    public string SettlementProfile { get; init; } = string.Empty;
    public string InstitutionCode { get; init; } = string.Empty;
}

public sealed record SinkNode : Entity
{
    public string NodeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public bool IsActive { get; init; }
    public NodeSecurityProfile Security { get; init; } = new();
    public NodeLimits Limits { get; init; } = new();
    public IReadOnlySet<string> PermittedMtis { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> PermittedChannels { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedBinRanges { get; init; } = new HashSet<string>();
    public string KeyProfile { get; init; } = string.Empty;
    public string SettlementProfile { get; init; } = string.Empty;
    public string InstitutionCode { get; init; } = string.Empty;
}

public sealed record NodeSecurityProfile
{
    public bool RequireMtls { get; init; } = true;
    public bool RequirePrivateNetwork { get; init; } = true;
    public IReadOnlySet<string> AllowedCidrs { get; init; } = new HashSet<string>();
    public string CertificateThumbprint { get; init; } = string.Empty;
}

public sealed record NodeLimits
{
    public int TpsLimit { get; init; } = 50;
    public decimal DailyAmountLimit { get; init; } = 0;
    public int MaxMessageBytes { get; init; } = 4096;
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromSeconds(65);
}

public sealed record RouteDefinition : Entity
{
    /// <summary>
    /// Legacy/single-value BIN prefix. Retained for backward compatibility and treated as
    /// the first entry in <see cref="CardRangePrefixes"/> when the list is empty.
    /// </summary>
    public string BinPrefix { get; init; } = string.Empty;

    /// <summary>Primary sink selected when this route is the best criteria match.</summary>
    public Guid SinkNodeId { get; init; }

    /// <summary>
    /// Optional secondary sink used when the primary sink's circuit breaker is open.
    /// When null, a circuit-open primary causes an immediate decline (91).
    /// When set, the engine tries the fallback before declining.
    /// </summary>
    public Guid? FallbackSinkNodeId { get; init; }

    public bool IsActive { get; init; } = true;

    /// <summary>Higher priority wins when multiple routes have the same specificity.</summary>
    public int Priority { get; init; }

    /// <summary>ISO 3166 numeric or alpha country codes allowed for this route.</summary>
    public IReadOnlySet<string> CountryCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allowed MCCs from ISO 8583 field 18.</summary>
    public IReadOnlySet<string> MerchantCategoryCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allowed ISO 4217 currency codes, usually from field 49.</summary>
    public IReadOnlySet<string> CurrencyCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allowed terminal/device ids or device type codes.</summary>
    public IReadOnlySet<string> DeviceCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allowed interchange codes such as VISA, MASTERCARD, RUPAY, NFS, UPI.</summary>
    public IReadOnlySet<string> InterchangeCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allowed card ranges. Supports prefixes and inclusive ranges like 400000-400999.</summary>
    public IReadOnlySet<string> CardRangePrefixes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allowed institution codes; matches source node institution and ISO institution fields.</summary>
    public IReadOnlySet<string> InstitutionCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allowed product codes such as DEBIT, PREPAID, CORPORATE, INSTA.</summary>
    public IReadOnlySet<string> ProductCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allowed network ids, e.g. NFS, VISA, MC, RUPAY, ONUS, OFFUS.</summary>
    public IReadOnlySet<string> NetworkCodes { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Allowed account-number prefixes, ranges, or exact values.</summary>
    public IReadOnlySet<string> AccountRanges { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record RouteMatchCriteria
{
    public string Pan { get; init; } = string.Empty;
    public string BinPrefix { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string MerchantCategoryCode { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public string DeviceCode { get; init; } = string.Empty;
    public string InterchangeCode { get; init; } = string.Empty;
    public string InstitutionCode { get; init; } = string.Empty;
    public string ProductCode { get; init; } = string.Empty;
    public string NetworkCode { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string ChannelCode { get; init; } = string.Empty;
    public string ProcessingCode { get; init; } = string.Empty;
}

public sealed record Scheme : Entity
{
    public string Name { get; init; } = string.Empty;
    public Guid SourceNodeId { get; init; }
    public Guid RouteId { get; init; }
    public IReadOnlyCollection<TransactionPermission> Permissions { get; init; } = Array.Empty<TransactionPermission>();
    public bool IsActive { get; init; } = true;
}

public sealed record TransactionPermission(string TransactionTypeCode, string ChannelCode, Guid FeeId);

public sealed record TransactionType : Entity
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}

public sealed record Channel : Entity
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}

public sealed record Fee : Entity
{
    public string Name { get; init; } = string.Empty;
    public decimal FlatAmount { get; init; }
    public decimal PercentageOfTransaction { get; init; }
    public decimal Minimum { get; init; }
    public decimal Maximum { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record TransactionLog : Entity
{
    public string CorrelationId { get; init; } = string.Empty;
    public string Mti { get; init; } = string.Empty;
    public string SourceNodeId { get; init; } = string.Empty;
    public string SinkNodeId { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string PanToken { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string ResponseCode { get; init; } = string.Empty;
    public long LatencyMilliseconds { get; init; }
    public string RouteUsed { get; init; } = string.Empty;
    public string SchemeUsed { get; init; } = string.Empty;
    public string FeeApplied { get; init; } = string.Empty;
    public ReversalState ReversalState { get; init; } = ReversalState.None;
    public string MacValidationStatus { get; init; } = string.Empty;
    public TransactionLifecycleState LifecycleState { get; init; } = TransactionLifecycleState.Received;

    /// <summary>
    /// Settlement profile from the sink node used to route this transaction.
    /// Used by the clearing engine to group transactions into the correct
    /// clearing batch (e.g. VISA_NG, MASTERCARD_NG, VERVE_NIBSS, DEFAULT).
    /// </summary>
    public string SettlementProfile { get; init; } = string.Empty;

    /// <summary>True once the clearing engine has included this transaction in a clearing batch.</summary>
    public bool IsCleared { get; init; } = false;

    /// <summary>Id of the <see cref="ClearingBatch"/> this transaction was assigned to. Null until cleared.</summary>
    public Guid? ClearingBatchId { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum ReversalState { None, Pending, Sent, Accepted, Rejected, Failed, RetryScheduled, ManuallyResolved, Reversed }

public enum AdminRole { Viewer, Operations, CardOperations, AgencyManager, CorporateManager, RiskAnalyst, RiskManager, FinanceOfficer, ReconciliationOfficer, ConfigMaker, ConfigChecker, SecurityAdmin, Auditor, SuperAdmin }

public enum AuditLogType { Transaction, Security, AdminAudit, System, Reconciliation }
