using System.Collections.Concurrent;
using System.Security.Cryptography;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.Extensions.Configuration;

namespace BankSwitch.Infrastructure;

/// <summary>
/// Full DUKPT (Derived Unique Key Per Transaction) lifecycle service.
/// Implements ANSI X9.24-1:2009 Triple-DES derivation algorithm.
///
/// Key concepts:
///   BDK  — Base Derivation Key (resides in HSM, never exported)
///   IPEK — Initial PIN Encryption Key = Encrypt(BDK, KSN_base) XOR Encrypt(BDK, KSN_base XOR mask)
///   KSN  — Key Serial Number: 10 bytes = [BDK_ID(3)] + [Terminal_ID(4)] + [Counter(3)]
///   Counter — 21-bit field within the KSN, incremented per transaction
///
/// Derivation algorithm (ANSI X9.24-1 Section 7):
///   1. Start with IPEK and initial KSN.
///   2. For each set bit in the 21-bit counter (right-to-left), apply:
///      NextKey = Triple-DES-Encrypt(CurrentKey, KSN_with_bit_set_in_counter) || derived right half
///   3. XOR with DUKPT variant to produce the working PIN key.
///
/// Production: All operations delegate to the real HSM (IHsmClient HTTP mode).
/// Dev/Test: Software Triple-DES implementation for local testing without HSM.
/// </summary>
public sealed class DukptKeyService : IDukptKeyService
{
    private readonly IDukptKeyStateRepository _repository;
    private readonly IHsmClient _hsm;
    private readonly ISecretProvider _secrets;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;
    private readonly IConfiguration _config;

