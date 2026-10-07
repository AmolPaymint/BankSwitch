using System.Security.Cryptography;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class PosAcquiringProductionService : IPosAcquiringProductionService
{
    private readonly IPosAcquiringProductionRepository _repo;
    private readonly IPosTerminalDrivingRepository _posRepo;
    private readonly IInterchangeFeeRuleEngine _interchange;
    private readonly IClock _clock;
    private readonly IAuditLogger _audit;

    public PosAcquiringProductionService(
        IPosAcquiringProductionRepository repo,
        IPosTerminalDrivingRepository posRepo,
        IInterchangeFeeRuleEngine interchange,
        IClock clock,
        IAuditLogger audit)
    {
        _repo = repo;
        _posRepo = posRepo;
        _interchange = interchange;
        _clock = clock;
        _audit = audit;
    }

    public async Task<CmsOperationResult<MerchantProfile>> OnboardMerchantAsync(OnboardMerchantRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.MerchantId)) return CmsOperationResult<MerchantProfile>.Fail("PAP01", "MerchantId is required.");
        if (string.IsNullOrWhiteSpace(request.LegalName)) return CmsOperationResult<MerchantProfile>.Fail("PAP02", "LegalName is required.");
        if (string.IsNullOrWhiteSpace(request.Mcc)) return CmsOperationResult<MerchantProfile>.Fail("PAP03", "MCC is required.");
        if (string.IsNullOrWhiteSpace(request.SettlementAccountNumberMasked)) return CmsOperationResult<MerchantProfile>.Fail("PAP04", "Settlement account is required.");

        var now = _clock.UtcNow;
        var existing = await _repo.GetMerchantAsync(request.MerchantId, ct).ConfigureAwait(false);
        var status = string.Equals(request.KycStatus, "VERIFIED", StringComparison.OrdinalIgnoreCase)
            ? MerchantOnboardingStatus.Active
            : MerchantOnboardingStatus.PendingKyc;
        var merchant = new MerchantProfile(
            Normalize(request.MerchantId),
            request.LegalName.Trim(),
            request.DisplayName.Trim(),
            request.Mcc.Trim(),
            request.PanOrTaxIdMasked.Trim(),
            request.KycStatus.Trim(),
            request.SettlementAccountNumberMasked.Trim(),
            request.SettlementIfsc.Trim(),
            request.SettlementCurrencyCode.Trim().ToUpperInvariant(),
            request.SettlementCycle,
            status,
            request.DefaultMdrPercent,
            request.DefaultMdrFlatFee,
            request.AllowCashAtPos,
            request.AllowOfflineContactless,
            existing?.CreatedAt ?? now,
            now,
            request.CorrelationId);
        await _repo.UpsertMerchantAsync(merchant, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "OnboardMerchant", existing?.Status.ToString() ?? "New", merchant.Status.ToString(), merchant.MerchantId, merchant.Mcc);
        return CmsOperationResult<MerchantProfile>.Success(merchant, "Merchant onboarded for POS acquiring.");
    }

    public Task<IReadOnlyList<MerchantProfile>> GetMerchantsAsync(CancellationToken ct = default) => _repo.GetMerchantsAsync(ct);

    public async Task<CmsOperationResult<MdrRule>> UpsertMdrRuleAsync(UpsertMdrRuleRequest request, string actor, CancellationToken ct = default)
    {
        if (request.PercentFee < 0 || request.FlatFee < 0) return CmsOperationResult<MdrRule>.Fail("PAP10", "MDR values cannot be negative.");
        var rule = new MdrRule(request.Id ?? Guid.NewGuid(), Normalize(request.MerchantId), request.Mcc.Trim(), request.Scheme.Trim().ToUpperInvariant(), request.Network,
            string.IsNullOrWhiteSpace(request.ProductCode) ? "*" : request.ProductCode.Trim().ToUpperInvariant(), request.CurrencyCode.Trim().ToUpperInvariant(), request.FlowType,
            request.PercentFee, request.FlatFee, request.MinimumFee, request.MaximumFee, request.GstPercent, request.EffectiveFrom, request.EffectiveTo, request.IsActive, request.Priority, _clock.UtcNow);
        await _repo.UpsertMdrRuleAsync(rule, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "UpsertMdrRule", string.Empty, rule.IsActive ? "Active" : "Inactive", rule.MerchantId, rule.Scheme);
        return CmsOperationResult<MdrRule>.Success(rule, "MDR rule saved.");
    }

    public async Task<CmsOperationResult<PosTerminalLifecycleRecord>> UpdateTerminalLifecycleAsync(UpdatePosTerminalLifecycleRequest request, string actor, CancellationToken ct = default)
    {
        var terminal = await _posRepo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<PosTerminalLifecycleRecord>.Fail("PAP20", "Terminal is not registered.");
        var record = new PosTerminalLifecycleRecord(Guid.NewGuid(), terminal.TerminalId, terminal.MerchantId, request.Status, terminal.Status.ToString(), request.ReasonCode, request.Remarks, actor, _clock.UtcNow, request.CorrelationId);
        await _repo.AddTerminalLifecycleAsync(record, ct).ConfigureAwait(false);

        var newDeviceStatus = request.Status switch
        {
            PosTerminalLifecycleStatus.Active => PosDeviceStatus.Active,
            PosTerminalLifecycleStatus.Suspended => PosDeviceStatus.Suspended,
            PosTerminalLifecycleStatus.Decommissioned => PosDeviceStatus.Retired,
            PosTerminalLifecycleStatus.Faulted => PosDeviceStatus.Offline,
            _ => terminal.Status
        };
        var updated = terminal with { Status = newDeviceStatus, UpdatedAt = _clock.UtcNow };
        await _posRepo.UpsertTerminalAsync(updated, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "UpdatePosTerminalLifecycle", record.PreviousStatus, record.Status.ToString(), terminal.TerminalId, request.ReasonCode);
        return CmsOperationResult<PosTerminalLifecycleRecord>.Success(record, "Terminal lifecycle updated.");
    }

    public async Task<CmsOperationResult<PosCommandQueueItem>> QueueDeviceCommandAsync(QueuePosCommandRequest request, string actor, CancellationToken ct = default)
    {
        var terminal = await _posRepo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<PosCommandQueueItem>.Fail("PAP30", "Terminal is not registered.");
        var now = _clock.UtcNow;
        var item = new PosCommandQueueItem(Guid.NewGuid(), terminal.TerminalId, request.Command.Trim(), request.Parameters ?? new Dictionary<string, string>(), PosDeviceCommandStatus.Queued, 0,
            request.MaxAttempts <= 0 ? 3 : request.MaxAttempts, now.AddSeconds(Math.Max(0, request.NotBeforeSeconds)), now.AddSeconds(request.TimeToLiveSeconds <= 0 ? 3600 : request.TimeToLiveSeconds), now, null, null, string.Empty, request.CorrelationId);
        await _repo.AddCommandQueueItemAsync(item, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "QueuePosCommand", string.Empty, item.Status.ToString(), item.TerminalId, item.Command);
        return CmsOperationResult<PosCommandQueueItem>.Success(item, "POS command queued for delivery.");
    }

    public async Task<CmsOperationResult<PosCommandQueueItem>> UpdateDeviceCommandStatusAsync(UpdatePosCommandStatusRequest request, string actor, CancellationToken ct = default)
    {
        var existing = await _repo.GetCommandQueueItemAsync(request.CommandId, ct).ConfigureAwait(false);
        if (existing is null) return CmsOperationResult<PosCommandQueueItem>.Fail("PAP31", "Command not found.");
        var now = _clock.UtcNow;
        var updated = existing with
        {
            Status = request.Status,
            AttemptCount = request.Status == PosDeviceCommandStatus.Dispatched ? existing.AttemptCount + 1 : existing.AttemptCount,
            DispatchedAt = request.Status == PosDeviceCommandStatus.Dispatched ? now : existing.DispatchedAt,
            AcknowledgedAt = request.Status == PosDeviceCommandStatus.Acknowledged ? now : existing.AcknowledgedAt,
            LastError = request.LastError ?? string.Empty
        };
        await _repo.UpdateCommandQueueItemAsync(updated, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "UpdatePosCommandStatus", existing.Status.ToString(), updated.Status.ToString(), updated.TerminalId, updated.Command);
        return CmsOperationResult<PosCommandQueueItem>.Success(updated, "POS command status updated.");
    }

    public Task<IReadOnlyList<PosCommandQueueItem>> GetPendingCommandsAsync(string? terminalId = null, CancellationToken ct = default) => _repo.GetPendingCommandsAsync(terminalId, ct);

    public async Task<CmsOperationResult<OfflineContactlessTxn>> CaptureOfflineContactlessAsync(CaptureOfflineContactlessRequest request, string actor, CancellationToken ct = default)
    {
        var merchant = await _repo.GetMerchantAsync(request.MerchantId, ct).ConfigureAwait(false);
        if (merchant is null) return CmsOperationResult<OfflineContactlessTxn>.Fail("PAP40", "Merchant is not onboarded.");
        if (!merchant.AllowOfflineContactless) return CmsOperationResult<OfflineContactlessTxn>.Fail("PAP41", "Merchant is not enabled for offline contactless.");
        var terminal = await _posRepo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        if (terminal is null || !terminal.ContactlessEnabled) return CmsOperationResult<OfflineContactlessTxn>.Fail("PAP42", "Terminal is not contactless-enabled.");
        var deadline = request.TerminalApprovedAt.AddHours(request.ClearingWindowHours <= 0 ? 24 : request.ClearingWindowHours);
        var status = _clock.UtcNow <= deadline ? OfflineContactlessClearingStatus.RiskChecked : OfflineContactlessClearingStatus.Expired;
        var decision = status == OfflineContactlessClearingStatus.RiskChecked ? "APPROVE_FOR_DEFERRED_CLEARING" : "EXPIRED_CLEARING_WINDOW";
        var txn = new OfflineContactlessTxn(Guid.NewGuid(), terminal.TerminalId, merchant.MerchantId, request.TransactionId.Trim(), request.PanMasked.Trim(), request.Amount, request.CurrencyCode.Trim().ToUpperInvariant(), request.EmvCryptogram.Trim(), request.TerminalApprovedAt, deadline, status == OfflineContactlessClearingStatus.RiskChecked ? OfflineContactlessClearingStatus.ReadyForClearing : status, decision, string.Empty, request.CorrelationId);
        await _repo.AddOfflineContactlessTxnAsync(txn, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "CaptureOfflineContactless", string.Empty, txn.Status.ToString(), txn.TerminalId, txn.TransactionId);
        return CmsOperationResult<OfflineContactlessTxn>.Success(txn, "Offline contactless transaction captured.");
    }

    public async Task<CmsOperationResult<OfflineContactlessClearingBatch>> CreateOfflineContactlessClearingBatchAsync(CreateOfflineContactlessClearingBatchRequest request, string actor, CancellationToken ct = default)
    {
        var txns = await _repo.GetOfflineContactlessTxnsAsync(request.MerchantId, request.BusinessDate, request.CurrencyCode, ct).ConfigureAwait(false);
        var eligible = txns.Where(t => t.Status == OfflineContactlessClearingStatus.ReadyForClearing).ToList();
        if (eligible.Count == 0) return CmsOperationResult<OfflineContactlessClearingBatch>.Fail("PAP43", "No offline contactless transactions are ready for clearing.");
        var payload = string.Join('|', eligible.OrderBy(t => t.TransactionId).Select(t => $"{t.TransactionId}:{t.Amount:F2}:{t.EmvCryptogram}"));
        var batch = new OfflineContactlessClearingBatch(Guid.NewGuid(), Normalize(request.MerchantId), request.BusinessDate, request.CurrencyCode.Trim().ToUpperInvariant(), eligible.Count, eligible.Sum(t => t.Amount), OfflineContactlessClearingStatus.Cleared, Sha256Text(payload), _clock.UtcNow, request.CorrelationId);
        await _repo.AddOfflineClearingBatchAsync(batch, ct).ConfigureAwait(false);
        foreach (var txn in eligible)
            await _repo.UpdateOfflineContactlessTxnAsync(txn with { Status = OfflineContactlessClearingStatus.Cleared, ClearingReference = batch.Id.ToString("N") }, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "CreateOfflineContactlessClearingBatch", string.Empty, batch.Status.ToString(), batch.MerchantId, batch.Id.ToString("N"));
        return CmsOperationResult<OfflineContactlessClearingBatch>.Success(batch, "Offline contactless clearing batch created.");
    }

    public async Task<CmsOperationResult<PosKeyCeremony>> StartKeyCeremonyAsync(StartPosKeyCeremonyRequest request, string actor, CancellationToken ct = default)
    {
        var terminal = await _posRepo.GetTerminalAsync(request.TerminalId, ct).ConfigureAwait(false);
        if (terminal is null) return CmsOperationResult<PosKeyCeremony>.Fail("PAP50", "Terminal is not registered.");
        var seed = $"{terminal.TerminalId}|{terminal.MerchantId}|{request.Scheme}|{request.KeyScheme}|{request.EvidencePayload}|{_clock.UtcNow:O}";
        var ceremony = new PosKeyCeremony(Guid.NewGuid(), terminal.TerminalId, terminal.MerchantId, request.CeremonyType, KeyCeremonyStatus.MakerSubmitted,
            request.Scheme.Trim().ToUpperInvariant(), request.KeyScheme.Trim().ToUpperInvariant(), Kcv(seed + "ZMK"), Kcv(seed + "TMK"), Kcv(seed + "TPK"), Kcv(seed + "TAK"), actor, string.Empty, Sha256Text(request.EvidencePayload), _clock.UtcNow, null, null, request.CorrelationId);
        await _repo.AddKeyCeremonyAsync(ceremony, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "StartPosKeyCeremony", string.Empty, ceremony.Status.ToString(), ceremony.TerminalId, ceremony.Scheme);
        return CmsOperationResult<PosKeyCeremony>.Success(ceremony, "POS key ceremony submitted for checker approval.");
    }

    public async Task<CmsOperationResult<PosKeyCeremony>> ApproveKeyCeremonyAsync(ApprovePosKeyCeremonyRequest request, string actor, CancellationToken ct = default)
    {
        var existing = await _repo.GetKeyCeremonyAsync(request.CeremonyId, ct).ConfigureAwait(false);
        if (existing is null) return CmsOperationResult<PosKeyCeremony>.Fail("PAP51", "Key ceremony not found.");
        var now = _clock.UtcNow;
        var updated = request.Approve
            ? existing with { Status = KeyCeremonyStatus.Completed, CheckerUser = actor, ApprovedAt = now, CompletedAt = now }
            : existing with { Status = KeyCeremonyStatus.Rejected, CheckerUser = actor, ApprovedAt = now };
        await _repo.UpdateKeyCeremonyAsync(updated, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "ApprovePosKeyCeremony", existing.Status.ToString(), updated.Status.ToString(), updated.TerminalId, request.Remarks);
        return CmsOperationResult<PosKeyCeremony>.Success(updated, request.Approve ? "POS key ceremony completed." : "POS key ceremony rejected.");
    }

    public async Task<CmsOperationResult<EmvCertificationEvidence>> RegisterEmvEvidenceAsync(RegisterEmvCertificationEvidenceRequest request, string actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.TerminalModel)) return CmsOperationResult<EmvCertificationEvidence>.Fail("PAP60", "Terminal model is required.");
        var evidence = new EmvCertificationEvidence(Guid.NewGuid(), request.TerminalModel.Trim(), request.Vendor, request.Protocol, request.Level, request.Scheme.Trim().ToUpperInvariant(), request.TestPackReference.Trim(), Sha256Text(request.EvidencePayload), request.Status, request.CertifiedFrom, request.CertifiedTo, request.Remarks, _clock.UtcNow, request.CorrelationId);
        await _repo.AddEmvEvidenceAsync(evidence, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "RegisterEmvCertificationEvidence", string.Empty, evidence.Status.ToString(), evidence.TerminalModel, evidence.Scheme);
        return CmsOperationResult<EmvCertificationEvidence>.Success(evidence, "EMV certification evidence registered.");
    }

    public Task<IReadOnlyList<EmvCertificationEvidence>> GetEmvEvidenceAsync(CancellationToken ct = default) => _repo.GetEmvEvidenceAsync(ct);

    public async Task<CmsOperationResult<MerchantSettlementPosting>> GenerateMerchantSettlementPostingAsync(GenerateProductionSettlementRequest request, string actor, CancellationToken ct = default)
    {
        var merchant = await _repo.GetMerchantAsync(request.MerchantId, ct).ConfigureAwait(false);
        if (merchant is null) return CmsOperationResult<MerchantSettlementPosting>.Fail("PAP70", "Merchant is not onboarded.");
        if (request.Lines.Count == 0) return CmsOperationResult<MerchantSettlementPosting>.Fail("PAP71", "At least one settlement line is required.");

        decimal interchangeTotal = 0m;
        decimal mdrTotal = 0m;
        decimal gstTotal = 0m;
        foreach (var line in request.Lines)
        {
            var clearingRecord = new ClearingRecord
            {
                Id = Guid.NewGuid(),
                ClearingBatchId = Guid.Empty,
                CorrelationId = request.CorrelationId,
                Stan = line.TransactionId,
                Rrn = line.TransactionId,
                TransactionAmount = line.Amount,
                CurrencyCode = request.CurrencyCode,
                AuthorizationCode = line.AuthorizationCode,
                TransactionAt = _clock.UtcNow
            };
            var interchange = await _interchange.CalculateAsync(request.Network, clearingRecord, new InterchangeFeeContext(request.SettlementDate, request.ProductCode, "POS", line.Mcc, line.CountryCode, request.CurrencyCode, line.FlowType.ToString()), ct).ConfigureAwait(false);
            interchangeTotal += interchange.Value?.TotalFeeAmount ?? 0m;

            var mdr = await CalculateMdrAsync(merchant, request, line, ct).ConfigureAwait(false);
            mdrTotal += mdr.fee;
            gstTotal += mdr.gst;
        }

        var gross = request.Lines.Sum(l => l.Amount);
        var net = Math.Round(gross - interchangeTotal - mdrTotal - gstTotal, 2, MidpointRounding.AwayFromZero);
        var hashPayload = string.Join('|', request.Lines.Select(l => $"{l.TransactionId}:{l.FlowType}:{l.Amount:F2}:{l.Mcc}:{l.CountryCode}")) + $"|{gross:F2}|{interchangeTotal:F2}|{mdrTotal:F2}|{gstTotal:F2}|{net:F2}";
        var posting = new MerchantSettlementPosting(Guid.NewGuid(), merchant.MerchantId, request.SettlementDate, request.CurrencyCode.Trim().ToUpperInvariant(), request.Lines.Count, gross, Math.Round(interchangeTotal, 2), Math.Round(mdrTotal, 2), Math.Round(gstTotal, 2), net, MerchantSettlementPostingStatus.MakerSubmitted, string.Empty, string.Empty, Sha256Text(hashPayload), _clock.UtcNow, null, request.CorrelationId);
        await _repo.AddSettlementPostingAsync(posting, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "GenerateMerchantSettlementPosting", string.Empty, posting.Status.ToString(), posting.MerchantId, posting.Id.ToString("N"));
        return CmsOperationResult<MerchantSettlementPosting>.Success(posting, "Merchant settlement posting generated with MDR/interchange integration.");
    }

    public async Task<CmsOperationResult<MerchantSettlementPosting>> PostMerchantSettlementAsync(PostMerchantSettlementRequest request, string actor, CancellationToken ct = default)
    {
        var existing = await _repo.GetSettlementPostingAsync(request.PostingId, ct).ConfigureAwait(false);
        if (existing is null) return CmsOperationResult<MerchantSettlementPosting>.Fail("PAP72", "Settlement posting not found.");
        var now = _clock.UtcNow;
        var updated = request.CheckerApproved
            ? existing with { Status = MerchantSettlementPostingStatus.PostedToGl, GlJournalReference = $"POS-GL-{now:yyyyMMddHHmmss}-{existing.Id.ToString("N")[..8]}", CoreBankingExportReference = $"CBS-EXP-{now:yyyyMMdd}-{existing.MerchantId}", PostedAt = now }
            : existing with { Status = MerchantSettlementPostingStatus.Failed, CoreBankingExportReference = request.Remarks };
        await _repo.UpdateSettlementPostingAsync(updated, ct).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, actor, "PostMerchantSettlement", existing.Status.ToString(), updated.Status.ToString(), updated.MerchantId, updated.GlJournalReference);
        return CmsOperationResult<MerchantSettlementPosting>.Success(updated, request.CheckerApproved ? "Merchant settlement posted to GL/export queue." : "Merchant settlement rejected by checker.");
    }

    private async Task<(decimal fee, decimal gst)> CalculateMdrAsync(MerchantProfile merchant, GenerateProductionSettlementRequest request, ProductionSettlementLineRequest line, CancellationToken ct)
    {
        var rules = await _repo.GetActiveMdrRulesAsync(merchant.MerchantId, line.Mcc, request.Scheme, request.Network, request.ProductCode, request.CurrencyCode, line.FlowType, request.SettlementDate, ct).ConfigureAwait(false);
        var rule = rules.OrderByDescending(Specificity).ThenByDescending(r => r.Priority).FirstOrDefault();
        decimal fee;
        decimal gstPercent;
        if (rule is null)
        {
            fee = merchant.DefaultMdrFlatFee + line.Amount * merchant.DefaultMdrPercent / 100m;
            gstPercent = 18m;
        }
        else
        {
            fee = rule.FlatFee + line.Amount * rule.PercentFee / 100m;
            if (rule.MinimumFee > 0m && fee < rule.MinimumFee) fee = rule.MinimumFee;
            if (rule.MaximumFee > 0m && fee > rule.MaximumFee) fee = rule.MaximumFee;
            gstPercent = rule.GstPercent;
        }
        fee = Math.Round(fee, 2, MidpointRounding.AwayFromZero);
        return (fee, Math.Round(fee * gstPercent / 100m, 2, MidpointRounding.AwayFromZero));
    }

    private static int Specificity(MdrRule r) => new[] { r.MerchantId, r.Mcc, r.Scheme, r.ProductCode, r.CurrencyCode }.Count(v => !string.IsNullOrWhiteSpace(v) && v != "*");
    private static string Normalize(string value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static string Kcv(string value) => Sha256Text(value)[..6].ToUpperInvariant();
    private static string Sha256Text(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
