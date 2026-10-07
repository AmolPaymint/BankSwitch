using System.Security.Cryptography;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class TokenizationService : ITokenizationService
{
    private readonly ITokenizationRepository _repository;
    private readonly ISensitiveDataProtector _protector;
    private readonly ISecretProvider _secrets;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly TokenizationOptions _options;

    public TokenizationService(ITokenizationRepository repository, ISensitiveDataProtector protector, ISecretProvider secrets, IAuditLogger audit, IClock clock, TokenizationOptions options)
    {
        _repository = repository;
        _protector = protector;
        _secrets = secrets;
        _audit = audit;
        _clock = clock;
        _options = options;
    }

    public async Task<CmsOperationResult<CardToken>> TokenizeAsync(TokenizeCardRequest request, CancellationToken cancellationToken = default)
    {
        var pan = (request.FullPan ?? string.Empty).Trim();
        if (pan.Length is < 12 or > 19 || !pan.All(char.IsDigit) || !LuhnValidator.IsValid(pan))
            return CmsOperationResult<CardToken>.Fail("14", "PAN is invalid.");
        if (request.ExpiryMonth is < 1 or > 12)
            return CmsOperationResult<CardToken>.Fail("30", "Expiry month must be between 1 and 12.");
        if (request.ExpiryYear < _clock.UtcNow.Year)
            return CmsOperationResult<CardToken>.Fail("54", "Expiration date is in the past.");
        if (string.IsNullOrWhiteSpace(request.MerchantId))
            return CmsOperationResult<CardToken>.Fail("30", "Merchant ID is required.");

        var lookupKey = await _secrets.GetSecretAsync("PanLookupHmacKey", cancellationToken).ConfigureAwait(false);
        var panHash = CardholderDataProtector.HashForLookup(pan, lookupKey);

        var existing = await _repository.GetActiveTokenByPanHashAsync(panHash, request.MerchantId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return CmsOperationResult<CardToken>.Success(existing, "Existing active token returned for this card and merchant.");
        }

        var token = new CardToken
        {
            Token = await GenerateUniqueTokenAsync(cancellationToken).ConfigureAwait(false),
            MaskedPan = CardholderDataProtector.MaskPan(pan),
            PanToken = _protector.Protect(pan, "PAN"),
            PanHash = panHash,
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            MerchantId = request.MerchantId.Trim(),
            SourceNodeId = (request.SourceNodeId ?? string.Empty).Trim(),
            Status = CardTokenStatus.Active,
            CreatedAt = _clock.UtcNow
        };

        await _repository.AddTokenAsync(token, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(request.CorrelationId, "system", "TokenizeCard", string.Empty, $"token={token.Token};maskedPan={token.MaskedPan};merchant={token.MerchantId}", "Card-on-file tokenization", string.Empty);
        return CmsOperationResult<CardToken>.Success(token, "Token created.");
    }

    public async Task<CmsOperationResult<TokenDetails>> DetokenizeAsync(string token, string merchantId, CancellationToken cancellationToken = default)
    {
        var record = await _repository.GetTokenAsync(token, cancellationToken).ConfigureAwait(false);
        if (record is null) return CmsOperationResult<TokenDetails>.Fail("25", "Token not found.");
        if (!string.Equals(record.MerchantId, merchantId, StringComparison.OrdinalIgnoreCase))
            return CmsOperationResult<TokenDetails>.Fail("58", "Token does not belong to this merchant.");
        if (record.Status == CardTokenStatus.Deleted) return CmsOperationResult<TokenDetails>.Fail("54", "Token has been deleted.");
        if (record.Status == CardTokenStatus.Suspended) return CmsOperationResult<TokenDetails>.Fail("62", "Token is suspended.");

        var updated = record with { LastUsedAt = _clock.UtcNow };
        await _repository.UpdateTokenAsync(updated, cancellationToken).ConfigureAwait(false);

        return CmsOperationResult<TokenDetails>.Success(new TokenDetails(updated.Token, updated.MaskedPan, updated.PanToken, updated.ExpiryMonth, updated.ExpiryYear, updated.Status));
    }

    public Task<CmsOperationResult<CardToken>> SuspendTokenAsync(string token, string actor, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(token, CardTokenStatus.Suspended, "SuspendCardToken", actor, cancellationToken);

    public Task<CmsOperationResult<CardToken>> ResumeTokenAsync(string token, string actor, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(token, CardTokenStatus.Active, "ResumeCardToken", actor, cancellationToken);

    public Task<CmsOperationResult<CardToken>> DeleteTokenAsync(string token, string actor, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(token, CardTokenStatus.Deleted, "DeleteCardToken", actor, cancellationToken);

    public Task<IReadOnlyList<CardToken>> GetTokensAsync(CancellationToken cancellationToken = default)
        => _repository.GetTokensAsync(cancellationToken);

    private async Task<CmsOperationResult<CardToken>> ChangeStatusAsync(string token, CardTokenStatus newStatus, string action, string actor, CancellationToken cancellationToken)
    {
        var record = await _repository.GetTokenAsync(token, cancellationToken).ConfigureAwait(false);
        if (record is null) return CmsOperationResult<CardToken>.Fail("25", "Token not found.");
        if (record.Status == CardTokenStatus.Deleted)
            return CmsOperationResult<CardToken>.Fail("54", "Token has been deleted and cannot be changed.");
        if (record.Status == newStatus)
            return CmsOperationResult<CardToken>.Success(record, "No change.");

        var updated = record with { Status = newStatus };
        await _repository.UpdateTokenAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, action, record.Status.ToString(), newStatus.ToString(), $"Token {updated.Token}", string.Empty);
        return CmsOperationResult<CardToken>.Success(updated);
    }

    private async Task<string> GenerateUniqueTokenAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var candidate = GenerateTokenCandidate();
            if (!await _repository.TokenExistsAsync(candidate, cancellationToken).ConfigureAwait(false))
                return candidate;
        }
        throw new InvalidOperationException("Unable to generate a unique card token after multiple attempts.");
    }

    private string GenerateTokenCandidate()
    {
        var prefix = _options.TokenBinPrefix;
        var bodyLength = _options.TokenLength - prefix.Length - 1;
        if (bodyLength < 1) throw new InvalidOperationException("TokenLength must be longer than TokenBinPrefix plus one check digit.");

        Span<byte> randomBytes = stackalloc byte[bodyLength];
        RandomNumberGenerator.Fill(randomBytes);
        var body = new char[bodyLength];
        for (var i = 0; i < bodyLength; i++) body[i] = (char)('0' + randomBytes[i] % 10);

        var withoutCheckDigit = prefix + new string(body);
        return withoutCheckDigit + LuhnValidator.ComputeCheckDigit(withoutCheckDigit);
    }
}
