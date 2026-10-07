using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>
/// CRUD persistence for core EFT switch configuration: source/sink nodes, BIN routing,
/// fees, and schemes (transaction-type/channel/fee permission sets). Implemented by
/// <c>InMemorySwitchStore</c> and <c>SqlSwitchRepository</c> so the admin configuration
/// panel works against either repository backend.
/// </summary>
public interface ISwitchConfigurationRepository
{
    Task<IReadOnlyList<SourceNode>> GetSourceNodesAsync(CancellationToken cancellationToken = default);
    Task<SourceNode?> GetSourceNodeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddSourceNodeAsync(SourceNode node, CancellationToken cancellationToken = default);
    Task UpdateSourceNodeAsync(SourceNode node, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SinkNode>> GetSinkNodesAsync(CancellationToken cancellationToken = default);
    Task AddSinkNodeAsync(SinkNode node, CancellationToken cancellationToken = default);
    Task UpdateSinkNodeAsync(SinkNode node, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RouteDefinition>> GetRoutesAsync(CancellationToken cancellationToken = default);
    Task<RouteDefinition?> GetRouteByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddRouteAsync(RouteDefinition route, CancellationToken cancellationToken = default);
    Task UpdateRouteAsync(RouteDefinition route, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Fee>> GetFeesAsync(CancellationToken cancellationToken = default);
    Task AddFeeAsync(Fee fee, CancellationToken cancellationToken = default);
    Task UpdateFeeAsync(Fee fee, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Scheme>> GetSchemesAsync(CancellationToken cancellationToken = default);
    Task<Scheme?> GetSchemeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddSchemeAsync(Scheme scheme, CancellationToken cancellationToken = default);
    Task UpdateSchemeAsync(Scheme scheme, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Institution>> GetInstitutionsAsync(CancellationToken cancellationToken = default);
    Task<Institution?> GetInstitutionByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Institution?> GetInstitutionByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddInstitutionAsync(Institution institution, CancellationToken cancellationToken = default);
    Task UpdateInstitutionAsync(Institution institution, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validated CRUD operations for the GUI configuration panel, on top of
/// <see cref="ISwitchConfigurationRepository"/>. Every mutation is audit-logged.
/// </summary>
public interface ISwitchConfigurationService
{
    Task<IReadOnlyList<SourceNode>> GetSourceNodesAsync(CancellationToken cancellationToken = default);
    Task<SourceNode?> GetSourceNodeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SourceNode>> SaveSourceNodeAsync(Guid? id, SourceNodeInput input, string actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SinkNode>> GetSinkNodesAsync(CancellationToken cancellationToken = default);
    Task<SinkNode?> GetSinkNodeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<SinkNode>> SaveSinkNodeAsync(Guid? id, SinkNodeInput input, string actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RouteDefinition>> GetRoutesAsync(CancellationToken cancellationToken = default);
    Task<CmsOperationResult<RouteDefinition>> SaveRouteAsync(Guid? id, RouteInput input, string actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Fee>> GetFeesAsync(CancellationToken cancellationToken = default);
    Task<CmsOperationResult<Fee>> SaveFeeAsync(Guid? id, FeeInput input, string actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Scheme>> GetSchemesAsync(CancellationToken cancellationToken = default);
    Task<Scheme?> GetSchemeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<Scheme>> SaveSchemeAsync(Guid? id, SchemeInput input, string actor, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Institution>> GetInstitutionsAsync(CancellationToken cancellationToken = default);
    Task<Institution?> GetInstitutionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<Institution>> SaveInstitutionAsync(Guid? id, InstitutionInput input, string actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Form-friendly input for a source node. Set-valued fields are accepted as
/// semicolon-or-comma-separated text and normalized by the service.
/// </summary>
public sealed record SourceNodeInput
{
    public string NodeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool RequireMtls { get; init; } = true;
    public bool RequirePrivateNetwork { get; init; } = true;
    public string AllowedCidrs { get; init; } = string.Empty;
    public string CertificateThumbprint { get; init; } = string.Empty;
    public int TpsLimit { get; init; } = 50;
    public decimal DailyAmountLimit { get; init; }
    public int MaxMessageBytes { get; init; } = 4096;
    public int IdleTimeoutSeconds { get; init; } = 65;
    public string PermittedMtis { get; init; } = string.Empty;
    public string PermittedChannels { get; init; } = string.Empty;
    public string AllowedBinRanges { get; init; } = string.Empty;
    public string KeyProfile { get; init; } = string.Empty;
    public string SettlementProfile { get; init; } = string.Empty;
    public string InstitutionCode { get; init; } = string.Empty;
}

/// <summary>Form-friendly input for a sink node (adds Host/Port to the source node fields).</summary>
public sealed record SinkNodeInput
{
    public string NodeId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 5001;
    public bool IsActive { get; init; }
    public bool RequireMtls { get; init; } = true;
    public bool RequirePrivateNetwork { get; init; } = true;
    public string AllowedCidrs { get; init; } = string.Empty;
    public string CertificateThumbprint { get; init; } = string.Empty;
    public int TpsLimit { get; init; } = 50;
    public decimal DailyAmountLimit { get; init; }
    public int MaxMessageBytes { get; init; } = 4096;
    public int IdleTimeoutSeconds { get; init; } = 65;
    public string PermittedMtis { get; init; } = string.Empty;
    public string PermittedChannels { get; init; } = string.Empty;
    public string AllowedBinRanges { get; init; } = string.Empty;
    public string KeyProfile { get; init; } = string.Empty;
    public string SettlementProfile { get; init; } = string.Empty;
    public string InstitutionCode { get; init; } = string.Empty;
}

public sealed record RouteInput(
    string BinPrefix,
    Guid SinkNodeId,
    bool IsActive,
    Guid? FallbackSinkNodeId = null,
    int Priority = 0,
    string CountryCodes = "",
    string MerchantCategoryCodes = "",
    string CurrencyCodes = "",
    string DeviceCodes = "",
    string InterchangeCodes = "",
    string CardRangePrefixes = "",
    string InstitutionCodes = "",
    string ProductCodes = "",
    string NetworkCodes = "",
    string AccountRanges = "");

public sealed record FeeInput(string Name, decimal FlatAmount, decimal PercentageOfTransaction, decimal Minimum, decimal Maximum, bool IsActive);

public sealed record SchemePermissionInput(string TransactionTypeCode, string ChannelCode, Guid FeeId);

public sealed record SchemeInput(string Name, Guid SourceNodeId, Guid RouteId, bool IsActive, IReadOnlyCollection<SchemePermissionInput> Permissions);

public sealed record InstitutionInput(string Code, string Name, InstitutionType Type, string CountryCode, string DefaultCurrencyCode, bool IsActive);
