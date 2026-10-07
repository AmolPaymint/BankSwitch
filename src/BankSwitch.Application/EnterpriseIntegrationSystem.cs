using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace BankSwitch.Application;

public enum EnterpriseIntegrationSystem { FinacleCbs, GenericCbs, Esb, ApiManager, PaymentHub, Iph, Acs3Ds, Frm, DwhBi, Sms, Email, Whatsapp, Ivrs, Kyc, Crm, Treasury }
public enum EnterpriseIntegrationTransport { TcpIp, Rest, Soap, Mq, Sftp, File, EventStream, Simulator }
public enum EnterpriseIntegrationStatus { Draft, Active, Suspended, Failed }
public enum CbsPostingType { Debit, Credit, Reversal, Hold, HoldRelease, Fee, Tax, GlTransfer }
public enum EnterpriseFeedStatus { Queued, Generated, Sent, Acknowledged, Failed }
public enum NotificationChannel {Sms,Email,Push,Webhook, Whatsapp, Ivrs}
public enum NotificationStatus {Queued,Pending,Sent,Failed,Suppressed}

public sealed record EnterpriseConnectorProfile(
    Guid ConnectorId,
    string ConnectorCode,
    EnterpriseIntegrationSystem System,
    EnterpriseIntegrationTransport Transport,
    string Endpoint,
    string InstitutionId,
    string Environment,
    bool IsProduction,
    EnterpriseIntegrationStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, string> Settings);

public sealed record RegisterEnterpriseConnectorRequest(
    string ConnectorCode,
    EnterpriseIntegrationSystem System,
    EnterpriseIntegrationTransport Transport,
    string Endpoint,
    string InstitutionId,
    string Environment,
    bool IsProduction,
    IReadOnlyDictionary<string, string>? Settings);

public sealed record CbsAccountValidationRequest(string AccountNumber, string CustomerId, string CurrencyCode, string Channel, string CorrelationId);
public sealed record CbsAccountValidationResult(string AccountNumber, string CustomerId, bool IsValid, string AccountStatus, string AccountType, string CurrencyCode, decimal AvailableBalance, string ResponseCode, string Message, string AuditHash);

public sealed record CbsPostingRequest(string AccountNumber, string CurrencyCode, decimal Amount, CbsPostingType PostingType, string TransactionReference, string Narration, string Channel, string CorrelationId, IReadOnlyDictionary<string, string>? Metadata);
public sealed record CbsPostingResult(string TransactionReference, string CbsReference, bool Accepted, string ResponseCode, string Message, string AuditHash, DateTimeOffset PostedAt);

public sealed record BalanceInquiryRequest(string AccountNumber, string CurrencyCode, string Channel, string CorrelationId);
public sealed record BalanceInquiryResult(string AccountNumber, string CurrencyCode, decimal LedgerBalance, decimal AvailableBalance, string ResponseCode, string AuditHash);

public sealed record MiniStatementRequest(string AccountNumber, int Count, string CorrelationId);
public sealed record MiniStatementEntry(DateTimeOffset PostedAt, string DrCr, decimal Amount, string CurrencyCode, string Narration, string Reference);
public sealed record MiniStatementResult(string AccountNumber, IReadOnlyList<MiniStatementEntry> Entries, string ResponseCode, string AuditHash);

public sealed record CardAccountLinkageRequest(string CustomerId, string CardMasked, string AccountNumber, string ProductCode, bool IsPrimary, string CorrelationId);
public sealed record CardAccountLinkageResult(Guid LinkageId, string CustomerId, string CardMasked, string AccountNumber, string ProductCode, bool IsPrimary, string Status, string AuditHash);

public sealed record CustomerKycSyncRequest(string CustomerId, string CustomerName, string Mobile, string Email, string KycStatus, DateTimeOffset KycUpdatedAt, string CorrelationId);
public sealed record CustomerKycSyncResult(string CustomerId, string KycStatus, bool Accepted, string ResponseCode, string AuditHash);

public sealed record PaymentHubInstructionRequest(string PaymentType, string DebitAccount, string CreditAccount, decimal Amount, string CurrencyCode, string Reference, string Channel, string CorrelationId);
public sealed record PaymentHubInstructionResult(string Reference, string HubReference, bool Accepted, string ResponseCode, string AuditHash);

