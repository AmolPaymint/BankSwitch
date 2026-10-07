using System.Text.Json;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class CardFeeService : ICardFeeService
{
    private readonly ICardFeeRepository _repository;
    private readonly ISwitchConfigurationRepository _switchConfig;
    private readonly ICmsRepository _cms;
    private readonly FeeCalculator _feeCalculator;
    private readonly IAuditLogger _audit;

    private static readonly JsonSerializerOptions AuditJsonOptions = new() { WriteIndented = false };

    public CardFeeService(ICardFeeRepository repository, ISwitchConfigurationRepository switchConfig, ICmsRepository cms, FeeCalculator feeCalculator, IAuditLogger audit)
    {
        _repository = repository;
        _switchConfig = switchConfig;
        _cms = cms;
        _feeCalculator = feeCalculator;
        _audit = audit;
    }

    public Task<IReadOnlyList<CardFeeRule>> GetCardFeeRulesAsync(CancellationToken cancellationToken = default)
        => _repository.GetCardFeeRulesAsync(cancellationToken);

    public Task<CardFeeRule?> GetCardFeeRuleAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetCardFeeRuleByIdAsync(id, cancellationToken);

    public async Task<CmsOperationResult<CardFeeRule>> SaveCardFeeRuleAsync(Guid? id, CardFeeRuleInput input, string actor, CancellationToken cancellationToken = default)
    {
        var scopeResult = await NormalizeScopeValueAsync(input.ScopeType, input.ScopeValue, cancellationToken).ConfigureAwait(false);
        if (scopeResult.Error is not null) return CmsOperationResult<CardFeeRule>.Fail(scopeResult.Error.ResponseCode, scopeResult.Error.Message);
        var scopeValue = scopeResult.Value!;

        Guid? feeId = null;
        if (!input.IsWaiver)
        {
            if (input.FeeId is null || input.FeeId == Guid.Empty)
                return CmsOperationResult<CardFeeRule>.Fail("30", "A fee must be selected unless this rule is a waiver.");

            var fees = await _switchConfig.GetFeesAsync(cancellationToken).ConfigureAwait(false);
            if (!fees.Any(x => x.Id == input.FeeId.Value))
                return CmsOperationResult<CardFeeRule>.Fail("58", "Selected fee was not found.");

            feeId = input.FeeId.Value;
        }

        var rules = await _repository.GetCardFeeRulesAsync(cancellationToken).ConfigureAwait(false);
        if (input.IsActive && rules.Any(x => x.IsActive
                && x.FeeType == input.FeeType
                && x.ScopeType == input.ScopeType
                && string.Equals(x.ScopeValue, scopeValue, StringComparison.Ordinal)
                && (id is null || x.Id != id.Value)))
        {
            return CmsOperationResult<CardFeeRule>.Fail("94", $"An active {input.FeeType} rule already exists for {input.ScopeType} '{scopeValue}'.");
        }

        CardFeeRule? before = null;
        if (id.HasValue)
        {
            before = await _repository.GetCardFeeRuleByIdAsync(id.Value, cancellationToken).ConfigureAwait(false);
            if (before is null) return CmsOperationResult<CardFeeRule>.Fail("25", "Card fee rule was not found.");
        }

        var rule = new CardFeeRule
        {
            Id = id ?? Guid.NewGuid(),
            FeeType = input.FeeType,
            ScopeType = input.ScopeType,
            ScopeValue = scopeValue,
            IsWaiver = input.IsWaiver,
            FeeId = feeId,
            IsActive = input.IsActive,
            Description = (input.Description ?? string.Empty).Trim(),
            CreatedAt = before?.CreatedAt ?? DateTimeOffset.UtcNow
        };

        if (id.HasValue)
        {
            await _repository.UpdateCardFeeRuleAsync(rule, cancellationToken).ConfigureAwait(false);
            Audit(actor, "UpdateCardFeeRule", before, rule);
        }
        else
        {
            await _repository.AddCardFeeRuleAsync(rule, cancellationToken).ConfigureAwait(false);
            Audit(actor, "CreateCardFeeRule", null, rule);
        }

        return CmsOperationResult<CardFeeRule>.Success(rule);
    }

    public async Task<CardFeeResolution> ResolveCardFeeAsync(CardFeeResolutionRequest request, CancellationToken cancellationToken = default)
    {
        var rules = (await _repository.GetCardFeeRulesAsync(cancellationToken).ConfigureAwait(false))
            .Where(x => x.IsActive && x.FeeType == request.FeeType)
            .ToList();

        CardFeeRule? matched = null;

        if (request.CardId.HasValue)
        {
            var cardKey = request.CardId.Value.ToString();
            matched = rules.FirstOrDefault(x => x.ScopeType == CardFeeScopeType.Card && string.Equals(x.ScopeValue, cardKey, StringComparison.Ordinal));
        }

        if (matched is null && !string.IsNullOrWhiteSpace(request.AccountSchemeCode))
        {
            var scheme = request.AccountSchemeCode.Trim().ToUpperInvariant();
            matched = rules.FirstOrDefault(x => x.ScopeType == CardFeeScopeType.AccountScheme && string.Equals(x.ScopeValue, scheme, StringComparison.Ordinal));
        }

        if (matched is null && !string.IsNullOrWhiteSpace(request.BinPrefix))
        {
            matched = rules
                .Where(x => x.ScopeType == CardFeeScopeType.Bin && request.BinPrefix.StartsWith(x.ScopeValue, StringComparison.Ordinal))
                .OrderByDescending(x => x.ScopeValue.Length)
                .FirstOrDefault();
        }

        if (matched is null) return new CardFeeResolution(request.FeeType, false, 0m, null);
        if (matched.IsWaiver) return new CardFeeResolution(request.FeeType, true, 0m, matched);

        var fees = await _switchConfig.GetFeesAsync(cancellationToken).ConfigureAwait(false);
        var fee = fees.FirstOrDefault(x => x.Id == matched.FeeId);
        if (fee is null || !fee.IsActive) return new CardFeeResolution(request.FeeType, false, 0m, matched);

        var amount = _feeCalculator.Calculate(fee, 0m);
        return new CardFeeResolution(request.FeeType, false, amount, matched);
    }

    private async Task<(string? Value, CmsOperationResult<CardFeeRule>? Error)> NormalizeScopeValueAsync(CardFeeScopeType scopeType, string? scopeValue, CancellationToken cancellationToken)
    {
        var value = (scopeValue ?? string.Empty).Trim();
        switch (scopeType)
        {
            case CardFeeScopeType.Bin:
                if (value.Length is < 4 or > 8 || !value.All(char.IsDigit))
                    return (null, CmsOperationResult<CardFeeRule>.Fail("30", "BIN must be 4-8 numeric digits."));
                return (value, null);

            case CardFeeScopeType.AccountScheme:
                var scheme = value.ToUpperInvariant();
                if (scheme.Length is < 2 or > 20)
                    return (null, CmsOperationResult<CardFeeRule>.Fail("30", "Account scheme code must be 2-20 characters."));
                return (scheme, null);

            case CardFeeScopeType.Card:
                if (!Guid.TryParse(value, out var cardId))
                    return (null, CmsOperationResult<CardFeeRule>.Fail("30", "Card scope value must be a valid card ID."));
                var card = await _cms.GetCardAsync(cardId, cancellationToken).ConfigureAwait(false);
                if (card is null)
                    return (null, CmsOperationResult<CardFeeRule>.Fail("25", "Card was not found."));
                return (cardId.ToString(), null);

            default:
                return (null, CmsOperationResult<CardFeeRule>.Fail("30", "Unsupported scope type."));
        }
    }

    private void Audit(string actor, string action, CardFeeRule? before, CardFeeRule after)
    {
        var oldValue = before is null ? string.Empty : JsonSerializer.Serialize(before, AuditJsonOptions);
        var newValue = JsonSerializer.Serialize(after, AuditJsonOptions);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, action, oldValue, newValue, "Card fee/waiver configuration change", string.Empty);
    }
}
