using BankSwitch.Domain;
using System.Collections.Concurrent;

namespace BankSwitch.Application;

public sealed class InMemoryPosAcquiringProductionRepository : IPosAcquiringProductionRepository
{
    private readonly ConcurrentDictionary<string, MerchantProfile> _merchants = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, MdrRule> _mdrRules = new();
    private readonly ConcurrentBag<PosTerminalLifecycleRecord> _lifecycle = new();
    private readonly ConcurrentDictionary<Guid, PosCommandQueueItem> _commands = new();
    private readonly ConcurrentDictionary<Guid, OfflineContactlessTxn> _offline = new();
    private readonly ConcurrentBag<OfflineContactlessClearingBatch> _offlineBatches = new();
    private readonly ConcurrentDictionary<Guid, PosKeyCeremony> _ceremonies = new();
    private readonly ConcurrentBag<EmvCertificationEvidence> _emv = new();
    private readonly ConcurrentDictionary<Guid, MerchantSettlementPosting> _postings = new();

    public Task UpsertMerchantAsync(MerchantProfile merchant, CancellationToken ct = default) { _merchants[merchant.MerchantId] = merchant; return Task.CompletedTask; }
    public Task<MerchantProfile?> GetMerchantAsync(string merchantId, CancellationToken ct = default) { _merchants.TryGetValue(merchantId, out var merchant); return Task.FromResult(merchant); }
    public Task<IReadOnlyList<MerchantProfile>> GetMerchantsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<MerchantProfile>>(_merchants.Values.OrderBy(m => m.MerchantId).ToList());
    public Task UpsertMdrRuleAsync(MdrRule rule, CancellationToken ct = default) { _mdrRules[rule.Id] = rule; return Task.CompletedTask; }
    public Task<IReadOnlyList<MdrRule>> GetActiveMdrRulesAsync(string merchantId, string mcc, string scheme, SettlementNetwork network, string productCode, string currencyCode, PosTransactionFlowType flowType, DateOnly businessDate, CancellationToken ct = default)
    {
        static bool Match(string r, string v) => string.IsNullOrWhiteSpace(r) || r == "*" || string.Equals(r, v, StringComparison.OrdinalIgnoreCase);
        var result = _mdrRules.Values.Where(r => r.IsActive && r.Network == network && r.FlowType == flowType
            && r.EffectiveFrom <= businessDate && (r.EffectiveTo is null || r.EffectiveTo >= businessDate)
            && Match(r.MerchantId, merchantId) && Match(r.Mcc, mcc) && Match(r.Scheme, scheme)
            && Match(r.ProductCode, productCode) && Match(r.CurrencyCode, currencyCode)).ToList();
        return Task.FromResult<IReadOnlyList<MdrRule>>(result);
    }
    public Task AddTerminalLifecycleAsync(PosTerminalLifecycleRecord record, CancellationToken ct = default) { _lifecycle.Add(record); return Task.CompletedTask; }
    public Task AddCommandQueueItemAsync(PosCommandQueueItem item, CancellationToken ct = default) { _commands[item.Id] = item; return Task.CompletedTask; }
    public Task<PosCommandQueueItem?> GetCommandQueueItemAsync(Guid id, CancellationToken ct = default) { _commands.TryGetValue(id, out var item); return Task.FromResult(item); }
    public Task UpdateCommandQueueItemAsync(PosCommandQueueItem item, CancellationToken ct = default) { _commands[item.Id] = item; return Task.CompletedTask; }
    public Task<IReadOnlyList<PosCommandQueueItem>> GetPendingCommandsAsync(string? terminalId = null, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var values = _commands.Values.Where(c => (terminalId is null || string.Equals(c.TerminalId, terminalId, StringComparison.OrdinalIgnoreCase))
            && c.Status == PosDeviceCommandStatus.Queued && c.NotBefore <= now && c.ExpiresAt > now && c.AttemptCount < c.MaxAttempts).OrderBy(c => c.CreatedAt).ToList();
        return Task.FromResult<IReadOnlyList<PosCommandQueueItem>>(values);
    }
    public Task AddOfflineContactlessTxnAsync(OfflineContactlessTxn txn, CancellationToken ct = default) { _offline[txn.Id] = txn; return Task.CompletedTask; }
    public Task<IReadOnlyList<OfflineContactlessTxn>> GetOfflineContactlessTxnsAsync(string merchantId, DateOnly businessDate, string currencyCode, CancellationToken ct = default)
    {
        var start = new DateTimeOffset(businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var end = new DateTimeOffset(businessDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var result = _offline.Values.Where(t => string.Equals(t.MerchantId, merchantId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(t.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase)
            && t.TerminalApprovedAt >= start && t.TerminalApprovedAt < end).ToList();
        return Task.FromResult<IReadOnlyList<OfflineContactlessTxn>>(result);
    }
    public Task UpdateOfflineContactlessTxnAsync(OfflineContactlessTxn txn, CancellationToken ct = default) { _offline[txn.Id] = txn; return Task.CompletedTask; }
    public Task AddOfflineClearingBatchAsync(OfflineContactlessClearingBatch batch, CancellationToken ct = default) { _offlineBatches.Add(batch); return Task.CompletedTask; }
    public Task AddKeyCeremonyAsync(PosKeyCeremony ceremony, CancellationToken ct = default) { _ceremonies[ceremony.Id] = ceremony; return Task.CompletedTask; }
    public Task<PosKeyCeremony?> GetKeyCeremonyAsync(Guid id, CancellationToken ct = default) { _ceremonies.TryGetValue(id, out var value); return Task.FromResult(value); }
    public Task UpdateKeyCeremonyAsync(PosKeyCeremony ceremony, CancellationToken ct = default) { _ceremonies[ceremony.Id] = ceremony; return Task.CompletedTask; }
    public Task AddEmvEvidenceAsync(EmvCertificationEvidence evidence, CancellationToken ct = default) { _emv.Add(evidence); return Task.CompletedTask; }
    public Task<IReadOnlyList<EmvCertificationEvidence>> GetEmvEvidenceAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<EmvCertificationEvidence>>(_emv.OrderByDescending(e => e.CreatedAt).ToList());
    public Task AddSettlementPostingAsync(MerchantSettlementPosting posting, CancellationToken ct = default) { _postings[posting.Id] = posting; return Task.CompletedTask; }
    public Task<MerchantSettlementPosting?> GetSettlementPostingAsync(Guid id, CancellationToken ct = default) { _postings.TryGetValue(id, out var value); return Task.FromResult(value); }
    public Task UpdateSettlementPostingAsync(MerchantSettlementPosting posting, CancellationToken ct = default) { _postings[posting.Id] = posting; return Task.CompletedTask; }
}
