using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class AdvancedReconciliationService : IAdvancedReconciliationService
{
    private readonly IAdvancedReconciliationRepository _repo;
    private readonly IReadOnlyDictionary<NetworkReconciliationFormat, INetworkReconciliationFormatParser> _parsers;
    private readonly IReadOnlyDictionary<OdrUdirNetwork, IOdrUdirGateway> _odrGateways;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public AdvancedReconciliationService(
        IAdvancedReconciliationRepository repo,
        IEnumerable<INetworkReconciliationFormatParser> parsers,
        IEnumerable<IOdrUdirGateway> odrGateways,
        IAuditLogger audit,
        IClock clock)
    {
        _repo = repo;
        _parsers = parsers.ToDictionary(p => p.Format);
        _odrGateways = odrGateways.ToDictionary(g => g.Network);
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<NetworkReconciliationFile>> ImportNetworkFileAsync(ImportNetworkReconciliationFileRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (!_parsers.TryGetValue(request.Format, out var parser))
            return CmsOperationResult<NetworkReconciliationFile>.Fail("30", $"No parser configured for {request.Format}.");

        var fileId = Guid.NewGuid();
        var records = await parser.ParseAsync(fileId, request.Network, request.BusinessDate, request.FileContent, cancellationToken).ConfigureAwait(false);
        var bytes = Encoding.UTF8.GetBytes(request.FileContent ?? string.Empty);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var debits = records.Where(r => r.TransactionAmount >= 0).Sum(r => r.TransactionAmount);
        var credits = records.Where(r => r.TransactionAmount < 0).Sum(r => Math.Abs(r.TransactionAmount));

        var file = new NetworkReconciliationFile
        {
            Id = fileId,
            Format = request.Format,
            Network = request.Network,
            BusinessDate = request.BusinessDate,
            FileName = request.FileName,
            SourceChannel = request.SourceChannel,
            FileHashSha256 = hash,
            RecordCount = records.Count,
            TotalDebitAmount = debits,
            TotalCreditAmount = credits,
            ImportedAt = _clock.UtcNow,
            ImportedBy = actor,
            ValidationSummary = records.Count == 0 ? "No transaction records found." : $"Imported {records.Count} normalized network records."
        };

        await _repo.AddNetworkFileAsync(file, records, cancellationToken).ConfigureAwait(false);
        _audit.LogSystem("NETWORK_RECON_IMPORT", $"{request.Network}/{request.Format} file {request.FileName} imported by {actor}; records={records.Count}; sha256={hash}");
        return CmsOperationResult<NetworkReconciliationFile>.Success(file, "Network reconciliation file imported.");
    }

    public Task<IReadOnlyList<NetworkReconciliationFile>> GetNetworkFilesAsync(DateOnly? businessDate, SettlementNetwork? network, CancellationToken cancellationToken = default)
        => _repo.GetNetworkFilesAsync(businessDate, network, cancellationToken);

    public Task<IReadOnlyList<NetworkReconciliationRecord>> GetNetworkRecordsAsync(Guid fileId, CancellationToken cancellationToken = default)
        => _repo.GetNetworkRecordsAsync(fileId, cancellationToken);

    public async Task<CmsOperationResult<AtmEvidenceItem>> AttachAtmEvidenceAsync(AttachAtmEvidenceRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Rrn) && string.IsNullOrWhiteSpace(request.Stan))
            return CmsOperationResult<AtmEvidenceItem>.Fail("12", "RRN or STAN is required for ATM evidence.");
        if (string.IsNullOrWhiteSpace(request.TerminalId))
            return CmsOperationResult<AtmEvidenceItem>.Fail("12", "TerminalId is required.");

        byte[] content;
        try { content = Convert.FromBase64String(request.Base64Content ?? string.Empty); }
        catch (FormatException) { return CmsOperationResult<AtmEvidenceItem>.Fail("30", "Base64Content is invalid."); }

        var hash = Convert.ToHexString(SHA256.HashData(content));
        var safeName = string.Join('_', (request.FileName ?? "evidence.bin").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var root = Path.Combine(AppContext.BaseDirectory, "atm-evidence", request.BusinessDate.ToString("yyyyMMdd"), request.TerminalId);
        Directory.CreateDirectory(root);
        var storagePath = Path.Combine(root, $"{Guid.NewGuid():N}-{safeName}");
        await File.WriteAllBytesAsync(storagePath, content, cancellationToken).ConfigureAwait(false);

        var item = new AtmEvidenceItem
        {
            Rrn = request.Rrn,
            Stan = request.Stan,
            TerminalId = request.TerminalId,
            BusinessDate = request.BusinessDate,
            EvidenceType = request.EvidenceType.ToUpperInvariant(),
            FileName = safeName,
            StorageUri = storagePath,
            HashSha256 = hash,
            ExtractedText = request.ExtractedText,
            CapturedAt = _clock.UtcNow,
            CapturedBy = request.CapturedBy
        };

        await _repo.AddEvidenceAsync(item, cancellationToken).ConfigureAwait(false);
        _audit.LogSystem("ATM_EVIDENCE", $"{item.EvidenceType} evidence attached for terminal={item.TerminalId} rrn={item.Rrn}; sha256={item.HashSha256}");
        return CmsOperationResult<AtmEvidenceItem>.Success(item, "ATM EJ/CCTV/pinhole evidence attached.");
    }

    public Task<IReadOnlyList<AtmEvidenceItem>> GetAtmEvidenceAsync(string? rrn, string? terminalId, DateOnly? businessDate, CancellationToken cancellationToken = default)
        => _repo.GetEvidenceAsync(rrn, terminalId, businessDate, cancellationToken);

    public async Task<CmsOperationResult<C3RAtmReconciliationRun>> CreateC3RRunAsync(CreateC3RRunRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var expectedClosing = request.OpeningBalance + request.LoadAmount + request.DepositedAmount - request.DispensedAmount - request.CashBroughtBackAmount;
        var variance = request.PhysicalClosingBalance - expectedClosing;
        var status = variance == 0 ? C3RStatus.Balanced : variance < 0 ? C3RStatus.Shortage : C3RStatus.Excess;
        var run = new C3RAtmReconciliationRun
        {
            RunReference = $"C3R-{request.BusinessDate:yyyyMMdd}-{request.TerminalId}-{Guid.NewGuid().ToString("N")[..8]}",
            TerminalId = request.TerminalId,
            BusinessDate = request.BusinessDate,
            OpeningBalance = request.OpeningBalance,
            LoadAmount = request.LoadAmount,
            DispensedAmount = request.DispensedAmount,
            DepositedAmount = request.DepositedAmount,
            CashBroughtBackAmount = request.CashBroughtBackAmount,
            SwitchExpectedClosingBalance = expectedClosing,
            PhysicalClosingBalance = request.PhysicalClosingBalance,
            ShortageAmount = variance < 0 ? Math.Abs(variance) : 0m,
            ExcessAmount = variance > 0 ? variance : 0m,
            Status = status,
            EvidenceIds = request.EvidenceIds ?? Array.Empty<Guid>(),
            CreatedAt = _clock.UtcNow,
            CreatedBy = actor,
            ApprovalNotes = variance == 0 ? "C3R balanced." : $"Variance detected: {variance}. Evidence and maker-checker approval required."
        };
        await _repo.AddC3RRunAsync(run, cancellationToken).ConfigureAwait(false);
        _audit.LogSystem("C3R_RECON", $"C3R run {run.RunReference} created for {run.TerminalId}; status={run.Status}; shortage={run.ShortageAmount}; excess={run.ExcessAmount}");
        return CmsOperationResult<C3RAtmReconciliationRun>.Success(run, "C3R ATM cash reconciliation completed.");
    }

    public Task<IReadOnlyList<C3RAtmReconciliationRun>> GetC3RRunsAsync(DateOnly? businessDate, string? terminalId, CancellationToken cancellationToken = default)
        => _repo.GetC3RRunsAsync(businessDate, terminalId, cancellationToken);

    public async Task<CmsOperationResult<OdrUdirCase>> SubmitOdrUdirCaseAsync(SubmitOdrUdirCaseRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (!_odrGateways.TryGetValue(request.Network, out var gateway))
            return CmsOperationResult<OdrUdirCase>.Fail("91", $"No {request.Network} gateway configured.");

        var c = new OdrUdirCase
        {
            Network = request.Network,
            LocalDisputeId = request.LocalDisputeId,
            ChargebackCaseId = request.ChargebackCaseId,
            CaseReference = $"{request.Network}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
            UdirTransactionId = request.UdirTransactionId,
            Rrn = request.Rrn,
            MaskedPan = request.MaskedPan,
            Amount = request.Amount,
            CurrencyCode = request.CurrencyCode,
            ComplaintCategory = request.ComplaintCategory,
            Status = OdrUdirCaseStatus.Submitted,
            CreatedAt = _clock.UtcNow,
            SubmittedAt = _clock.UtcNow,
            CreatedBy = actor
        };
        var evidence = await _repo.GetEvidenceAsync(request.Rrn, null, null, cancellationToken).ConfigureAwait(false);
        var ack = await gateway.SubmitAsync(c, evidence, cancellationToken).ConfigureAwait(false);
        c = c with
        {
            ExternalCaseReference = ack.ExternalCaseReference,
            LastNetworkResponseCode = ack.ResponseCode,
            LastNetworkResponseMessage = ack.ResponseMessage,
            Status = ack.Status,
            UpdatedAt = _clock.UtcNow
        };
        await _repo.AddOdrUdirCaseAsync(c, cancellationToken).ConfigureAwait(false);
        _audit.LogSystem("ODR_UDIR_SUBMIT", $"{c.Network} case {c.CaseReference} submitted; external={c.ExternalCaseReference}; status={c.Status}");
        return CmsOperationResult<OdrUdirCase>.Success(c, "ODR/UDIR case submitted.");
    }

    public async Task<CmsOperationResult<OdrUdirCase>> SubmitOdrUdirEvidenceAsync(Guid caseId, string actor, CancellationToken cancellationToken = default)
    {
        var c = await _repo.GetOdrUdirCaseAsync(caseId, cancellationToken).ConfigureAwait(false);
        if (c is null) return CmsOperationResult<OdrUdirCase>.Fail("25", "ODR/UDIR case not found.");
        if (!_odrGateways.TryGetValue(c.Network, out var gateway)) return CmsOperationResult<OdrUdirCase>.Fail("91", $"No {c.Network} gateway configured.");
        var evidence = await _repo.GetEvidenceAsync(c.Rrn, null, null, cancellationToken).ConfigureAwait(false);
        var ack = await gateway.SubmitEvidenceAsync(c, evidence, cancellationToken).ConfigureAwait(false);
        var updated = c with
        {
            LastNetworkResponseCode = ack.ResponseCode,
            LastNetworkResponseMessage = ack.ResponseMessage,
            Status = ack.Status,
            UpdatedAt = _clock.UtcNow
        };
        await _repo.UpdateOdrUdirCaseAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogSystem("ODR_UDIR_EVIDENCE", $"Evidence submitted for {updated.Network} case {updated.CaseReference}; files={evidence.Count}; status={updated.Status}; actor={actor}");
        return CmsOperationResult<OdrUdirCase>.Success(updated, "ODR/UDIR evidence submitted.");
    }

    public Task<IReadOnlyList<OdrUdirCase>> GetOdrUdirCasesAsync(OdrUdirCaseStatus? status, OdrUdirNetwork? network, CancellationToken cancellationToken = default)
        => _repo.GetOdrUdirCasesAsync(status, network, cancellationToken);
}

