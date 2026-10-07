using System.Security.Cryptography;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class SettlementCertificationService : ISettlementCertificationService
{
    public Task<SettlementValidationResult> ValidateAsync(ClearingBatch batch, IReadOnlyList<ClearingRecord> records, byte[] fileBytes, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (batch.RecordCount != records.Count(r => r.IsIncluded)) errors.Add("Batch record count does not equal included clearing records.");
        if (fileBytes.Length == 0) errors.Add("Settlement file is empty.");
        if (string.IsNullOrWhiteSpace(batch.BatchReference)) errors.Add("Batch reference is mandatory.");
        if (batch.BusinessDate == default) errors.Add("Business date is mandatory.");
        if (string.IsNullOrWhiteSpace(batch.CurrencyCode)) errors.Add("Settlement currency is mandatory.");

        foreach (var r in records.Where(r => r.IsIncluded))
        {
            if (string.IsNullOrWhiteSpace(r.Rrn)) errors.Add($"RRN missing for correlation {r.CorrelationId}.");
            if (string.IsNullOrWhiteSpace(r.Stan)) errors.Add($"STAN missing for correlation {r.CorrelationId}.");
            if (r.TransactionAmount <= 0m) errors.Add($"Invalid amount for correlation {r.CorrelationId}.");
            if (string.IsNullOrWhiteSpace(r.CurrencyCode)) errors.Add($"Currency missing for correlation {r.CorrelationId}.");
            if (string.IsNullOrWhiteSpace(r.MaskedPan)) warnings.Add($"Masked PAN missing for correlation {r.CorrelationId}; file may be rejected by scheme certification.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(fileBytes));
        warnings.Add($"SHA256={hash}; retain this value with scheme acknowledgement for RBI/NPCI audit evidence.");
        warnings.Add("Certification-ready validation completed. Final certification still depends on scheme host/file certification and bank/NPCI/Visa/Mastercard sign-off.");

        var valid = errors.Count == 0;
        return Task.FromResult(new SettlementValidationResult
        {
            IsValid = valid,
            Status = valid ? SettlementCertificationStatus.CertificationReady : SettlementCertificationStatus.Rejected,
            Errors = errors,
            Warnings = warnings
        });
    }
}
