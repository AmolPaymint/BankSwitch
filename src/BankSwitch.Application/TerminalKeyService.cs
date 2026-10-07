using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BankSwitch.Domain;

namespace BankSwitch.Application;

public sealed class TerminalKeyService : ITerminalKeyService
{
    private const int MaxKeySerialCounter = 0xFFFF;

    private readonly ITerminalKeyRepository _repository;
    private readonly INodeRepository _nodeRepository;
    private readonly IHsmClient _hsm;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    public TerminalKeyService(ITerminalKeyRepository repository, INodeRepository nodeRepository, IHsmClient hsm, IAuditLogger audit, IClock clock)
    {
        _repository = repository;
        _nodeRepository = nodeRepository;
        _hsm = hsm;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<TerminalKeyProfile>> RegisterTerminalAsync(RegisterTerminalRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var terminalId = (request.TerminalId ?? string.Empty).Trim().ToUpperInvariant();
        if (terminalId.Length is < 4 or > 16)
            return CmsOperationResult<TerminalKeyProfile>.Fail("30", "Terminal ID must be 4-16 characters.");
        if (string.IsNullOrWhiteSpace(request.KeyProfile))
            return CmsOperationResult<TerminalKeyProfile>.Fail("30", "Key profile is required.");

        var sourceNode = await _nodeRepository.GetSourceNodeAsync(request.SourceNodeId, cancellationToken).ConfigureAwait(false);
        if (sourceNode is null)
            return CmsOperationResult<TerminalKeyProfile>.Fail("58", "Source node was not found.");

        if (await _repository.GetTerminalAsync(terminalId, cancellationToken).ConfigureAwait(false) is not null)
            return CmsOperationResult<TerminalKeyProfile>.Fail("94", "Terminal is already registered.");

        var profile = new TerminalKeyProfile
        {
            TerminalId = terminalId,
            SourceNodeId = sourceNode.NodeId,
            KeyProfile = request.KeyProfile.Trim(),
            KeySerialNumber = string.Empty,
            KeyCheckValue = string.Empty,
            IsActive = true,
            CreatedAt = _clock.UtcNow
        };

        await _repository.AddTerminalAsync(profile, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "RegisterTerminal", string.Empty, $"terminal={profile.TerminalId};node={profile.SourceNodeId};keyProfile={profile.KeyProfile}", "Terminal key registration", string.Empty);
        return CmsOperationResult<TerminalKeyProfile>.Success(profile);
    }

    public async Task<CmsOperationResult<TerminalKeyProfile>> GenerateSessionKeyAsync(string terminalId, string actor, CancellationToken cancellationToken = default)
    {
        var normalized = (terminalId ?? string.Empty).Trim().ToUpperInvariant();
        var profile = await _repository.GetTerminalAsync(normalized, cancellationToken).ConfigureAwait(false);
        if (profile is null) return CmsOperationResult<TerminalKeyProfile>.Fail("25", "Terminal not found.");
        if (!profile.IsActive) return CmsOperationResult<TerminalKeyProfile>.Fail("62", "Terminal is inactive.");

        var nextKsn = NextKeySerialNumber(profile);
        if (nextKsn is null)
            return CmsOperationResult<TerminalKeyProfile>.Fail("96", "Key serial number exhausted; the terminal requires base-derivation-key re-injection.");

        var keyCheckValue = await _hsm.DeriveTerminalKeyCheckValueAsync(profile.KeyProfile, nextKsn, cancellationToken).ConfigureAwait(false);
        var updated = profile with { KeySerialNumber = nextKsn, KeyCheckValue = keyCheckValue, LastRotatedAt = _clock.UtcNow };
        await _repository.UpdateTerminalAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogAdminAudit(Guid.NewGuid().ToString("N"), actor, "GenerateTerminalSessionKey", profile.KeySerialNumber, $"ksn={updated.KeySerialNumber};kcv={updated.KeyCheckValue}", $"Terminal {updated.TerminalId}", string.Empty);
        return CmsOperationResult<TerminalKeyProfile>.Success(updated);
    }

    public Task<IReadOnlyList<TerminalKeyProfile>> GetTerminalsAsync(CancellationToken cancellationToken = default)
        => _repository.GetTerminalsAsync(cancellationToken);

    public Task<TerminalKeyProfile?> GetTerminalAsync(string terminalId, CancellationToken cancellationToken = default)
        => _repository.GetTerminalAsync((terminalId ?? string.Empty).Trim().ToUpperInvariant(), cancellationToken);

    /// <summary>
    /// Derives the next 20-hex-character key-serial-number: a 16-hex-character device identifier
    /// (a stable hash of the terminal ID) followed by a 4-hex-character transaction counter that
    /// increments on every call. Returns null once the counter is exhausted.
    /// </summary>
    private static string? NextKeySerialNumber(TerminalKeyProfile profile)
    {
        var devicePortion = DeriveDevicePortion(profile.TerminalId);
        var counter = 0;
        if (!string.IsNullOrWhiteSpace(profile.KeySerialNumber) && profile.KeySerialNumber.Length == 20)
        {
            _ = int.TryParse(profile.KeySerialNumber[16..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out counter);
        }

        counter++;
        if (counter > MaxKeySerialCounter) return null;
        return devicePortion + counter.ToString("X4", CultureInfo.InvariantCulture);
    }

    private static string DeriveDevicePortion(string terminalId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(terminalId));
        return Convert.ToHexString(hash)[..16];
    }
}
