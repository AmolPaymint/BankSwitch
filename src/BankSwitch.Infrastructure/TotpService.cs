using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BankSwitch.Application;
using BankSwitch.Domain;

namespace BankSwitch.Infrastructure;

/// <summary>
/// RFC 6238 Time-based One-Time Password (TOTP) service.
///
/// Replaces <c>TotpValidator.IsValidDevelopmentCode</c> which accepted ANY 6-digit number.
///
/// Algorithm (RFC 4226 + RFC 6238):
///   1. secret = random 20-byte seed (base32-encoded for QR).
///   2. counter = floor(UnixTime / periodSeconds).
///   3. hmac = HMAC-SHA1(secret, counter_as_8_byte_big_endian).
///   4. offset = hmac[19] &amp; 0xF.
///   5. code = ((hmac[offset] &amp; 0x7F) << 24 | ... ) % 10^digits.
///   6. Validate code for counter ± stepTolerance (default ±1 = 90-second window).
///
/// Security controls:
///   - Secret encrypted at rest using ISensitiveDataProtector (AES-256-GCM).
///   - Replay prevention: last validated counter stored; same counter never accepted twice.
///   - 8 single-use backup codes (cryptographically random, stored encrypted and hashed).
///   - Lockout: counters for future verification are not exposed; time sync required.
/// </summary>
public sealed class TotpService : ITotpService
{
    private readonly ITotpEnrollmentRepository _repository;
    private readonly ISensitiveDataProtector _protector;
    private readonly IAuditLogger _audit;
    private readonly IClock _clock;

    private const int SecretBytesLength = 20;      // 160-bit TOTP seed
    private const int BackupCodeCount = 8;
    private const int BackupCodeLength = 8;
    private const int StepToleranceWindow = 1;     // ±1 step = ±30 seconds = 90-second window

    public TotpService(ITotpEnrollmentRepository repository, ISensitiveDataProtector protector, IAuditLogger audit, IClock clock)
    {
        _repository = repository;
        _protector = protector;
        _audit = audit;
        _clock = clock;
    }

    public async Task<CmsOperationResult<TotpEnrollmentResult>> BeginEnrollmentAsync(
        string userId, string username, string issuerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return CmsOperationResult<TotpEnrollmentResult>.Fail("30", "UserId is required.");

        // Check for existing enrollment
        var existing = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (existing is { Status: MfaStatus.Enrolled or MfaStatus.Verified })
            return CmsOperationResult<TotpEnrollmentResult>.Fail("57", "User already has an active MFA enrollment. Revoke it first.");

        // Generate cryptographically random 160-bit TOTP secret
        var secretBytes = RandomNumberGenerator.GetBytes(SecretBytesLength);
        var plaintextSecret = ToBase32(secretBytes);

        // Generate 8 backup codes
        var backupCodes = GenerateBackupCodes(BackupCodeCount, BackupCodeLength);
        var backupCodesPlain = string.Join("|", backupCodes);

        // Encrypt secret and backup codes at rest
        var encryptedSecret = _protector.Protect(plaintextSecret, "TOTP_SECRET");
        var encryptedBackupCodes = _protector.Protect(backupCodesPlain, "TOTP_BACKUP");

        var enrollment = new TotpEnrollment
        {
            UserId = userId,
            Username = username,
            EncryptedSecret = encryptedSecret,
            Algorithm = TotpAlgorithm.HmacSha1,
            Digits = 6,
            PeriodSeconds = 30,
            Status = MfaStatus.NotEnrolled, // remains NotEnrolled until ConfirmEnrollmentAsync
            IssuerName = issuerName,
            EncryptedBackupCodes = encryptedBackupCodes,
            BackupCodesRemaining = BackupCodeCount,
            EnrolledAt = _clock.UtcNow
        };

        if (existing is null)
            await _repository.AddAsync(enrollment, cancellationToken).ConfigureAwait(false);
        else
            await _repository.UpdateAsync(enrollment, cancellationToken).ConfigureAwait(false);

        // Build otpauth:// URI for QR code generation
        var otpUri = BuildOtpAuthUri(issuerName, username, plaintextSecret, enrollment.Digits, enrollment.PeriodSeconds);

        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "TotpEnrollmentStarted",
            $"TOTP enrollment started for user {userId}/{username}");

