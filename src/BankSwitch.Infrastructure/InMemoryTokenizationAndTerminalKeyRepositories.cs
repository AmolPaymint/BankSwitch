using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryTokenizationRepository : ITokenizationRepository
{
    private readonly ConcurrentDictionary<string, CardToken> _tokens = new(StringComparer.Ordinal);

    public Task AddTokenAsync(CardToken token, CancellationToken cancellationToken = default)
    {
        _tokens[token.Token] = token;
        return Task.CompletedTask;
    }

    public Task<CardToken?> GetTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        _tokens.TryGetValue(token, out var record);
        return Task.FromResult(record);
    }

    public Task<CardToken?> GetActiveTokenByPanHashAsync(string panHash, string merchantId, CancellationToken cancellationToken = default)
    {
        var match = _tokens.Values.FirstOrDefault(x =>
            x.PanHash == panHash
            && string.Equals(x.MerchantId, merchantId, StringComparison.OrdinalIgnoreCase)
            && x.Status == CardTokenStatus.Active);
        return Task.FromResult(match);
    }

    public Task<IReadOnlyList<CardToken>> GetTokensAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CardToken>>(_tokens.Values.OrderByDescending(x => x.CreatedAt).ToList());

    public Task UpdateTokenAsync(CardToken token, CancellationToken cancellationToken = default)
    {
        _tokens[token.Token] = token;
        return Task.CompletedTask;
    }

    public Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(_tokens.ContainsKey(token));
}

public sealed class InMemoryTerminalKeyRepository : ITerminalKeyRepository
{
    private readonly ConcurrentDictionary<string, TerminalKeyProfile> _terminals = new(StringComparer.OrdinalIgnoreCase);

    public Task AddTerminalAsync(TerminalKeyProfile profile, CancellationToken cancellationToken = default)
    {
        _terminals[profile.TerminalId] = profile;
        return Task.CompletedTask;
    }

    public Task<TerminalKeyProfile?> GetTerminalAsync(string terminalId, CancellationToken cancellationToken = default)
    {
        _terminals.TryGetValue(terminalId, out var profile);
        return Task.FromResult(profile);
    }

    public Task<IReadOnlyList<TerminalKeyProfile>> GetTerminalsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TerminalKeyProfile>>(_terminals.Values.OrderBy(x => x.TerminalId, StringComparer.OrdinalIgnoreCase).ToList());

    public Task UpdateTerminalAsync(TerminalKeyProfile profile, CancellationToken cancellationToken = default)
    {
        _terminals[profile.TerminalId] = profile;
        return Task.CompletedTask;
    }
}