public sealed record Acs3DsExchangeRequest(string CardMasked, string MerchantId, decimal Amount, string CurrencyCode, string TransactionId, string DeviceChannel, string CorrelationId);
public sealed record Acs3DsExchangeResult(string TransactionId, bool ChallengeRequired, string ThreeDsServerTransId, string AuthenticationValue, string Eci, string ResponseCode, string AuditHash);

public sealed record FrmRiskEventRequest(string TransactionReference, string CardMasked, string AccountNumber, string Channel, string EventType, decimal Amount, string CurrencyCode, int RiskScore, IReadOnlyDictionary<string, string>? Features, string CorrelationId);
public sealed record FrmRiskEventResult(string TransactionReference, bool Published, string FrmReference, string Action, string AuditHash);

public sealed record DwhBiFeedRequest(string FeedType, DateOnly BusinessDate, string OutputFormat, string RequestedBy);
public sealed record DwhBiFeedResult(Guid FeedId, string FeedType, DateOnly BusinessDate, string OutputFormat, EnterpriseFeedStatus Status, string FileName, string Sha256Hash, DateTimeOffset CreatedAt);

public sealed record NotificationAdapterRequest(NotificationChannel Channel, string Recipient, string TemplateCode, IReadOnlyDictionary<string, string> Parameters, string CorrelationId);
public sealed record NotificationAdapterResult(Guid NotificationId, NotificationChannel Channel, string RecipientMasked, NotificationStatus Status, string ProviderReference, string AuditHash);

public sealed record EnterpriseIntegrationDashboard(int Connectors, int ActiveConnectors, int CbsPostings, int Feeds, int Notifications, DateTimeOffset GeneratedAt);

public interface IEnterpriseIntegrationRepository
{
    Task SaveConnectorAsync(EnterpriseConnectorProfile profile, CancellationToken ct);
    Task<IReadOnlyList<EnterpriseConnectorProfile>> GetConnectorsAsync(EnterpriseIntegrationSystem? system, CancellationToken ct);
    Task<EnterpriseConnectorProfile?> GetActiveConnectorAsync(EnterpriseIntegrationSystem system, CancellationToken ct);
    Task SaveCbsPostingAsync(CbsPostingResult posting, CancellationToken ct);
    Task SaveCardLinkageAsync(CardAccountLinkageResult linkage, CancellationToken ct);
    Task SaveKycSyncAsync(CustomerKycSyncResult sync, CancellationToken ct);
    Task SavePaymentHubInstructionAsync(PaymentHubInstructionResult instruction, CancellationToken ct);
    Task SaveRiskEventAsync(FrmRiskEventResult riskEvent, CancellationToken ct);
    Task SaveFeedAsync(DwhBiFeedResult feed, CancellationToken ct);
    Task SaveNotificationAsync(NotificationAdapterResult notification, CancellationToken ct);
    Task<EnterpriseIntegrationDashboard> GetDashboardAsync(CancellationToken ct);
}

public interface IEnterpriseConnectorAdapter
{
    EnterpriseIntegrationSystem System { get; }
    Task<CbsAccountValidationResult> ValidateAccountAsync(EnterpriseConnectorProfile connector, CbsAccountValidationRequest request, CancellationToken ct);
    Task<CbsPostingResult> PostAsync(EnterpriseConnectorProfile connector, CbsPostingRequest request, CancellationToken ct);
    Task<BalanceInquiryResult> BalanceInquiryAsync(EnterpriseConnectorProfile connector, BalanceInquiryRequest request, CancellationToken ct);
    Task<MiniStatementResult> MiniStatementAsync(EnterpriseConnectorProfile connector, MiniStatementRequest request, CancellationToken ct);
    Task<PaymentHubInstructionResult> SendPaymentInstructionAsync(EnterpriseConnectorProfile connector, PaymentHubInstructionRequest request, CancellationToken ct);
    Task<Acs3DsExchangeResult> ExchangeAcs3DsAsync(EnterpriseConnectorProfile connector, Acs3DsExchangeRequest request, CancellationToken ct);
    Task<FrmRiskEventResult> PublishRiskEventAsync(EnterpriseConnectorProfile connector, FrmRiskEventRequest request, CancellationToken ct);
    Task<NotificationAdapterResult> SendNotificationAsync(EnterpriseConnectorProfile connector, NotificationAdapterRequest request, CancellationToken ct);
}

