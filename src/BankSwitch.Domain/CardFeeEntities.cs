namespace BankSwitch.Domain;

/// <summary>Card-lifecycle fee categories, each independently configurable per scope.</summary>
public enum CardFeeType
{
    Issuance,
    Replacement,
    Upgrade,
    RePin,
    AnnualMaintenance,
    AddOnCard
}

/// <summary>The dimension a <see cref="CardFeeRule"/> applies to.</summary>
public enum CardFeeScopeType
{
    /// <summary><see cref="CardFeeRule.ScopeValue"/> is a numeric BIN prefix (4-8 digits).</summary>
    Bin,
    /// <summary><see cref="CardFeeRule.ScopeValue"/> is an account/card scheme (network) code, e.g. VISA, MASTERCARD, VERVE.</summary>
    AccountScheme,
    /// <summary><see cref="CardFeeRule.ScopeValue"/> is the <c>Id</c> of a specific <see cref="PrepaidCard"/>.</summary>
    Card
}

/// <summary>
/// A single fee-or-waiver rule for one <see cref="CardFeeType"/> at one scope (BIN, account
/// scheme, or individual card). When <see cref="IsWaiver"/> is true the fee is waived (zero)
/// for matching cards/transactions regardless of any BIN/scheme-level fee; otherwise
/// <see cref="FeeId"/> references the <see cref="Fee"/> (flat amount and/or percentage with
/// min/max bounds) charged for matching cards.
/// </summary>
public sealed record CardFeeRule : Entity
{
    public CardFeeType FeeType { get; init; }
    public CardFeeScopeType ScopeType { get; init; }
    public string ScopeValue { get; init; } = string.Empty;
    public bool IsWaiver { get; init; }
    public Guid? FeeId { get; init; }
    public bool IsActive { get; init; } = true;
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
