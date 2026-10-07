namespace BankSwitch.Domain;

// ---------------------------------------------------------------
// Card-on-file tokenization (CoFT)
// ---------------------------------------------------------------

public enum CardTokenStatus { Active, Suspended, Deleted }

/// <summary>
/// A surrogate "token PAN" that stands in for a real card number for card-on-file (CNP) use,
/// e.g. recurring billing, installments, or merchant-initiated transactions. The real PAN is
/// held only as an encrypted reference (<see cref="PanToken"/>, via ISensitiveDataProtector)
/// plus a keyed hash for lookup; the token itself is what merchants store.
/// </summary>
public sealed record CardToken : Entity
{
    public string Token { get; init; } = string.Empty;
    public string MaskedPan { get; init; } = string.Empty;
    public string PanToken { get; init; } = string.Empty;
    public string PanHash { get; init; } = string.Empty;
    public int ExpiryMonth { get; init; }
    public int ExpiryYear { get; init; }
    public string MerchantId { get; init; } = string.Empty;
    public string SourceNodeId { get; init; } = string.Empty;
    public CardTokenStatus Status { get; init; } = CardTokenStatus.Active;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; init; }
}

// ---------------------------------------------------------------
// Terminal session key management
// ---------------------------------------------------------------

/// <summary>
/// Tracks the current key-derivation state for a registered POS/ATM terminal. The switch never
/// stores the working/session key itself - only the key-serial-number (KSN) used to derive it
/// and the resulting key-check-value (KCV), both of which are safe to display.
/// </summary>
public sealed record TerminalKeyProfile : Entity
{
    public string TerminalId { get; init; } = string.Empty;
    public string SourceNodeId { get; init; } = string.Empty;
    public string KeyProfile { get; init; } = string.Empty;
    public string KeySerialNumber { get; init; } = string.Empty;
    public string KeyCheckValue { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastRotatedAt { get; init; }
}

// ---------------------------------------------------------------
// Multi-institutional configuration
// ---------------------------------------------------------------

public enum InstitutionType { Acquirer, Issuer, Both }

/// <summary>
/// A participating institution (acquiring bank, issuing bank, or both). Source and sink nodes
/// can reference an institution by <see cref="Code"/> so routing, fees, and schemes can be
/// reasoned about and reported on per institution.
/// </summary>
public sealed record Institution : Entity
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public InstitutionType Type { get; init; } = InstitutionType.Both;
    public string CountryCode { get; init; } = string.Empty;
    public string DefaultCurrencyCode { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
}
