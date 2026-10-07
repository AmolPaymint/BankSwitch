using BankSwitch.Admin.Services;
using BankSwitch.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BankSwitch.Tests;

public sealed class AdminAccountLockoutTests
{
    private const string Username = "admin";
    private const string Password = "Sup3r-Secret-Passw0rd!";
    private const string ValidMfaCode = "123456";
    private const string InvalidMfaCode = "12345"; // five digits: fails the six-digit TOTP format check

    private static IAdminAuthService CreateService(int maxAttempts = 3, int lockoutMinutes = 15)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:BootstrapUser"] = Username,
                ["Admin:BootstrapPassword"] = Password,
                ["Admin:MaxFailedLoginAttempts"] = maxAttempts.ToString(),
                ["Admin:AccountLockoutMinutes"] = lockoutMinutes.ToString()
            })
            .Build();
        var audit = new StructuredAuditLogger(NullLogger<StructuredAuditLogger>.Instance);
        return new InMemoryAdminAuthService(audit, configuration, null);
       // return new InMemoryAdminAuthService(audit, configuration);
    }

    [Fact]
    public async Task Valid_credentials_and_mfa_authenticate_successfully()
    {
        var service = CreateService();
        var result = await service.ValidateAsync(Username, Password, ValidMfaCode, "127.0.0.1");
        Assert.True(result.IsAuthenticated);
    }

    [Fact]
    public async Task Wrong_password_below_lockout_threshold_returns_generic_failure()
    {
        var service = CreateService(maxAttempts: 3);

        for (var i = 0; i < 2; i++)
        {
            var result = await service.ValidateAsync(Username, "wrong-password", ValidMfaCode, "127.0.0.1");
            Assert.False(result.IsAuthenticated);
            Assert.Equal("Invalid username or password.", result.FailureReason);
        }
    }

    [Fact]
    public async Task Account_locks_after_max_failed_attempts_and_blocks_subsequent_correct_login()
    {
        var service = CreateService(maxAttempts: 3, lockoutMinutes: 15);

        for (var i = 0; i < 3; i++)
        {
            await service.ValidateAsync(Username, "wrong-password", ValidMfaCode, "127.0.0.1");
        }

        // Even correct credentials must be rejected while the account is locked.
        var result = await service.ValidateAsync(Username, Password, ValidMfaCode, "127.0.0.1");
        Assert.False(result.IsAuthenticated);
        Assert.Contains("locked", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Successful_login_resets_failed_attempt_counter()
    {
        var service = CreateService(maxAttempts: 3, lockoutMinutes: 15);

        await service.ValidateAsync(Username, "wrong-password", ValidMfaCode, "127.0.0.1");
        await service.ValidateAsync(Username, "wrong-password", ValidMfaCode, "127.0.0.1");

        var success = await service.ValidateAsync(Username, Password, ValidMfaCode, "127.0.0.1");
        Assert.True(success.IsAuthenticated);

        // Counter was reset by the successful login, so two more failures should not trip lockout (threshold is 3).
        await service.ValidateAsync(Username, "wrong-password", ValidMfaCode, "127.0.0.1");
        var result = await service.ValidateAsync(Username, "wrong-password", ValidMfaCode, "127.0.0.1");
        Assert.False(result.IsAuthenticated);
        Assert.Equal("Invalid username or password.", result.FailureReason);
    }

    [Fact]
    public async Task Failed_mfa_attempts_count_toward_lockout()
    {
        var service = CreateService(maxAttempts: 2, lockoutMinutes: 15);

        var first = await service.ValidateAsync(Username, Password, InvalidMfaCode, "127.0.0.1");
        Assert.False(first.IsAuthenticated);
        Assert.Equal("Invalid MFA code.", first.FailureReason);

        var second = await service.ValidateAsync(Username, Password, InvalidMfaCode, "127.0.0.1");
        Assert.False(second.IsAuthenticated);
        Assert.Contains("locked", second.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lockout_is_scoped_per_account()
    {
        var service = CreateService(maxAttempts: 2, lockoutMinutes: 15);

        await service.ValidateAsync(Username, "wrong-password", ValidMfaCode, "127.0.0.1");
        await service.ValidateAsync(Username, "wrong-password", ValidMfaCode, "127.0.0.1");

        var lockedAccount = await service.ValidateAsync(Username, Password, ValidMfaCode, "127.0.0.1");
        Assert.Contains("locked", lockedAccount.FailureReason, StringComparison.OrdinalIgnoreCase);

        // A different username is not affected by the lockout on "admin".
        var otherAccount = await service.ValidateAsync("auditor", "wrong-password", ValidMfaCode, "127.0.0.1");
        Assert.False(otherAccount.IsAuthenticated);
        Assert.Equal("Invalid username or password.", otherAccount.FailureReason);
    }
}
