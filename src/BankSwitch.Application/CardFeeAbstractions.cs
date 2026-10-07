using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>Persistence for card-lifecycle fee/waiver rules.</summary>
public interface ICardFeeRepository
{
    Task<IReadOnlyList<CardFeeRule>> GetCardFeeRulesAsync(CancellationToken cancellationToken = default);
    Task<CardFeeRule?> GetCardFeeRuleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddCardFeeRuleAsync(CardFeeRule rule, CancellationToken cancellationToken = default);
    Task UpdateCardFeeRuleAsync(CardFeeRule rule, CancellationToken cancellationToken = default);
}

/// <summary>
/// Maintains card-lifecycle fee and fee-waiver parameters - issuance, replacement, upgrade,
/// RePIN, annual maintenance, and add-on card - each configurable BIN-wise, account-scheme-wise,
/// or for an individual card, and resolves the applicable fee (or waiver) for a given card.
/// </summary>
public interface ICardFeeService
{
    Task<IReadOnlyList<CardFeeRule>> GetCardFeeRulesAsync(CancellationToken cancellationToken = default);
    Task<CardFeeRule?> GetCardFeeRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<CardFeeRule>> SaveCardFeeRuleAsync(Guid? id, CardFeeRuleInput input, string actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the fee (or waiver) that applies for <paramref name="request"/>, preferring a
    /// card-specific rule, then an account-scheme rule, then the most specific matching BIN rule.
    /// </summary>
    Task<CardFeeResolution> ResolveCardFeeAsync(CardFeeResolutionRequest request, CancellationToken cancellationToken = default);
}

public sealed record CardFeeRuleInput(
    CardFeeType FeeType,
    CardFeeScopeType ScopeType,
    string ScopeValue,
    bool IsWaiver,
    Guid? FeeId,
    bool IsActive,
    string Description);

public sealed record CardFeeResolutionRequest(CardFeeType FeeType, string BinPrefix, string AccountSchemeCode, Guid? CardId);

/// <summary>The outcome of resolving a card-lifecycle fee: either waived, charged at <see cref="Amount"/>, or no rule matched (amount zero).</summary>
public sealed record CardFeeResolution(CardFeeType FeeType, bool IsWaived, decimal Amount, CardFeeRule? MatchedRule);
