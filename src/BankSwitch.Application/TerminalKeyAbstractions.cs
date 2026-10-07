using BankSwitch.Domain;

namespace BankSwitch.Application;

/// <summary>Persistence for registered terminals and their current key-derivation state.</summary>
public interface ITerminalKeyRepository
{
    Task AddTerminalAsync(TerminalKeyProfile profile, CancellationToken cancellationToken = default);
    Task<TerminalKeyProfile?> GetTerminalAsync(string terminalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TerminalKeyProfile>> GetTerminalsAsync(CancellationToken cancellationToken = default);
    Task UpdateTerminalAsync(TerminalKeyProfile profile, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registers POS/ATM terminals and dynamically derives a fresh working/session key for each
/// transaction (DUKPT-style: a new key-serial-number is issued and the resulting key-check-value
/// is returned, while the derived key itself stays inside the HSM).
/// </summary>
public interface ITerminalKeyService
{
    Task<CmsOperationResult<TerminalKeyProfile>> RegisterTerminalAsync(RegisterTerminalRequest request, string actor, CancellationToken cancellationToken = default);
    Task<CmsOperationResult<TerminalKeyProfile>> GenerateSessionKeyAsync(string terminalId, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TerminalKeyProfile>> GetTerminalsAsync(CancellationToken cancellationToken = default);
    Task<TerminalKeyProfile?> GetTerminalAsync(string terminalId, CancellationToken cancellationToken = default);
}

public sealed record RegisterTerminalRequest(string TerminalId, string SourceNodeId, string KeyProfile);
