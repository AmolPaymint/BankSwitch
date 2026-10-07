using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryCardFeeRepository : ICardFeeRepository
{
    private readonly ConcurrentDictionary<Guid, CardFeeRule> _rules = new();

    public Task<IReadOnlyList<CardFeeRule>> GetCardFeeRulesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CardFeeRule>>(_rules.Values
            .OrderBy(x => x.FeeType)
            .ThenBy(x => x.ScopeType)
            .ThenBy(x => x.ScopeValue, StringComparer.Ordinal)
            .ToList());

    public Task<CardFeeRule?> GetCardFeeRuleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _rules.TryGetValue(id, out var rule);
        return Task.FromResult(rule);
    }

    public Task AddCardFeeRuleAsync(CardFeeRule rule, CancellationToken cancellationToken = default)
    {
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    public Task UpdateCardFeeRuleAsync(CardFeeRule rule, CancellationToken cancellationToken = default)
    {
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }
}
