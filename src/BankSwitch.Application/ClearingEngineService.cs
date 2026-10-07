using System.Security.Cryptography;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class ClearingEngineService : IClearingEngineService
{
    private readonly IClearingRepository _repository;
    private readonly ITransactionRepository _transactions;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly ClearingEngineOptions _options;
    private readonly IInterchangeFeeRuleEngine _feeEngine;
    private readonly ISettlementCertificationService _certification;
    private readonly INetworkSettlementRunRepository _runs;
    private readonly IReadOnlyDictionary<SettlementNetwork, INetworkSettlementGateway> _gateways;

    public ClearingEngineService(
        IClearingRepository repository,
        ITransactionRepository transactions,
        IAuditLogger audit,
        IClock clock,
        ClearingEngineOptions options,
        IInterchangeFeeRuleEngine feeEngine,
        ISettlementCertificationService certification,
        INetworkSettlementRunRepository runs,
        IEnumerable<INetworkSettlementGateway> gateways)
    {
        _repository = repository;
        _transactions = transactions;
        _audit = audit;
        _clock = clock;
        _options = options;
        _feeEngine = feeEngine;
        _certification = certification;
        _runs = runs;
        _gateways = gateways.ToDictionary(g => g.Network);
    }

    public async Task<CmsOperationResult<IReadOnlyList<ClearingBatch>>> GenerateClearingBatchesAsync(DateOnly businessDate, CancellationToken cancellationToken = default)
    {
        var settlementProfiles = _options.SettlementProfiles.Count > 0
            ? _options.SettlementProfiles
            : new[] { "VISA_IN", "MASTERCARD_IN", "RUPAY_NPCI", "NFS_NPCI", "DEFAULT" };
        var batches = new List<ClearingBatch>();

        foreach (var profile in settlementProfiles)
        {
            var transactions = await _transactions.GetUnclearedTransactionsAsync(businessDate, profile, cancellationToken).ConfigureAwait(false);
            if (transactions.Count == 0) continue;

            var format = ResolveFormat(profile);
            var network = ResolveNetwork(profile, format);
            var batchRef = $"{format}-{businessDate:yyyyMMdd}-{profile[..Math.Min(12, profile.Length)]}";

            if (await _repository.GetBatchByReferenceAsync(batchRef, cancellationToken).ConfigureAwait(false) is not null)
            {
                _audit.LogSystem(batchRef, $"Clearing batch {batchRef} already exists for {businessDate}. Skipping.");
                continue;
            }

            var records = new List<ClearingRecord>();
            foreach (var tx in transactions)
            {
                var baseRecord = new ClearingRecord
                {
                    ClearingBatchId = Guid.Empty,
                    CorrelationId = tx.CorrelationId,
                    Stan = tx.Stan,
                    Rrn = tx.Rrn,
                    MaskedPan = tx.MaskedPan,
                    PanHash = tx.PanHash,
                    Mti = tx.Mti,
                    ProcessingCode = ExtractProcessingCode(tx),
                    TransactionAmount = tx.Amount,
                    FeeAmount = 0m,
                    CurrencyCode = string.IsNullOrWhiteSpace(tx.CurrencyCode) ? _options.DefaultCurrencyCode : tx.CurrencyCode,
                    SourceNodeId = tx.SourceNodeId,
                    SinkNodeId = tx.SinkNodeId,
                    AuthorizationCode = string.Empty,
                    TransactionAt = tx.CreatedAt,
                    IsIncluded = true
                };

                var feeContext = BuildFeeContext(businessDate, tx, baseRecord);
                var fee = await _feeEngine.CalculateAsync(network, baseRecord, feeContext, cancellationToken).ConfigureAwait(false);
                var finalRecord = baseRecord with { FeeAmount = fee.Value?.TotalFeeAmount ?? 0m };
                records.Add(finalRecord);
            }

            var totalDebit = records.Where(r => r.IsIncluded).Sum(r => r.TransactionAmount + r.FeeAmount);
            var totalFee = records.Where(r => r.IsIncluded).Sum(r => r.FeeAmount);
            var batch = new ClearingBatch
            {
                BatchReference = batchRef,
                FileFormat = format,
                Status = ClearingBatchStatus.Draft,
                BusinessDate = businessDate,
                SettlementProfile = profile,
                CurrencyCode = _options.DefaultCurrencyCode,
                InstitutionCode = ResolveInstitutionCode(profile),
                RecordCount = records.Count(r => r.IsIncluded),
                TotalDebitAmount = totalDebit,
                TotalCreditAmount = 0m,
                NetSettlementAmount = totalDebit,
                CreatedAt = _clock.UtcNow
            };

            var finalRecords = records.Select(r => r with { ClearingBatchId = batch.Id }).ToList();
            await _repository.AddBatchAsync(batch, finalRecords, cancellationToken).ConfigureAwait(false);
            await _transactions.MarkTransactionsClearedAsync(transactions.Select(t => t.CorrelationId).ToList(), batch.Id, cancellationToken).ConfigureAwait(false);

            batches.Add(batch);
            _audit.LogSystem(batch.BatchReference, $"Clearing batch generated: profile={profile} network={network} records={batch.RecordCount} amount={batch.TotalDebitAmount} interchangeFees={totalFee} date={businessDate}");
        }

        return CmsOperationResult<IReadOnlyList<ClearingBatch>>.Success(batches, $"{batches.Count} clearing batch(es) generated for {businessDate}.");
    }

    public async Task<CmsOperationResult<byte[]>> BuildClearingFileAsync(Guid batchId, CancellationToken cancellationToken = default)
    {
        var batch = await _repository.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null) return CmsOperationResult<byte[]>.Fail("25", "Clearing batch not found.");

        var records = await _repository.GetRecordsAsync(batchId, cancellationToken).ConfigureAwait(false);
        var included = records.Where(r => r.IsIncluded).ToList();

        var fileBytes = batch.FileFormat switch
        {
            ClearingFileFormat.VisaTc5 => BuildVisaSettlementFile(batch, included),
            ClearingFileFormat.MastercardIpm => BuildMastercardIpmFile(batch, included),
            ClearingFileFormat.RupayNpci => BuildRupayNpciSettlementFile(batch, included),
            ClearingFileFormat.NpciNfs => BuildNpciNfsSettlementFile(batch, included),
            ClearingFileFormat.Iso20022Xml => BuildIso20022XmlFile(batch, included),
            _ => BuildInternalCsvFile(batch, included)
        };

        var validation = await _certification.ValidateAsync(batch, included, fileBytes, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            await RecordSettlementRunAsync(batch, fileBytes, validation, SettlementCertificationStatus.Rejected, string.Empty, cancellationToken).ConfigureAwait(false);
            return CmsOperationResult<byte[]>.Fail("30", "Clearing file failed certification validation: " + string.Join("; ", validation.Errors));
        }

        var updated = batch with { Status = ClearingBatchStatus.Generated, GeneratedAt = _clock.UtcNow };
        await _repository.UpdateBatchAsync(updated, cancellationToken).ConfigureAwait(false);
        await RecordSettlementRunAsync(updated, fileBytes, validation, validation.Status, string.Empty, cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<byte[]>.Success(fileBytes, $"Clearing file built and certification-ready: {included.Count} records, {fileBytes.Length} bytes, format={batch.FileFormat}");
    }

    public async Task<CmsOperationResult<ClearingBatch>> MarkTransmittedAsync(Guid batchId, string outputFilePath, string actor, CancellationToken cancellationToken = default)
    {
        var batch = await _repository.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null) return CmsOperationResult<ClearingBatch>.Fail("25", "Clearing batch not found.");

        var records = await _repository.GetRecordsAsync(batchId, cancellationToken).ConfigureAwait(false);
        var fileBytes = batch.FileFormat switch
        {
            ClearingFileFormat.VisaTc5 => BuildVisaSettlementFile(batch, records.Where(r => r.IsIncluded).ToList()),
            ClearingFileFormat.MastercardIpm => BuildMastercardIpmFile(batch, records.Where(r => r.IsIncluded).ToList()),
            ClearingFileFormat.RupayNpci => BuildRupayNpciSettlementFile(batch, records.Where(r => r.IsIncluded).ToList()),
            ClearingFileFormat.NpciNfs => BuildNpciNfsSettlementFile(batch, records.Where(r => r.IsIncluded).ToList()),
            ClearingFileFormat.Iso20022Xml => BuildIso20022XmlFile(batch, records.Where(r => r.IsIncluded).ToList()),
            _ => BuildInternalCsvFile(batch, records.Where(r => r.IsIncluded).ToList())
        };

        var network = ResolveNetwork(batch.SettlementProfile, batch.FileFormat);
        var fileName = string.IsNullOrWhiteSpace(outputFilePath) ? BuildNetworkFileName(batch, network) : outputFilePath;
        var transmissionReference = string.Empty;
        if (_gateways.TryGetValue(network, out var gateway))
        {
            var submitted = await gateway.SubmitAsync(batch, fileBytes, fileName, cancellationToken).ConfigureAwait(false);
            transmissionReference = submitted.TransmissionReference;
        }

        var updated = batch with { Status = ClearingBatchStatus.Transmitted, OutputFilePath = fileName, TransmittedAt = _clock.UtcNow, NetworkAckReference = transmissionReference };
        await _repository.UpdateBatchAsync(updated, cancellationToken).ConfigureAwait(false);
        await RecordSettlementRunAsync(updated, fileBytes, new SettlementValidationResult { IsValid = true, Status = SettlementCertificationStatus.Submitted }, SettlementCertificationStatus.Submitted, transmissionReference, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(batchId.ToString("N"), actor, "ClearingBatchTransmitted", batch.Status.ToString(), ClearingBatchStatus.Transmitted.ToString(), $"{fileName}|{transmissionReference}", string.Empty);
        return CmsOperationResult<ClearingBatch>.Success(updated);
    }

    public async Task<CmsOperationResult<ClearingBatch>> RecordNetworkAcknowledgementAsync(Guid batchId, string networkAckReference, ClearingBatchStatus outcomeStatus, string actor, CancellationToken cancellationToken = default)
    {
        var batch = await _repository.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        if (batch is null) return CmsOperationResult<ClearingBatch>.Fail("25", "Clearing batch not found.");

        var updated = batch with { Status = outcomeStatus, NetworkAckReference = networkAckReference, AcknowledgedAt = _clock.UtcNow };
        await _repository.UpdateBatchAsync(updated, cancellationToken).ConfigureAwait(false);
        if (await _runs.GetRunByBatchAsync(batchId, cancellationToken).ConfigureAwait(false) is { } run)
        {
            await _runs.UpdateRunAsync(run with
            {
                CertificationStatus = outcomeStatus == ClearingBatchStatus.AcknowledgedByNetwork || outcomeStatus == ClearingBatchStatus.Settled
                    ? SettlementCertificationStatus.Accepted
                    : SettlementCertificationStatus.Rejected,
                TransmissionReference = networkAckReference,
                AcceptedAt = outcomeStatus == ClearingBatchStatus.AcknowledgedByNetwork || outcomeStatus == ClearingBatchStatus.Settled ? _clock.UtcNow : null
            }, cancellationToken).ConfigureAwait(false);
        }
        _audit.LogAdminAudit(batchId.ToString("N"), actor, "ClearingNetworkAck", batch.Status.ToString(), outcomeStatus.ToString(), networkAckReference, string.Empty);
        return CmsOperationResult<ClearingBatch>.Success(updated);
    }

    public Task<IReadOnlyList<ClearingBatch>> GetClearingBatchesAsync(DateOnly? businessDate, CancellationToken cancellationToken = default)
        => _repository.GetBatchesAsync(businessDate, cancellationToken);

    public Task<IReadOnlyList<ClearingRecord>> GetClearingRecordsAsync(Guid batchId, CancellationToken cancellationToken = default)
        => _repository.GetRecordsAsync(batchId, cancellationToken);

    private async Task RecordSettlementRunAsync(ClearingBatch batch, byte[] fileBytes, SettlementValidationResult validation, SettlementCertificationStatus status, string transmissionReference, CancellationToken cancellationToken)
    {
        var network = ResolveNetwork(batch.SettlementProfile, batch.FileFormat);
        var existing = await _runs.GetRunByBatchAsync(batch.Id, cancellationToken).ConfigureAwait(false);
        var fileName = BuildNetworkFileName(batch, network);
        var run = existing is null
            ? new NetworkSettlementRun
            {
                ClearingBatchId = batch.Id,
                Network = network,
                SettlementCycle = $"{batch.BusinessDate:yyyyMMdd}-{batch.SettlementProfile}",
                FileName = fileName,
                FileHashSha256 = Convert.ToHexString(SHA256.HashData(fileBytes)),
                FileSizeBytes = fileBytes.LongLength,
                CertificationStatus = status,
                ValidationReport = validation.ReportText,
                TransmissionReference = transmissionReference,
                CreatedAt = _clock.UtcNow,
                SubmittedAt = status == SettlementCertificationStatus.Submitted ? _clock.UtcNow : null
            }
            : existing with
            {
                FileName = fileName,
                FileHashSha256 = Convert.ToHexString(SHA256.HashData(fileBytes)),
                FileSizeBytes = fileBytes.LongLength,
                CertificationStatus = status,
                ValidationReport = validation.ReportText,
                TransmissionReference = transmissionReference,
                SubmittedAt = status == SettlementCertificationStatus.Submitted ? _clock.UtcNow : existing.SubmittedAt
            };
        if (existing is null) await _runs.AddRunAsync(run, cancellationToken).ConfigureAwait(false);
        else await _runs.UpdateRunAsync(run, cancellationToken).ConfigureAwait(false);
    }

    private static byte[] BuildVisaSettlementFile(ClearingBatch batch, IReadOnlyList<ClearingRecord> records)
    {
        // Visa settlement file layout: fixed-width header/detail/trailer using TC05-style fields.
        // Field layout is isolated here so a bank-specific Visa certification pack can replace it without touching clearing logic.
        var sb = new StringBuilder();
        sb.AppendLine(Fixed("0", 1) + Fixed("VISA", 8) + Fixed(batch.BatchReference, 24) + batch.BusinessDate.ToString("yyyyMMdd") + Money(batch.TotalDebitAmount, 15) + Count(records.Count, 8));
        foreach (var r in records)
        {
            sb.AppendLine(Fixed("1", 1) + Fixed(r.Rrn, 12) + Fixed(r.Stan, 6) + Fixed(r.MaskedPan, 19) + Fixed(r.ProcessingCode, 6) + r.TransactionAt.ToString("yyyyMMddHHmmss") + Money(r.TransactionAmount, 12) + Money(r.FeeAmount, 10) + Fixed(r.CurrencyCode, 3) + Fixed(r.AuthorizationCode, 6) + Fixed(r.SourceNodeId, 12) + Fixed(r.SinkNodeId, 12));
        }
        sb.AppendLine(Fixed("9", 1) + Count(records.Count, 8) + Money(records.Sum(r => r.TransactionAmount), 15) + Money(records.Sum(r => r.FeeAmount), 15));
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static byte[] BuildMastercardIpmFile(ClearingBatch batch, IReadOnlyList<ClearingRecord> records)
    {
        // Mastercard IPM/File Express: DE-style tagged ISO 8583 records. The encoding boundary is explicit;
        // a production adapter can binary-pack the same DE map for MDS/IPM certification.
        var sb = new StringBuilder();
        sb.AppendLine($"HDR|NETWORK=MASTERCARD|FILE={batch.BatchReference}|DATE={batch.BusinessDate:yyyyMMdd}|COUNT={records.Count}|AMOUNT={MoneyText(batch.TotalDebitAmount)}");
        foreach (var r in records)
        {
            sb.AppendLine("IPM|MTI=1240"
                + $"|DE2={DigitsOnly(r.MaskedPan)}"
                + $"|DE3={FixedRight(r.ProcessingCode, 6)}"
                + $"|DE4={Money(r.TransactionAmount, 12)}"
                + $"|DE12={r.TransactionAt:yyMMddHHmmss}"
                + $"|DE24=200"
                + $"|DE37={FixedRight(r.Rrn, 12)}"
                + $"|DE38={FixedRight(r.AuthorizationCode, 6)}"
                + $"|DE49={FixedRight(r.CurrencyCode, 3)}"
                + $"|DE63=FEE{Money(r.FeeAmount, 10)}SRC{FixedRight(r.SourceNodeId, 12)}SNK{FixedRight(r.SinkNodeId, 12)}");
        }
        sb.AppendLine($"TRL|COUNT={records.Count}|AMOUNT={MoneyText(records.Sum(r => r.TransactionAmount))}|FEE={MoneyText(records.Sum(r => r.FeeAmount))}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] BuildRupayNpciSettlementFile(ClearingBatch batch, IReadOnlyList<ClearingRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("HDR|RUPAY|" + batch.BatchReference + "|" + batch.BusinessDate.ToString("yyyyMMdd") + "|" + batch.CurrencyCode);
        foreach (var r in records)
            sb.AppendLine($"DTL|{r.Rrn}|{r.Stan}|{r.MaskedPan}|{r.ProcessingCode}|{MoneyText(r.TransactionAmount)}|{MoneyText(r.FeeAmount)}|{r.CurrencyCode}|{r.TransactionAt:yyyyMMddHHmmss}|{r.SourceNodeId}|{r.SinkNodeId}");
        sb.AppendLine($"TRL|{records.Count}|{MoneyText(records.Sum(r => r.TransactionAmount))}|{MoneyText(records.Sum(r => r.FeeAmount))}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] BuildNpciNfsSettlementFile(ClearingBatch batch, IReadOnlyList<ClearingRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("RECORD_TYPE,NETWORK,BATCH_REF,BUSINESS_DATE,RRN,STAN,MASKED_PAN,PROC_CODE,AMOUNT,FEE,CURRENCY,TXN_AT,SOURCE,SINK");
        foreach (var r in records)
            sb.AppendLine($"D,NFS,{batch.BatchReference},{batch.BusinessDate:yyyyMMdd},{r.Rrn},{r.Stan},{r.MaskedPan},{r.ProcessingCode},{MoneyText(r.TransactionAmount)},{MoneyText(r.FeeAmount)},{r.CurrencyCode},{r.TransactionAt:yyyyMMddHHmmss},{r.SourceNodeId},{r.SinkNodeId}");
        sb.AppendLine($"T,NFS,{batch.BatchReference},{batch.BusinessDate:yyyyMMdd},,,,,{MoneyText(records.Sum(r => r.TransactionAmount))},{MoneyText(records.Sum(r => r.FeeAmount))},{batch.CurrencyCode},,,");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] BuildIso20022XmlFile(ClearingBatch batch, IReadOnlyList<ClearingRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<Document xmlns=\"urn:iso:std:iso:20022:tech:xsd:pacs.008.001.08\">");
        sb.AppendLine($"  <FIToFICstmrCdtTrf><GrpHdr><MsgId>{Xml(batch.BatchReference)}</MsgId><CreDtTm>{batch.CreatedAt:O}</CreDtTm><NbOfTxs>{records.Count}</NbOfTxs><CtrlSum>{MoneyText(records.Sum(r => r.TransactionAmount))}</CtrlSum></GrpHdr>");
        foreach (var r in records)
        {
            sb.AppendLine($"    <CdtTrfTxInf><PmtId><InstrId>{Xml(r.Stan)}</InstrId><EndToEndId>{Xml(r.Rrn)}</EndToEndId></PmtId><IntrBkSttlmAmt Ccy=\"{Xml(r.CurrencyCode)}\">{MoneyText(r.TransactionAmount)}</IntrBkSttlmAmt><IntrBkSttlmDt>{r.TransactionAt:yyyy-MM-dd}</IntrBkSttlmDt></CdtTrfTxInf>");
        }
        sb.AppendLine("  </FIToFICstmrCdtTrf></Document>");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static byte[] BuildInternalCsvFile(ClearingBatch batch, IReadOnlyList<ClearingRecord> records)
        => BuildNpciNfsSettlementFile(batch, records);

    private static ClearingFileFormat ResolveFormat(string settlementProfile) => settlementProfile switch
    {
        var p when p.StartsWith("VISA", StringComparison.OrdinalIgnoreCase) => ClearingFileFormat.VisaTc5,
        var p when p.StartsWith("MASTERCARD", StringComparison.OrdinalIgnoreCase) || p.StartsWith("MC", StringComparison.OrdinalIgnoreCase) => ClearingFileFormat.MastercardIpm,
        var p when p.StartsWith("RUPAY", StringComparison.OrdinalIgnoreCase) => ClearingFileFormat.RupayNpci,
        var p when p.StartsWith("NFS", StringComparison.OrdinalIgnoreCase) || p.StartsWith("NPCI", StringComparison.OrdinalIgnoreCase) => ClearingFileFormat.NpciNfs,
        _ => ClearingFileFormat.InternalCsv
    };

    private static SettlementNetwork ResolveNetwork(string settlementProfile, ClearingFileFormat format) => format switch
    {
        ClearingFileFormat.VisaTc5 => SettlementNetwork.Visa,
        ClearingFileFormat.MastercardIpm => SettlementNetwork.Mastercard,
        ClearingFileFormat.RupayNpci => SettlementNetwork.Rupay,
        ClearingFileFormat.NpciNfs => SettlementNetwork.NpciNfs,
        _ when settlementProfile.StartsWith("RUPAY", StringComparison.OrdinalIgnoreCase) => SettlementNetwork.Rupay,
        _ when settlementProfile.StartsWith("NFS", StringComparison.OrdinalIgnoreCase) || settlementProfile.StartsWith("NPCI", StringComparison.OrdinalIgnoreCase) => SettlementNetwork.NpciNfs,
        _ => SettlementNetwork.Internal
    };

    private static InterchangeFeeContext BuildFeeContext(DateOnly businessDate, TransactionLog tx, ClearingRecord record)
    {
        return new InterchangeFeeContext(
            businessDate,
            ProductCode: string.IsNullOrWhiteSpace(tx.SchemeUsed) ? "DEBIT" : tx.SchemeUsed,
            ChannelCode: GuessChannel(tx),
            MerchantCategoryCode: "*",
            CountryCode: "IN",
            CurrencyCode: string.IsNullOrWhiteSpace(record.CurrencyCode) ? "356" : record.CurrencyCode,
            TransactionTypeCode: GuessTransactionType(tx, record));
    }

    private static string GuessChannel(TransactionLog tx)
    {
        var route = (tx.RouteUsed ?? string.Empty).ToUpperInvariant();
        if (route.Contains("ATM")) return "ATM";
        if (route.Contains("ECOM")) return "ECOM";
        if (route.Contains("POS")) return "POS";
        return "POS";
    }

    private static string GuessTransactionType(TransactionLog tx, ClearingRecord record)
    {
        if (tx.ReversalState != ReversalState.None) return "REVERSAL";
        if (record.ProcessingCode.StartsWith("01", StringComparison.Ordinal)) return "WITHDRAWAL";
        return "PURCHASE";
    }

    private static string ExtractProcessingCode(TransactionLog tx) => string.IsNullOrWhiteSpace(tx.FeeApplied) ? "000000" : tx.FeeApplied[..Math.Min(6, tx.FeeApplied.Length)].PadRight(6, '0');
    private static string ResolveInstitutionCode(string settlementProfile) => settlementProfile.Split('_', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "BANK";
    private static string BuildNetworkFileName(ClearingBatch batch, SettlementNetwork network) => $"{network}_{batch.BusinessDate:yyyyMMdd}_{batch.BatchReference}.dat";
    private static string Count(int value, int width) => value.ToString().PadLeft(width, '0');
    private static string Money(decimal value, int width) => ((long)Math.Round(value * 100m, 0, MidpointRounding.AwayFromZero)).ToString().PadLeft(width, '0');
    private static string MoneyText(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    private static string Fixed(string value, int width) => FixedRight(value, width);
    private static string FixedRight(string value, int width) => (value ?? string.Empty).Length > width ? value[..width] : (value ?? string.Empty).PadRight(width, ' ');
    private static string DigitsOnly(string value) => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    private static string Xml(string value) => System.Security.SecurityElement.Escape(value ?? string.Empty) ?? string.Empty;
}
