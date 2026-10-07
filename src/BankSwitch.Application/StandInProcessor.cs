using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class StandInProcessor : IStandInProcessor
{
    private readonly IStandInRepository _repository;
    private readonly ICardNumberGenerator _cardNumberGenerator;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly StandInOptions _options;

    public StandInProcessor(
        IStandInRepository repository,
        ICardNumberGenerator cardNumberGenerator,
        IAuditLogger audit,
        IClock clock,
        StandInOptions options)
    {
        _repository = repository;
        _cardNumberGenerator = cardNumberGenerator;
        _audit = audit;
        _clock = clock;
        _options = options;
    }

    public async Task<StandInDecision> EvaluateAsync(
        IsoMessage request,
        SourceNode sourceNode,
        string cardPanHash,
        StandInProfile? profile,
        CancellationToken cancellationToken = default)
    {
        var correlationId = request.CorrelationId;

        // 1. Reject if stand-in is globally disabled
        if (!_options.Enabled)
        {
            return Decline("91", "Stand-in processing is disabled.", StandInApprovalBasis.NotApplicable);
        }

        // 2. Extract fields
        var processingCode = IsoFieldHelper.TryGetString(request, 3, out var pc) ? pc : string.Empty;
        var transactionType = processingCode.Length >= 2 ? processingCode[..2] : string.Empty;
        IsoFieldHelper.TryGetDecimalAmount(request, 4, out var amount);
        IsoFieldHelper.TryGetString(request, 49, out var currencyCode);
        var binPrefix = IsoFieldHelper.TryGetString(request, 2, out var pan) ? pan[..Math.Min(6, pan.Length)] : string.Empty;

        // 3. Check transaction type eligibility
        var effectiveProfile = profile;
        var eligibleTypes = effectiveProfile?.EligibleTransactionTypes ?? new HashSet<string> { "00" };
        if (!eligibleTypes.Contains(transactionType))
        {
            _audit.LogSystem(correlationId, $"Stand-in: declined — transaction type {transactionType} not eligible for stand-in.");
            return Decline("57", $"Transaction type {transactionType} is not eligible for stand-in approval.", StandInApprovalBasis.TransactionTypeNotEligible);
        }

        // 4. Check floor limit
        var floorLimit = effectiveProfile?.FloorLimitAmount ?? _options.GlobalFloorLimitAmount;
        if (amount > floorLimit)
        {
            _audit.LogSystem(correlationId, $"Stand-in: declined — amount {amount} exceeds floor limit {floorLimit}.");
            return Decline("13", $"Amount {amount} exceeds stand-in floor limit {floorLimit}.", StandInApprovalBasis.ExceedsFloorLimit);
        }

        // 5. Check velocity limit
        if (!string.IsNullOrWhiteSpace(cardPanHash) && effectiveProfile is not null)
        {
            var velocityWindow = effectiveProfile.VelocityWindow;
            var currentCount = await _repository.GetVelocityCountAsync(cardPanHash, binPrefix, velocityWindow, cancellationToken).ConfigureAwait(false);
            if (currentCount >= effectiveProfile.VelocityCountLimit)
            {
                _audit.LogSystem(correlationId, $"Stand-in: declined — velocity limit {effectiveProfile.VelocityCountLimit} exceeded (current={currentCount}) for PAN hash {cardPanHash[..8]}***.");
                return Decline("65", $"Stand-in velocity limit exceeded ({currentCount}/{effectiveProfile.VelocityCountLimit} within {velocityWindow.TotalHours}h).", StandInApprovalBasis.VelocityLimitExceeded);
            }

            // Increment before approving
            await _repository.IncrementVelocityAsync(cardPanHash, binPrefix, velocityWindow, cancellationToken).ConfigureAwait(false);
        }

        // 6. Approve stand-in
        var authCode = _cardNumberGenerator.GenerateAuthorizationCode();
        _audit.LogSystem(correlationId, $"Stand-in: APPROVED amount={amount} authCode={authCode} basis=BelowFloorLimit floorLimit={floorLimit} bin={binPrefix}");

        return new StandInDecision(
            IsApproved: true,
            ResponseCode: "00",
            DeclineReason: string.Empty,
            AuthorizationCode: authCode,
            ApprovedAmount: amount,
            Basis: StandInApprovalBasis.BelowFloorLimit);
    }

    public async Task<StandInProfile?> GetProfileForBinAsync(string binPrefix, CancellationToken cancellationToken = default)
    {
        // Try progressively shorter BIN prefixes (longest-match wins)
        for (var len = Math.Min(8, binPrefix.Length); len >= 4; len--)
        {
            var prefix = binPrefix[..len];
            var profile = await _repository.GetProfileByBinPrefixAsync(prefix, cancellationToken).ConfigureAwait(false);
            if (profile is not null && profile.IsActive)
                return profile;
        }
        // Fall back to global profile (empty BIN prefix)
        return await _repository.GetProfileByBinPrefixAsync(string.Empty, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<StandInProfile>> GetAllProfilesAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllProfilesAsync(cancellationToken);

    public async Task<CmsOperationResult<StandInProfile>> SaveProfileAsync(StandInProfileInput input, Guid? id, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.ProfileCode))
            return CmsOperationResult<StandInProfile>.Fail("30", "Profile code is required.");
        if (input.FloorLimitAmount < 0)
            return CmsOperationResult<StandInProfile>.Fail("30", "Floor limit must be non-negative.");

        var existing = id.HasValue ? await _repository.GetProfileAsync(id.Value, cancellationToken).ConfigureAwait(false) : null;

        var profile = new StandInProfile
        {
            Id = id ?? Guid.NewGuid(),
            ProfileCode = input.ProfileCode.Trim().ToUpperInvariant(),
            BinPrefix = (input.BinPrefix ?? string.Empty).Trim(),
            FloorLimitAmount = input.FloorLimitAmount,
            CurrencyCode = input.CurrencyCode.Trim(),
            VelocityCountLimit = Math.Max(1, input.VelocityCountLimit),
            VelocityWindow = TimeSpan.FromHours(Math.Max(1, input.VelocityWindowHours)),
            EligibleTransactionTypes = input.EligibleTransactionTypes,
            IsActive = input.IsActive,
            CreatedAt = existing?.CreatedAt ?? _clock.UtcNow
        };

        if (existing is null)
            await _repository.AddProfileAsync(profile, cancellationToken).ConfigureAwait(false);
        else
            await _repository.UpdateProfileAsync(profile, cancellationToken).ConfigureAwait(false);

        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, id.HasValue ? "UpdateStandInProfile" : "CreateStandInProfile",
            existing?.ProfileCode ?? string.Empty, profile.ProfileCode,
            $"floor={profile.FloorLimitAmount} bin={profile.BinPrefix} velocity={profile.VelocityCountLimit}", string.Empty);

        return CmsOperationResult<StandInProfile>.Success(profile);
    }

    private static StandInDecision Decline(string code, string reason, StandInApprovalBasis basis)
        => new(false, code, reason, string.Empty, 0m, basis);
}
