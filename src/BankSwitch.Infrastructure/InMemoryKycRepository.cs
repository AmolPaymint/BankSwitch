using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryKycRepository : IKycRepository
{
    private readonly ConcurrentDictionary<Guid, KycDocument> _documents = new();
    private readonly ConcurrentDictionary<Guid, AuthorizationHold> _holds = new();

    // ---------------------------------------------------------------
    // KYC Documents
    // ---------------------------------------------------------------

    public Task AddDocumentAsync(KycDocument document, CancellationToken cancellationToken = default)
    {
        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    public Task<KycDocument?> GetDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        _documents.TryGetValue(documentId, out var doc);
        return Task.FromResult(doc);
    }

    public Task<IReadOnlyList<KycDocument>> GetDocumentsForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var results = _documents.Values
            .Where(d => d.CustomerId == customerId)
            .OrderByDescending(d => d.SubmittedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<KycDocument>>(results);
    }

    public Task UpdateDocumentAsync(KycDocument document, CancellationToken cancellationToken = default)
    {
        _documents[document.Id] = document;
        return Task.CompletedTask;
    }

    // ---------------------------------------------------------------
    // Authorization Holds
    // ---------------------------------------------------------------

    public Task AddAuthHoldAsync(AuthorizationHold hold, CancellationToken cancellationToken = default)
    {
        _holds[hold.Id] = hold;
        return Task.CompletedTask;
    }

    public Task<AuthorizationHold?> GetAuthHoldAsync(Guid holdId, CancellationToken cancellationToken = default)
    {
        _holds.TryGetValue(holdId, out var hold);
        return Task.FromResult(hold);
    }

    public Task<AuthorizationHold?> GetAuthHoldByRrnAsync(string rrn, Guid walletId, CancellationToken cancellationToken = default)
    {
        var hold = _holds.Values.FirstOrDefault(h =>
            string.Equals(h.Rrn, rrn, StringComparison.Ordinal) &&
            h.WalletAccountId == walletId);
        return Task.FromResult(hold);
    }

    public Task<IReadOnlyList<AuthorizationHold>> GetActiveHoldsAsync(Guid walletId, CancellationToken cancellationToken = default)
    {
        var results = _holds.Values
            .Where(h => h.WalletAccountId == walletId && h.Status == AuthHoldStatus.Active)
            .OrderByDescending(h => h.PlacedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<AuthorizationHold>>(results);
    }

    public Task UpdateAuthHoldAsync(AuthorizationHold hold, CancellationToken cancellationToken = default)
    {
        _holds[hold.Id] = hold;
        return Task.CompletedTask;
    }

    public Task ReleaseExpiredHoldsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        foreach (var hold in _holds.Values.Where(h => h.Status == AuthHoldStatus.Active && h.ExpiresAt <= now))
        {
            _holds[hold.Id] = hold with { Status = AuthHoldStatus.Expired, ReleasedAt = now };
        }
        return Task.CompletedTask;
    }
}
