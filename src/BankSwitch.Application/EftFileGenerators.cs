using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>
/// NEFT batch file generator. Produces NPCI CRF1 (Credit Return File) format files
/// transmitted to the Clearing House each hourly settlement session.
///
/// File structure (fixed-width ASCII, one record per line):
///   FILE_HEADER  — 3 chars "FIH" + member ID + date + session + record count
///   BATCH_HEADER — 3 chars "BIH" + batch reference + transfer type
///   TRANSACTION  — "BTR" per transfer (sender/beneficiary/amount/IFSC/narration)
///   BATCH_TRAILER— "BIT" + counts + totals
///   FILE_TRAILER — "FIT" + file-level totals
///
/// Return file processing: parses NPCI return response (NRTN format),
/// updates the original transfer status to ReturnedByBeneficiary,
/// and creates EftReturn records.
/// </summary>
public sealed class NeftBatchGenerator : INeftBatchGenerator
{
    private readonly INeftBatchRepository _batchRepo;
    private readonly IEftRepository _eftRepo;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public NeftBatchGenerator(INeftBatchRepository batchRepo, IEftRepository eftRepo, IAuditLogger audit, IClock clock)
    {
        _batchRepo = batchRepo;
        _eftRepo = eftRepo;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<NeftBatch>> GenerateBatchAsync(
        string cycleId, string memberId, DateOnly settlementDate, string actor,
        CancellationToken cancellationToken = default)
    {
        // Idempotency: don't double-generate for the same cycle
        if (await _batchRepo.GetBatchByCycleIdAsync(cycleId, cancellationToken).ConfigureAwait(false) is { } existing)
            return CmsOperationResult<NeftBatch>.Success(existing, $"NEFT batch for cycle {cycleId} already exists.");

        var pending = await _eftRepo.GetPendingTransfersAsync(EftRailType.Neft, cancellationToken).ConfigureAwait(false);
        if (pending.Count == 0)
            return CmsOperationResult<NeftBatch>.Success(new NeftBatch { CycleId = cycleId, Status = NeftBatchStatus.Generated, RecordCount = 0, CreatedAt = _clock.UtcNow }, "No pending NEFT transfers; empty batch.");

        var fileContent = BuildNeftFile(cycleId, memberId, settlementDate, pending);
        var batch = new NeftBatch
        {
            BatchReference = $"NEFT-{memberId}-{settlementDate:yyyyMMdd}-{cycleId.Split('-').LastOrDefault() ?? "C00"}",
            CycleId = cycleId,
            MemberId = memberId.ToUpperInvariant(),
            SettlementDate = settlementDate,
            RecordCount = pending.Count,
            TotalAmount = pending.Sum(t => t.Amount),
            Status = NeftBatchStatus.Generated,
            FileContent = fileContent,
            CreatedAt = _clock.UtcNow
        };
        await _batchRepo.AddBatchAsync(batch, cancellationToken).ConfigureAwait(false);

        // Update transfers to SubmittedToRail
        foreach (var t in pending)
        {
            var updated = t with { Status = EftTransferStatus.SubmittedToRail, SubmittedAt = _clock.UtcNow };
            await _eftRepo.UpdateTransferAsync(updated, cancellationToken).ConfigureAwait(false);
        }

        _audit.LogAdminAudit(batch.BatchReference, actor, "NeftBatchGenerated",
            string.Empty, $"records={batch.RecordCount} total={batch.TotalAmount}", cycleId, string.Empty);

        return CmsOperationResult<NeftBatch>.Success(batch, $"NEFT batch generated: {pending.Count} transfers in cycle {cycleId}.");
    }

    public async Task<CmsOperationResult<NeftBatch>> ProcessReturnFileAsync(
        string returnFileContent, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(returnFileContent))
            return CmsOperationResult<NeftBatch>.Fail("30", "Return file content is empty.");

        var lines = returnFileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var processedCount = 0;

        foreach (var line in lines)
        {
            if (!line.StartsWith("RTR", StringComparison.Ordinal) || line.Length < 60) continue;

            // Parse NPCI return record: RTR + UTR(22) + ReturnCode(3) + Amount(16) + IFSC(11)
            try
            {
                var utr = line.Substring(3, 22).Trim();
                var returnCode = line.Substring(25, 3).Trim();
                var amountStr = line.Substring(28, 16).Trim();
                if (!decimal.TryParse(amountStr, out var amount)) continue;

                var filter = new EftTransferFilter(RailType: EftRailType.Neft, Status: EftTransferStatus.PendingSettlement);
                var transfers = await _eftRepo.GetTransfersAsync(filter, cancellationToken).ConfigureAwait(false);
                var matched = transfers.FirstOrDefault(t => string.Equals(t.RailTransactionRef, utr, StringComparison.Ordinal));
                if (matched is null) continue;

                var returned = matched with
                {
                    Status = EftTransferStatus.ReturnedByBeneficiary,
                    ReturnReasonCode = returnCode,
                    IsReturn = true
                };
                await _eftRepo.UpdateTransferAsync(returned, cancellationToken).ConfigureAwait(false);
                processedCount++;
                _audit.LogSystem(matched.CorrelationId, $"NEFT return: UTR={utr} code={returnCode} amount={amount}");
            }
            catch { /* skip malformed lines */ }
        }

        var batchRef = $"NEFT-RETURN-{_clock.UtcNow:yyyyMMddHHmmss}";
        var returnBatch = new NeftBatch { BatchReference = batchRef, CycleId = batchRef, RecordCount = processedCount, Status = NeftBatchStatus.Settled, CreatedAt = _clock.UtcNow };
        await _batchRepo.AddBatchAsync(returnBatch, cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<NeftBatch>.Success(returnBatch, $"NEFT return file processed: {processedCount} returns handled.");
    }

    public Task<IReadOnlyList<NeftBatch>> GetBatchesAsync(DateOnly? date, CancellationToken cancellationToken = default)
        => _batchRepo.GetBatchesAsync(date, cancellationToken);

    // ---------------------------------------------------------------
    // NPCI CRF1 file builder
    // ---------------------------------------------------------------

    private static string BuildNeftFile(string cycleId, string memberId, DateOnly settlementDate, IReadOnlyList<EftTransfer> transfers)
    {
        var sb = new StringBuilder();
        var dateStr = settlementDate.ToString("ddMMyyyy");
        var totalAmountMinor = (long)(transfers.Sum(t => t.Amount) * 100);

        // FILE HEADER (FIH)
        sb.AppendLine($"FIH{memberId,-4}{dateStr}{cycleId,-12}{transfers.Count:D8}{totalAmountMinor:D16}NEFT");

        // BATCH HEADER (BIH)
        sb.AppendLine($"BIH001{memberId,-4}{dateStr}{transfers.Count:D6}CREDIT");

        // TRANSACTION RECORDS (BTR) — one per transfer
        foreach (var t in transfers)
        {
            var narration = (t.Narration.Length > 40 ? t.Narration[..40] : t.Narration).PadRight(40);
            var amtMinor = (long)(t.Amount * 100);
            sb.AppendLine($"BTR{t.BeneficiaryIfscCode,-11}{t.BeneficiaryAccountNumber,-20}{t.BeneficiaryName.PadRight(35)[..35]}{amtMinor:D16}INR{t.SenderIfscCode,-11}{t.SenderAccountNumber,-20}{narration}{t.CorrelationId[..Math.Min(16, t.CorrelationId.Length)],-16}");
        }

        // BATCH TRAILER (BIT)
        sb.AppendLine($"BIT001{transfers.Count:D6}{totalAmountMinor:D16}");

        // FILE TRAILER (FIT)
        sb.AppendLine($"FIT{transfers.Count:D8}{totalAmountMinor:D16}{dateStr}END");

        return sb.ToString();
    }
}

/// <summary>
/// SWIFT FIN message generator for RTGS individual high-value transfers.
/// Produces ISO 15022 FIN message blocks conforming to:
///   MT103 — Single Customer Credit Transfer (PACS.008 semantic)
///   MT202 — General Financial Institution Transfer (PACS.009 semantic)
/// In production the raw FIN text would be submitted to a SWIFT Service Bureau
/// or connected via SWIFTNet Fin API.
/// </summary>
public sealed class SwiftMessageGenerator : ISwiftMessageGenerator
{
    private readonly ISwiftMessageRepository _repo;
    private readonly IEftRepository _eftRepo;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public SwiftMessageGenerator(ISwiftMessageRepository repo, IEftRepository eftRepo, IAuditLogger audit, IClock clock)
    {
        _repo = repo;
        _eftRepo = eftRepo;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<SwiftMessage>> GenerateMt103Async(
        Guid eftTransferId, string senderBic, string receiverBic, string actor,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _eftRepo.GetTransferAsync(eftTransferId, cancellationToken).ConfigureAwait(false);
        if (transfer is null) return CmsOperationResult<SwiftMessage>.Fail("25", "EFT transfer not found.");
        if (transfer.RailType != EftRailType.Rtgs) return CmsOperationResult<SwiftMessage>.Fail("57", "MT103 is only applicable to RTGS transfers.");

        var txnRef = GenerateSwiftRef();
        var valueDate = transfer.CreatedAt.ToString("yyMMdd");
        var fin = BuildMt103(txnRef, valueDate, senderBic, receiverBic, transfer);

        var msg = new SwiftMessage
        {
            MessageType = SwiftMessageType.MT103,
            Status = SwiftMessageStatus.Draft,
            EftTransferId = eftTransferId,
            SenderBic = senderBic.ToUpperInvariant(),
            ReceiverBic = receiverBic.ToUpperInvariant(),
            TransactionReference = txnRef,
            ValueDate = valueDate,
            CurrencyCode = transfer.CurrencyCode,
            Amount = transfer.Amount,
            OrderingCustomerName = transfer.SenderBankName,
            OrderingCustomerAccount = transfer.SenderAccountNumber,
            BeneficiaryName = transfer.BeneficiaryName,
            BeneficiaryAccount = transfer.BeneficiaryAccountNumber,
            BeneficiaryBic = receiverBic,
            RemittanceInfo = transfer.Narration.Length > 35 ? transfer.Narration[..35] : transfer.Narration,
            RawMessageContent = fin,
            CreatedAt = _clock.UtcNow
        };
        await _repo.AddAsync(msg, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(txnRef, actor, "SwiftMT103Generated", string.Empty, $"EFT={eftTransferId} amount={transfer.Amount}", senderBic, receiverBic);
        return CmsOperationResult<SwiftMessage>.Success(msg, "MT103 message generated.");
    }

    public async Task<CmsOperationResult<SwiftMessage>> GenerateMt202Async(
        Guid eftTransferId, string senderBic, string receiverBic, string actor,
        CancellationToken cancellationToken = default)
    {
        var transfer = await _eftRepo.GetTransferAsync(eftTransferId, cancellationToken).ConfigureAwait(false);
        if (transfer is null) return CmsOperationResult<SwiftMessage>.Fail("25", "EFT transfer not found.");

        var txnRef = GenerateSwiftRef();
        var valueDate = transfer.CreatedAt.ToString("yyMMdd");
        var fin = BuildMt202(txnRef, valueDate, senderBic, receiverBic, transfer);

        var msg = new SwiftMessage
        {
            MessageType = SwiftMessageType.MT202,
            Status = SwiftMessageStatus.Draft,
            EftTransferId = eftTransferId,
            SenderBic = senderBic.ToUpperInvariant(),
            ReceiverBic = receiverBic.ToUpperInvariant(),
            TransactionReference = txnRef,
            ValueDate = valueDate,
            CurrencyCode = transfer.CurrencyCode,
            Amount = transfer.Amount,
            RawMessageContent = fin,
            CreatedAt = _clock.UtcNow
        };
        await _repo.AddAsync(msg, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(txnRef, actor, "SwiftMT202Generated", string.Empty, $"EFT={eftTransferId} amount={transfer.Amount}", senderBic, receiverBic);
        return CmsOperationResult<SwiftMessage>.Success(msg, "MT202 message generated.");
    }

    public async Task<CmsOperationResult<SwiftMessage>> MarkAcknowledgedAsync(
        Guid messageId, string ackReference, string actor, CancellationToken cancellationToken = default)
    {
        var msg = await _repo.GetAsync(messageId, cancellationToken).ConfigureAwait(false);
        if (msg is null) return CmsOperationResult<SwiftMessage>.Fail("25", "SWIFT message not found.");
        var updated = msg with { Status = SwiftMessageStatus.Acknowledged, AckReference = ackReference, AcknowledgedAt = _clock.UtcNow };
        await _repo.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<SwiftMessage>.Success(updated);
    }

    public Task<IReadOnlyList<SwiftMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken = default)
        => _repo.GetPendingAsync(cancellationToken);

    // ---------------------------------------------------------------
    // MT103 / MT202 FIN builders
    // ---------------------------------------------------------------

    private static string BuildMt103(string txnRef, string valueDate, string senderBic, string receiverBic, EftTransfer t)
    {
        var sb = new StringBuilder();
        // Application Header (Block 1-2)
        sb.AppendLine($"{{1:F01{senderBic}XXXX0000000000}}");
        sb.AppendLine($"{{2:O1031200{valueDate}{{3:{{108:{txnRef}}}}}{{4:");
        sb.AppendLine($":20:{txnRef}");
        sb.AppendLine(":23B:CRED");
        sb.AppendLine($":32A:{valueDate}INR{(long)(t.Amount * 100):D15}");
        sb.AppendLine($":50K:/{t.SenderAccountNumber}");
        sb.AppendLine($"{t.SenderBankName.PadRight(35)[..Math.Min(35, t.SenderBankName.Length)]}");
        sb.AppendLine($":57A://{receiverBic}");
        sb.AppendLine($"{t.BeneficiaryIfscCode}");
        sb.AppendLine($":59:/{t.BeneficiaryAccountNumber}");
        sb.AppendLine($"{(t.BeneficiaryName.Length > 35 ? t.BeneficiaryName[..35] : t.BeneficiaryName)}");
        sb.AppendLine($":70:{(t.Narration.Length > 35 ? t.Narration[..35] : t.Narration)}");
        sb.AppendLine(":71A:SHA");
        sb.AppendLine("-}");
        return sb.ToString();
    }

    private static string BuildMt202(string txnRef, string valueDate, string senderBic, string receiverBic, EftTransfer t)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{{1:F01{senderBic}XXXX0000000000}}");
        sb.AppendLine($"{{2:O2021200{valueDate}{{3:{{108:{txnRef}}}}}{{4:");
        sb.AppendLine($":20:{txnRef}");
        sb.AppendLine($":21:{t.CorrelationId[..Math.Min(16, t.CorrelationId.Length)]}");
        sb.AppendLine($":32A:{valueDate}INR{(long)(t.Amount * 100):D15}");
        sb.AppendLine($":58A://{receiverBic}");
        sb.AppendLine($"{t.BeneficiaryBankName}");
        sb.AppendLine("-}");
        return sb.ToString();
    }

    private static string GenerateSwiftRef() => $"SWFT{DateTimeOffset.UtcNow:yyyyMMddHHmmss}{Guid.NewGuid():N}"[..16].ToUpperInvariant();
}

/// <summary>
/// NACHA ACH batch file generator. Produces standard 94-character fixed-width
/// records for US/India ACH (PPD, CCD) and NACH (National Automated Clearing House).
/// Also handles return batch processing (NACHA return reason codes).
/// </summary>
public sealed class AchBatchGenerator : IAchBatchGenerator
{
    private readonly IAchFileRepository _repo;
    private readonly IEftRepository _eftRepo;
    private readonly IDirectDebitMandateRepository _mandateRepo;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public AchBatchGenerator(IAchFileRepository repo, IEftRepository eftRepo, IDirectDebitMandateRepository mandateRepo, IAuditLogger audit, IClock clock)
    {
        _repo = repo;
        _eftRepo = eftRepo;
        _mandateRepo = mandateRepo;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<AchFile>> GenerateCreditBatchAsync(
        string companyId, string companyName, DateOnly effectiveDate, AchEntryType entryType,
        string actor, CancellationToken cancellationToken = default)
    {
        var pending = await _eftRepo.GetPendingTransfersAsync(EftRailType.Ach, cancellationToken).ConfigureAwait(false);
        var credits = pending.Where(t => !t.IsReturn).ToList();
        if (credits.Count == 0)
            return CmsOperationResult<AchFile>.Fail("25", "No pending ACH credit transfers.");

        var content = BuildNachaFile(companyId, companyName, effectiveDate, entryType, credits, isDebit: false);
        var file = new AchFile
        {
            FileReference = $"ACH-CR-{effectiveDate:yyyyMMdd}-{Guid.NewGuid():N[..8]}",
            FileType = AchFileType.CreditBatch,
            EntryType = entryType,
            Status = AchFileStatus.Validated,
            OriginatingDfi = companyId[..Math.Min(9, companyId.Length)],
            OriginatingCompanyId = companyId,
            OriginatingCompanyName = companyName,
            EffectiveDate = effectiveDate,
            RecordCount = credits.Count,
            TotalCreditAmount = credits.Sum(t => t.Amount),
            FileContent = content,
            CreatedAt = _clock.UtcNow
        };
        await _repo.AddAsync(file, cancellationToken).ConfigureAwait(false);
        foreach (var t in credits)
            await _eftRepo.UpdateTransferAsync(t with { Status = EftTransferStatus.SubmittedToRail, SubmittedAt = _clock.UtcNow }, cancellationToken).ConfigureAwait(false);

        _audit.LogAdminAudit(file.FileReference, actor, "AchCreditBatchGenerated", string.Empty, $"count={credits.Count} amount={file.TotalCreditAmount}", effectiveDate.ToString(), string.Empty);
        return CmsOperationResult<AchFile>.Success(file, $"ACH credit batch generated: {credits.Count} entries.");
    }

    public async Task<CmsOperationResult<AchFile>> GenerateDebitBatchAsync(
        string mandateGroupCode, DateOnly effectiveDate, AchEntryType entryType,
        string actor, CancellationToken cancellationToken = default)
    {
        var mandates = await _mandateRepo.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        if (mandates.Count == 0) return CmsOperationResult<AchFile>.Fail("25", "No active mandates for debit batch.");

        var companyId = mandateGroupCode;
        var content = BuildNachaDebitFile(mandates, effectiveDate, entryType);
        var file = new AchFile
        {
            FileReference = $"ACH-DR-{effectiveDate:yyyyMMdd}-{Guid.NewGuid():N[..8]}",
            FileType = AchFileType.DebitBatch,
            EntryType = entryType,
            Status = AchFileStatus.Validated,
            OriginatingDfi = companyId[..Math.Min(9, companyId.Length)],
            OriginatingCompanyId = mandateGroupCode,
            EffectiveDate = effectiveDate,
            RecordCount = mandates.Count,
            TotalDebitAmount = mandates.Sum(m => m.MaximumAmount),
            FileContent = content,
            CreatedAt = _clock.UtcNow
        };
        await _repo.AddAsync(file, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(file.FileReference, actor, "AchDebitBatchGenerated", string.Empty, $"mandates={mandates.Count}", effectiveDate.ToString(), string.Empty);
        return CmsOperationResult<AchFile>.Success(file, $"ACH debit batch generated: {mandates.Count} mandate entries.");
    }

    public async Task<CmsOperationResult<AchFile>> ProcessReturnBatchAsync(
        string returnFileContent, string actor, CancellationToken cancellationToken = default)
    {
        var lines = returnFileContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var processed = 0;
        foreach (var line in lines)
        {
            if (line.Length < 94 || line[0] != '6') continue; // detail record type 6
            var returnCode = line.Substring(79, 3).Trim();
            var traceNumber = line.Substring(87, 7).Trim();
            // Best-effort: match trace number to existing transfer batch sequence
            var filter = new EftTransferFilter(RailType: EftRailType.Ach, Status: EftTransferStatus.PendingSettlement);
            var transfers = await _eftRepo.GetTransfersAsync(filter, cancellationToken).ConfigureAwait(false);
            var matched = transfers.FirstOrDefault(t => t.BatchSequenceNumber.EndsWith(traceNumber, StringComparison.Ordinal));
            if (matched is null) continue;
            var returned = matched with { Status = EftTransferStatus.ReturnedByBeneficiary, ReturnReasonCode = returnCode, IsReturn = true };
            await _eftRepo.UpdateTransferAsync(returned, cancellationToken).ConfigureAwait(false);
            processed++;
        }
        var file = new AchFile { FileReference = $"ACH-RTN-{_clock.UtcNow:yyyyMMddHHmmss}", FileType = AchFileType.ReturnBatch, Status = AchFileStatus.Returned, RecordCount = processed, CreatedAt = _clock.UtcNow };
        await _repo.AddAsync(file, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<AchFile>.Success(file, $"ACH return batch processed: {processed} returns.");
    }

    public Task<IReadOnlyList<AchFile>> GetFilesAsync(DateOnly? effectiveDate, CancellationToken cancellationToken = default)
        => _repo.GetAsync(effectiveDate, cancellationToken);

    // ---------------------------------------------------------------
    // NACHA file builders (94-char fixed-width)
    // ---------------------------------------------------------------

    private static string BuildNachaFile(string companyId, string companyName, DateOnly effectiveDate, AchEntryType entryType, IReadOnlyList<EftTransfer> transfers, bool isDebit)
    {
        var sb = new StringBuilder();
        var today = DateTimeOffset.UtcNow;
        var fileDate = today.ToString("yyMMdd");
        var fileTime = today.ToString("HHmm");
        var effDate = effectiveDate.ToString("yyMMdd");
        var originatingDfi = companyId.Length >= 9 ? companyId[..9] : companyId.PadLeft(9, '0');

        // File Header (type 1, 94 chars)
        sb.AppendLine($"1{" ",-1}{"01",-2}{originatingDfi,-23}{companyName,-23}{fileDate,-6}{fileTime,-4}{"A",-1}{"094",-3}{"10",-2}{"1",-1}{"BBK",-8}{Guid.NewGuid():N}"[..9].PadLeft(94));

        // Batch Header (type 5)
        var debitCode = isDebit ? "27" : "22"; // 27=checking debit, 22=checking credit
        sb.AppendLine($"5{debitCode,-3}{companyName,-16}{companyId.PadLeft(10),-10}{entryType,-3}{"PAYMENT",-10}{effDate,-6}{" ",-13}{"1",-1}{originatingDfi[..8],-8}{"0000001",-7}".PadRight(94));

        // Detail Records (type 6)
        var seqNum = 1;
        decimal batchDebit = 0m, batchCredit = 0m;
        foreach (var t in transfers)
        {
            var amtCents = (long)(t.Amount * 100);
            var accountNum = t.BeneficiaryAccountNumber.PadLeft(17)[..17];
            var name = (t.BeneficiaryName.Length > 22 ? t.BeneficiaryName[..22] : t.BeneficiaryName).PadRight(22);
            sb.AppendLine($"6{debitCode,-2}{"000000000",-9}{accountNum,-17}{amtCents:D10}{name,-22}{originatingDfi[..8],-8}{seqNum:D7}".PadRight(94));
            if (isDebit) batchDebit += t.Amount; else batchCredit += t.Amount;
            seqNum++;
        }

        // Batch Control (type 8)
        var entryCount = transfers.Count;
        sb.AppendLine($"8{debitCode,-3}{entryCount:D6}{originatingDfi,-10}{(long)(batchDebit * 100):D12}{(long)(batchCredit * 100):D12}{companyId.PadLeft(10),-10}{" ",-39}{originatingDfi[..8],-8}{"0000001",-7}".PadRight(94));

        // File Control (type 9)
        var blockCount = (int)Math.Ceiling((seqNum + 3) / 10.0);
        sb.AppendLine($"9{"000001",-6}{blockCount:D6}{entryCount:D8}{(long)(batchDebit * 100):D12}{(long)(batchCredit * 100):D12}{" ",-39}".PadRight(94));

        return sb.ToString();
    }

    private static string BuildNachaDebitFile(IReadOnlyList<DirectDebitMandate> mandates, DateOnly effectiveDate, AchEntryType entryType)
    {
        // Build debit entries from active mandates (simplified — real implementation would check frequency/last charge date)
        var syntheticTransfers = mandates.Select(m => new EftTransfer
        {
            BeneficiaryAccountNumber = m.DebtorAccountNumber,
            BeneficiaryName = m.CreditorName,
            Amount = m.MaximumAmount,
            BatchSequenceNumber = m.MandateReference
        }).ToList();
        return BuildNachaFile("BATCH-DR", "NACH Direct Debit", effectiveDate, entryType, syntheticTransfers, isDebit: true);
    }
}