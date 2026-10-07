using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>
/// Interchange fee rule engine. Rules are scheme-bulletin driven and date effective.
/// This is used by clearing to calculate exact interchange, scheme and settlement fees
/// before Visa/Mastercard/RuPay/NPCI files are generated.
/// </summary>
public interface IInterchangeFeeRuleEngine
{
    Task<CmsOperationResult<InterchangeFeeCalculation>> CalculateAsync(
        SettlementNetwork network,
        ClearingRecord record,
        InterchangeFeeContext context,
        CancellationToken cancellationToken = default);
}

public sealed record InterchangeFeeContext(
    DateOnly BusinessDate,
    string ProductCode,
    string ChannelCode,
    string MerchantCategoryCode,
    string CountryCode,
    string CurrencyCode,
    string TransactionTypeCode);

public interface IInterchangeFeeRuleRepository
{
    Task<IReadOnlyList<InterchangeFeeRule>> GetActiveRulesAsync(SettlementNetwork network, DateOnly businessDate, CancellationToken cancellationToken = default);
    Task UpsertRuleAsync(InterchangeFeeRule rule, CancellationToken cancellationToken = default);
}

/// <summary>Validates settlement files against network and RBI/NPCI controls before transmission.</summary>
public interface ISettlementCertificationService
{
    Task<SettlementValidationResult> ValidateAsync(ClearingBatch batch, IReadOnlyList<ClearingRecord> records, byte[] fileBytes, CancellationToken cancellationToken = default);
}

/// <summary>Persists certification and transmission evidence for audit and regulator inspection.</summary>
public interface INetworkSettlementRunRepository
{
    Task AddRunAsync(NetworkSettlementRun run, CancellationToken cancellationToken = default);
    Task<NetworkSettlementRun?> GetRunByBatchAsync(Guid clearingBatchId, CancellationToken cancellationToken = default);
    Task UpdateRunAsync(NetworkSettlementRun run, CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstract gateway for real card-network settlement channels.
/// Production implementations should connect to Visa settlement SFTP, Mastercard File Express,
/// NPCI/RuPay/NFS settlement channels, or bank middleware.
/// </summary>
public interface INetworkSettlementGateway
{
    SettlementNetwork Network { get; }
    Task<NetworkSettlementTransmissionResult> SubmitAsync(ClearingBatch batch, byte[] fileBytes, string fileName, CancellationToken cancellationToken = default);
}