public interface IEnterpriseIntegrationService
{
    Task<EnterpriseIntegrationDashboard> GetDashboardAsync(CancellationToken ct);
    Task<IReadOnlyList<EnterpriseConnectorProfile>> GetConnectorsAsync(EnterpriseIntegrationSystem? system, CancellationToken ct);
    Task<CmsOperationResult<EnterpriseConnectorProfile>> RegisterConnectorAsync(RegisterEnterpriseConnectorRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<CbsAccountValidationResult>> ValidateAccountAsync(CbsAccountValidationRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<CbsPostingResult>> PostDebitCreditAsync(CbsPostingRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<BalanceInquiryResult>> BalanceInquiryAsync(BalanceInquiryRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<MiniStatementResult>> MiniStatementAsync(MiniStatementRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<CardAccountLinkageResult>> SyncCardAccountLinkageAsync(CardAccountLinkageRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<CustomerKycSyncResult>> SyncCustomerKycAsync(CustomerKycSyncRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<PaymentHubInstructionResult>> SendPaymentHubInstructionAsync(PaymentHubInstructionRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<Acs3DsExchangeResult>> ExchangeAcs3DsAsync(Acs3DsExchangeRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<FrmRiskEventResult>> PublishFrmRiskEventAsync(FrmRiskEventRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<DwhBiFeedResult>> GenerateDwhBiFeedAsync(DwhBiFeedRequest request, string actor, CancellationToken ct);
    Task<CmsOperationResult<NotificationAdapterResult>> SendNotificationAsync(NotificationAdapterRequest request, string actor, CancellationToken ct);
}

public sealed class InMemoryEnterpriseIntegrationRepository : IEnterpriseIntegrationRepository
{
    private readonly ConcurrentDictionary<Guid, EnterpriseConnectorProfile> _connectors = new();
    private readonly ConcurrentBag<CbsPostingResult> _postings = new();
    private readonly ConcurrentBag<CardAccountLinkageResult> _linkages = new();
    private readonly ConcurrentBag<CustomerKycSyncResult> _kyc = new();
    private readonly ConcurrentBag<PaymentHubInstructionResult> _payments = new();
    private readonly ConcurrentBag<FrmRiskEventResult> _risks = new();
    private readonly ConcurrentBag<DwhBiFeedResult> _feeds = new();
    private readonly ConcurrentBag<NotificationAdapterResult> _notifications = new();

    public Task SaveConnectorAsync(EnterpriseConnectorProfile profile, CancellationToken ct) { _connectors[profile.ConnectorId] = profile; return Task.CompletedTask; }
    public Task<IReadOnlyList<EnterpriseConnectorProfile>> GetConnectorsAsync(EnterpriseIntegrationSystem? system, CancellationToken ct) => Task.FromResult<IReadOnlyList<EnterpriseConnectorProfile>>(_connectors.Values.Where(x => system is null || x.System == system).OrderBy(x => x.System).ThenBy(x => x.ConnectorCode).ToList());
    public Task<EnterpriseConnectorProfile?> GetActiveConnectorAsync(EnterpriseIntegrationSystem system, CancellationToken ct) => Task.FromResult(_connectors.Values.FirstOrDefault(x => x.System == system && x.Status == EnterpriseIntegrationStatus.Active));
    public Task SaveCbsPostingAsync(CbsPostingResult posting, CancellationToken ct) { _postings.Add(posting); return Task.CompletedTask; }
    public Task SaveCardLinkageAsync(CardAccountLinkageResult linkage, CancellationToken ct) { _linkages.Add(linkage); return Task.CompletedTask; }
    public Task SaveKycSyncAsync(CustomerKycSyncResult sync, CancellationToken ct) { _kyc.Add(sync); return Task.CompletedTask; }
    public Task SavePaymentHubInstructionAsync(PaymentHubInstructionResult instruction, CancellationToken ct) { _payments.Add(instruction); return Task.CompletedTask; }
    public Task SaveRiskEventAsync(FrmRiskEventResult riskEvent, CancellationToken ct) { _risks.Add(riskEvent); return Task.CompletedTask; }
    public Task SaveFeedAsync(DwhBiFeedResult feed, CancellationToken ct) { _feeds.Add(feed); return Task.CompletedTask; }
    public Task SaveNotificationAsync(NotificationAdapterResult notification, CancellationToken ct) { _notifications.Add(notification); return Task.CompletedTask; }
    public Task<EnterpriseIntegrationDashboard> GetDashboardAsync(CancellationToken ct) => Task.FromResult(new EnterpriseIntegrationDashboard(_connectors.Count, _connectors.Values.Count(x => x.Status == EnterpriseIntegrationStatus.Active), _postings.Count, _feeds.Count, _notifications.Count, DateTimeOffset.UtcNow));
}

public sealed class EnterpriseIntegrationService : IEnterpriseIntegrationService
{
    private readonly IEnterpriseIntegrationRepository _repo;
    private readonly IReadOnlyDictionary<EnterpriseIntegrationSystem, IEnterpriseConnectorAdapter> _adapters;
    public EnterpriseIntegrationService(IEnterpriseIntegrationRepository repo, IEnumerable<IEnterpriseConnectorAdapter> adapters)
    {
        _repo = repo;
        _adapters = adapters.GroupBy(a => a.System).ToDictionary(g => g.Key, g => g.First());
    }

    public Task<EnterpriseIntegrationDashboard> GetDashboardAsync(CancellationToken ct) => _repo.GetDashboardAsync(ct);
    public Task<IReadOnlyList<EnterpriseConnectorProfile>> GetConnectorsAsync(EnterpriseIntegrationSystem? system, CancellationToken ct) => _repo.GetConnectorsAsync(system, ct);

    public async Task<CmsOperationResult<EnterpriseConnectorProfile>> RegisterConnectorAsync(RegisterEnterpriseConnectorRequest request, string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectorCode) || string.IsNullOrWhiteSpace(request.Endpoint)) return CmsOperationResult<EnterpriseConnectorProfile>.Fail("V39-01", "Connector code and endpoint are required.");
        var profile = new EnterpriseConnectorProfile(Guid.NewGuid(), request.ConnectorCode.Trim().ToUpperInvariant(), request.System, request.Transport, request.Endpoint.Trim(), request.InstitutionId.Trim(), request.Environment.Trim(), request.IsProduction, EnterpriseIntegrationStatus.Active, DateTimeOffset.UtcNow, request.Settings ?? new Dictionary<string, string>());
        await _repo.SaveConnectorAsync(profile, ct).ConfigureAwait(false);
        return CmsOperationResult<EnterpriseConnectorProfile>.Success(profile, "Enterprise connector registered.");
    }

    public async Task<CmsOperationResult<CbsAccountValidationResult>> ValidateAccountAsync(CbsAccountValidationRequest request, string actor, CancellationToken ct)
    {
        var adapter = await AdapterAsync(EnterpriseIntegrationSystem.FinacleCbs, ct).ConfigureAwait(false);
        var profile = await ProfileAsync(EnterpriseIntegrationSystem.FinacleCbs, ct).ConfigureAwait(false);
        return CmsOperationResult<CbsAccountValidationResult>.Success(await adapter.ValidateAccountAsync(profile, request, ct).ConfigureAwait(false));
    }

    public async Task<CmsOperationResult<CbsPostingResult>> PostDebitCreditAsync(CbsPostingRequest request, string actor, CancellationToken ct)
    {
        if (request.Amount <= 0) return CmsOperationResult<CbsPostingResult>.Fail("V39-02", "Posting amount must be positive.");
        var adapter = await AdapterAsync(EnterpriseIntegrationSystem.FinacleCbs, ct).ConfigureAwait(false);
        var profile = await ProfileAsync(EnterpriseIntegrationSystem.FinacleCbs, ct).ConfigureAwait(false);
        var result = await adapter.PostAsync(profile, request, ct).ConfigureAwait(false);
        await _repo.SaveCbsPostingAsync(result, ct).ConfigureAwait(false);
        return CmsOperationResult<CbsPostingResult>.Success(result, "CBS posting boundary executed.");
    }

    public async Task<CmsOperationResult<BalanceInquiryResult>> BalanceInquiryAsync(BalanceInquiryRequest request, string actor, CancellationToken ct)
    {
        var adapter = await AdapterAsync(EnterpriseIntegrationSystem.FinacleCbs, ct).ConfigureAwait(false);
        var profile = await ProfileAsync(EnterpriseIntegrationSystem.FinacleCbs, ct).ConfigureAwait(false);
        return CmsOperationResult<BalanceInquiryResult>.Success(await adapter.BalanceInquiryAsync(profile, request, ct).ConfigureAwait(false));
    }

    public async Task<CmsOperationResult<MiniStatementResult>> MiniStatementAsync(MiniStatementRequest request, string actor, CancellationToken ct)
    {
        var adapter = await AdapterAsync(EnterpriseIntegrationSystem.FinacleCbs, ct).ConfigureAwait(false);
        var profile = await ProfileAsync(EnterpriseIntegrationSystem.FinacleCbs, ct).ConfigureAwait(false);
        return CmsOperationResult<MiniStatementResult>.Success(await adapter.MiniStatementAsync(profile, request, ct).ConfigureAwait(false));
    }

    public async Task<CmsOperationResult<CardAccountLinkageResult>> SyncCardAccountLinkageAsync(CardAccountLinkageRequest request, string actor, CancellationToken ct)
    {
        var result = new CardAccountLinkageResult(Guid.NewGuid(), request.CustomerId, MaskCard(request.CardMasked), request.AccountNumber, request.ProductCode, request.IsPrimary, "Synchronized", Hash($"{request.CustomerId}|{request.AccountNumber}|{request.CorrelationId}"));
        await _repo.SaveCardLinkageAsync(result, ct).ConfigureAwait(false);
        return CmsOperationResult<CardAccountLinkageResult>.Success(result, "Card-account linkage synchronized.");
    }

    public async Task<CmsOperationResult<CustomerKycSyncResult>> SyncCustomerKycAsync(CustomerKycSyncRequest request, string actor, CancellationToken ct)
    {
        var result = new CustomerKycSyncResult(request.CustomerId, request.KycStatus, true, "00", Hash($"{request.CustomerId}|{request.KycStatus}|{request.CorrelationId}"));
        await _repo.SaveKycSyncAsync(result, ct).ConfigureAwait(false);
        return CmsOperationResult<CustomerKycSyncResult>.Success(result, "Customer/KYC sync accepted.");
    }

    public async Task<CmsOperationResult<PaymentHubInstructionResult>> SendPaymentHubInstructionAsync(PaymentHubInstructionRequest request, string actor, CancellationToken ct)
    {
        var adapter = await AdapterAsync(EnterpriseIntegrationSystem.PaymentHub, ct).ConfigureAwait(false);
        var profile = await ProfileAsync(EnterpriseIntegrationSystem.PaymentHub, ct).ConfigureAwait(false);
        var result = await adapter.SendPaymentInstructionAsync(profile, request, ct).ConfigureAwait(false);
        await _repo.SavePaymentHubInstructionAsync(result, ct).ConfigureAwait(false);
        return CmsOperationResult<PaymentHubInstructionResult>.Success(result, "Payment Hub/IPH instruction accepted.");
    }

    public async Task<CmsOperationResult<Acs3DsExchangeResult>> ExchangeAcs3DsAsync(Acs3DsExchangeRequest request, string actor, CancellationToken ct)
    {
        var adapter = await AdapterAsync(EnterpriseIntegrationSystem.Acs3Ds, ct).ConfigureAwait(false);
        var profile = await ProfileAsync(EnterpriseIntegrationSystem.Acs3Ds, ct).ConfigureAwait(false);
        return CmsOperationResult<Acs3DsExchangeResult>.Success(await adapter.ExchangeAcs3DsAsync(profile, request, ct).ConfigureAwait(false));
    }

    public async Task<CmsOperationResult<FrmRiskEventResult>> PublishFrmRiskEventAsync(FrmRiskEventRequest request, string actor, CancellationToken ct)
    {
        var adapter = await AdapterAsync(EnterpriseIntegrationSystem.Frm, ct).ConfigureAwait(false);
        var profile = await ProfileAsync(EnterpriseIntegrationSystem.Frm, ct).ConfigureAwait(false);
        var result = await adapter.PublishRiskEventAsync(profile, request, ct).ConfigureAwait(false);
        await _repo.SaveRiskEventAsync(result, ct).ConfigureAwait(false);
        return CmsOperationResult<FrmRiskEventResult>.Success(result, "FRM risk event published.");
    }

    public async Task<CmsOperationResult<DwhBiFeedResult>> GenerateDwhBiFeedAsync(DwhBiFeedRequest request, string actor, CancellationToken ct)
    {
        var raw = $"{request.FeedType}|{request.BusinessDate:yyyyMMdd}|{request.OutputFormat}|{actor}|{DateTimeOffset.UtcNow:O}";
        var hash = Hash(raw);
        var result = new DwhBiFeedResult(Guid.NewGuid(), request.FeedType, request.BusinessDate, request.OutputFormat, EnterpriseFeedStatus.Generated, $"{request.FeedType}_{request.BusinessDate:yyyyMMdd}.{request.OutputFormat.ToLowerInvariant()}", hash, DateTimeOffset.UtcNow);
        await _repo.SaveFeedAsync(result, ct).ConfigureAwait(false);
        return CmsOperationResult<DwhBiFeedResult>.Success(result, "DWH/BI feed generated.");
    }

    public async Task<CmsOperationResult<NotificationAdapterResult>> SendNotificationAsync(NotificationAdapterRequest request, string actor, CancellationToken ct)
    {
        var system = request.Channel switch { NotificationChannel.Sms => EnterpriseIntegrationSystem.Sms, NotificationChannel.Email => EnterpriseIntegrationSystem.Email, NotificationChannel.Whatsapp => EnterpriseIntegrationSystem.Whatsapp, NotificationChannel.Ivrs => EnterpriseIntegrationSystem.Ivrs, _ => EnterpriseIntegrationSystem.Sms };
        var adapter = await AdapterAsync(system, ct).ConfigureAwait(false);
        var profile = await ProfileAsync(system, ct).ConfigureAwait(false);
        var result = await adapter.SendNotificationAsync(profile, request, ct).ConfigureAwait(false);
        await _repo.SaveNotificationAsync(result, ct).ConfigureAwait(false);
        return CmsOperationResult<NotificationAdapterResult>.Success(result, "Notification adapter accepted message.");
    }

    private async Task<IEnterpriseConnectorAdapter> AdapterAsync(EnterpriseIntegrationSystem system, CancellationToken ct)
    {
        if (_adapters.TryGetValue(system, out var adapter)) return adapter;
        if (_adapters.TryGetValue(EnterpriseIntegrationSystem.GenericCbs, out var generic)) return generic;
        throw new InvalidOperationException($"No adapter registered for {system}.");
    }

    private async Task<EnterpriseConnectorProfile> ProfileAsync(EnterpriseIntegrationSystem system, CancellationToken ct)
    {
        var profile = await _repo.GetActiveConnectorAsync(system, ct).ConfigureAwait(false);
        if (profile is not null) return profile;
        return new EnterpriseConnectorProfile(Guid.Empty, $"{system}-SIM", system, EnterpriseIntegrationTransport.Simulator, "simulator://enterprise", "IOB", "UAT", false, EnterpriseIntegrationStatus.Active, DateTimeOffset.UtcNow, new Dictionary<string, string>());
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string MaskCard(string value) => value.Length <= 10 ? value : value[..6] + new string('X', Math.Max(0, value.Length - 10)) + value[^4..];
}

public abstract class SimulatedEnterpriseConnectorAdapter : IEnterpriseConnectorAdapter
{
    public abstract EnterpriseIntegrationSystem System { get; }
    public virtual Task<CbsAccountValidationResult> ValidateAccountAsync(EnterpriseConnectorProfile connector, CbsAccountValidationRequest request, CancellationToken ct) => Task.FromResult(new CbsAccountValidationResult(request.AccountNumber, request.CustomerId, !string.IsNullOrWhiteSpace(request.AccountNumber), "ACTIVE", "CASA", request.CurrencyCode, 250000m, "00", $"{System} account validation accepted.", EnterpriseIntegrationService.Hash($"{System}|VAL|{request.AccountNumber}|{request.CorrelationId}")));
    public virtual Task<CbsPostingResult> PostAsync(EnterpriseConnectorProfile connector, CbsPostingRequest request, CancellationToken ct) => Task.FromResult(new CbsPostingResult(request.TransactionReference, $"CBS{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}", true, "00", $"{System} posting accepted.", EnterpriseIntegrationService.Hash($"{System}|POST|{request.TransactionReference}|{request.Amount}"), DateTimeOffset.UtcNow));
    public virtual Task<BalanceInquiryResult> BalanceInquiryAsync(EnterpriseConnectorProfile connector, BalanceInquiryRequest request, CancellationToken ct) => Task.FromResult(new BalanceInquiryResult(request.AccountNumber, request.CurrencyCode, 275000m, 250000m, "00", EnterpriseIntegrationService.Hash($"{System}|BAL|{request.AccountNumber}|{request.CorrelationId}")));
    public virtual Task<MiniStatementResult> MiniStatementAsync(EnterpriseConnectorProfile connector, MiniStatementRequest request, CancellationToken ct)
    {
        var count = Math.Clamp(request.Count <= 0 ? 5 : request.Count, 1, 20);
        var entries = Enumerable.Range(1, count).Select(i => new MiniStatementEntry(DateTimeOffset.UtcNow.AddDays(-i), i % 2 == 0 ? "CR" : "DR", 100m * i, "INR", $"Simulated entry {i}", $"STMT{i:000000}")).ToList();
        return Task.FromResult(new MiniStatementResult(request.AccountNumber, entries, "00", EnterpriseIntegrationService.Hash($"{System}|STMT|{request.AccountNumber}|{request.CorrelationId}")));
    }
    public virtual Task<PaymentHubInstructionResult> SendPaymentInstructionAsync(EnterpriseConnectorProfile connector, PaymentHubInstructionRequest request, CancellationToken ct) => Task.FromResult(new PaymentHubInstructionResult(request.Reference, $"HUB{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}", true, "00", EnterpriseIntegrationService.Hash($"{System}|HUB|{request.Reference}")));
    public virtual Task<Acs3DsExchangeResult> ExchangeAcs3DsAsync(EnterpriseConnectorProfile connector, Acs3DsExchangeRequest request, CancellationToken ct) => Task.FromResult(new Acs3DsExchangeResult(request.TransactionId, request.Amount >= 5000m, Guid.NewGuid().ToString("N"), Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(request.TransactionId)))[..28], request.Amount >= 5000m ? "05" : "02", "00", EnterpriseIntegrationService.Hash($"{System}|ACS|{request.TransactionId}")));
    public virtual Task<FrmRiskEventResult> PublishRiskEventAsync(EnterpriseConnectorProfile connector, FrmRiskEventRequest request, CancellationToken ct) => Task.FromResult(new FrmRiskEventResult(request.TransactionReference, true, $"FRM{Guid.NewGuid():N}"[..18], request.RiskScore >= 85 ? "DECLINE" : request.RiskScore >= 60 ? "REVIEW" : "ALLOW", EnterpriseIntegrationService.Hash($"{System}|FRM|{request.TransactionReference}|{request.RiskScore}")));
    public virtual Task<NotificationAdapterResult> SendNotificationAsync(EnterpriseConnectorProfile connector, NotificationAdapterRequest request, CancellationToken ct) => Task.FromResult(new NotificationAdapterResult(Guid.NewGuid(), request.Channel, MaskRecipient(request.Recipient), NotificationStatus.Sent, $"NTF{Guid.NewGuid():N}"[..18], EnterpriseIntegrationService.Hash($"{System}|NTF|{request.Channel}|{request.CorrelationId}")));
    private static string MaskRecipient(string value) => value.Contains('@') ? value[0] + "***@" + value.Split('@').Last() : (value.Length <= 4 ? "****" : new string('X', value.Length - 4) + value[^4..]);
}

public sealed class FinacleCbsAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.FinacleCbs; }
public sealed class GenericCbsAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.GenericCbs; }
public sealed class EsbApiManagerAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.Esb; }
public sealed class PaymentHubAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.PaymentHub; }
public sealed class IphAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.Iph; }
public sealed class Acs3DsAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.Acs3Ds; }
public sealed class FrmAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.Frm; }
public sealed class SmsAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.Sms; }
public sealed class EmailAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.Email; }
public sealed class WhatsappAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.Whatsapp; }
public sealed class IvrsAdapter : SimulatedEnterpriseConnectorAdapter { public override EnterpriseIntegrationSystem System => EnterpriseIntegrationSystem.Ivrs; }