public abstract class DelimitedNetworkReconciliationParser : INetworkReconciliationFormatParser
{
    public abstract NetworkReconciliationFormat Format { get; }
    protected abstract char Separator { get; }

    public Task<IReadOnlyList<NetworkReconciliationRecord>> ParseAsync(Guid fileId, SettlementNetwork network, DateOnly businessDate, string fileContent, CancellationToken cancellationToken = default)
    {
        var records = new List<NetworkReconciliationRecord>();
        var lines = (fileContent ?? string.Empty).Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
            var cols = line.Split(Separator).Select(x => x.Trim().Trim('"')).ToArray();
            if (cols.Length < 8 || IsHeader(cols)) continue;
            records.Add(Map(fileId, network, businessDate, cols, line));
        }
        return Task.FromResult<IReadOnlyList<NetworkReconciliationRecord>>(records);
    }

    protected virtual bool IsHeader(string[] c) => c[0].Contains("RRN", StringComparison.OrdinalIgnoreCase) || c[0].Contains("REC", StringComparison.OrdinalIgnoreCase);

    protected virtual NetworkReconciliationRecord Map(Guid fileId, SettlementNetwork network, DateOnly businessDate, string[] c, string raw)
    {
        decimal ParseAmount(int idx) => idx < c.Length && decimal.TryParse(c[idx], out var v) ? v : 0m;
        string Get(int idx) => idx < c.Length ? c[idx] : string.Empty;
        return new NetworkReconciliationRecord
        {
            FileId = fileId,
            Network = network,
            Format = Format,
            BusinessDate = businessDate,
            RecordType = Get(0),
            Rrn = Get(1),
            Stan = Get(2),
            Arn = Get(3),
            NetworkReference = Get(4),
            MaskedPan = Get(5),
            TerminalId = Get(6),
            MerchantId = Get(7),
            MerchantCategoryCode = Get(8),
            TransactionCode = Get(9),
            TransactionAmount = ParseAmount(10),
            SettlementAmount = ParseAmount(11),
            InterchangeFee = ParseAmount(12),
            CurrencyCode = Get(13),
            ResponseCode = Get(14),
            RawLine = raw
        };
    }
}

public sealed class NpciNfsSettlementParser : DelimitedNetworkReconciliationParser { public override NetworkReconciliationFormat Format => NetworkReconciliationFormat.NpciNfsSettlementCsv; protected override char Separator => ','; }
public sealed class RupaySettlementParser : DelimitedNetworkReconciliationParser { public override NetworkReconciliationFormat Format => NetworkReconciliationFormat.RupaySettlementCsv; protected override char Separator => ','; }
public sealed class VisaSettlementParser : DelimitedNetworkReconciliationParser { public override NetworkReconciliationFormat Format => NetworkReconciliationFormat.VisaSettlementCsv; protected override char Separator => ','; }
public sealed class MastercardIpmParser : DelimitedNetworkReconciliationParser { public override NetworkReconciliationFormat Format => NetworkReconciliationFormat.MastercardIpmCsv; protected override char Separator => ','; }
public sealed class GenericIso8583SettlementParser : DelimitedNetworkReconciliationParser { public override NetworkReconciliationFormat Format => NetworkReconciliationFormat.GenericIso8583Csv; protected override char Separator => ','; }