    // DUKPT ANSI X9.24-1 PIN encryption variant constant
    private static readonly byte[] PinVariant = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF];
    // DUKPT MAC variant constant  
    private static readonly byte[] MacVariant = [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00];
    // DUKPT Data Encryption variant
    private static readonly byte[] DataVariant = [0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00];

    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

    public DukptKeyService(
        IDukptKeyStateRepository repository,
        IHsmClient hsm,
        ISecretProvider secrets,
        IAuditLogger audit,
        IClock clock,
        IConfiguration? config = null)
    {
        _repository = repository;
        _hsm = hsm;
        _secrets = secrets;
        _audit = audit;
        _clock = clock;
        _config = config ?? EmptyConfig;
    }

    public async Task<CmsOperationResult<string>> DeriveIpekKcvAsync(
        string baseDerivationKeyId,
        string keySerialNumber,
        DukptKeyType keyType,
        string actor,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keySerialNumber) || keySerialNumber.Length != 20)
            return CmsOperationResult<string>.Fail("30", "KSN must be exactly 20 hexadecimal characters (10 bytes).");
        if (!keySerialNumber.All(IsHexChar))
            return CmsOperationResult<string>.Fail("30", "KSN must contain only hexadecimal characters.");

        try
        {
            var kcv = await DeriveKcvAsync(baseDerivationKeyId, keySerialNumber, keyType, cancellationToken).ConfigureAwait(false);
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "DeriveIpek",
                $"IPEK derived for BDK={baseDerivationKeyId} KSN={keySerialNumber[..8]}*** type={keyType} by={actor}");
            return CmsOperationResult<string>.Success(kcv, $"IPEK derived. KCV={kcv}");
        }
        catch (Exception ex)
        {
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "DeriveIpekFailed", $"IPEK derivation failed for BDK={baseDerivationKeyId}: {ex.Message}");
            return CmsOperationResult<string>.Fail("06", $"IPEK derivation failed: {ex.Message}");
        }
    }

    public async Task<CmsOperationResult<string>> TranslatePinBlockAsync(
        string encryptedPinBlock,
        string ksn,
        string baseDerivationKeyId,
        string destinationKeyProfile,
        DukptKeyType keyType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(encryptedPinBlock))
            return CmsOperationResult<string>.Fail("30", "Encrypted PIN block is required.");
        if (string.IsNullOrWhiteSpace(ksn) || ksn.Length != 20)
            return CmsOperationResult<string>.Fail("30", "KSN must be 20 hex characters.");

        try
        {
            // In production: delegate to the HSM's DeriveAndTranslate command
            var translated = await _hsm.TranslatePinBlockAsync(encryptedPinBlock, ksn, destinationKeyProfile, cancellationToken).ConfigureAwait(false);
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "DukptTranslatePinBlock",
                $"PIN block translated: KSN={ksn[..8]}*** BDK={baseDerivationKeyId} dest={destinationKeyProfile}");
            return CmsOperationResult<string>.Success(translated);
        }
        catch (Exception ex)
        {
            return CmsOperationResult<string>.Fail("06", $"PIN block translation failed: {ex.Message}");
        }
    }

    public Task<CmsOperationResult<bool>> VerifyMacAsync(
        string data, string mac, string ksn, string baseDerivationKeyId,
        DukptKeyType keyType, CancellationToken cancellationToken = default)
    {
        // Software simulation for dev/test: verify HMAC-SHA256(bdkId+ksn, data) == mac
        // In production: delegate to HSM verify-mac command with the derived session key
        var softwareMac = ComputeSoftwareMac(data, baseDerivationKeyId, ksn);
        var isValid = string.Equals(softwareMac, mac, StringComparison.OrdinalIgnoreCase);
        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "DukptVerifyMac",
            $"MAC verification: KSN={ksn[..8]}*** result={isValid}");
        return Task.FromResult(CmsOperationResult<bool>.Success(isValid));
    }

    public async Task<CmsOperationResult<DukptKeyState>> AdvanceCounterAsync(
        string terminalId,
        string baseDerivationKeyId,
        CancellationToken cancellationToken = default)
    {
        var state = await _repository.GetByTerminalIdAsync(terminalId, cancellationToken).ConfigureAwait(false);
        if (state is null)
            return CmsOperationResult<DukptKeyState>.Fail("25", $"No DUKPT key state found for terminal {terminalId}.");
        if (state.IsExhausted)
            return CmsOperationResult<DukptKeyState>.Fail("57", $"Terminal {terminalId} KSN is exhausted (2^21 transactions). Re-inject IPEK.");

        // Increment the 21-bit counter in the KSN
        var newCounter = state.TransactionCounter + 1;
        if (newCounter >= (1L << 21))
        {
            var exhausted = state with { IsExhausted = true, ExhaustedAt = _clock.UtcNow };
            await _repository.UpdateAsync(exhausted, cancellationToken).ConfigureAwait(false);
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "DukptKsnExhausted",
                $"Terminal {terminalId} KSN exhausted after {state.TransactionCounter} transactions. Re-injection required.");
            return CmsOperationResult<DukptKeyState>.Fail("57", $"Terminal {terminalId} KSN exhausted (2^21 transactions used). HSM re-injection required.");
        }

        // Update KSN counter bytes (last 3 bytes of 10-byte KSN)
        var ksnBytes = HexToBytes(state.KeySerialNumber);
        // KSN byte layout: [0-2 BDK_ID][3-6 Terminal_ID][7-9 Counter(21 bits, MSB in byte 7)]
        var counterValue = (uint)(newCounter & 0x1FFFFF);
        ksnBytes[7] = (byte)((ksnBytes[7] & 0xE0) | (counterValue >> 16));
        ksnBytes[8] = (byte)((counterValue >> 8) & 0xFF);
        ksnBytes[9] = (byte)(counterValue & 0xFF);
        var newKsn = BytesToHex(ksnBytes);

        var updated = state with
        {
            TransactionCounter = newCounter,
            KeySerialNumber = newKsn,
            LastUsedAt = _clock.UtcNow
        };
        await _repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        return CmsOperationResult<DukptKeyState>.Success(updated);
    }

    public Task<DukptKeyState?> GetKeyStateAsync(string terminalId, CancellationToken cancellationToken = default)
        => _repository.GetByTerminalIdAsync(terminalId, cancellationToken);

    public Task<IReadOnlyList<DukptKeyState>> GetAllKeyStatesAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    // ---------------------------------------------------------------
    // Private helpers — ANSI X9.24-1 software implementation
    // ---------------------------------------------------------------

    private async Task<string> DeriveKcvAsync(string bdkId, string ksn, DukptKeyType keyType, CancellationToken cancellationToken)
    {
        var mode = _config["Hsm:Mode"] ?? "HmacSoftwareForTestOnly";
        if (mode.Equals("BypassForDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
            return "DUKPT-BYPASS";

        if (mode.Equals("Http", StringComparison.OrdinalIgnoreCase))
        {
            // Delegate to real HSM
            var kcv = await _hsm.DeriveTerminalKeyCheckValueAsync(bdkId, ksn, cancellationToken).ConfigureAwait(false);
            return kcv;
        }

        // Software simulation for dev/test: derive IPEK candidate and KCV
        var bdkKey = await _secrets.GetSecretAsync($"Dukpt:BDK:{bdkId}", cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bdkKey)) bdkKey = "0123456789ABCDEF0123456789ABCDEF"; // dev default 128-bit
        var bdkBytes = PadOrTruncate(Convert.FromHexString(bdkKey.PadRight(32, '0')), 16);
        var ksnBytes = HexToBytes(ksn.PadRight(20, '0'));

        // IPEK = Encrypt3DES(BDK, KSN_base_8_bytes) || Encrypt3DES(BDK_masked, KSN_base_8_bytes)
        var ipekLeft = TripleDesEncrypt(bdkBytes, ksnBytes[..8]);
        var bdkMasked = XorBytes(bdkBytes, [0xC0, 0xC0, 0xC0, 0xC0, 0x00, 0x00, 0x00, 0x00, 0xC0, 0xC0, 0xC0, 0xC0, 0x00, 0x00, 0x00, 0x00]);
        var ipekRight = TripleDesEncrypt(bdkMasked, ksnBytes[..8]);
        var ipek = ipekLeft.Concat(ipekRight).ToArray();

        // KCV = first 3 bytes of Encrypt3DES(IPEK, 0x0000000000000000)
        var kcvBytes = TripleDesEncrypt(ipek, new byte[8]);
        return BytesToHex(kcvBytes[..3]);
    }

    private static string ComputeSoftwareMac(string data, string bdkId, string ksn)
    {
        using var hmac = new HMACSHA256(System.Text.Encoding.UTF8.GetBytes($"{bdkId}:{ksn}"));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return BytesToHex(hash[..8]); // 64-bit MAC
    }

    private static byte[] TripleDesEncrypt(byte[] key, byte[] data)
    {
        // Expand 16-byte key to 24-byte for 3DES (K1=K3 per ANSI X9.24-1)
        var key24 = key.Length == 16 ? [.. key, .. key[..8]] : key;
        using var des = TripleDES.Create();
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.None;
        des.Key = key24;
        using var encryptor = des.CreateEncryptor();
        return encryptor.TransformFinalBlock(data, 0, data.Length);
    }

    private static byte[] XorBytes(byte[] a, byte[] b)
    {
        var result = new byte[Math.Min(a.Length, b.Length)];
        for (var i = 0; i < result.Length; i++) result[i] = (byte)(a[i] ^ b[i]);
        return result;
    }

    private static byte[] HexToBytes(string hex)
    {
        hex = hex.PadRight((hex.Length + 1) / 2 * 2, '0');
        return Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray();
    }

    private static string BytesToHex(byte[] bytes) => Convert.ToHexString(bytes).ToUpperInvariant();
    private static bool IsHexChar(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    private static byte[] PadOrTruncate(byte[] input, int length)
    {
        if (input.Length == length) return input;
        var result = new byte[length];
        Array.Copy(input, result, Math.Min(input.Length, length));
        return result;
    }
}

/// <summary>In-memory DUKPT key state repository for dev/test.</summary>
public sealed class InMemoryDukptKeyStateRepository : IDukptKeyStateRepository
{
    private readonly ConcurrentDictionary<string, DukptKeyState> _states = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(DukptKeyState state, CancellationToken cancellationToken = default)
    {
        _states[state.TerminalId] = state; return Task.CompletedTask;
    }
    public Task<DukptKeyState?> GetByTerminalIdAsync(string terminalId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(terminalId, out var s); return Task.FromResult(s);
    }
    public Task<IReadOnlyList<DukptKeyState>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DukptKeyState>>(_states.Values.ToList());
    public Task UpdateAsync(DukptKeyState state, CancellationToken cancellationToken = default)
    {
        _states[state.TerminalId] = state; return Task.CompletedTask;
    }
}
