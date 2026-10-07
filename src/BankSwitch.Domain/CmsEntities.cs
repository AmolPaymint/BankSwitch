namespace BankSwitch.Domain;

public enum ProgramLifecycleStatus { Draft, Submitted, Approved, Active, Suspended, Closed }

public enum CardProductStatus { Draft, Active, Suspended, Closed }

//public enum KycTier{Tier1 = 1, Tier2 = 2, Tier3 = 3}
public enum KycTier{Tier0 = 0, Tier1 = 1, Tier2 = 2, Tier3 = 3}

public enum KycStatus { Pending, Verified, Rejected, Expired }

public enum CustomerLifecycleStatus { Pending, Active, Suspended, Closed }

public enum PrepaidCardKind { Physical, Virtual }

public enum PrepaidCardStatus { Created, Issued, Inactive, Active, TemporarilyBlocked, Lost, Stolen, Replaced, Expired, Closed }

public enum WalletStatus { Active, Frozen, Closed }

public enum LedgerEntryDirection { Debit, Credit }

public enum LedgerEntryType { Load, Purchase, Fee, Reversal, Refund, Adjustment, AuthorizationHold, AuthorizationRelease }

public sealed record PrepaidProgram : Entity
{
    public string ProgramCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    public bool Reloadable { get; init; } = true;
    public IReadOnlySet<string> AllowedChannels { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedTransactionTypes { get; init; } = new HashSet<string>();
    public ProgramLifecycleStatus Status { get; init; } = ProgramLifecycleStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; init; }
}

public sealed record CardProduct : Entity
{
    public Guid ProgramId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string CurrencyCode { get; init; } = string.Empty;
    /// <summary>ISO 3166-1 alpha-2 or alpha-3 code of the country in which the product is issued. Used for cross-border fraud detection.</summary>
    public string IssuingCountryCode { get; init; } = string.Empty;
    public PrepaidCardKind CardKind { get; init; } = PrepaidCardKind.Virtual;
    public bool Reloadable { get; init; } = true;
    public int ExpiryPeriodMonths { get; init; } = 36;
    public string BinPrefix { get; init; } = string.Empty;
    public Guid? DefaultFeeId { get; init; }
    public Guid? TopUpFeeId { get; init; }
    public Guid? PurchaseFeeId { get; init; }
    public Guid LimitProfileId { get; init; }
    public IReadOnlySet<string> AllowedChannels { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AllowedTransactionTypes { get; init; } = new HashSet<string>();
    public CardProductStatus Status { get; init; } = CardProductStatus.Draft;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record LimitProfile : Entity
{
    public Guid ProgramId { get; init; }
    public Guid? ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public KycTier KycTier { get; init; } = KycTier.Tier1;
    public decimal MaxBalance { get; init; }
    public decimal PerTransactionLimit { get; init; }
    public decimal DailyLoadLimit { get; init; }
    public decimal MonthlyLoadLimit { get; init; }
    public decimal DailySpendLimit { get; init; }
    public decimal MonthlySpendLimit { get; init; }
    public int DailyTransactionCountLimit { get; init; } = 0;
    public bool IsActive { get; init; } = true;
}

public sealed record CustomerProfile : Entity
{
    public string CustomerNumber { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string MobileNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public KycTier KycTier { get; init; } = KycTier.Tier1;
    public KycStatus KycStatus { get; init; } = KycStatus.Pending;
    public CustomerLifecycleStatus Status { get; init; } = CustomerLifecycleStatus.Pending;
    public string RiskRating { get; init; } = "LOW";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; init; }
    public string DateOfBirth { get; init; } = string.Empty;
    public string AddressLine1 { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string StateOrRegion { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
}

public sealed record WalletAccount : Entity
{
    public string AccountNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public Guid ProductId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal LedgerBalance { get; init; }
    public decimal AvailableBalance { get; init; }
    public decimal ReservedBalance { get; init; }
    public WalletStatus Status { get; init; } = WalletStatus.Active;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// SQL Server ROWVERSION for optimistic concurrency. Prevents double-spend when two
    /// concurrent authorizations race on the same wallet. Populated on every GetWalletAsync
    /// and checked on every UpdateWalletAsync — a stale write throws WalletConcurrencyException.
    /// Empty array for in-memory/test wallets (concurrency check skipped).
    /// </summary>
    public byte[] RowVersion { get; init; } = Array.Empty<byte>();
}

public sealed record PrepaidCard : Entity
{
    public Guid CustomerId { get; init; }
    public Guid ProductId { get; init; }
    public Guid WalletAccountId { get; init; }
    public string CardNumberToken { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public PrepaidCardKind CardKind { get; init; } = PrepaidCardKind.Virtual;
    public PrepaidCardStatus Status { get; init; } = PrepaidCardStatus.Created;
    public CardOwnerType OwnerType { get; init; } = CardOwnerType.Customer;
    public Guid? AgencyId { get; init; }
    public Guid? CorporateId { get; init; }
    public Guid? CorporateDepartmentId { get; init; }
    public Guid? CorporateEmployeeId { get; init; }
    public string InventoryBatchReference { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>AES-256-GCM encrypted CVV2 value generated by the HSM at card issuance. Never stored in clear text.</summary>
    public string Cvv2Token { get; init; } = string.Empty;

    /// <summary>
    /// AES-256-GCM encrypted PIN verification reference, set when the cardholder calls SetPinAsync.
    /// The cleartext PIN never leaves the HSM; this token is used only for subsequent VerifyPinAsync calls.
    /// Empty string until the cardholder first sets a PIN.
    /// </summary>
    public string PinToken { get; init; } = string.Empty;

    /// <summary>Reason the card was blocked (TemporarilyBlocked / Lost / Stolen states). Empty for non-blocked cards.</summary>
    public string BlockReason { get; init; } = string.Empty;

    /// <summary>When the card was blocked. Null for non-blocked cards.</summary>
    public DateTimeOffset? BlockedAt { get; init; }

    /// <summary>Id of the replacement card issued when this card was replaced or upgraded. Null if not replaced.</summary>
    public Guid? ReplacedByCardId { get; init; }

    public bool IsExpired(DateOnly businessDate)
    {
        var endOfExpiryMonth = new DateOnly(ExpiryYear, ExpiryMonth, DateTime.DaysInMonth(ExpiryYear, ExpiryMonth));
        return businessDate > endOfExpiryMonth;
    }
}

public sealed record LedgerEntry : Entity
{
    public Guid WalletAccountId { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public LedgerEntryType EntryType { get; init; }
    public LedgerEntryDirection Direction { get; init; }
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal BalanceAfter { get; init; }
    public string Reference { get; init; } = string.Empty;
    public string Narrative { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record CmsTransactionLog : Entity
{
    public string CorrelationId { get; init; } = string.Empty;
    public Guid? CardId { get; init; }
    public Guid? WalletAccountId { get; init; }
    public string MaskedPan { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public string TransactionTypeCode { get; init; } = string.Empty;
    public string ChannelCode { get; init; } = string.Empty;
    public string Stan { get; init; } = string.Empty;
    public string Rrn { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal FeeAmount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string ResponseCode { get; init; } = string.Empty;
    public string ResponseDescription { get; init; } = string.Empty;
    public string AuthorizationCode { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}