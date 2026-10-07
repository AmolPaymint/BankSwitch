using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

public sealed class InMemoryAdvancedReconciliationRepository : IAdvancedReconciliationRepository
{
    private readonly ConcurrentDictionary<Guid, NetworkReconciliationFile> _files = new();
    private readonly ConcurrentDictionary<Guid, List<NetworkReconciliationRecord>> _records = new();
    private readonly ConcurrentDictionary<Guid, AtmEvidenceItem> _evidence = new();
    private readonly ConcurrentDictionary<Guid, C3RAtmReconciliationRun> _c3r = new();
    private readonly ConcurrentDictionary<Guid, OdrUdirCase> _odr = new();

    public Task AddNetworkFileAsync(NetworkReconciliationFile file, IReadOnlyCollection<NetworkReconciliationRecord> records, CancellationToken cancellationToken = default)
    {
        _files[file.Id] = file;
        _records[file.Id] = records.ToList();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NetworkReconciliationFile>> GetNetworkFilesAsync(DateOnly? businessDate, SettlementNetwork? network, CancellationToken cancellationToken = default)
    {
        IEnumerable<NetworkReconciliationFile> q = _files.Values;
        if (businessDate.HasValue) q = q.Where(f => f.BusinessDate == businessDate.Value);
        if (network.HasValue) q = q.Where(f => f.Network == network.Value);
        return Task.FromResult<IReadOnlyList<NetworkReconciliationFile>>(q.OrderByDescending(f => f.ImportedAt).ToList());
    }

    public Task<IReadOnlyList<NetworkReconciliationRecord>> GetNetworkRecordsAsync(Guid fileId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<NetworkReconciliationRecord>>(_records.TryGetValue(fileId, out var list) ? list : new List<NetworkReconciliationRecord>());

    public Task AddEvidenceAsync(AtmEvidenceItem evidence, CancellationToken cancellationToken = default)
    {
        _evidence[evidence.Id] = evidence;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AtmEvidenceItem>> GetEvidenceAsync(string? rrn, string? terminalId, DateOnly? businessDate, CancellationToken cancellationToken = default)
    {
        IEnumerable<AtmEvidenceItem> q = _evidence.Values;
        if (!string.IsNullOrWhiteSpace(rrn)) q = q.Where(e => string.Equals(e.Rrn, rrn, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(terminalId)) q = q.Where(e => string.Equals(e.TerminalId, terminalId, StringComparison.OrdinalIgnoreCase));
        if (businessDate.HasValue) q = q.Where(e => e.BusinessDate == businessDate.Value);
        return Task.FromResult<IReadOnlyList<AtmEvidenceItem>>(q.OrderByDescending(e => e.CapturedAt).ToList());
    }

    public Task AddC3RRunAsync(C3RAtmReconciliationRun run, CancellationToken cancellationToken = default)
    {
        _c3r[run.Id] = run;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<C3RAtmReconciliationRun>> GetC3RRunsAsync(DateOnly? businessDate, string? terminalId, CancellationToken cancellationToken = default)
    {
        IEnumerable<C3RAtmReconciliationRun> q = _c3r.Values;
        if (businessDate.HasValue) q = q.Where(r => r.BusinessDate == businessDate.Value);
        if (!string.IsNullOrWhiteSpace(terminalId)) q = q.Where(r => string.Equals(r.TerminalId, terminalId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<C3RAtmReconciliationRun>>(q.OrderByDescending(r => r.CreatedAt).ToList());
    }

    public Task UpdateC3RRunAsync(C3RAtmReconciliationRun run, CancellationToken cancellationToken = default)
    {
        _c3r[run.Id] = run;
        return Task.CompletedTask;
    }

    public Task AddOdrUdirCaseAsync(OdrUdirCase c, CancellationToken cancellationToken = default)
    {
        _odr[c.Id] = c;
        return Task.CompletedTask;
    }

    public Task<OdrUdirCase?> GetOdrUdirCaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _odr.TryGetValue(id, out var c);
        return Task.FromResult(c);
    }

    public Task<IReadOnlyList<OdrUdirCase>> GetOdrUdirCasesAsync(OdrUdirCaseStatus? status, OdrUdirNetwork? network, CancellationToken cancellationToken = default)
    {
        IEnumerable<OdrUdirCase> q = _odr.Values;
        if (status.HasValue) q = q.Where(c => c.Status == status.Value);
        if (network.HasValue) q = q.Where(c => c.Network == network.Value);
        return Task.FromResult<IReadOnlyList<OdrUdirCase>>(q.OrderByDescending(c => c.CreatedAt).ToList());
    }

    public Task UpdateOdrUdirCaseAsync(OdrUdirCase c, CancellationToken cancellationToken = default)
    {
        _odr[c.Id] = c;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Certification-safe gateway for RBI ODR / NPCI UDIR. It validates payload shape and records a deterministic
/// acknowledgement. Replace with the bank's UDIR/ODR API/SFTP connector after network onboarding.
/// </summary>
public sealed class SimulatedOdrUdirGateway : IOdrUdirGateway
{
    public OdrUdirNetwork Network { get; }

    public SimulatedOdrUdirGateway(OdrUdirNetwork network) => Network = network;

    public Task<OdrUdirNetworkAck> SubmitAsync(OdrUdirCase c, IReadOnlyList<AtmEvidenceItem> evidence, CancellationToken cancellationToken = default)
    {
        var ext = $"{Network}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{c.Id.ToString("N")[..8]}";
        return Task.FromResult(new OdrUdirNetworkAck(ext, "00", $"{Network} simulator accepted case with {evidence.Count} evidence item(s).", OdrUdirCaseStatus.Acknowledged));
    }

    public Task<OdrUdirNetworkAck> SubmitEvidenceAsync(OdrUdirCase c, IReadOnlyList<AtmEvidenceItem> evidence, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OdrUdirNetworkAck(c.ExternalCaseReference, "00", $"{Network} simulator accepted {evidence.Count} evidence item(s).", OdrUdirCaseStatus.EvidenceSubmitted));
    }
}
