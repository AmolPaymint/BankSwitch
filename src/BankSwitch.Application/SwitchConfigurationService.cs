using System.Text.Json;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class SwitchConfigurationService : ISwitchConfigurationService
{
    private readonly ISwitchConfigurationRepository _repository;
    private readonly IAuditLogger _audit;

    private static readonly JsonSerializerOptions AuditJsonOptions = new() { WriteIndented = false };

    public SwitchConfigurationService(ISwitchConfigurationRepository repository, IAuditLogger audit)
    {
        _repository = repository;
        _audit = audit;
    }

    // ---------------------------------------------------------------
    // Source nodes
    // ---------------------------------------------------------------

    public Task<IReadOnlyList<SourceNode>> GetSourceNodesAsync(CancellationToken cancellationToken = default)
        => _repository.GetSourceNodesAsync(cancellationToken);

    public Task<SourceNode?> GetSourceNodeAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetSourceNodeByIdAsync(id, cancellationToken);

    public async Task<CmsOperationResult<SourceNode>> SaveSourceNodeAsync(Guid? id, SourceNodeInput input, string actor, CancellationToken cancellationToken = default)
    {
        var nodeId = NormalizeCode(input.NodeId);
        if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(input.Name))
            return CmsOperationResult<SourceNode>.Fail("30", "Node ID and name are required.");
        if (input.TpsLimit <= 0) return CmsOperationResult<SourceNode>.Fail("30", "TPS limit must be greater than zero.");
        if (input.MaxMessageBytes <= 0) return CmsOperationResult<SourceNode>.Fail("30", "Max message bytes must be greater than zero.");
        if (input.IdleTimeoutSeconds <= 0) return CmsOperationResult<SourceNode>.Fail("30", "Idle timeout must be greater than zero.");

        var existingByNodeId = (await _repository.GetSourceNodesAsync(cancellationToken).ConfigureAwait(false))
            .FirstOrDefault(x => string.Equals(x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        if (existingByNodeId is not null && (id is null || existingByNodeId.Id != id.Value))
            return CmsOperationResult<SourceNode>.Fail("94", $"A source node with NodeId '{nodeId}' already exists.");

        var institutionCode = await ResolveInstitutionCodeAsync(input.InstitutionCode, cancellationToken).ConfigureAwait(false);
        if (institutionCode is null)
            return CmsOperationResult<SourceNode>.Fail("58", "Selected institution was not found.");

        SourceNode? before = null;
        if (id.HasValue)
        {
            before = await _repository.GetSourceNodeByIdAsync(id.Value, cancellationToken).ConfigureAwait(false);
            if (before is null) return CmsOperationResult<SourceNode>.Fail("25", "Source node was not found.");
        }

        var node = new SourceNode
        {
            Id = id ?? Guid.NewGuid(),
            NodeId = nodeId,
            Name = input.Name.Trim(),
            IsActive = input.IsActive,
            Security = new NodeSecurityProfile
            {
                RequireMtls = input.RequireMtls,
                RequirePrivateNetwork = input.RequirePrivateNetwork,
                AllowedCidrs = ParseSet(input.AllowedCidrs),
                CertificateThumbprint = input.CertificateThumbprint?.Trim() ?? string.Empty
            },
            Limits = new NodeLimits
            {
                TpsLimit = input.TpsLimit,
                DailyAmountLimit = input.DailyAmountLimit,
                MaxMessageBytes = input.MaxMessageBytes,
                IdleTimeout = TimeSpan.FromSeconds(input.IdleTimeoutSeconds)
            },
            PermittedMtis = ParseSet(input.PermittedMtis),
            PermittedChannels = ParseSet(input.PermittedChannels),
            AllowedBinRanges = ParseSet(input.AllowedBinRanges),
            KeyProfile = input.KeyProfile?.Trim() ?? string.Empty,
            SettlementProfile = input.SettlementProfile?.Trim() ?? string.Empty,
            InstitutionCode = institutionCode
        };

        if (id.HasValue)
        {
            await _repository.UpdateSourceNodeAsync(node, cancellationToken).ConfigureAwait(false);
            Audit(actor, "UpdateSourceNode", before, node);
        }
        else
        {
            await _repository.AddSourceNodeAsync(node, cancellationToken).ConfigureAwait(false);
            Audit(actor, "CreateSourceNode", null, node);
        }

        return CmsOperationResult<SourceNode>.Success(node);
    }

    // ---------------------------------------------------------------
    // Sink nodes
    // ---------------------------------------------------------------

    public Task<IReadOnlyList<SinkNode>> GetSinkNodesAsync(CancellationToken cancellationToken = default)
        => _repository.GetSinkNodesAsync(cancellationToken);

    public async Task<SinkNode?> GetSinkNodeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var nodes = await _repository.GetSinkNodesAsync(cancellationToken).ConfigureAwait(false);
        return nodes.FirstOrDefault(x => x.Id == id);
    }

    public async Task<CmsOperationResult<SinkNode>> SaveSinkNodeAsync(Guid? id, SinkNodeInput input, string actor, CancellationToken cancellationToken = default)
    {
        var nodeId = NormalizeCode(input.NodeId);
        if (string.IsNullOrWhiteSpace(nodeId) || string.IsNullOrWhiteSpace(input.Name))
            return CmsOperationResult<SinkNode>.Fail("30", "Node ID and name are required.");
        if (string.IsNullOrWhiteSpace(input.Host)) return CmsOperationResult<SinkNode>.Fail("30", "Host is required.");
        if (input.Port is <= 0 or > 65535) return CmsOperationResult<SinkNode>.Fail("30", "Port must be between 1 and 65535.");
        if (input.TpsLimit <= 0) return CmsOperationResult<SinkNode>.Fail("30", "TPS limit must be greater than zero.");
        if (input.MaxMessageBytes <= 0) return CmsOperationResult<SinkNode>.Fail("30", "Max message bytes must be greater than zero.");
        if (input.IdleTimeoutSeconds <= 0) return CmsOperationResult<SinkNode>.Fail("30", "Idle timeout must be greater than zero.");

        var sinks = await _repository.GetSinkNodesAsync(cancellationToken).ConfigureAwait(false);
        var existingByNodeId = sinks.FirstOrDefault(x => string.Equals(x.NodeId, nodeId, StringComparison.OrdinalIgnoreCase));
        if (existingByNodeId is not null && (id is null || existingByNodeId.Id != id.Value))
            return CmsOperationResult<SinkNode>.Fail("94", $"A sink node with NodeId '{nodeId}' already exists.");

        var institutionCode = await ResolveInstitutionCodeAsync(input.InstitutionCode, cancellationToken).ConfigureAwait(false);
        if (institutionCode is null)
            return CmsOperationResult<SinkNode>.Fail("58", "Selected institution was not found.");

        SinkNode? before = null;
        if (id.HasValue)
        {
            before = sinks.FirstOrDefault(x => x.Id == id.Value);
            if (before is null) return CmsOperationResult<SinkNode>.Fail("25", "Sink node was not found.");
        }

        var node = new SinkNode
        {
            Id = id ?? Guid.NewGuid(),
            NodeId = nodeId,
            Name = input.Name.Trim(),
            Host = input.Host.Trim(),
            Port = input.Port,
            IsActive = input.IsActive,
            Security = new NodeSecurityProfile
            {
                RequireMtls = input.RequireMtls,
                RequirePrivateNetwork = input.RequirePrivateNetwork,
                AllowedCidrs = ParseSet(input.AllowedCidrs),
                CertificateThumbprint = input.CertificateThumbprint?.Trim() ?? string.Empty
            },
            Limits = new NodeLimits
            {
                TpsLimit = input.TpsLimit,
                DailyAmountLimit = input.DailyAmountLimit,
                MaxMessageBytes = input.MaxMessageBytes,
                IdleTimeout = TimeSpan.FromSeconds(input.IdleTimeoutSeconds)
            },
            PermittedMtis = ParseSet(input.PermittedMtis),
            PermittedChannels = ParseSet(input.PermittedChannels),
            AllowedBinRanges = ParseSet(input.AllowedBinRanges),
            KeyProfile = input.KeyProfile?.Trim() ?? string.Empty,
            SettlementProfile = input.SettlementProfile?.Trim() ?? string.Empty,
            InstitutionCode = institutionCode
        };

        if (id.HasValue)
        {
            await _repository.UpdateSinkNodeAsync(node, cancellationToken).ConfigureAwait(false);
            Audit(actor, "UpdateSinkNode", before, node);
        }
        else
        {
            await _repository.AddSinkNodeAsync(node, cancellationToken).ConfigureAwait(false);
            Audit(actor, "CreateSinkNode", null, node);
        }

        return CmsOperationResult<SinkNode>.Success(node);
    }

    // ---------------------------------------------------------------
    // Routes
    // ---------------------------------------------------------------

    public Task<IReadOnlyList<RouteDefinition>> GetRoutesAsync(CancellationToken cancellationToken = default)
        => _repository.GetRoutesAsync(cancellationToken);

    public async Task<CmsOperationResult<RouteDefinition>> SaveRouteAsync(Guid? id, RouteInput input, string actor, CancellationToken cancellationToken = default)
    {
        var binPrefix = (input.BinPrefix ?? string.Empty).Trim();
        if (binPrefix.Length < 4 || binPrefix.Length > 12 || !binPrefix.All(char.IsDigit))
            return CmsOperationResult<RouteDefinition>.Fail("30", "BIN prefix must be 4-12 numeric digits.");

        var sinks = await _repository.GetSinkNodesAsync(cancellationToken).ConfigureAwait(false);
        if (!sinks.Any(x => x.Id == input.SinkNodeId))
            return CmsOperationResult<RouteDefinition>.Fail("58", "Selected sink node was not found.");

        var routes = await _repository.GetRoutesAsync(cancellationToken).ConfigureAwait(false);
        var normalizedCardRanges = ParseSet(input.CardRangePrefixes);
        var normalizedCountries = ParseSet(input.CountryCodes);
        var normalizedMccs = ParseSet(input.MerchantCategoryCodes);
        var normalizedCurrencies = ParseSet(input.CurrencyCodes);
        var normalizedDevices = ParseSet(input.DeviceCodes);
        var normalizedInterchanges = ParseSet(input.InterchangeCodes);
        var normalizedInstitutions = ParseSet(input.InstitutionCodes);
        var normalizedProducts = ParseSet(input.ProductCodes);
        var normalizedNetworks = ParseSet(input.NetworkCodes);
        var normalizedAccounts = ParseSet(input.AccountRanges);

        if (input.IsActive && routes.Any(x => x.IsActive
            && string.Equals(x.BinPrefix, binPrefix, StringComparison.Ordinal)
            && SetEquals(x.CardRangePrefixes, normalizedCardRanges)
            && SetEquals(x.CountryCodes, normalizedCountries)
            && SetEquals(x.MerchantCategoryCodes, normalizedMccs)
            && SetEquals(x.CurrencyCodes, normalizedCurrencies)
            && SetEquals(x.DeviceCodes, normalizedDevices)
            && SetEquals(x.InterchangeCodes, normalizedInterchanges)
            && SetEquals(x.InstitutionCodes, normalizedInstitutions)
            && SetEquals(x.ProductCodes, normalizedProducts)
            && SetEquals(x.NetworkCodes, normalizedNetworks)
            && SetEquals(x.AccountRanges, normalizedAccounts)
            && x.Id != id))
            return CmsOperationResult<RouteDefinition>.Fail("94", "An identical active route rule already exists.");

        RouteDefinition? before = null;
        if (id.HasValue)
        {
            before = routes.FirstOrDefault(x => x.Id == id.Value);
            if (before is null) return CmsOperationResult<RouteDefinition>.Fail("25", "Route was not found.");
        }

        var route = new RouteDefinition
        {
            Id = id ?? Guid.NewGuid(),
            BinPrefix = binPrefix,
            SinkNodeId = input.SinkNodeId,
            FallbackSinkNodeId = input.FallbackSinkNodeId,
            IsActive = input.IsActive,
            Priority = input.Priority,
            CountryCodes = normalizedCountries,
            MerchantCategoryCodes = normalizedMccs,
            CurrencyCodes = normalizedCurrencies,
            DeviceCodes = normalizedDevices,
            InterchangeCodes = normalizedInterchanges,
            CardRangePrefixes = normalizedCardRanges,
            InstitutionCodes = normalizedInstitutions,
            ProductCodes = normalizedProducts,
            NetworkCodes = normalizedNetworks,
            AccountRanges = normalizedAccounts
        };

        if (id.HasValue)
        {
            await _repository.UpdateRouteAsync(route, cancellationToken).ConfigureAwait(false);
            Audit(actor, "UpdateRoute", before, route);
        }
        else
        {
            await _repository.AddRouteAsync(route, cancellationToken).ConfigureAwait(false);
            Audit(actor, "CreateRoute", null, route);
        }

        return CmsOperationResult<RouteDefinition>.Success(route);
    }


    private static bool SetEquals(IReadOnlySet<string> left, IReadOnlySet<string> right)
        => left.SetEquals(right);

    // ---------------------------------------------------------------
    // Fees
    // ---------------------------------------------------------------

    public Task<IReadOnlyList<Fee>> GetFeesAsync(CancellationToken cancellationToken = default)
        => _repository.GetFeesAsync(cancellationToken);

    public async Task<CmsOperationResult<Fee>> SaveFeeAsync(Guid? id, FeeInput input, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return CmsOperationResult<Fee>.Fail("30", "Fee name is required.");
        if (input.FlatAmount < 0m || input.PercentageOfTransaction < 0m || input.Minimum < 0m || input.Maximum < 0m)
            return CmsOperationResult<Fee>.Fail("30", "Fee amounts cannot be negative.");
        if (input.Minimum > 0m && input.Maximum > 0m && input.Minimum > input.Maximum)
            return CmsOperationResult<Fee>.Fail("30", "Minimum fee cannot be greater than maximum fee.");

        var fees = await _repository.GetFeesAsync(cancellationToken).ConfigureAwait(false);
        Fee? before = null;
        if (id.HasValue)
        {
            before = fees.FirstOrDefault(x => x.Id == id.Value);
            if (before is null) return CmsOperationResult<Fee>.Fail("25", "Fee was not found.");
        }

        var fee = new Fee
        {
            Id = id ?? Guid.NewGuid(),
            Name = input.Name.Trim(),
            FlatAmount = input.FlatAmount,
            PercentageOfTransaction = input.PercentageOfTransaction,
            Minimum = input.Minimum,
            Maximum = input.Maximum,
            IsActive = input.IsActive
        };

        if (id.HasValue)
        {
            await _repository.UpdateFeeAsync(fee, cancellationToken).ConfigureAwait(false);
            Audit(actor, "UpdateFee", before, fee);
        }
        else
        {
            await _repository.AddFeeAsync(fee, cancellationToken).ConfigureAwait(false);
            Audit(actor, "CreateFee", null, fee);
        }

        return CmsOperationResult<Fee>.Success(fee);
    }

    // ---------------------------------------------------------------
    // Schemes
    // ---------------------------------------------------------------

    public Task<IReadOnlyList<Scheme>> GetSchemesAsync(CancellationToken cancellationToken = default)
        => _repository.GetSchemesAsync(cancellationToken);

    public Task<Scheme?> GetSchemeAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetSchemeByIdAsync(id, cancellationToken);

    public async Task<CmsOperationResult<Scheme>> SaveSchemeAsync(Guid? id, SchemeInput input, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return CmsOperationResult<Scheme>.Fail("30", "Scheme name is required.");

        var sources = await _repository.GetSourceNodesAsync(cancellationToken).ConfigureAwait(false);
        if (!sources.Any(x => x.Id == input.SourceNodeId))
            return CmsOperationResult<Scheme>.Fail("58", "Selected source node was not found.");

        var routes = await _repository.GetRoutesAsync(cancellationToken).ConfigureAwait(false);
        if (!routes.Any(x => x.Id == input.RouteId))
            return CmsOperationResult<Scheme>.Fail("58", "Selected route was not found.");

        var fees = await _repository.GetFeesAsync(cancellationToken).ConfigureAwait(false);
        var permissions = new List<TransactionPermission>();
        foreach (var permission in input.Permissions)
        {
            var transactionTypeCode = (permission.TransactionTypeCode ?? string.Empty).Trim().ToUpperInvariant();
            var channelCode = (permission.ChannelCode ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(transactionTypeCode) && string.IsNullOrEmpty(channelCode) && permission.FeeId == Guid.Empty)
                continue; // blank row, skip silently

            if (transactionTypeCode.Length is < 1 or > 2 || channelCode.Length is < 1 or > 2)
                return CmsOperationResult<Scheme>.Fail("30", "Transaction type and channel codes must be 1-2 characters.");
            if (!fees.Any(x => x.Id == permission.FeeId))
                return CmsOperationResult<Scheme>.Fail("58", $"Fee for permission {transactionTypeCode}/{channelCode} was not found.");
            if (permissions.Any(x => x.TransactionTypeCode == transactionTypeCode && x.ChannelCode == channelCode))
                return CmsOperationResult<Scheme>.Fail("94", $"Duplicate permission for transaction type {transactionTypeCode} and channel {channelCode}.");

            permissions.Add(new TransactionPermission(transactionTypeCode, channelCode, permission.FeeId));
        }

        Scheme? before = null;
        if (id.HasValue)
        {
            before = await _repository.GetSchemeByIdAsync(id.Value, cancellationToken).ConfigureAwait(false);
            if (before is null) return CmsOperationResult<Scheme>.Fail("25", "Scheme was not found.");
        }

        var scheme = new Scheme
        {
            Id = id ?? Guid.NewGuid(),
            Name = input.Name.Trim(),
            SourceNodeId = input.SourceNodeId,
            RouteId = input.RouteId,
            IsActive = input.IsActive,
            Permissions = permissions
        };

        if (id.HasValue)
        {
            await _repository.UpdateSchemeAsync(scheme, cancellationToken).ConfigureAwait(false);
            Audit(actor, "UpdateScheme", before, scheme);
        }
        else
        {
            await _repository.AddSchemeAsync(scheme, cancellationToken).ConfigureAwait(false);
            Audit(actor, "CreateScheme", null, scheme);
        }

        return CmsOperationResult<Scheme>.Success(scheme);
    }

    // ---------------------------------------------------------------
    // Institutions
    // ---------------------------------------------------------------

    public Task<IReadOnlyList<Institution>> GetInstitutionsAsync(CancellationToken cancellationToken = default)
        => _repository.GetInstitutionsAsync(cancellationToken);

    public Task<Institution?> GetInstitutionAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetInstitutionByIdAsync(id, cancellationToken);

    public async Task<CmsOperationResult<Institution>> SaveInstitutionAsync(Guid? id, InstitutionInput input, string actor, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(input.Code);
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(input.Name))
            return CmsOperationResult<Institution>.Fail("30", "Institution code and name are required.");
        if (!string.IsNullOrWhiteSpace(input.CountryCode) && input.CountryCode.Trim().Length != 2)
            return CmsOperationResult<Institution>.Fail("30", "Country code must be a 2-letter ISO code.");
        if (!string.IsNullOrWhiteSpace(input.DefaultCurrencyCode) && input.DefaultCurrencyCode.Trim().Length != 3)
            return CmsOperationResult<Institution>.Fail("30", "Currency code must be a 3-letter ISO code.");

        var existingByCode = await _repository.GetInstitutionByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        if (existingByCode is not null && (id is null || existingByCode.Id != id.Value))
            return CmsOperationResult<Institution>.Fail("94", $"An institution with code '{code}' already exists.");

        Institution? before = null;
        if (id.HasValue)
        {
            before = await _repository.GetInstitutionByIdAsync(id.Value, cancellationToken).ConfigureAwait(false);
            if (before is null) return CmsOperationResult<Institution>.Fail("25", "Institution was not found.");
        }

        var institution = new Institution
        {
            Id = id ?? Guid.NewGuid(),
            Code = code,
            Name = input.Name.Trim(),
            Type = input.Type,
            CountryCode = (input.CountryCode ?? string.Empty).Trim().ToUpperInvariant(),
            DefaultCurrencyCode = (input.DefaultCurrencyCode ?? string.Empty).Trim().ToUpperInvariant(),
            IsActive = input.IsActive
        };

        if (id.HasValue)
        {
            await _repository.UpdateInstitutionAsync(institution, cancellationToken).ConfigureAwait(false);
            Audit(actor, "UpdateInstitution", before, institution);
        }
        else
        {
            await _repository.AddInstitutionAsync(institution, cancellationToken).ConfigureAwait(false);
            Audit(actor, "CreateInstitution", null, institution);
        }

        return CmsOperationResult<Institution>.Success(institution);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    /// <summary>Normalizes and validates an optional institution code reference. Returns null if the code was supplied but not found.</summary>
    private async Task<string?> ResolveInstitutionCodeAsync(string? institutionCode, CancellationToken cancellationToken)
    {
        var code = NormalizeCode(institutionCode);
        if (string.IsNullOrEmpty(code)) return string.Empty;
        var institution = await _repository.GetInstitutionByCodeAsync(code, cancellationToken).ConfigureAwait(false);
        return institution is null ? null : institution.Code;
    }

    private void Audit<T>(string actor, string action, T? before, T after)
    {
        var oldValue = before is null ? string.Empty : JsonSerializer.Serialize(before, AuditJsonOptions);
        var newValue = JsonSerializer.Serialize(after, AuditJsonOptions);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, action, oldValue, newValue, "Configuration panel change", string.Empty);
    }

    private static string NormalizeCode(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static IReadOnlySet<string> ParseSet(string? value) => string.IsNullOrWhiteSpace(value)
        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        : value.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
