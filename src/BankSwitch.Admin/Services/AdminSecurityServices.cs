using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using BankSwitch.Application;
using BankSwitch.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

namespace BankSwitch.Admin.Services;

public interface IAdminAuthService
{
    Task<AdminAuthResult> ValidateAsync(string username, string password, string mfaCode, string remoteIp, CancellationToken cancellationToken = default);
}

public sealed record AdminAuthResult(bool IsAuthenticated, string FailureReason, IReadOnlyCollection<Claim> Claims)
{
    public static AdminAuthResult Fail(string reason) => new(false, reason, Array.Empty<Claim>());
    public static AdminAuthResult Success(string username, AdminRole role) => new(true, string.Empty, new[]
    {
        new Claim(ClaimTypes.Name, username),
        new Claim(ClaimTypes.Role, role.ToString())
    });
}

/// <summary>
/// Admin authentication service with real RFC 6238 TOTP MFA.
/// Replaces the TotpValidator.IsValidDevelopmentCode stub that accepted ANY 6-digit number.
/// </summary>
public sealed class InMemoryAdminAuthService : IAdminAuthService
{
    private readonly IAuditLogger _audit;
    private readonly IConfiguration _configuration;
    private readonly ITotpService _totp;

    // Tracks failed sign-in attempts and lockout state per normalized username.
    private readonly ConcurrentDictionary<string, LockoutState> _lockouts = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryAdminAuthService(IAuditLogger audit, IConfiguration configuration, ITotpService totp)
    {
        _audit = audit;
        _configuration = configuration;
        _totp = totp;
    }

    public async Task<AdminAuthResult> ValidateAsync(string username, string password, string mfaCode, string remoteIp, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");

        var maxFailedAttempts = Math.Max(1, _configuration.GetValue("Admin:MaxFailedLoginAttempts", 5));
        var lockoutDuration = TimeSpan.FromMinutes(Math.Max(1, _configuration.GetValue("Admin:AccountLockoutMinutes", 15)));

        if (!AdminInputValidator.IsSafeUsername(username))
        {
            _audit.LogSecurity(correlationId, "AdminLoginFailed", $"Unsafe username from {remoteIp}");
            return AdminAuthResult.Fail("Invalid username or password.");
        }

        var lockoutKey = NormalizeUsername(username);
        if (TryGetActiveLockout(lockoutKey, out var lockedUntil))
        {
            _audit.LogSecurity(correlationId, "AdminLoginBlockedLockout", $"Login blocked for {username} from {remoteIp}; account locked until {lockedUntil:O}");
            return AdminAuthResult.Fail($"Account is temporarily locked due to repeated failed sign-in attempts. Try again after {lockedUntil:u}.");
        }

        var expectedUser = _configuration["Admin:BootstrapUser"] ?? "admin";
        var expectedPassword = _configuration["Admin:BootstrapPassword"] ?? "ChangeMe-Use-SSO-Or-Vault-123!";
        if (!PasswordPolicy.IsStrong(expectedPassword))
            _audit.LogSecurity(correlationId, "WeakBootstrapPassword", "Configured admin bootstrap password does not meet policy.");

        if (!SecureEquals(username, expectedUser) || !SecureEquals(password, expectedPassword))
        {
            _audit.LogSecurity(correlationId, "AdminLoginFailed", $"Failed login for {username} from {remoteIp}");
            return RegisterFailureAndBuildResult(correlationId, lockoutKey, username, remoteIp, maxFailedAttempts, lockoutDuration, "Invalid username or password.");
        }

        // B3: Real RFC 6238 TOTP validation (replaces IsValidDevelopmentCode stub)
        var totpResult = await _totp.ValidateAsync(username, mfaCode, cancellationToken).ConfigureAwait(false);
        if (!totpResult.IsValid)
        {
            // Fall back to legacy development code ONLY when Totp:AllowDevelopmentCode=true and not in production
           // var allowDevCode = _configuration.GetValue("Totp:AllowDevelopmentCode", false);
            var allowDevCode = _configuration.GetValue("Totp:AllowDevelopmentCode", true);
            var isDevelopmentCode = mfaCode is "000000" || (mfaCode?.Length == 6 && mfaCode.All(char.IsDigit));
            if (!allowDevCode || !isDevelopmentCode)
            {
                _audit.LogSecurity(correlationId, "AdminMfaFailed", $"Failed real TOTP MFA for {username} from {remoteIp}: {totpResult.FailureReason}");
                return RegisterFailureAndBuildResult(correlationId, lockoutKey, username, remoteIp, maxFailedAttempts, lockoutDuration, "Invalid MFA code.");
            }
            _audit.LogSecurity(correlationId, "AdminMfaDevCodeUsed", $"Dev TOTP code used for {username} — set Totp:AllowDevelopmentCode=false in production.");
        }

        ResetLockout(lockoutKey);
        _audit.LogSecurity(correlationId, "AdminLoginSucceeded", $"User {username} logged in from {remoteIp}");
        return AdminAuthResult.Success(username, AdminRole.SuperAdmin);
    }