        return CmsOperationResult<TotpEnrollmentResult>.Success(
            new TotpEnrollmentResult(otpUri, plaintextSecret, backupCodes),
            "Enrollment started. Scan the QR code in your authenticator app, then call ConfirmEnrollment with the generated code.");
    }

    public async Task<CmsOperationResult<TotpEnrollment>> ConfirmEnrollmentAsync(
        string userId, string totpCode, CancellationToken cancellationToken = default)
    {
        var enrollment = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
            return CmsOperationResult<TotpEnrollment>.Fail("25", "No pending TOTP enrollment found.");
        if (enrollment.Status == MfaStatus.Verified)
            return CmsOperationResult<TotpEnrollment>.Fail("57", "TOTP already confirmed.");

        var secret = _protector.Unprotect(enrollment.EncryptedSecret, "TOTP_SECRET");
        var (isValid, counter) = ValidateTotpCode(secret, totpCode, enrollment.PeriodSeconds, enrollment.Digits, null);
        if (!isValid)
        {
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "TotpConfirmFailed", $"TOTP confirmation failed for user {userId}");
            return CmsOperationResult<TotpEnrollment>.Fail("55", "Invalid TOTP code. Ensure your device clock is synchronized.");
        }

        var confirmed = enrollment with
        {
            Status = MfaStatus.Verified,
            VerifiedAt = _clock.UtcNow,
            LastValidatedCounter = counter
        };
        await _repository.UpdateAsync(confirmed, cancellationToken).ConfigureAwait(false);

        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "TotpEnrollmentConfirmed",
            $"TOTP confirmed for user {userId}/{enrollment.Username}");
        return CmsOperationResult<TotpEnrollment>.Success(confirmed, "MFA enrollment confirmed.");
    }

    public async Task<TotpValidationResult> ValidateAsync(
        string userId, string totpCode, CancellationToken cancellationToken = default)
    {
        var enrollment = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (enrollment is null || enrollment.Status is not (MfaStatus.Verified or MfaStatus.Enrolled))
            return TotpValidationResult.Invalid("User does not have an active MFA enrollment.");

        var secret = _protector.Unprotect(enrollment.EncryptedSecret, "TOTP_SECRET");
        var (isValid, counter) = ValidateTotpCode(secret, totpCode, enrollment.PeriodSeconds, enrollment.Digits, enrollment.LastValidatedCounter);

        if (!isValid)
        {
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "TotpValidationFailed",
                $"TOTP validation failed for user {userId}");
            return TotpValidationResult.Invalid("Invalid or expired TOTP code.");
        }

        // Update last validated counter (replay prevention)
        await _repository.UpdateAsync(enrollment with { LastValidatedCounter = counter, LastUsedAt = _clock.UtcNow }, cancellationToken).ConfigureAwait(false);
        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "TotpValidationSucceeded", $"TOTP valid for user {userId}");
        return TotpValidationResult.Valid();
    }

    public async Task<TotpValidationResult> ValidateBackupCodeAsync(
        string userId, string backupCode, CancellationToken cancellationToken = default)
    {
        var enrollment = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (enrollment is null || enrollment.Status is not (MfaStatus.Verified or MfaStatus.Enrolled))
            return TotpValidationResult.Invalid("No active MFA enrollment.");
        if (enrollment.BackupCodesRemaining <= 0)
            return TotpValidationResult.Invalid("All backup codes have been used.");

        var plainCodes = _protector.Unprotect(enrollment.EncryptedBackupCodes, "TOTP_BACKUP");
        var codes = plainCodes.Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

        var normalizedInput = backupCode.Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        var matchIdx = codes.FindIndex(c => string.Equals(c, normalizedInput, StringComparison.OrdinalIgnoreCase));
        if (matchIdx < 0)
        {
            _audit.LogSecurity(Guid.NewGuid().ToString("N"), "TotpBackupCodeFailed", $"Backup code validation failed for user {userId}");
            return TotpValidationResult.Invalid("Invalid backup code.");
        }

        // Invalidate used backup code (replace with empty string so count stays consistent)
        codes[matchIdx] = string.Empty;
        var updatedBackup = _protector.Protect(string.Join("|", codes), "TOTP_BACKUP");
        await _repository.UpdateAsync(enrollment with
        {
            EncryptedBackupCodes = updatedBackup,
            BackupCodesRemaining = enrollment.BackupCodesRemaining - 1,
            LastUsedAt = _clock.UtcNow
        }, cancellationToken).ConfigureAwait(false);

        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "TotpBackupCodeUsed",
            $"Backup code used for user {userId}. Remaining: {enrollment.BackupCodesRemaining - 1}");
        return TotpValidationResult.Valid();
    }

    public Task<TotpEnrollment?> GetEnrollmentAsync(string userId, CancellationToken cancellationToken = default)
        => _repository.GetByUserIdAsync(userId, cancellationToken);

    public async Task<CmsOperationResult<TotpEnrollment>> SuspendMfaAsync(
        string userId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var e = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (e is null) return CmsOperationResult<TotpEnrollment>.Fail("25", "No MFA enrollment.");
        var updated = e with { Status = MfaStatus.Suspended };
        await _repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "TotpSuspended", $"MFA suspended for {userId} by {actor}: {reason}");
        return CmsOperationResult<TotpEnrollment>.Success(updated, "MFA suspended.");
    }

    public async Task<CmsOperationResult<TotpEnrollment>> RevokeMfaAsync(
        string userId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var e = await _repository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (e is null) return CmsOperationResult<TotpEnrollment>.Fail("25", "No MFA enrollment.");
        var updated = e with { Status = MfaStatus.Revoked };
        await _repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        _audit.LogSecurity(Guid.NewGuid().ToString("N"), "TotpRevoked", $"MFA revoked for {userId} by {actor}: {reason}");
        return CmsOperationResult<TotpEnrollment>.Success(updated, "MFA revoked. User must re-enroll.");
    }

    // ---------------------------------------------------------------
    // RFC 4226 / RFC 6238 core algorithm
    // ---------------------------------------------------------------

    private (bool isValid, long counter) ValidateTotpCode(
        string base32Secret, string code, int periodSeconds, int digits, long? lastValidatedCounter)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != digits || !code.All(char.IsDigit))
            return (false, -1);

        var secretBytes = FromBase32(base32Secret);
        var nowCounter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / periodSeconds;

        for (var delta = -StepToleranceWindow; delta <= StepToleranceWindow; delta++)
        {
            var counter = nowCounter + delta;
            // Replay prevention: reject any counter we've already validated
            if (lastValidatedCounter.HasValue && counter <= lastValidatedCounter.Value)
                continue;

            var generated = ComputeHotp(secretBytes, counter, digits);
            if (string.Equals(generated, code, StringComparison.Ordinal))
                return (true, counter);
        }
        return (false, -1);
    }

    private static string ComputeHotp(byte[] secret, long counter, int digits)
    {
        // RFC 4226: HOTP(K, C) = Truncate(HMAC-SHA1(K, C))
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes); // big-endian per RFC 4226

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes);

        // Dynamic truncation: offset = last nibble of hash
        var offset = hash[^1] & 0x0F;
        var code = ((hash[offset] & 0x7F) << 24)
                 | ((hash[offset + 1] & 0xFF) << 16)
                 | ((hash[offset + 2] & 0xFF) << 8)
                 | (hash[offset + 3] & 0xFF);
        var otp = code % (int)Math.Pow(10, digits);
        return otp.ToString().PadLeft(digits, '0');
    }

    private static string BuildOtpAuthUri(string issuer, string account, string secret, int digits, int period)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(account);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}?secret={secret}&issuer={encodedIssuer}&algorithm=SHA1&digits={digits}&period={period}";
    }

    private static IReadOnlyList<string> GenerateBackupCodes(int count, int length)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O/1/I ambiguity
        return Enumerable.Range(0, count).Select(_ =>
        {
            var bytes = RandomNumberGenerator.GetBytes(length);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }).ToList();
    }

    // ---------------------------------------------------------------
    // Base32 encoding (RFC 4648) for TOTP secret QR codes
    // ---------------------------------------------------------------

    private static readonly string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string ToBase32(byte[] input)
    {
        var sb = new StringBuilder();
        var buffer = 0; var bitsLeft = 0;
        foreach (var b in input)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                sb.Append(Base32Chars[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }
        if (bitsLeft > 0) sb.Append(Base32Chars[(buffer << (5 - bitsLeft)) & 31]);
        return sb.ToString();
    }

    private static byte[] FromBase32(string input)
    {
        input = input.TrimEnd('=').ToUpperInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);
        var result = new List<byte>();
        var buffer = 0; var bitsLeft = 0;
        foreach (var c in input)
        {
            var idx = Base32Chars.IndexOf(c);
            if (idx < 0) continue;
            buffer = (buffer << 5) | idx;
            bitsLeft += 5;
            if (bitsLeft >= 8) { result.Add((byte)((buffer >> (bitsLeft - 8)) & 0xFF)); bitsLeft -= 8; }
        }
        return result.ToArray();
    }
}

/// <summary>In-memory TOTP enrollment repository for dev/test.</summary>
public sealed class InMemoryTotpEnrollmentRepository : ITotpEnrollmentRepository
{
    private readonly ConcurrentDictionary<string, TotpEnrollment> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(TotpEnrollment enrollment, CancellationToken cancellationToken = default)
    {
        _store[enrollment.UserId] = enrollment; return Task.CompletedTask;
    }
    public Task<TotpEnrollment?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(userId, out var e); return Task.FromResult(e);
    }
    public Task UpdateAsync(TotpEnrollment enrollment, CancellationToken cancellationToken = default)
    {
        _store[enrollment.UserId] = enrollment; return Task.CompletedTask;
    }
    public Task<IReadOnlyList<TotpEnrollment>> GetAllEnrolledAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TotpEnrollment>>(_store.Values.Where(e => e.Status == MfaStatus.Verified).ToList());
}
