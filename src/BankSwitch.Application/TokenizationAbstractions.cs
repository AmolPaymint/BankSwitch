using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>Persistence for card-on-file tokens.</summary>
public interface ITokenizationRepository
{
    Task AddTokenAsync(CardToken token, CancellationToken cancellationToken = default);
    Task<CardToken?> GetTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<CardToken?> GetActiveTokenByPanHashAsync(string panHash, string merchantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CardToken>> GetTokensAsync(CancellationToken cancellationToken = default);
    Task UpdateTokenAsync(CardToken token, CancellationToken cancellationToken = default);
    Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Card-on-file tokenization (CoFT): issues surrogate "token PANs" that merchants store instead
/// of real card numbers for recurring billing, installments, and other card-not-present
/// merchant-initiated transactions.
/// </summary>
public interface ITokenizationService
{
    Task<CmsOperationResult<CardToken>> TokenizeAsync(TokenizeCardRequest request, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<TokenDetails>> DetokenizeAsync(string token, string merchantId, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CardToken>> SuspendTokenAsync(string token, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CardToken>> ResumeTokenAsync(string token, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CardToken>> DeleteTokenAsync(string token, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CardToken>> GetTokensAsync(CancellationToken cancellationToken = default);
}

public sealed record TokenizationOptions
{
    /// <summary>Numeric prefix used for generated tokens, reserved so tokens are never mistaken for real PANs.</summary>
    public string TokenBinPrefix { get; init; } = "999999";
    /// <summary>Total length (digits, including the BIN prefix and Luhn check digit) of a generated token.</summary>
    public int TokenLength { get; init; } = 16;
}

public sealed record TokenizeCardRequest(
    string FullPan,
    int ExpiryMonth,
    int ExpiryYear,
    string MerchantId,
    string SourceNodeId,
    string CorrelationId);

/// <summary>Token details safe to return to a caller that has already proven it owns the token (merchant-scoped).</summary>
public sealed record TokenDetails(string Token, string MaskedPan, string PanToken, int ExpiryMonth, int ExpiryYear, CardTokenStatus Status);
