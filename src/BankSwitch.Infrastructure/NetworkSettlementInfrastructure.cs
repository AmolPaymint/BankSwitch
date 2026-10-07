using System.Collections.Concurrent;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryInterchangeFeeRuleRepository : IInterchangeFeeRuleRepository
{
    private readonly ConcurrentDictionary<Guid, InterchangeFeeRule> _rules = new();

    public InMemoryInterchangeFeeRuleRepository()
    {
        SeedDefaults();
    }

    public Task<IReadOnlyList<InterchangeFeeRule>> GetActiveRulesAsync(SettlementNetwork network, DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var rules = _rules.Values
            .Where(r => r.Network == network && r.IsActive && r.EffectiveFrom <= businessDate && (!r.EffectiveTo.HasValue || r.EffectiveTo.Value >= businessDate))
            .OrderByDescending(r => r.Priority)
            .ToList();
        return Task.FromResult<IReadOnlyList<InterchangeFeeRule>>(rules);
    }

    public Task UpsertRuleAsync(InterchangeFeeRule rule, CancellationToken cancellationToken = default)
    {
        _rules[rule.Id] = rule;
        return Task.CompletedTask;
    }

    private void SeedDefaults()
    {
        var effective = new DateOnly(2020, 1, 1);
        Add("VISA-DEBIT-DOM-ATM", SettlementNetwork.Visa, "DEBIT", "ATM", "*", "IN", "356", "WITHDRAWAL", 0m, 0.45m, 0m, 25m, InterchangeDirection.AcquirerReceives, effective, 90);
        Add("VISA-DEBIT-POS", SettlementNetwork.Visa, "DEBIT", "POS", "*", "IN", "356", "PURCHASE", 0m, 0.35m, 0m, 20m, InterchangeDirection.IssuerReceives, effective, 80);
        Add("MC-DEBIT-DOM-ATM", SettlementNetwork.Mastercard, "DEBIT", "ATM", "*", "IN", "356", "WITHDRAWAL", 0m, 0.45m, 0m, 25m, InterchangeDirection.AcquirerReceives, effective, 90);
        Add("MC-DEBIT-POS", SettlementNetwork.Mastercard, "DEBIT", "POS", "*", "IN", "356", "PURCHASE", 0m, 0.35m, 0m, 20m, InterchangeDirection.IssuerReceives, effective, 80);
        Add("RUPAY-POS", SettlementNetwork.Rupay, "DEBIT", "POS", "*", "IN", "356", "PURCHASE", 0m, 0.25m, 0m, 15m, InterchangeDirection.IssuerReceives, effective, 80);
        Add("NPCI-NFS-ATM", SettlementNetwork.NpciNfs, "DEBIT", "ATM", "*", "IN", "356", "WITHDRAWAL", 0m, 0.40m, 0m, 20m, InterchangeDirection.AcquirerReceives, effective, 80);
        Add("DEFAULT-WAIVED", SettlementNetwork.Internal, "*", "*", "*", "*", "*", "*", 0m, 0m, 0m, 0m, InterchangeDirection.Waived, effective, 1);
    }

    private void Add(string code, SettlementNetwork network, string product, string channel, string mcc, string country, string currency, string txnType, decimal flat, decimal pct, decimal min, decimal max, InterchangeDirection direction, DateOnly effective, int priority)
    {
        var rule = new InterchangeFeeRule
        {
            RuleCode = code,
            Network = network,
            ProductCode = product,
            ChannelCode = channel,
            MerchantCategoryCode = mcc,
            CountryCode = country,
            CurrencyCode = currency,
            TransactionTypeCode = txnType,
            FlatFee = flat,
            PercentFee = pct,
            MinimumFee = min,
            MaximumFee = max,
            Direction = direction,
            EffectiveFrom = effective,
            Priority = priority,
            IsActive = true
        };
        _rules[rule.Id] = rule;
    }
}

public sealed class InMemoryNetworkSettlementRunRepository : INetworkSettlementRunRepository
{
    private readonly ConcurrentDictionary<Guid, NetworkSettlementRun> _runs = new();

    public Task AddRunAsync(NetworkSettlementRun run, CancellationToken cancellationToken = default)
    {
        _runs[run.Id] = run;
        return Task.CompletedTask;
    }

    public Task<NetworkSettlementRun?> GetRunByBatchAsync(Guid clearingBatchId, CancellationToken cancellationToken = default)
    {
        var run = _runs.Values.FirstOrDefault(r => r.ClearingBatchId == clearingBatchId);
        return Task.FromResult(run);
    }

    public Task UpdateRunAsync(NetworkSettlementRun run, CancellationToken cancellationToken = default)
    {
        _runs[run.Id] = run;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Certification-safe non-networking gateway used in development/UAT.
/// It records what would be submitted to Visa SFTP, Mastercard File Express, or NPCI/RuPay settlement channels.
/// Replace with concrete connector implementations when bank network credentials and scheme specs are available.
/// </summary>
public sealed class SimulatedNetworkSettlementGateway : INetworkSettlementGateway
{
    public SettlementNetwork Network { get; }

    public SimulatedNetworkSettlementGateway(SettlementNetwork network)
    {
        Network = network;
    }

    public Task<NetworkSettlementTransmissionResult> SubmitAsync(ClearingBatch batch, byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new NetworkSettlementTransmissionResult
        {
            AcceptedForDelivery = true,
            TransmissionReference = $"{Network}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{batch.Id.ToString("N")[..8]}",
            NetworkResponseCode = "SIM-ACCEPTED",
            NetworkResponseMessage = $"UAT simulator accepted {fileName} ({fileBytes.Length} bytes). Configure real scheme gateway for production transmission."
        });
    }
}