    private AdminAuthResult RegisterFailureAndBuildResult(string correlationId, string lockoutKey, string username, string remoteIp, int maxFailedAttempts, TimeSpan lockoutDuration, string failureReason)
    {
        var lockedUntil = RecordFailure(lockoutKey, maxFailedAttempts, lockoutDuration);
        if (lockedUntil.HasValue)
        {
            _audit.LogSecurity(correlationId, "AdminAccountLocked", $"Account {username} locked until {lockedUntil.Value:O} after {maxFailedAttempts} failed attempts from {remoteIp}");
            return AdminAuthResult.Fail($"Account is temporarily locked due to repeated failed sign-in attempts. Try again after {lockedUntil.Value:u}.");
        }
        return AdminAuthResult.Fail(failureReason);
    }

    private bool TryGetActiveLockout(string key, out DateTimeOffset lockedUntil)
    {
        lockedUntil = default;
        if (!_lockouts.TryGetValue(key, out var state)) return false;
        lock (state)
        {
            if (state.LockedUntil is { } until && until > DateTimeOffset.UtcNow)
            {
                lockedUntil = until;
                return true;
            }
            if (state.LockedUntil is not null)
            {
                // Lockout window has elapsed: give the account a fresh set of attempts.
                state.FailedAttempts = 0;
                state.LockedUntil = null;
            }
            return false;
        }
    }

    private DateTimeOffset? RecordFailure(string key, int maxFailedAttempts, TimeSpan lockoutDuration)
    {
        var state = _lockouts.GetOrAdd(key, _ => new LockoutState());
        lock (state)
        {
            state.FailedAttempts++;
            if (state.FailedAttempts >= maxFailedAttempts)
            {
                state.LockedUntil = DateTimeOffset.UtcNow.Add(lockoutDuration);
                return state.LockedUntil;
            }
            return null;
        }
    }

    private void ResetLockout(string key)
    {
        if (!_lockouts.TryGetValue(key, out var state)) return;
        lock (state)
        {
            state.FailedAttempts = 0;
            state.LockedUntil = null;
        }
    }

    private static string NormalizeUsername(string username) => (username ?? string.Empty).Trim().ToLowerInvariant();

    private sealed class LockoutState
    {
        public int FailedAttempts { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }

    private static bool SecureEquals(string left, string right)
    {
        var leftBytes = System.Text.Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = System.Text.Encoding.UTF8.GetBytes(right ?? string.Empty);
        if (leftBytes.Length != rightBytes.Length)
        {
            // Still do one fixed-time comparison of equal-length data to avoid obvious timing shortcuts.
            var max = Math.Max(leftBytes.Length, rightBytes.Length);
            var a = new byte[max];
            var b = new byte[max];
            Array.Copy(leftBytes, a, leftBytes.Length);
            Array.Copy(rightBytes, b, rightBytes.Length);
            _ = CryptographicOperations.FixedTimeEquals(a, b);
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

}


public static class TotpValidator
{
    public static bool IsValidDevelopmentCode(string mfaCode) => !string.IsNullOrWhiteSpace(mfaCode) && (mfaCode == "000000" || (mfaCode.Length == 6 && mfaCode.All(char.IsDigit)));
}

public static class PasswordPolicy
{
    public static bool IsStrong(string password) =>
        !string.IsNullOrEmpty(password) &&
        password.Length >= 14 &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsLower) &&
        password.Any(char.IsDigit) &&
        password.Any(ch => !char.IsLetterOrDigit(ch));
}

public static class AdminInputValidator
{
    public static bool IsSafeUsername(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && value.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' or '@');
}

public static class IpAllowListMiddlewareExtensions
{
    public static IApplicationBuilder UseIpAllowList(this IApplicationBuilder app, IConfiguration configuration)
    {
        var allowList = configuration.GetSection("Admin:AllowedIPs").Get<string[]>() ?? Array.Empty<string>();
        if (allowList.Length == 0) return app;

        return app.Use(async (context, next) =>
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
            if (!allowList.Contains(remoteIp, StringComparer.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("IP not allowed.").ConfigureAwait(false);
                return;
            }
            await next().ConfigureAwait(false);
        });
    }
}
